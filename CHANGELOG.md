# History

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

- 2026-07-03:
  - renamed `CadmusApi` to `Cadmus.Api`.
  - Docker images.
- 2026-07-01: initial commit.
