# History

## 17.0.0

- 2026-11-05: ⚠️ **breaking**: the items index (`Cadmus.Index*`) was rewritten in plain SQL for PostgreSQL, dropping Entity Framework and the unused SqlKata dependency, fixing several bugs, and optimizing its schema. See [the index documentation](docs/index.md) for the query syntax and the schema.
  - packages:
    - `Cadmus.Index.PgSql` is now the single implementation for both writing and searching (Npgsql only). Its components are tagged `item-index-writer.pgsql` and `item-index-reader.pgsql`.
    - removed `Cadmus.Index.Ef` and `Cadmus.Index.Sql`.
    - `Cadmus.Index.Ef.PgSql` is now a **deprecated** shim without EF, aliasing `Cadmus.Index.PgSql` under the legacy tags `item-index-writer.ef-pg` and `item-index-reader.ef-pg`, so that existing profiles keep working. It will be removed in the next major version.
    - the query parser (`IndexQueryParser`, producing a syntax tree in `Cadmus.Index.Search`) is now in `Cadmus.Index`, independent from any storage; `PgSqlQueryBuilder` turns it into parameterized SQL.
    - removed dead code: the MySQL-era SQL writer/reader base classes, `PgSqlHelper`, `DbDataReaderExtensions`, `IndexItem`, `IndexPin`.
  - interfaces (`Cadmus.Index`):
    - `IItemIndexWriter` and `IItemIndexReader` methods are now asynchronous with cancellation (`WriteItemAsync`, `WritePartAsync`, `DeleteItemAsync`, `DeletePartAsync`, `WriteItemsAsync`, `CreateIndexAsync`, `ClearAsync`, `SearchItemsAsync`, `SearchPinsAsync`). The old members are kept as obsolete default interface members, which wait for completion like before; `Close` is obsolete and does nothing, as connections are pooled and released after each operation.
    - `ItemIndexer`: `BuildAsync`, `AddItemAsync`, `ClearAsync` (old members obsolete). Each page of 100 items is written in a single batch; if it fails, its items are written one by one, logging and skipping the faulty ones. Fixed: `AddItem` and `Build` did not await the writer.
    - `ItemIndexFactory` supplies the profile flag definitions (`flags`) to component options having a `FlagDefinitions` property, so that flag labels can be used in queries (previously flag labels always resolved to 0).
    - `CadmusQueryException` has a `Position` property.
  - fixed query bugs:
    - 🔒 **SQL injection**: any text outside clauses was copied into the SQL code (e.g. `[title=x] OR (subquery)` allowed reading any data to authenticated users). Now the query is parsed, and all the values are SQL parameters.
    - in items search, two clauses on different pins combined with `AND` (e.g. `[tag=odd] AND [year==1200]`) never matched: now each pin clause can be satisfied by any pin of the item.
    - items without pins (e.g. without parts) were never found, even by title or flags.
    - values with `_` or `%` never matched with `=`, `<>`, regular expressions and similarity; backslashes were doubled, so that regular expressions like `\d` never matched.
    - numeric operators (`==`, `<`, etc.) failed with a PostgreSQL error (they used MySQL syntax).
    - syntax errors and invalid regular expressions now produce an `error` message in the API response, instead of HTTP 500.
  - query language additions: `NOT`; implicit `AND` between consecutive operands; `@name` to target pins whose name is equal to a field alias (e.g. `[@title=x]`: parts emitting pins named `title`, `type` or `name` could not be searched by them); fields `creatorId`, `userId`, `timeCreated`, `timeModified`; date comparisons for time fields (`[timeModified>=2024-05-01]`). A query without `[` now means "title contains" rather than "title equals".
  - writer:
    - each save is a single round trip, executed atomically; writes to the same item are serialized, so that concurrent saves of the same part can no longer duplicate pins.
    - values longer than their columns are truncated (previously, a pin name longer than 100 characters made the whole write fail, leaving the index out of sync with the data); NUL characters are removed; pins always get the ID of the item and part being written.
    - the database is ensured (created or upgraded) once per process, rather than checked at each write.
  - schema: idempotent script, applied at each API startup (`HostSeedExtensions.EnsureIndexDatabaseAsync`) and by the writer, which **upgrades legacy databases in place**:
    - IDs are `varchar(36)` rather than `bpchar(36)` (comparisons with text parameters prevented the use of their indexes), pin IDs are `int8` (pins are reinserted at each save, so their IDs grow continuously), the sort key is not nullable.
    - new indexes for the actual queries: `pin(item_id)`, `pin(part_id)` (previously missing: every save and every pin join scanned the whole table), `pin(name, value)`, trigram indexes on pin values, item titles and descriptions, `item(sort_key, id)` for sorted paging. Removed indexes useless to the queries (e.g. on flags).
  - API (`Cadmus.Api.*`): the index factory is cached per profile, and the profile is loaded once per process, rather than building a new host and reading the profile at each index operation; searches are cancelled when the request is aborted; `EnableSensitiveDataLogging` is no longer used.
  - CLI: `create-db index` also upgrades an existing index database; `build-index-sql` shows the parameterized SQL for items or pins (`-p`).

