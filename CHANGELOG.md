# History

- 2026-08-05:
  - merged `cadmus-mig` into `cadmus-tool`.
  - completed `JsonExporter` factory infrastructure.
- 2026-08-04: further mapping generalization:
  - `GraphSource` renamed into `MapperSource` and moved into `Cadmus.Export`.
  - ⚠️ moved all mapping components to `Cadmus.Export` under a new `Mapping` namespace, adjusting them accordingly.
  - added `Cadmus.Codicology.Mapping` project from `Cadmus.Codicology` to include the correponding mapping macro in this solution.
  - added `JsonExporter` and its dependencies to export Cadmus items into transformed JSON.

## 16.0.0

- 2026-07-30: ⚠️ generalized some features of the `Cadmus.Graph` project's mapping components to reuse them in a slightly different context. `JsonNodeMapper` was used to build RDF nodes and triples from a JSON object and relies on `NodeMapping`'s. This represented the mapping of a single node in the tree of properties which underlies a JSON object. This node was selected via JMESPath for maximum flexibility; and `NodeMapping`s could be nested as they reflected the nesting of properties in a JSON object. We need to generalize the whole select-and-process logic in mapper and mappings decoupling it from the specific RDF output. To this end, a new abstract class was created to represent the mapping model and logic, completely decoupled from the specific output one wants to emit for each processed node. Then, a `GraphNodeMapping` was derived from it, adding the output generation part specific to RDF graph.

This preserved the whole functionality of the Graph projects intact, while splitting it into more components. The plan is then to reuse the same node mapping + node mapper model and logic to transform a source JSON object into another JSON object with a different schema. We will have a tree of node mappings with their JMES select expressions, and then use them to generate pieces of JSON code to be finally merged into a single JSON root object representing the complete output of the transformation. I will be using Fluid to provide templates for the transformation of each selected node. This means that I will implement a new `JsonNodeMapping` derived from this new abstract `NodeMapping` and add it properties and logic for outputting JSON via Fluid, like a `Template` property for its Fluid template, and a `TargetProperty` for the name of the property of the root JSON object working as a target slot in the target root JSON object. In the end, applying all these mappings in their cascading order will generate a full JSON object instead of an RDF graph.

While I could just use a single template to transform the whole source document at once, this approach is better because:

- it allows to leverage the full selection power of JMESPath. Fluid (https://github.com/sebastienros/fluid) is not specifically targeted to a JSON source.
- it allows a modular approach to the transformation, node by node composing a final result, rather than all at once.
- the tree of mappings fits the tree of properties of a JSON document.

Changes:

- `Cadmus.Graph`/`NodeMapping.cs` — now abstract; dropped `Output`, added abstract `Clone()` and a `CopyTo`/`AppendExtra` hook for subclasses.
- new `GraphNodeMapping.cs` — concrete, adds `Output`.
- `Cadmus.Graph`/`JsonNodeMapper.cs` — now the abstract select-and-process engine.
- new `JsonGraphNodeMapper.cs` — concrete RDF mapper (AddNodes/AddTriples/SID-mandatory check), carries the [Tag("node-mapper.json")] attribute that was on the old class.
- new `NodeMappingJsonConverter.cs` — System.Text.Json can't deserialize into an abstract type on its own (needed once Children became IList<NodeMapping>); this converter is attached via [JsonConverter(...)] on NodeMapping itself, so no call site had to change its serializer options.
- every consumer updated to the new names: Cadmus.Graph.Ef (EfMapping, EfGraphRepository), Cadmus.GraphStudio.Api, Cadmus.Api/WalkerDemoGraphRepository, cadmus-tool graph commands, and all Cadmus.Graph* test projects.

- 2026-07-28:
  - more documentation, code modernization and full code revision for `Cadmus.Graph*` projects.
  - more tests for `Cadmus.Graph*` projects.

## 15.0.1

- 2026-07-24:
  - added options to `CadmusEntrySetContextPatcher`.
  - updated packages.

## 15.0.0

- 2026-07-21:
  - updated packages and reviewed code using `TextRange` which changed from `struct` to `record`.
  - added `<PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />` to all library projects.
  - ⚠️ commented out `MspOperation` and related code, and fixed orthography layer seeder which was still using it.
- 2026-07-13: configured `Cadmus.Api` demo for taxonomies store.

## 14.0.2

- 2026-07-12:
  - added account management commands to cadmus-tool.
  - added CI workflow for cadmus-tool.
  - updated packages.

## 14.0.2

- 2026-07-05: replaced old reference to Fusi.Tools.Config namespace with Fusi.Tools.Configuration in `TagAttributeToTypeMap`.`GetTag`.
- 2026-07-04: 🆕 added item metadata builders feature with its endpoint `api/items/{id}/metadata`. These are components which can be configured from the API profile to generate title and/or description of a given item from its parts. ⚠️ You need to add this code to your API `Program`.`ConfigureAppServices`:

```cs
// metadata builder factory provider
services.AddSingleton<IItemMetadataBuilderFactoryProvider>(_ =>
    new StandardItemMetadataBuilderFactoryProvider(
        config.GetConnectionString("Default")!));
```

To use this feature, configure builders in your `seed-profile.json`: for each builder specify its ID (equal to its tag attribute) and key(s) with the item's facet ID(s) it targets (separated by space) like in this example:

```json
{
  "metadataBuilders": [
    {
      "id": "item-metadata-builder.eid",
      "keys": "facet1 facet2"
    }
  ]
}
```

## 14.0.0

- 2026-07-03:
  - renamed `CadmusApi` to `Cadmus.Api`.
  - Docker images.
- 2026-07-01: initial commit.
