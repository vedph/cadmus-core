# Cadmus Backend Core

This is the unified backend core for the Cadmus framework. It merges the following repositories:

- [cadmus-bricks](https://github.com/vedph/cadmus-bricks)
- [cadmus_core](https://github.com/vedph/cadmus_core)
- [cadmus-migration-v3](https://github.com/vedph/cadmus-migration-v3)
- [cadmus-general](https://github.com/vedph/cadmus-general)
- [cadmus-philology](https://github.com/vedph/cadmus-philology)
- [cadmus_tool](https://github.com/vedph/cadmus_tool)
- [cadmus-api](https://github.com/vedph/cadmus-api)
- [cadmus-graph-studio-api](https://github.com/vedph/cadmus-graph-studio-api)

These repositories are now archived and no longer maintained. Please use this repository instead, which provides:

- centralized codebase for all core Cadmus backend components.
- unified versioning and release management.

The projects were just copied into this solution from the above repositories, and then their configuration files were updated to use the new unified versioning and release management. The code itself was not changed, so it is still the same as in the original repositories. Anyway, new development will be done here.

## Projects

- **bricks**:
  - `Cadmus.Refs.Bricks`: sub-models for references
  - `Cadmus.Mat.Bricks`: sub-models for material description
  - `Cadmus.Bricks.Api`: API for bricks demo
- **core**:
  - `Cadmus.Core`: core models and logic
  - `Cadmus.Seed`: core models and logic for seeding mock data
  - `Cadmus.Mongo`: MongoDB data repository
- **index**:
  - `Cadmus.Index`: core models and logic for data index
  - `Cadmus.Index.Ef`: EntityFramework base implementation for data index (currently used for data storage)
  - `Cadmus.Index.Ef.PgSql`: PostgreSQL implementation of `Cadmus.Index.Ef`
  - `Cadmus.Index.Sql`: SQL base index implementation (currently used for query)
  - `Cadmus.Index.PgSql`: PostgreSQL implementation of `Cadmus.Index.Sql`
- **graph**:
  - `Cadmus.Graph`: core models and logic for semantic data graph
  - `Cadmus.Graph.Api`: API for semantic data graph demo
  - `Cadmus.Graph.Ef`: EntityFramework base models for semantic data graph
  - `Cadmus.Graph.Ef.PgSql`: PostgreSQL implementation of `Cadmus.Graph.Ef`
  - `Cadmus.Graph.Extras`: semantic data graph extensions
- **migration**:
  - `Cadmus.Export`: core models and logic for data export
  - `Cadmus.Export.ML`: data export to markup languages
  - `Cadmus.Export.Rdf`: data export to RDF
  - `Cadmus.Import`: core models and logic for data import
  - `Cadmus.Import.Excel`: import from Excel data source
  - `Cadmus.Import.Proteus`: import within the Proteus conversion framework
  - `Proteus.Rendering`: data rendering into text based on the Proteus conversion framework
- **parts**:
  - `Cadmus.General.Parts`: generic data models
  - `Cadmus.Seed.General.Parts`: seeders for generic data models
  - `Cadmus.Philology.Tools`: tools for philology data models
  - `Cadmus.Philology.Parts`: philology data models
  - `Cadmus.Seed.Philology.Parts`: seeders for philology data models
- **CLI**:
  - `Cadmus.Cli.Core`: core models and logic for CLI
  - `cadmus-mig`: CLI migration tool
  - `cadmus-tool`: CLI tool
- **API**:
  - `Cadmus.Api`: demo API
  - `Cadmus.Api.Config`: API configuration
  - `Cadmus.Api.Controllers`: API controllers
  - `Cadmus.Api.Controllers.Export`: data export API controllers
  - `Cadmus.Api.Controllers.Import`: data import API controllers
  - `Cadmus.Api.Models`: API data exchange models
  - `Cadmus.Api.Services`: API services
- **graph studio**:
  -	`Cadmus.GraphStudio.Api`: API for graph studio

>If you add new projects to this solution, ensure to add them in `buildnpub.ps1` (you can use getbuildorder.ps1 to get the build order).

## Docker Images

🐋 Before creating Docker images, ensure you have a buildx builder instance running that supports multi-arch:

```sh
docker buildx create --use --name multi-arch-builder || docker buildx use multi-arch-builder
docker buildx inspect --bootstrap
```

>To run natively on Linux VMs, macOS (both Intel and Apple Silicon), and Windows (via WSL2 or Docker Desktop)—`linux/amd64` and `linux/arm64` are the only two targets we need. Note that `docker buildx` automatically injects variables like `TARGETARCH` and `TARGETOS` into the scope of your build. In `Dockerfile` we pass these directly to the .NET CLI commands.

These commands build for multiple platforms and push directly to Docker Hub:

- 🐋 **Cadmus.Api**:

```sh
docker buildx build --platform linux/amd64,linux/arm64 -t vedph2020/cadmus-api:13.0.4 -t vedph2020/cadmus-api:latest --push .
```

- 🐋 **Cadmus.Bricks.Api**:

```sh
docker buildx build -f Dockerfile-bricks --platform linux/amd64,linux/arm64 -t vedph2020/cadmus-bricks-api:0.0.4 -t vedph2020/cadmus-bricks-api:latest --push .
```

- 🐋 **Cadmus.GraphStudio.Api**:

```sh
docker buildx build -f Dockerfile-graph-studio --platform linux/amd64,linux/arm64 -t vedph2020/cadmus-graph-studio-api:1.0.0 -t vedph2020/cadmus-graph-studio-api:latest --push .
```