### Migrating to 17.0.0

Solutions depending on these packages (API and CLI projects) should:

1. **update all the Cadmus packages** to 17.0.0, and remove any reference to `Cadmus.Index.Ef` or `Cadmus.Index.Sql`, which no longer exist. Replace `Cadmus.Index.Ef.PgSql` with `Cadmus.Index.PgSql` where possible; keeping it still works, as a deprecated shim.
2. **index factory providers**: projects with their own provider (e.g. `StandardItemIndexFactoryProvider` in CLI tools) should register both assemblies, so that both the new and the legacy tags are resolved:

   ```cs
   ItemIndexFactory.ConfigureServices(services,
       // Cadmus.Index.PgSql
       typeof(PgSqlItemIndexWriter).Assembly,
       // Cadmus.Index.Ef.PgSql (legacy tags)
   #pragma warning disable CS0618
       typeof(Cadmus.Index.Ef.PgSql.EfPgSqlItemIndexWriter).Assembly);
   #pragma warning restore CS0618
   ```

   Providers registering only the legacy assembly keep working with profiles using the legacy tags. API projects using `Cadmus.Api.Services` need no change.
3. **profiles** (optional, recommended): replace `item-index-writer.ef-pg` with `item-index-writer.pgsql`, and `item-index-reader.ef-pg` with `item-index-reader.pgsql`. This requires a provider registering `Cadmus.Index.PgSql` (see above).
4. **code** using the index directly (obsolete members produce warnings, but keep working as before):
   - `await writer.WriteItemAsync(item)` instead of `writer.WriteItem(item)`, and so on for the other members; remove `Close()` calls. Note that calls to the obsolete members are now blocking until completion, so that non-awaited calls (e.g. `_indexWriter.WriteItem(item);` in importers) still work.
   - `await indexer.BuildAsync(repository, filter, progress, cancel)` instead of `indexer.Build(repository, filter, cancel, progress)` (mind the parameters order); `ClearAsync` instead of `Clear`.
   - `PgSqlIndexSchema.GetSql()` instead of `EfPgSqlItemIndexWriter.GetPgSqlSchema()`.
   - `PgSqlQueryBuilder.BuildItemQuery`/`BuildPinQuery` instead of `BuildForItem`/`BuildForPin`.
5. **databases**: no action required. Existing index databases are upgraded in place at the first API startup: for big indexes this rewrites the `item` and `pin` tables once, which may take some time (the startup waits for it). Alternatively, run `cadmus-tool create-db index <name>` before deploying, or drop the index database and rebuild it with `cadmus-tool index`.
6. **frontend**: no change required. Queries using plain text now search titles containing it rather than equal to it.

## 16.0.15

