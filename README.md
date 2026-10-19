# Cadmus Backend Core

This is the unified backend core for the [Cadmus framework](https://vedph.github.io/cadmus-doc/). It merges the following repositories:

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

The projects were just copied into this solution from the above repositories, and then their configuration files were updated to use the new unified versioning (starting from 14.0.0) and release management. The code itself was not changed, so it is still the same as in the original repositories. Anyway, new development will be done here.

## Projects

- **bricks**:
  - `Cadmus.Refs.Bricks`: sub-models for references
  - `Cadmus.Mat.Bricks`: sub-models for material description
  - `Cadmus.Bricks.Api`: API for bricks demo
- **core**:
  - `Cadmus.Core`: core models and logic
  - `Cadmus.Seed`: core models and logic for seeding mock data
  - `Cadmus.Mongo`: MongoDB implementation of data repository
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
  - `Cadmus.Export.Json`: data export and remapping to JSON
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
docker buildx build -f Dockerfile-bricks --platform linux/amd64,linux/arm64 -t vedph2020/cadmus-bricks-api:16.0.12 -t vedph2020/cadmus-bricks-api:latest --push .
```

- 🐋 **Cadmus.GraphStudio.Api**:

```sh
docker buildx build -f Dockerfile-graph-studio --platform linux/amd64,linux/arm64 -t vedph2020/cadmus-graph-studio-api:1.0.0 -t vedph2020/cadmus-graph-studio-api:latest --push .
```

## Server Settings

These API settings (handled by `Cadmus.Api.Config`, so they apply to `Cadmus.Api` and to any API project built on it) are relevant when deploying in a server. You can set them in `appsettings.json` or, more commonly, via environment variables in `docker-compose.yml` (a `:` in a setting path becomes `__`, and array items are numbered, e.g. `ForwardedHeaders:KnownNetworks[0]` is `FORWARDEDHEADERS__KNOWNNETWORKS__0`):

| setting                            | environment variable                 | default    | description                                                                                                                                                                                     |
| ---------------------------------- | ------------------------------------ | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AllowedOrigins`                   | `ALLOWEDORIGINS__0`, ...             |            | the origins of the web app (CORS).                                                                                                                                                              |
| `Jwt:SecureKey`                    | `JWT__SECUREKEY`                     |            | the key used to sign authentication tokens: **always replace it** in a server.                                                                                                                  |
| `ForwardedHeaders:KnownProxies`    | `FORWARDEDHEADERS__KNOWNPROXIES__0`  |            | the IP addresses of trusted reverse proxies, whose `X-Forwarded-For` and `X-Forwarded-Proto` headers are used to get the client IP address and scheme (localhost is always trusted).            |
| `ForwardedHeaders:KnownNetworks`   | `FORWARDEDHEADERS__KNOWNNETWORKS__0` |            | the networks (CIDR, e.g. `172.16.0.0/12` for the default Docker networks) of trusted reverse proxies.                                                                                           |
| `ForwardedHeaders:TrustAllProxies` | `FORWARDEDHEADERS__TRUSTALLPROXIES`  | `false`    | true to trust the forwarded headers from any sender (the behavior before v16.0.12). Use only if the API port is reachable exclusively via the proxy, otherwise clients can fake their IP.       |
| `ForwardedHeaders:ForwardLimit`    | `FORWARDEDHEADERS__FORWARDLIMIT`     | `1`        | the number of proxies in front of the API (e.g. 2 for a CDN in front of NGINX).                                                                                                                 |
| `RateLimit:IsDisabled`             | `RATELIMIT__ISDISABLED`              | `true`     | true to disable rate limiting; set it to `false` to enable it.                                                                                                                                  |
| `RateLimit:PermitLimit`            | `RATELIMIT__PERMITLIMIT`             | `100`      | the number of requests allowed to each client (the authenticated user name, or the IP address for anonymous requests) in the time window. Further requests get 429 with a `Retry-After` header. |
| `RateLimit:QueueLimit`             | `RATELIMIT__QUEUELIMIT`              | `0`        | the number of requests over the limit which are queued (rather than rejected) until the next time window.                                                                                       |
| `RateLimit:TimeWindow`             | `RATELIMIT__TIMEWINDOW`              | `00:01:00` | the rate limit time window, as `HH:MM:SS`.                                                                                                                                                      |

When the limit is exceeded and the mailer is enabled (`Mailer:IsEnabled`, `Mailer:Recipients`), a notification is sent to the recipients, at most once every 30 minutes.

⚠️ **Rate limiting and reverse proxies**: anonymous clients are identified by their IP address, so the rate limiter works only when the API sees the real client address. When a reverse proxy in the host (e.g. NGINX providing HTTPS) forwards requests to the API container, or in some Docker setups even without a proxy, the API sees the same address (the proxy's or the Docker gateway's, like `172.18.0.1`) for all the requests, so that all the users share a single quota and quickly hit the limit. To avoid this:

1. have the proxy set the `X-Forwarded-For` and `X-Forwarded-Proto` headers (Apache's `mod_proxy` sets `X-Forwarded-For` automatically; in NGINX add `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;` and `proxy_set_header X-Forwarded-Proto $scheme;`).
2. trust the proxy: with a dockerized proxy (like nginx-proxy) set the subnet of the Docker network shared by the proxy and the API, e.g. `FORWARDEDHEADERS__KNOWNNETWORKS__0=172.30.0.0/16` (create the network with an explicit subnet, like `docker network create --subnet 172.30.0.0/16 nginx-proxy`); with a proxy in the host, requests reach the container from the Docker network gateway, so set `172.16.0.0/12` (the default Docker networks) or the specific address.
3. publish the API port to localhost only (e.g. `127.0.0.1:5000:8080`), so that the API can be reached only via the proxy, and nobody can fake the `X-Forwarded-For` header to evade the limit.

For example:

```yml
cadmus-api:
  # ...
  ports:
    - 127.0.0.1:5000:8080
  environment:
    # ...
    - RATELIMIT__ISDISABLED=false
    - RATELIMIT__PERMITLIMIT=100
    - FORWARDEDHEADERS__KNOWNNETWORKS__0=172.16.0.0/12
```

To check the result, set the `Serilog:MinimumLevel:Default` (or `SERILOG__MINIMUMLEVEL__DEFAULT`) to `Debug` and look at the `Rate limit key: ...` lines in the API log while using the app from different machines: each anonymous user should have a different `ip:...` key. Also, the `Forwarded headers: ...` line logged at startup shows the trusted proxies.

🛠️ **API projects** built on `Cadmus.Api.Config` should configure their pipeline in `Program.cs` like `Cadmus.Api` does:

```cs
// first in the pipeline
ServiceConfigurator.UseForwardedHeaders(app, config);
// ...
app.UseCors("CorsPolicy");
app.UseAuthentication();
// after authentication, so that users are limited by name rather than IP
ServiceConfigurator.UseRateLimiter(app, config);
app.UseAuthorization();
```

The rate limiter services are already added by `ServiceConfigurator.ConfigureServices`.

## CI Workflow

This workspace has a CI workflow for publishing the CLI tool application. Should it fail, you can re-run it after fixing the workflow if the error is there. Yet, remember that "Re-run all jobs" reuses the workflow file as it was pinned to that specific run (the commit/tag that triggered it), not your latest push. Since a fix lands in a commit after that run started, the rerun replays the old, broken ci.yaml. To avoid this, use the workflow_dispatch trigger — that one runs whatever ci.yaml currently exists on the branch/ref you pick. Via the UI:

1. Actions tab → select the Publish workflow in the left sidebar (not the old run)
2. Click Run workflow (top right)
3. Pick branch master, enter v14.0.2 in the tag field
4. Run workflow
