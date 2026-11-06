# Cadmus.Graph

Cadmus RDF graph core components. This library contains the core logic and models for projecting a subset of Cadmus data (which are essentially objects as properties trees, stored in JSON) into an RDF-like graph, and keeping it in synch whenever data get saved.

- [Cadmus.Graph](#cadmusgraph)
  - [Packages](#packages)
  - [Adapters](#adapters)
  - [Mappings](#mappings)
    - [Mapping Identifiers](#mapping-identifiers)
  - [Mapping Model](#mapping-model)
  - [Templates](#templates)
  - [Updating the Graph](#updating-the-graph)
  - [Database](#database)
  - [Example](#example)

At the heart of the graph is data projection via mapping rules:

(1) a **source object** (`MapperSource`, with an item and optionally one of its parts) is provided to the mapper. The current implementation relies on objects serialized into JSON. Source objects are Cadmus _items_ or _parts_ (thesauri, i.e. taxonomies, can be imported as nodes, but this does not happen via mapping as it's a one-time procedure). Ultimately, from the point of view of the mapper any source object is just JSON code representing it. Between the _source_ object and the _mappings_ there is an intermediate layer, represented by adapter components. Their task is adapting that object to the mappings, and providing additional information from it.

(2) the **mapper** (`JsonGraphNodeMapper`) finds all the mappings matching the source object, and applies each of them, collecting the results (nodes and triples) into a **graph set** (`GraphSet`).

(3) the graph set is **merged** into the graph store (`IGraphRepository.UpdateGraphAsync`), replacing whatever was previously generated from the same source.

The top-level component orchestrating this flow is `GraphUpdater`, which is used whenever an item or part gets saved.

## Packages

- `Cadmus.Graph`: core models, mapper, updater, and the storage-agnostic repository interface (`IGraphRepository`).
- `Cadmus.Graph.Ef`: base Entity Framework repository (`EfGraphRepository`), with the update logic.
- `Cadmus.Graph.Ef.PgSql`: PostgreSQL repository (`EfPgSqlGraphRepository`, tag `graph-repository.ef-pg`) and schema (`PgSqlGraphSchema`).
- `Cadmus.Graph.Extras`: additional metadata sources (`ItemEidMetadataSource`).

## Adapters

The mappings used to project data on the semantic graph are weakly coupled to the type of their source objects, thanks to an intermediate layer added between source objects and mappings, in the form of _adapters_.

A _graph source adapter_ is a component implementing an interface (`IGraphSourceAdapter`), which dictates that the component must have a function that:

- gets as input an item or a part (`MapperSource`), together with a dictionary of additional metadata;
- possibly injects _metadata_ in the received dictionary, extracting them from the source object, to be later consumed by mapping rules;
- outputs an object (or null when there is nothing to map) adapted to be plugged into the mapping process, plus a filter (`RunNodeMappingFilter`) for the mappings to be applied. Currently the adapted object is serialized into JSON.

Two adapters are provided: `ItemGraphSourceAdapter` and `PartGraphSourceAdapter`.

## Mappings

The mapping between Cadmus source data (items and parts) and nodes is defined by node mappings (`GraphNodeMapping`). This is the core of the projection mechanism, which extracts a subset of source data into a graph of nodes.

There are two types of sources:

- item: a Cadmus item.
- part: a Cadmus part.

For _item titles_ conventions dictate that:

- if the title ends with `[#...]`, then the text between `[#` and `]` is assumed to be the UID. The only processing applied to this UID is prepending the prefix defined in the mapping, if any.
- if the title ends with `[@...]`, then the text between `[@` and `]` is prefixed to the generated UID. If the mapping already defines a prefix, it gets prepended to this one.

### Mapping Identifiers

In mapping the following identifiers are used:

- `SID` (source ID)
- `UID` (entity's URI-based ID)
- `EID` (entry's ID)

#### SID

The source ID (SID) of each node and triple is calculated so that _the same sources always point to the same entities_. The SID is essential for connecting Cadmus data to the entities and keeping them in synch.

The SID is built by the `sid` template of each mapping (inherited by children mappings unless overridden). By convention, ⚠️ **a SID must always start with the GUID of its source** (the item or part ID, as provided by metadata `item-id` and `part-id`), optionally followed by other components: for instance `{$part-id}/{@eid}` for events in a part, or `{$item-id}` for an item. This is essential, because the graph generated from a source is identified by the GUID prefix of its SIDs: when updating the graph for a saved part, everything having a SID starting with that part's ID is replaced; when deleting the part, it gets removed. The algorithm building the SID is idempotent, as GUIDs are unique by definition.

Examples:

- `76066733-6f81-48dd-a653-284d5be54cfb`: an entity derived from an item or a part.
- `76066733-6f81-48dd-a653-284d5be54cfb/birth`: an entity derived from the entry with EID `birth` in a part.
- `76066733-6f81-48dd-a653-284d5be54cfb/birth/chronotopes`: an entity derived from the chronotopes of the same entry.

#### EID

The "entry" ID is just a convention followed in models of Cadmus multi-entries parts. For instance, a manuscript's decorations part is a collection of decorations, each corresponding to an entry, optionally having its EID (exposed via an `eid` property). All the entries with EIDs get mapped into entities.

Thus, here we call EIDs the identifiers provided by users for entries in a Cadmus collection-part. When present, such EIDs are used to build node identifiers URIs (UIDs).

> This is not the unique purpose of EIDs. In general, this convention provides a mechanism to set a human-friendly identifier for some entity contained in a data model. Often these can also be used to deep-link some data in a model to another one.

#### UID

The entity ID is a _shortened URI_ where a conventional prefix replaces the namespace, calculated as defined by the entity mapping. URIs are case-insensitive.

To get human-friendly UIDs, the UID is derived from a _template_ defined in the mapping rule generating a node. Whenever the template provides a result which happens to be already used by another source and the mapping explicitly requests a unique UID, the UID gets a numeric suffix preceded by `#`. This suffix is granted to be unique in the context of our data.

> 💡 By convention, any UID built by mapping and potentially requiring this suffix must end with `##`, to indicate that a unique UID via an optional numeric suffix is required. For instance, `itn:timespans/ts##` means that the first time such a UID is generated it will be stored as `itn:timespans/ts`; when generated from another source, it will rather be suffixed with a number, e.g. `itn:timespans/ts#3`.

The UID is built by a component implementing interface `IUidBuilder`. A RAM-based implementation (`RamUidBuilder`) is provided for testing; in real world, the implementation is the graph repository, which relies on the `uid_lookup` table, having these fields:

- `id` PK AI: an autonumber primary key handled by the database engine.
- `sid`: the SID linked to the UID.
- `unsuffixed`: the UID as generated by the mapping rule, before any suffixation.
- `has_suffix`: true if the UID must be suffixed. If true, the numeric value to append to the suffix is represented by `id`. This trick allows us to efficiently leverage the autonumber capabilities of a RDBMS to append a unique numeric suffix to whatever UID.

The rules (implemented once in `UidResolver`, and shared by all the builders) are:

1. if no entry exists with the same unsuffixed UID, a new entry without suffix is added, and the UID is used as such.
2. else, if an entry exists for the same SID, it is reused: so ⚠️ **the same source always gets back the same UID** (with its suffix if any), whenever it is saved again. (Databases written by versions before 17.1.0 may have several entries for the same SID, as a new one was added at each save: the newest one is reused, which is the one of the current node.)
3. else, if the mapping did not request a unique UID, the UID is used as such (it's shared among all the sources generating it, like e.g. a place `x:places/arezzo`).
4. else, a new entry with suffix is added, and the UID is the unsuffixed UID plus `#` and the ID of the new entry.

When the same source requests more than once the same unique UID in the same mapping session (e.g. a mapping emitting `x:timespans/ts##` for each chronotope of an event, with a SID like `{$sid}/chronotopes` shared by all of them), each request gets a different UID, which stays the same at each save: the N-th request (for N greater than 1) uses as its key in the lookup the SID plus `#N`. Anyway, it is best to make SIDs specific to each entity, e.g. by including the `index` metadatum.

Concurrent requests for the same UID are serialized, so that two sources can never get the same unique UID.

## Mapping Model

A mapping rule is modeled as an object having a number of properties, defining:

- its _metadata_ (like source type, SID, description, etc.).
- its _input_ (used to match sources).
- its _output_ (which nodes and triples to emit).

In turn, each mapping rule can include any number of _children rules_.

The model of each mapping is:

- `name`: the mapping's name. Root mappings are replaced by name when imported; named children mappings are shared among all the mappings using them (e.g. named mappings of a mappings document), and updated by name. Unnamed children are never shared.
- `ordinal`: an optional number defining the order of application of sibling mappings (and of root mappings), default 0.
- `sourceType`\*: the type of the source object. This is meaningful for _root_ mappings only. The source type is a number: `0`=user, `1`=item, `2`=part, `3`=thesaurus, `4`=implicit (assigned to nodes automatically added because used in a triple without yet being present in the graph). Thus, mappings defined in a mappings document effectively use only `1` and `2`.
- `sid`\*: the SID template of this mapping (see [SID](#sid)). This is usually specified at the root mapping level, but you can also override the root sid in your children mappings (almost always this means adding suffix(es) to the root SID). Thus, SIDs are inherited unless overridden in descendants. If a SID includes the `index` metadatum, and the mapper is processing an array, it will be recalculated for each array's item.

Filters (meaningful for root mappings only): when a filter is specified, it must be satisfied by the source for the mapping to be applied. So, a mapping with a filter on a value which is missing in the source (e.g. a part role filter for a part without role) is not applied.

- `facetFilter`: an optional item's facet filter. When specified, the mapping will target only those items whose facet ID is _equal_ to this value (case-insensitively).
- `groupFilter`: an optional item's group filter. This is a regular expression; when specified, the mapping will target only those items whose group ID _matches_ this expression. An invalid expression never matches.
- `titleFilter`: an optional item's title filter. This is a regular expression; when specified, the mapping will target only those items whose title _matches_ this expression.
- `flagsFilter`: an optional item's flags filter. This is a numeric value, where each bit represents a flag. When specified, the mapping will target only those items whose flags include at least _all_ the bits set in this value, i.e. all the flags specified in the filter must be present.
- `partTypeFilter`: an optional part's type ID filter. When specified, the mapping will target only those parts whose type ID is _equal_ to this value.
- `partRoleFilter`: an optional part's role ID filter. When specified, the mapping will target only those parts whose role ID is _equal_ to this value.

Other properties:

- `description`: an optional, human-readable short description for the mapping rule. This is useful for documentation purposes.
- `source`\*: the source expression representing the data selected by this mapping. In the current implementation this is a JMES path. For instance, `events[?type=='person.birth']` matches only the entries in the `events` array property of a part's model whose type is equal to `person.birth`.
  - when the source expression selects an _object_, you can refer to it as a whole with `.`, or to any of its properties by their name.
  - when the source expression selects an _array_, the mapper will loop through all its items, and run for each of them. So, you can still define your mappings in terms of a single object, which here is the array's item. Additionally, the `index` metadatum will be used to represent the 0-based index number of the item in the array.
- `scalarPattern`: the optional regular expression pattern which should match against a scalar value defined by the mapping's source expression for the mapping to be applied. When this is defined and does not match, the mapping will not be applied. This can be used to overcome the limitations of the source expression in languages like JMESPath, where e.g. `.[?lost==true]` is always evaluated as a match, even when the value of the scalar property `lost` is `false`. So, in this example setting `scalarPattern` to `true` and source to `lost` will apply the mapping only if this property's value is `true`.

- `output`:
  - `nodes`: an object (dictionary) where each property is the key of a node emitted by the mapping rule, whose string value is the node's identifier [template](#templates). Optionally, this template can be followed by space plus the node's label, and/or its tag between square brackets, the tag being preceded by `|`. For instance, `x:events/{$.} [label|tag]` defines the node's UID, its human-friendly label, and an optional tag. Nodes are emitted in their definition order, so that later templates can refer to earlier nodes.
  - `triples`: an array of strings, each representing a triple [template](#templates). Each triple is in any of these forms:
    - `S P O`: subject, predicate, object (all URIs). The predicate `a` is a shortcut for `rdf:type`;
    - `S P "O"`: subject, predicate, literal object in double quotes;
    - `S P "O"@lang`: subject, predicate, literal object in double quotes followed by a [BCP47](https://www.rfc-editor.org/info/bcp47) language tag (e.g. `"sample"@en`, `"color"@en-US`);
    - `S P "O"^^type`: subject, predicate, literal object in double quotes followed by a [datatype IRI](https://www.w3.org/TR/xmlschema-2/) (e.g. `"123"^^xs:int`). For numeric and boolean types, the numeric value is also stored, to allow for numeric searches.

    The language or type suffix is recognized only when it immediately follows the closing quote, so that quotes, `@` or `^^` inside the value are preserved.
  - `metadata`: optional metadata to be consumed in [templates](#templates). Metadata come from several sources: the source object, the mapping process itself, and these definitions in the mapping.

- `children`: children mappings. Each child mapping has the same properties of a root mapping, except for those which would make no sense in children, as noted above.

## Templates

Templates are used in mappings to build node identifiers and triple values. A template contains text with placeholders, delimited by `{}`, where the opening brace is followed by a single character representing the placeholder type:

1. `{@...}` = _expression_: the expression used to select source data for the mapping.
2. `{?...}` = _node key_: the key for a previously emitted node, optionally suffixed.
3. `{$...}` = _metadata_: any metadata set during the mapping process.
4. `{!...}` = _macro_: the output of a custom function, receiving the current data context from the source, and returning a string or null.

These placeholders can also be nested. The mapping rules will take care of resolving them starting from the deepest ones. Placeholder resolution is driven by a simple tree shaped representation of the template (`TemplateTree`).

### Expressions

- syntax: `{@...}`

Expressions select data from the source. The syntax of an expression depends on the mapper's implementation. Currently the only implementation is JSON-based, so expressions are JMES paths.

For instance, say you are mapping an event object having an `eid` property equal to some string: you select the value of this string with the placeholder `{@eid}`.

### Node Keys

- syntax: `{?...}`

During the mapping process, nodes emitted in the context of each mapping (including all its descendant mappings) are stored in a dictionary with the keys specified in the mapping itself for each node.

As a sample, consider this mapping fragment:

```json
{
  "name": "events.type=birth",
  "sourceType": 2,
  "partTypeFilter": "it.vedph.historical-events",
  "source": "events[?type=='person.birth']",
  "sid": "{$part-id}/{@eid}",
  "children": [
    {
      "source": "eid",
      "output": {
        "nodes": {
          "event": "x:events/{$.}"
        }
      }
    }
  ]
}
```

Here we map each birth event (as specified by `source`). For each of them, a child mapping matches the event's `eid` property, and outputs a node under the key `event`, whose template is `x:events/{$.}` (where `{$.}` is a [metadatum](#metadata) representing the value of the _current leaf node_ in the source tree). So, in this case the generated node will have an UID equal to `x:events/` plus the event's EID.

As a node is a complex object, in a template placeholder you can pick different properties from it. These are specified by adding to the node's key a **suffix** preceded by `:`. Available suffixes are:

- `:uri` = the node's generated URI. This is the default property; so when there is no suffix specified, the URI is picked.
- `:label` = the node's label.
- `:sid` = the node's SID.
- `:src_type` = the node's source type.

A key not (yet) defined resolves to an empty string.

### Metadata

- syntax: `{$...}`

The mapping process can set metadata in the form of name=value string pairs. These get stored under arbitrary keys (some names are reserved), and are available to any template in the context of its root mapping.

Metadata can be emitted by the mapping process itself, or be defined in a mapping's output under the `metadata` property (an object where each property is a metadatum with its string value).

Currently the mapping process emits these metadata (whose names are reserved):

- `item-id`: the item ID (GUID).
- `item-title`: the item's title.
- `item-facet`: the item's facet ID.
- `item-group`: the item's group ID, if any. When the group ID is composite (with `/`, e.g. `alpha/beta`), each of its components is also emitted as `item-group@N`, where N is the 1-based ordinal of the component (e.g. `item-group@1`=`alpha`, `item-group@2`=`beta`).
- `part-id`: the part ID (GUID).
- `part-type-id`: the part's type ID.
- `part-role-id`: the part's role ID, if any.
- `.`: the value of the current leaf node in the source JSON data. For instance, if the mapping is selecting a string property from `events/event[0].eid`, this is the value of `eid`.
- `index`: the index of the element being processed from a source array. When the source expression used by the mapping points to an array, every item of the array gets processed separately from that mapping onwards. At each iteration, the `index` metadatum is set to the current index.

Additionally, your backend code can use a metadata supplier (`MetadataSupplier`) with extra metadata sources (`IMetadataSource`) to provide more metadata. A typical source is `ItemEidMetadataSource` (in `Cadmus.Graph.Extras`), which adds `item-eid` (the value of metadatum `eid` in the `MetadataPart` of the current item, if any) and `metadata-pid` (the ID of that metadata part). The Cadmus API uses it by default:

```cs
GraphUpdater updater = new(graphRepository)
{
    // item-eid and metadata-pid from the item's MetadataPart
    MetadataSupplier = new MetadataSupplier()
        .SetCadmusRepository(repository)
        .AddItemEid()
};
```

### Macros

- syntax: `{!...}`

Macros are a modular way for customizing the mapping process when more complex logic is required. A macro is an object implementing an interface (`INodeMappingMacro`), whose `Run` method receives the current data context (`MapperSource`) and the macro's arguments, and returns a string or null. Macros are registered in the mapper (or in `GraphUpdater`) under an arbitrary key, via `AddMacro` or `SetMacros`; this key is used to call the macro from templates.

The macro syntax in the placeholder consists of the macro key, optionally followed by any number of arguments, separated by ` & ` (with spaces), included in brackets. For instance:

```txt
{!some_macro(arg1 & arg2)}
```

Some macros are **built-in**, and conventionally their key starts with an underscore:

- `_hdate(json & property)`: this macro handles a Cadmus historical date and returns either its sort value, or its human-friendly, machine-parsable text value. Its arguments are:
  1. the JSON code representing a Cadmus historical date;
  2. the property of the date to return: `value` (default) or `text`.
- `_substring(string & index & [length])`: substring.

### Filters

Whenever a template represents a URI, i.e. in all the cases except for triple's object literals, once the template has been filled, the result gets filtered as follows:

- whitespaces are replaced with underscores;
- only letters, digits 0-9, and characters `:-_#/&%=.?` are preserved;
- letters are all lowercased;
- diacritics are removed.

Should you want to disable this filtering (which is generally _not_ recommended, as this filtering provides fairly common URI forms), start the template with `!`, which being a preprocessing directive will be discarded from the template itself.

## Updating the Graph

`GraphUpdater` is used whenever an item or a part gets saved (`UpdateAsync`), while `IGraphRepository.DeleteGraphSetAsync` is used when it gets deleted. Also, `Explain` shows the mappings applied to a source, and the resulting graph set, without changing the graph. An updater instance is not thread-safe: use one per update (the API registers it as transient).

When a source gets saved, the updater runs all the mappings matching it, and passes the resulting graph set to `UpdateGraphAsync`. The set also lists the ID of its source (`GraphSet.SourceIds`), so that the repository knows which source it represents even when the set is empty (e.g. when the user removed all the entries which were mapped). Then, for each source in the set, in a single transaction:

1. all the URIs are resolved into numeric IDs (adding the missing ones).
2. **implicit nodes** are added for the nodes used by triples but not existing (e.g. the object `crm:e21_person` of a triple `x:persons/petrarch a crm:E21_person`). They get source type 4 (implicit), the SID of the triple, and their URI as label; predicates are tagged as `property`.
3. **nodes**: new nodes are added; nodes already generated from the same source are updated; nodes created implicitly by another source and now explicitly generated by this one become owned by this source. Nodes owned by other sources or created by users are never updated.
4. **triples**: triples are identified by subject, predicate, object (node, or literal with its type and language) and SID. New triples are added, those no longer generated are deleted, and those still generated keep their ID (their tag and other details are updated when changed). Saving the same data again changes nothing.
5. **nodes no longer generated** are released: they are deleted when no triple uses them anymore (unless they are classes or properties); when they are still used by triples of other sources, or by triples created by users, they are kept and handed over to the source of one of these triples (as implicit nodes), so that they get removed when that source no longer uses them. This ensures that removing a node never deletes in cascade the triples of other sources or users. `DeleteGraphSetAsync` follows the same rules after deleting all the triples of the source.
6. **node classes** (`node_class`, i.e. the classes of each node, including the inherited ones) are updated for the subjects of changed `rdf:type` triples, and for all the instances of classes whose `rdfs:subClassOf` triples changed.

Concurrent updates of the same source are serialized, and concurrent insertions of the same URIs or nodes (e.g. two users saving different parts which refer to the same new entity) are safe. The PostgreSQL implementation uses transaction-level advisory locks for this.

The repository exposes asynchronous methods for the save path (`UpdateGraphAsync`, `DeleteGraphSetAsync`, `GetGraphSetAsync`, `FindMappingsAsync`). In `IGraphRepository` these are default interface members wrapping the synchronous ones, so that other implementations keep working unchanged; the Entity Framework repository implements them natively. The other methods (used by the graph editor) are synchronous.

The mappings to run are fetched at each save. To avoid this, set the repository's `Cache` (an `IMemoryCache`): mappings are cached by source type for `MappingCacheDuration` (5 minutes by default). Any change to mappings via a repository in the same process invalidates the cache; changes made by other processes (e.g. by the CLI tool) are seen when the cache expires. The Cadmus API sets the cache by default.

## Database

The PostgreSQL schema is in `Cadmus.Graph.Ef.PgSql/Assets`: `Schema.pgsql` defines tables and preset data, while `SchemaUpdate.pgsql` defines all the indexes and procedures, and is idempotent (`PgSqlGraphSchema.GetSql()` returns both). Existing databases created by older versions are upgraded by applying the update script (`PgSqlGraphSchema.ApplyAsync`): the Cadmus API does it at each startup, and so do the CLI `create-db graph` command and `EfPgSqlGraphRepository.CreateStore` for existing databases.

The indexes are designed for the queries actually used, considering that the graph is updated at each save:

- case-insensitive lookups of URIs (unique), namespaces, UIDs;
- SID exact and prefix lookups for nodes and triples (the graph set of each source), via pattern-operators indexes on `lower(sid)`;
- trigram indexes for "contains" searches on node labels and URIs (the node lookups of the editor);
- triples by subject+predicate, object+predicate and predicate (graph walking, linked nodes, node classes population, FK cascades);
- node classes by node and by class.

The `populate_node_class` procedure limits the depth of the classes hierarchy walk, so that cycles in `rdfs:subClassOf` cannot make it endless.

## Example

Given these sample data (from a part):

```json
{
  "events": [
    {
      "eid": "birth",
      "type": "person.birth",
      "chronotopes": [
        {
          "place": {
            "value": "Arezzo"
          },
          "date": {
            "a": {
              "value": 1304,
              "day": 20,
              "month": 7
            }
          }
        }
      ],
      "description": "Petrarch was born on July 20, 1304 at Arezzo from ser Petracco and Eletta Canigiani.",
      "relatedEntities": [
        {
          "relation": "mother",
          "id": {
            "target": {
              "gid": "x:guys/eletta_canigiani"
            }
          }
        },
        {
          "relation": "father",
          "id": {
            "target": {
              "gid": "x:guys/ser_petracco"
            }
          }
        }
      ]
    },
    {
      "eid": "death",
      "type": "person.death",
      "chronotopes": [
        {
          "place": {
            "value": "Arquà"
          },
          "date": {
            "a": {
              "value": 1374,
              "day": 18,
              "month": 7
            }
          }
        }
      ],
      "assertion": {
        "rank": 2,
        "references": [
          {
            "type": "paper",
            "citation": "Rossi 1963 p.123"
          }
        ]
      },
      "description": "Petrarch died in 1374, July 18 (or 19) at Arquà."
    }
  ]
}
```

These are the mappings for birth and death events:

```jsonc
{
  "namedMappings": {
    "event_description": {
      "name": "event_description",
      "description": "Map the description of an event to EVENT crm:P3_has_note LITERAL.",
      "source": "description",
      "sid": "{$sid}/description",
      "output": {
        "triples": ["{?event} crm:P3_has_note \"{$.}\""]
      }
    },
    "event_note": {
      "name": "event_note",
      "description": "Map the note of an event to EVENT crm:P3_has_note LITERAL.",
      "source": "note",
      "sid": "{$sid}/note",
      "output": {
        "triples": ["{?event} crm:P3_has_note \"{$.}\""]
      }
    },
    "event_chronotopes": {
      "name": "event_chronotopes",
      "description": "For each chronotope, map the place/date of an event to triples which create a place node for the place and link it to the event via a triple using crm:P7_took_place_at for places; and to triples using crm:P4_has_time_span which in turn has a new timespan node has object.",
      "source": "chronotopes",
      "sid": "{$sid}/chronotopes",
      "children": [
        {
          "name": "event_chronotopes/place",
          "source": "place",
          "output": {
            "nodes": {
              "place": "x:places/{@value}"
            },
            "triples": [
              "{?place} a crm:E53_Place",
              "{?event} crm:P7_took_place_at {?place}"
            ]
          }
        },
        {
          "name": "event_chronotopes/date",
          "source": "date",
          "output": {
            "metadata": {
              "date_value": "{!_hdate({@.} & value)}",
              "date_text": "{!_hdate({@.} & text)}"
            },
            "nodes": {
              "timespan": "x:timespans/ts##"
            },
            "triples": [
              "{?event} crm:P4_has_time-span {?timespan}",
              "{?timespan} crm:P82_at_some_time_within \"{$date_value}\"^^xs:float",
              "{?timespan} crm:P87_is_identified_by \"{$date_text}\"@en"
            ]
          }
        }
      ]
    },
    "event_assertion": {
      "name": "event_assertion",
      "description": "Map the assertion of an event to EVENT x:has_probability RANK^^xsd:short.",
      "source": "assertion",
      "sid": "{$sid}/assertion",
      "output": {
        "nodes": {
          "assertion": "x:assertions/as##"
        },
        "triples": [
          "{?event} x:has_probability \"{@rank}\"^^xsd:short",
          "{?assertion} a crm:E13_attribute_assignment",
          "{?assertion} crm:P140_assigned_attribute_to {?event}",
          "{?assertion} crm:P141_assigned x:has_probability",
          "{?assertion} crm:P177_assigned_property_of_type crm:E55_type"
        ]
      },
      "children": [
        {
          "name": "event_assertion/references",
          "source": "references",
          "sid": "{$sid}/assertion/reference",
          "children": [
            {
              "name": "event/references/citation",
              "source": "citation",
              "output": {
                "nodes": {
                  "citation": "x:citations/cit##"
                },
                "triples": [
                  "{?citation} a crm:E31_Document",
                  "{?citation} rdfs:label \"{@.}\"",
                  "{?assertion} crm:P70i_is_documented_in {?citation}"
                ]
              }
            }
          ]
        }
      ]
    },
    "event_tag": {
      "name": "event_tag",
      "description": "Map the tag of an event to EVENT P9i_forms_part_of GROUP.",
      "source": "tag",
      "sid": "{$sid}/tag",
      "output": {
        "nodes": {
          "period": "x:periods/{$part-id}/{@value}"
        },
        "triples": ["{?event} P9i_forms_part_of {?period}"]
      }
    }
  },
  "documentMappings": [
    {
      "name": "person",
      "sourceType": 2,
      "facetFilter": "person",
      "partTypeFilter": "it.vedph.metadata",
      "description": "Map a person item to a node via the item's EID extracted from its MetadataPart.",
      "source": "metadata[?name=='eid']",
      "sid": "{$part-id}/{@value}",
      "output": {
        "nodes": {
          "person": "x:persons/{$part-id}/{@value} [x:persons/{@value}]"
        },
        "triples": ["{?person} a crm:E21_person"]
      }
    },
    {
      "name": "person_birth_event",
      "sourceType": 2,
      "facetFilter": "person",
      "partTypeFilter": "it.vedph.historical-events",
      "description": "Map person birth event",
      "source": "events[?type=='person.birth']",
      "sid": "{$part-id}/{@eid}",
      "output": {
        "metadata": {
          "sid": "{$part-id}/{@eid}",
          "person": "x:persons/{$metadata-pid}/{$item-eid}"
        },
        "nodes": {
          "event": "x:events/{$sid} [x:events/{@eid}]"
        },
        "triples": [
          "{?event} a crm:E67_birth",
          "{?event} crm:P2_has_type x:event-types/person.birth",
          "{?event} crm:P98_brought_into_life {$person}"
        ]
      },
      "children": [
        {
          "name": "event_description"
        },
        {
          "name": "event_note"
        },
        {
          "name": "event_chronotopes"
        },
        {
          "name": "event_assertion"
        },
        {
          "name": "event_tag"
        },
        {
          "name": "person_birth_event/related/by_mother",
          "source": "relatedEntities[?relation=='mother']",
          "output": {
            "nodes": {
              "mother": "{@id.target.gid}"
            },
            "triples": ["{?event} crm:P96_by_mother {?mother}"]
          }
        },
        {
          "name": "person_birth_event/related/from_father",
          "source": "relatedEntities[?relation=='father']",
          "output": {
            "nodes": {
              "father": "{@id.target.gid}"
            },
            "triples": ["{?event} crm:P97_from_father {?father}"]
          }
        }
      ]
    },
    {
      "name": "person_death_event",
      "sourceType": 2,
      "facetFilter": "person",
      "partTypeFilter": "it.vedph.historical-events",
      "description": "Map person death event",
      "source": "events[?type=='person.death']",
      "sid": "{$part-id}/{@eid}",
      "output": {
        "metadata": {
          "sid": "{$part-id}/{@eid}",
          "person": "x:persons/{$metadata-pid}/{$item-eid}"
        },
        "nodes": {
          "event": "x:events/{$sid} [x:events/{@eid}]"
        },
        "triples": [
          "{?event} a crm:E69_death",
          "{?event} crm:P2_has_type x:event-types/person.death",
          "{?event} crm:P100_was_death_of {$person}"
        ]
      },
      "children": [
        {
          "name": "event_description"
        },
        {
          "name": "event_note"
        },
        {
          "name": "event_chronotopes"
        },
        {
          "name": "event_assertion"
        },
        {
          "name": "event_tag"
        }
      ]
    }
  ]
}
```

These are the resulting nodes for birth (to make them more readable I replaced the part's GUID with `PID`):

| label                   | uri                     | sid                   |
| ----------------------- | ----------------------- | --------------------- |
| x:events/birth          | x:events/pid/birth      | PID/birth             |
| x:places/arezzo         | x:places/arezzo         | PID/birth/chronotopes |
| x:timespans/ts#5        | x:timespans/ts#5        | PID/birth/chronotopes |
| x:guys/eletta_canigiani | x:guys/eletta_canigiani | PID/birth/tag         |
| x:guys/ser_petracco     | x:guys/ser_petracco     | PID/birth/tag         |

These are the birth triples:

| S                  | P                           | O                                                                           | sid                   |
| ------------------ | --------------------------- | --------------------------------------------------------------------------- | --------------------- |
| x:events/pid/birth | rdf:type                    | crm:e67_birth                                                               | PID/birth             |
| x:events/pid/birth | crm:p2_has_type             | x:event-types/person.birth                                                  | PID/birth             |
| x:events/pid/birth | crm:p98_brought_into_life   | x:persons/mpid/alpha                                                        | PID/birth             |
| x:events/pid/birth | crm:p3_has_note             | Petrarch was born in 1304 at Arezzo from ser Petracco and Eletta Canigiani. | PID/birth/description |
| x:places/arezzo    | rdf:type                    | crm:e53_place                                                               | PID/birth/chronotopes |
| x:events/pid/birth | crm:p7_took_place_at        | x:places/arezzo                                                             | PID/birth/chronotopes |
| x:events/pid/birth | crm:p4_has_time-span        | x:timespans/ts#5                                                            | PID/birth/chronotopes |
| x:timespans/ts#5   | crm:p82_at_some_time_within | 1304                                                                        | PID/birth/chronotopes |
| x:timespans/ts#5   | crm:p87_is_identified_by    | 1304 AD                                                                     | PID/birth/chronotopes |
| x:events/pid/birth | crm:p96_by_mother           | x:guys/eletta_canigiani                                                     | PID/birth/tag         |
| x:events/pid/birth | crm:p97_from_father         | x:guys/ser_petracco                                                         | PID/birth/tag         |

These are the death nodes:

| label              | uri                | sid                           |
| ------------------ | ------------------ | ----------------------------- |
| x:events/death     | x:events/pid/death | PID/death                     |
| x:places/arqua     | x:places/arqua     | PID/death/chronotopes         |
| x:timespans/ts#12  | x:timespans/ts#12  | PID/death/chronotopes         |
| x:assertions/as#14 | x:assertions/as#14 | PID/death/assertion           |
| x:citations/cit#16 | x:citations/cit#16 | PID/death/assertion/reference |

These are the death triples:

| S                  | P                                  | O                                                | sid                           |
| ------------------ | ---------------------------------- | ------------------------------------------------ | ----------------------------- |
| x:events/pid/death | rdf:type                           | crm:e69_death                                    | PID/death                     |
| x:events/pid/death | crm:p2_has_type                    | x:event-types/person.death                       | PID/death                     |
| x:events/pid/death | crm:p100_was_death_of              | x:persons/mpid/alpha                             | PID/death                     |
| x:events/pid/death | crm:p3_has_note                    | Petrarch died in 1374, July 18 (or 19) at Arquà. | PID/death/description         |
| x:places/arqua     | rdf:type                           | crm:e53_place                                    | PID/death/chronotopes         |
| x:events/pid/death | crm:p7_took_place_at               | x:places/arqua                                   | PID/death/chronotopes         |
| x:events/pid/death | crm:p4_has_time-span               | x:timespans/ts#12                                | PID/death/chronotopes         |
| x:timespans/ts#12  | crm:p82_at_some_time_within        | 1374.63172                                       | PID/death/chronotopes         |
|                    |                                    |                                                  |                               |
|                    |                                    | type: xs:float                                   |                               |
|                    |                                    | numeric: 1,374.63                                |                               |
|                    |                                    |                                                  |                               |
| x:timespans/ts#12  | crm:p87_is_identified_by           | 18 Jul 1374 AD                                   | PID/death/chronotopes         |
|                    |                                    |                                                  |                               |
|                    |                                    | language: en                                     |                               |
|                    |                                    |                                                  |                               |
| x:events/pid/death | x:has_probability                  | 2                                                | PID/death/assertion           |
|                    |                                    |                                                  |                               |
|                    |                                    | type: xsd:short                                  |                               |
|                    |                                    | numeric: 2                                       |                               |
|                    |                                    |                                                  |                               |
| x:assertions/as#14 | rdf:type                           | crm:e13_attribute_assignment                     | PID/death/assertion           |
| x:assertions/as#14 | crm:p140_assigned_attribute_to     | x:events/pid/death                               | PID/death/assertion           |
| x:assertions/as#14 | crm:p141_assigned                  | x:has_probability                                | PID/death/assertion           |
| x:assertions/as#14 | crm:p177_assigned_property_of_type | crm:e55_type                                     | PID/death/assertion           |
| x:citations/cit#16 | rdf:type                           | crm:e31_document                                 | PID/death/assertion/reference |
| x:citations/cit#16 | rdfs:label                         | Rossi 1963 p.123                                 | PID/death/assertion/reference |
| x:assertions/as#14 | crm:p70i_is_documented_in          | x:citations/cit#16                               | PID/death/assertion/reference |