- 2026-11-02: ⚠️ fixed and hardened databases initialization at API startup (`Cadmus.Api.Services`, `Cadmus.Api.Config`, `Cadmus.Api`):
  - fixed: the graph database was created only while seeding mock items, i.e. only when the data database did not exist and `Seed:ItemCount` was greater than 0. So with `Seed:ItemCount=0` (typical in production), or when the data database already existed, the graph database was never created and every save failed. Now each database (index, graph, data) is ensured independently at each startup, whatever the state of the others: index and graph come first, so they exist before seeded items are written to them.
  - fixed: if data seeding failed after creating the MongoDB database, retries and later startups found it existing and silently skipped seeding. Now an incomplete data database is dropped so that it gets recreated.
  - PostgreSQL databases (auth, index, graph) are created via the new `PgSqlDatabaseInitializer`, which holds a server-wide advisory lock during check-and-create (safe for concurrent startups), and drops a partially created database if its schema fails.
  - new `CadmusDatabaseConfig` is the single source of truth for database names and connection strings, used by both `ServiceConfigurator` and the seeders.
  - new `SeedRetryPolicy` retries only transient connection errors (PostgreSQL and MongoDB not yet ready) with delays of 10, 30, 60 seconds; other errors fail fast.
  - all the seed output now goes through `ILogger`, with clear per-database messages for container logs. `HostSeedExtensions` exposes the individual steps (`EnsureIndexDatabaseAsync`, `EnsureGraphDatabaseAsync`, `SeedDataDatabaseAsync`) for other API projects.
  - fixed: TaxoStore services were registered twice in `Program.cs`, so two hosted initializers raced on the same database (the loser shuts down the API).
  - CLI `create-db`: no longer fails if the database exists, falls back to `ConnectionStrings:Index` for the graph like the API, and shows the target server.
  - updated packages: `Fusi.DbManager*` 6.1.0 (database owner and locale no longer hard-coded, partially created databases removed on error, `ClearDatabase` restarts all owned sequences); `TaxoStore.*` 2.0.2, built against CsvHelper 34: this fixes the API shutting down at its first startup, when seeding the TaxoStore database failed with `MissingMethodException` (TaxoStore 2.0.1 was built against CsvHelper 33).

## 16.0.14

- 2026-10-30:
  - added `IsMergeEnabled` to `MongoEntrySetExporterOptions` for multi-pass imports.
  - fixes to some test race conditions.
  - updated packages.

## 16.0.13

- 2026-10-23: updated packages (authorization revision).

## 16.0.12

