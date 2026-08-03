# Copilot Instructions — trip-side-kick

Travel itinerary planner. One ASP.NET Core (.NET 10) modular monolith serves **two public surfaces** from a single Azure App Service; Terraform provisions the infrastructure; Cloudflare owns DNS. Start with `AGENTS.md` for task-execution rules and `docs/architecture-overview.md` for the full picture.

## Org conventions via MCP (when available)

If a `frasermolyneux-copilot` MCP server is configured in your client (`~/.copilot/mcp-config.json`, VS Code user `mcp.json`, or an equivalent stdio MCP wire-up), **prefer its catalog tools** over your own assumptions when answering questions about org standards, branching, workflows, Terraform, .NET projects, Azure patterns, or shared library / platform consumption contracts. The catalog source-of-truth lives in `frasermolyneux/.github-copilot` — see `mcp-server/README.md` there for the tool contract.

This is **complementary** to the file-load model: if `./.github-copilot/` is checked out in the runner (per `copilot-setup-steps.yml`), continue to read those files directly. If both are available, prefer MCP for freshness. If no MCP server is configured in your client, treat this section as a no-op and fall back to the file paths above.

## The one thing that will surprise you: host-aware routing

The same deployed app serves two hostnames and refuses to blur them.

| Surface | prd | dev | Content |
| --- | --- | --- | --- |
| `site` | `tripsidekick.net` | `dev.tripsidekick.net` | Razor Pages brochure/marketing |
| `app` | `tripsidekick.app` | `dev.tripsidekick.app` | React PWA shell + versioned `/v1` API |

* `Hosting/HostSurfaceMiddleware.cs` runs pre-routing: unknown `Host` ⇒ `400`, `www.<apex>` ⇒ `308` to apex.
* `Program.cs` applies `.RequireHost(...)` to `MapRazorPages()` (site hosts) and to `MapControllers()` / `MapOpenApi()` / `MapFallbackToFile("index.html")` (app hosts).
* Static files are host-scoped too, via `UseWhen` on the resolved surface: the app surface serves the Vite-generated `wwwroot/`, the site surface serves the source-controlled `SiteAssets/`. Without that split `UseStaticFiles` would serve `/index.html` on the brochure host and defeat the fallback restriction.
* Allow lists come from config (`HostRouting:SiteHosts` / `HostRouting:AppHosts`), written by Terraform as `HostRouting__SiteHosts__0`-style app settings, always including the `*.azurewebsites.net` default hostname.
* **Do not** put host lists in `appsettings.Development.json` — the dev App Service sets `ASPNETCORE_ENVIRONMENT=Development` and index-keyed arrays would silently shadow Terraform's values. Local dev host lists live in `Properties/launchSettings.json`.
* `/api/health/live`, `/api/health/ready` and `/info` are intentionally **not** host-restricted (App Service probes and the deployment version gate hit the default hostname).
* Guarded by `MX.TripSideKick.Web.Tests/Hosting/HostRoutingTests.cs` — extend it whenever you add a route.

## Project layout and boundaries

`src/MX.TripSideKick.sln` — dependencies point inwards only, no MediatR/CQRS.

* `MX.TripSideKick.Domain` — aggregates/value objects. Noda Time dates, `decimal` + ISO 4217 money (`Common/Money.cs`), UUIDv7 ids (`Trips/TripId.cs`), `rowversion` concurrency.
* `MX.TripSideKick.Application` — feature-oriented services (`Trips/TripCatalogService.cs`) and the interfaces they need (`Trips/ITripRepository.cs`, `Abstractions/ICurrentUser.cs`).
* `MX.TripSideKick.Infrastructure` — EF Core `DbContext`, cache-first repositories (`Persistence/Repositories/SqlTripRepository.cs`), Azure SDK clients.
* `MX.TripSideKick.Web` — host: Razor Pages (`Pages/`), versioned controllers (`Controllers/V1/`), middleware (`Hosting/`), composition root (`Program.cs`).
* `MX.TripSideKick.Web.Tests` — xUnit; in-process pipeline tests via `TripSideKickApplicationFactory`.

Each layer has its own `*ServiceCollectionExtensions.cs`; `Program.cs` calls them and owns nothing else DI-shaped.

## Non-obvious conventions

* **Identity is stubbed.** No auth scheme is registered. `AnonymousCurrentUser` is the only `ICurrentUser`. Grep `IDENTITY STUB` / `TODO (identity slice)` before touching auth. See `docs/identity-and-access.md`.
* **The SPA is built by MSBuild.** `MX.TripSideKick.Web.csproj` targets `EnsureClientDependencies`/`BuildClient`/`PublishClientAssets` run `npm ci` + `npm run build` into `wwwroot/` (git-ignored) and inject the output into publish. `dotnet publish` alone produces a complete artefact. `-p:SkipClientBuild=true` skips it.
* **DI is validated on build.** `TripCatalogService` needs an `ITripRepository`, so `EmptyTripRepository` is registered when no SQL connection string exists — otherwise Development startup fails validation. Remove it when real SQL access lands.
* **Health is deliberately shallow.** `/api/health/ready` uses `Predicate = check => check.Tags.Contains("ready")` with nothing registered. Do **not** gate readiness on SQL in this slice.
* **Warnings are errors** everywhere (`Directory.Build.props`); analyzer severity policy lives in `.editorconfig`.
* **LF line endings.** `dotnet format --verify-no-changes` fails on CRLF — the classic Windows-authored CI break.
* Test FQNs must avoid the string `IntegrationTests`; the shared CI action filters it out of `dotnet test`.
* Versioning is Nerdbank.GitVersioning (`version.json`); the build version is exposed at `/info` and used by the deploy workflows' `wait-for-version` gate.

## Infrastructure facts

* `terraform/` consumes remote state from `platform-hosting` (shared Linux App Service plan — **never create a plan**) and `platform-monitoring` (shared Log Analytics workspace). Environments: `dev`, `prd`. Region: `swedencentral`.
* **DNS is Cloudflare, not Azure DNS.** No Azure DNS zone, no `DNS Zone Contributor`. `cloudflare_dns_record` resources with zone IDs from the `cloudflare_zone` data source; the token arrives as `TF_VAR_cloudflare_api_token` from the `CLOUDFLARE_API_KEY` environment secret. All records are **DNS-only (grey cloud)** — App Service managed certificates cannot validate or renew through the proxy. See `docs/dns-and-custom-domains.md`.
* SQL is `GP_S_Gen5_1` serverless with 60-minute auto-pause, Entra-only auth, no SQL logins. Storage has shared keys disabled and public blob access off. Everything runtime-side uses the system-assigned managed identity.
* No secrets in app settings, ever.

## Commands

```bash
dotnet build src/MX.TripSideKick.sln          # warnings are errors
dotnet test  src/MX.TripSideKick.sln
cd src && dotnet format "." --verify-no-changes
cd src/MX.TripSideKick.Web/ClientApp && npm ci && npm run build && npm run test
cd terraform && terraform fmt -check -recursive && terraform init -backend=false && terraform validate
dotnet run --project src/MX.TripSideKick.Web  # https://localhost:7207 app · http://127.0.0.1:5207 site
```

CI required checks on `main`: `dependabot-policy`, `SonarCloud Code Analysis`, `build-and-test`, `quality / Code Quality`, `devops-secure-scanning / DevOps Secure Scanning`. PR label `deploy-dev` plans+applies+deploys to Development; `run-prd-plan` plans Production.
