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
  - `CadmusApi`: demo API
  - `Cadmus.Api.Config`: API configuration
  - `Cadmus.Api.Controllers`: API controllers
  - `Cadmus.Api.Controllers.Export`: data export API controllers
  - `Cadmus.Api.Controllers.Import`: data import API controllers
  - `Cadmus.Api.Models`: API data exchange models
  - `Cadmus.Api.Services`: API services
- **graph studio**:
  -	`Cadmus.GraphStudio.Api`: API for graph studio