- 2026-10-17: ⚠️ fixed rate limiter behind reverse proxies, where all the clients appeared to have the same IP address (see the _Server Settings_ section in the README):
  - forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`) are now honored only from trusted proxies: localhost, plus those listed in the new `ForwardedHeaders:KnownProxies` (IPs) and `ForwardedHeaders:KnownNetworks` (CIDR) settings. **Breaking**: previously any sender was trusted; to restore this behavior, set `ForwardedHeaders:TrustAllProxies` to `true` (only if the API port is not publicly reachable). Also added `ForwardedHeaders:ForwardLimit`.
  - added `ServiceConfigurator.UseForwardedHeaders` and `ServiceConfigurator.UseRateLimiter` to `Cadmus.Api.Config`. API projects should use them in `Program.cs` and call `UseRateLimiter` **after** `UseAuthentication`, so that authenticated users are limited by their name rather than by their IP address (previously the rate limiter came first, so all clients were limited by IP).
  - rate limit partition keys are now prefixed (`user:` or `ip:`), and IPv4-mapped IPv6 addresses are normalized; the key is logged at debug level only.
  - rejected requests get a `Retry-After` header and JSON content type, and the rate limit email notification is throttled (at most once every 30 minutes).

## 16.0.11

- 2026-10-16: updated packages (taxonomies store v2, CLI auth fixing runtime issues in tool).

## 16.0.9

- 2026-10-15:
  - updated packages.
  - 🆕 added `ItemIdEntrySetExporter` (`it.vedph.entry-set-exporter.cadmus.item-id`) to `Cadmus.Import.Proteus`, dumping the item IDs, titles, metadata part ID and `eid` metadata values of imported items into a JSON file, for multi-pass imports. `Cadmus.Import.Proteus` now depends on `Cadmus.General.Parts`.
  - 🆕 added `ExportedItemIdSet` to load the output of `ItemIdEntrySetExporter` and look up items by `eid` or item ID.

## 16.0.8

- 2026-10-13: updated packages.
- 2026-09-30: handle `*` in CORS implementations.

## 16.0.7

- 2026-09-25: 🆕 added repository `ImportItem` and `ExportItem` and the corresponding API controller methods to upload and download an item. This can be used to export a single item from the editor, and later import it into another or the same database with the same schema. The exported item is a JSON file with the following structure:

```json
{
  "item": { ... },
  "parts": [ ... ]
}
```

When importing into a database an item with the same ID cannot exist. So we are always adding new item and parts. If the item exists in history only, the imported item and its part IDs are regenerated to avoid conflicts in the history. If it exists in the items collection, the import fails.

## 16.0.6

- 2026-09-22:
  - updated packages.
  - migrated to MTP test platform (<https://learn.microsoft.com/en-us/dotnet/core/testing/migrating-vstest-microsoft-testing-platform>).

## 16.0.5

- 2026-09-15:
  - 🆕 added `TaxoStore` `import-store` command to CLI tool.
  - updated packages.

## 16.0.4

- 2026-09-11: updated packages (accepting NPOI license).

## 16.0.3

- 2026-09-04: more tests for JSON exporter.
- 2026-09-03: updated packages.
- 2026-09-02: complete implementation of `JsonExporter`.
- 2026-09-01: Added `MongoThesaurusFluidFilter`.

## 16.0.2

- 2026-08-30: updated packages.
- 2026-08-20: updated packages (incorporating Proteus v14). This affected `Cadmus.Export` and `Proteus.Rendering` which required some refactoring for filters.
- 2026-08-16: updated packages.
- 2026-08-05:
  - merged `cadmus-mig` into `cadmus-tool`.
  - completed `JsonExporter` factory infrastructure.
- 2026-08-04: further mapping generalization:
  - `GraphSource` renamed into `MapperSource` and moved into `Cadmus.Export`.
  - ⚠️ moved all mapping components to `Cadmus.Export` under a new `Mapping` namespace, adjusting them accordingly.
  - added `Cadmus.Codicology.Mapping` project from `Cadmus.Codicology` to include the correponding mapping macro in this solution.
  - added `JsonExporter` and its dependencies to export Cadmus items into transformed JSON.

## 16.0.0

- 2026-07-30: ⚠️ generalized some features of the `Cadmus.Graph` project's mapping components to reuse them in a slightly different context. `JsonNodeMapper` was used to build RDF nodes and triples from a JSON object and relies on `NodeMapping`'s. This represented the mapping of a single node in the tree of properties which underlies a JSON object. This node was selected via JMESPath for maximum flexibility; and `NodeMapping`s could be nested as they reflected the nesting of properties in a JSON object. The whole select-and-process logic in mapper and mappings has been generalized, decoupling it from the specific RDF output. To this end, a new abstract class was created to represent the mapping model and logic, completely decoupled from the specific output one wants to emit for each processed node. Then, a `GraphNodeMapping` was derived from it, adding the output generation part specific to RDF graph.

This preserved the whole functionality of the Graph projects intact, while splitting it into more components. The plan is then to reuse the same node mapping + node mapper model and logic to transform a source JSON object into another JSON object with a different schema. We will have a tree of node mappings with their JMES select expressions, and then use them to generate pieces of JSON code to be finally merged into a single JSON root object representing the complete output of the transformation. Fluid will provide templates for the transformation of each selected node. This means implementing a new `JsonNodeMapping` derived from this new abstract `NodeMapping` and add it properties and logic for outputting JSON via Fluid, like a `Template` property for its Fluid template, and a `TargetProperty` for the name of the property of the root JSON object working as a target slot in the target root JSON object. In the end, applying all these mappings in their cascading order will generate a full JSON object instead of an RDF graph.

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
