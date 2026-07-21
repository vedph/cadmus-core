# History

- 2026-07-21:
  - updated packages and reviewed code using `TextRange` which changed from `struct` to `record`.
  - added `<PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />` to all library projects.
  - commented out `MspOperation` and related code, and fixed orthography layer seeder which was still using it.
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
