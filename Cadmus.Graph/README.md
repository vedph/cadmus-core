# Cadmus.Graph

Cadmus RDF graph export core components. This library contains the core logic and models for exporting a subset of Cadmus data (which are essentially objects as properties trees and are stored in a JSON format) into RDF graphs.

At the heart of the graph is data projection via mapping rules:

(1) a **source object** (`GraphSource`) is provided to the mapper. The current implementation relies on objects serialized into JSON. Source object are Cadmus _items_ or _parts_ (thesauri, i.e. taxonomies, can be imported as nodes, but this does not happen via mapping as it's a one-time procedure). Ultimately, from the point of view of the mapper any source object is just JSON code representing it. Between the _source_ object and the _mappings_ there is an intermediate layer, represented by adapter components. Their task is adapting that object to the mappings, and providing additional information from it.

(2) the **mapper** finds all the mappings matching the source object, and applies each of them, collecting the results (nodes and triples) into a graph set.

(3) the **graph set** is merged into the graph store.

## Adapters

The mappings used to project data on the semantic graph are weakly coupled to the type of their source objects, thanks to an intermediate layer added between source objects and mappings, in the form of _adapters_.

A _graph source adapter_ is a component implementing an interface (`IGraphSourceAdapter`), which dictates that the component must have a function that:

- gets as input an item or a part, together with a dictionary of additional metadata;
- possibly injects _metadata_ in a received dictionary, extracting them from the source object, to be later consumed by mapping rules.
- outputs an object (or null) adapted to be plugged into the mapping process, plus a filter for the mappings to be applied. Currently the adapted object is serialized into JSON.

## Mappings

The mapping between Cadmus source data (items and parts) and nodes is defined by node mappings (`NodeMapping`). This is the core of the projection mechanism, which extracts a subset of source data into a graph of nodes.

There are two types of sources:

- item: a Cadmus item. You can have mappings for the item; its group; and its facet.
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

The entity source ID (SID) is calculated so that _the same sources always point to the same entities_. The SID is essential for connecting Cadmus data to the entities and keeping them in synch.

The algorithm building the SID is idempotent. This is ensured by the fact that GUIDs are unique by definitions. A SID is built with these components:

(a) for **items**:

1. the 36-characters _GUID_ of the source (item).
2. if the node comes from a group or a facet, the suffix `|group` or `|facet`. The group ID can be composite (using slash, e.g. `alpha/beta`); in this case, a mapping producing nodes for groups emits several nodes, one for each component. The top component is the first in the group ID, followed by its children (in the above sample, `beta` is child of `alpha`). Each of these nodes has an additional suffix for the component ordinal, preceded by `|`.

Examples:

- `76066733-6f81-48dd-a653-284d5be54cfb`: an entity derived from an item.
- `76066733-6f81-48dd-a653-284d5be54cfb|group`: an entity derived from an item's group.
- `76066733-6f81-48dd-a653-284d5be54cfb|group|2`: an entity derived from the 2nd component of an item's composite group.

(b) for **parts**:

1. the _GUID_ of the source (part).
2. if the part has a role ID, the _role ID_ preceded by `#`.

Examples:

- `76066733-6f81-48dd-a653-284d5be54cfb`: an entity derived from a part.
- `76066733-6f81-48dd-a653-284d5be54cfb#some-role`: an entity derived from a part with a role.

#### EID

The "entry" ID is just a convention followed in models of Cadmus multi-entries parts. For instance, a manuscript's decorations part is a collection of decorations, each corresponding to an entry, optionally having its EID (exposed via an `eid` property). All the entries with EIDs get mapped into entities.

Thus, here we call EIDs the identifiers provided by users for entries in a Cadmus collection-part. When present, such EIDs are used to build node identifiers URIs (UIDs).

> This is not the unique purpose of EIDs. In general, this convention provides a mechanism to set a human-friendly identifier for some entity contained in a data model. Often these can also be used to deep-link some data in a model to another one.

#### UID

The entity ID is a _shortened URI_ where a conventional prefix replaces the namespace, calculated as defined by the entity mapping.

To get human-friendly UIDs, the UID is derived from a _template_ defined in the mapping rule generating a node. Whenever the template provides a result which happens to be already present and the mapping explicitly requests a unique UID, the UID gets a numeric suffix preceded by `#`. This suffix is granted to be unique in the context of our data.

> 💡 By convention, any UID built by mapping and potentially requiring this suffix must end with `##`, to indicate that a unique UID via an optional numeric suffix is required. For instance, `itn:timespans/ts##` means that the first time such a UID is generated it will be stored as `itn:timespans/ts`; the next time, it will rather be suffixed with a number, e.g. `itn:timespans/ts#3`.

The UID is built by a component implementing interface `IUidBuilder`. A RAM-based implementation of this UID (`RamUidBuilder`) is provided for testing. In real world, the implementation relies on a RDBMS database. The table used for it is named `uid_lookup`, and has these fields:

- `id` PK AI: an autonumber primary key handled by the database engine.
- `sid`: the SID linked to the UID.
- `unsuffixed`: the UID as generated by the mapping rule, before any suffixation.
- `has_suffix`: true if the UID must be suffixed. If true, the numeric value to append to the suffix is represented by `id`. This trick allows us to efficiently leverage the autonumber capabilities of a RDBMS to append a unique numeric suffix to whatever UID.

So, the RDBMS based implementation of the builder looks in this table for an UID whose unsuffixed form is equal to that being handled. If none is found, the UID is used as such, without any suffix, and stored there with `has_suffix`=false. If any is found but the caller requested a unique UID, the UID gets its `has_suffix` field set to true. This means that effectively the UID of the entity will be equal to the unsuffixed form + `#` + the value of `id`.

## Mapping Model

A mapping rule is modeled as an object having a number of properties, defining:

- its _metadata_ (like source type, SID, description, etc.).
- its _input_ (used to match sources).
- its _output_ (which nodes and triples to emit).

In turn, each mapping rule can include any number of _children rules_.

The model of each mapping is:

- `sourceType`\*: the type of the source object. This is meaningful for _root_ mappings only. The source type is a number: `0`=user, `1`=item, `2`=part, `3`=thesaurus, `4`=implicit (assigned to nodes automatically added because used in a triple without yet being present in the graph). Thus, mappings defined in a mappings document effectively use only `1` and `2`.
- `sid`\*: the SID of this mapping. This is usually specified at the root mapping level, but you can also override the root sid in your children mappings (almost always this means adding suffix(es) to the root SID). Thus, SIDs are inherited unless overridden in descendants. If a SID includes the `index` metadatum, and the mapper is processing an array, it will be recalculated for each array's item.
- `facetFilter`: an optional item's facet filter. When specified, the mapping will target only those items whose facet ID is _equal_ to this value.
- `groupFilter`: an optional item's group filter. This is a regular expression; when specified, the mapping will target only those items whose group ID _matches_ this expression.
- `flagsFilter`: an optional item's flags filter. This is a numeric value, where each bit represents a flag. When specified, the mapping will target only those items whose flags include at least _all_ the bits set in this value, i.e. all the flags specified in the filter must be present.
- `partTypeFilter`: an optional part's type ID filter. When specified, the mapping will target only those items whose part type ID is _equal_ to this value.
- `partRoleFilter`: an optional part's role ID filter. When specified, the mapping will target only those items whose part role ID is _equal_ to this value.
- `description`: an optional, human-readable short description for the mapping rule. This is useful for documentation purposes.

- `source`\*: the source expression representing the data selected by this mapping. In the current implementation this is a JMES path. For instance, `events[?type=='person.birth']` matches only the entries in the `events` array property of a part's model whose type is equal to `person.birth`.
  - when the source expression selects an _object_, you can refer to it as a whole with `.`, or to any of its properties by their name.
  - when the source expression selects an _array_, the mapper will loop through all its items, and run for each of them. So, you can still define your mappings in terms of a single object, which here is the array's item. Additionally, the `index` metadatum will be used to represent the 0-based index number of the item in the array.
- `scalarPattern`: the optional regular expression pattern which should match against a scalar value defined by the mapping's source expression for the mapping to be applied. When this is defined and does not match, the mapping will not be applied. This can be used to overcome the limitations of the source expression in languages like JMESPath, where e.g. `.[?lost==true]` is always evaluated as a match, even when the value of the scalar property `lost` is `false`. So, in this example setting `scalarPattern` to `true` and source to `lost` will apply the mapping only if this property's value is `true`.

- `output`:
  - `nodes`: an object (dictionary) where each property is the key of a node emitted by the mapping rule, whose string value is the node's identifier [template](#templates). Optionally, this template can be followed by space plus the node's label, and/or its tag between square brackets, the tag being preceded by `|`. For instance, `x:events/{$.} [label|tag]` defines the node's UID, its human-friendly label, and an optional tag.
  - `triples`: an array of strings, each representing a triple [template](#templates). Each triple is in any of these forms:
    - `S P O`: subject, predicate, object (all URIs);
    - `S P "O"`: subject, predicate, literal object in double quotes;
    - `S P "O"@lang`: subject, predicate, literal object in double quotes followed by a [BCP647](https://www.rfc-editor.org/info/bcp47) language tag (e.g. `"sample"@en`);
    - `S P "O"^^type`: subject, predicate, literal object in double quotes followed by a [datatype IRI](https://www.w3.org/TR/xmlschema-2/) (e.g. `"123"^^xs:int`).
  - `metadata`: optional metadata to be consumed in [templates](#templates). Metadata come from several sources: the source object, the mapping process itself, and these definitions in the mapping.

- `children`: children mappings. Each child mapping has the same properties of a root mapping, except for those which would make no sense in children, as noted above.

> 💡 The _source type_ is a number where `0`=user, `1`=item, `2`=part, `3`=thesaurus, `4`=implicit (assigned to nodes automatically added because used in a triple without yet being present in the graph).

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
  "id": "events.type=birth",
  "sourceType": "part",
  "partTypeFilter": "it.vedph.historical-events",
  "source": "events[?type=='person.birth']",
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

Here we map each birth event (as specified by `source`). For each of them, a child mapping matches the event's `eid` property, and outputs a node under the key `event`, whose template is `x:events/{$.}` (where `{$.}` is a [metadatum](#metadata) representing the value of the _current leaf node_ in the source tree). So, in this case the generated node will have an UID equal to `x:events/` plus the node's URI.

As a node is a complex object, in a template placeholder you can pick different properties from it. These are specified by adding to the node's key a **suffix** preceded by `:`. Available suffixes are:

- `:uri` = the node's generated URI. This is the default property; so when there is no suffix specified, the URI is picked.
- `:label` = the node's label.
- `:sid` = the node's SID.
- `:src_type` = the node's source type.

### Metadata

- syntax: `{$...}`

The mapping process can set metadata in the form of name=value string pairs. These get stored under arbitrary keys (some names are reserved), and are available to any template in the context of its root mapping.

Metadata can be emitted by the mapping process itself, or be defined in a mapping's output under the `metadata` property (an object where each property is a metadatum with its string value).

Currently the mapping process emits these metadata (whose names are reserved):

- `item-id`: the item ID (GUID).
- `item-eid` (\*): the EID of the item, as conventionally defined by the first matching metadatum with name = `eid` from the item's `MetadataPart`, if present. As this is the typical lookup mechanism, your consumer code can provide this additional metadatum by opting in via a metadata supplier.
- `part-id`: the part ID (GUID).
- `group-id`: the item's group ID.
- `facet-id`: the item's facet ID.
- `flags`: the item's flags.
- `.`: the value of the current leaf node in the source JSON data. For instance, if the mapping is selecting a string property from `events/event[0].eid`, this is the value of `eid`.
- `index`: the index of the element being processed from a source array. When the source expression used by the mapping points to an array, every item of the array gets processed separately from that mapping onwards. At each iteration, the `index` metadatum is set to the current index.

>Additionally, your backend code might use a metadata supplier with extra metadata sources to provide more metadata. A typical source is `ItemEidMetadataSource`, which adds `item-eid` (the value of metadatum `eid` in the `MetadataPart` if any) of the current item and `metadata-pid` (the part ID (GUID) of the metadata part, if any, of the current item).

### Macros

- syntax: `{!...}`

Macros are a modular way for customizing the mapping process when more complex logic is required. A macro is an object implementing an interface (`INodeMappingMacro`), requiring:

- the macro `Id` (an arbitrary string). This is used to call the macro from the template.
- the `Run` method, which runs the macro receiving the current data context, the placeholder position in the template and the template itself, and any arguments following the macro's ID; and returning a string or null.

The macro syntax in the placeholder is very simple: it consists of the macro ID, optionally followed by any number of arguments, separated by `&`, included in brackets. For instance:

```txt
!{some_macro(arg1 & arg2)}
```

Some macros are **built-in**, and conventionally their ID start with an underscore:

- `_hdate(json,property)`: this macro handles a Cadmus historical date and returns either its sort value, or its human-friendly, machine-parsable text value. Its arguments are:
  1. the JSON code representing a Cadmus historical date;
  2. the property of the date to return: `value` (default) or `text`.
- `_substring(string,index,[length])`: substring.

### Filters

Whenever a template represents a URI, i.e. in all the cases except for triple's object literals, once the template has been filled, the result gets filtered as follows:

- whitespaces are replaced with underscores;
- only letters, digits 0-9, and characters `:-_#/&%=.?` are preserved;
- letters are all lowercased;
- diacritics are removed.

Should you want to disable this filtering (which is generally _not_ recommended, as this filtering provides fairly common URI forms), start the template with `!`, which being a preprocessing directive will be discarded from the template itself.

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
