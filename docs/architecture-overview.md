# Architecture Overview

## Runtime topology

One Azure App Service (Linux, .NET 10) per environment serves **both** public surfaces. The shared
`platform-hosting` App Service plan is consumed via Terraform remote state — this workload never
creates a plan of its own.

```
                    Cloudflare DNS (DNS-only)
   tripsidekick.net ─┐                        ┌─ tripsidekick.app
www.tripsidekick.net ┤                        ├─ www.tripsidekick.app
                     └──> app-trip-side-kick-<env>-swedencentral-<id>.azurewebsites.net
                                    │
                                    ├── Razor Pages brochure site   (site surface)
                                    ├── React PWA shell + /v1 API   (app surface)
                                    └── /api/health/live|ready, /info (all surfaces)
                                    │
                    ┌───────────────┼────────────────┬─────────────────┐
              Azure SQL      Blob Storage       Key Vault      Application Insights
             (Entra-only)   (private, MSI)     (RBAC only)    -> platform-monitoring LAW
```

## Host-aware surface split

Two hostnames, one deployment, strictly separated:

| Surface | Hosts (prd) | Hosts (dev) | Serves |
| --- | --- | --- | --- |
| `site` | `tripsidekick.net` | `dev.tripsidekick.net` | Razor Pages brochure/marketing pages |
| `app` | `tripsidekick.app` | `dev.tripsidekick.app` | React PWA shell and the versioned `/v1` API |

Enforcement is two layers deep, both in `src/MX.TripSideKick.Web/Hosting/`:

1. **`HostSurfaceMiddleware`** runs before routing. It rejects any unrecognised `Host` header with
   `400 Unrecognised host.` (deny by default) and permanently redirects `www.<apex>` to `<apex>`
   with a `308`. The resolved `HostSurface` is stashed in `HttpContext.Items`.
2. **Endpoint `RequireHost(...)` conventions** in `Program.cs`. `MapRazorPages()` is restricted to
   the site hosts; `MapControllers()`, `MapOpenApi()` and `MapFallbackToFile("index.html")` are
   restricted to the app hosts. A brochure URL on the app host therefore falls through to the SPA
   shell, and an API URL on the brochure host returns `404`.

Hostnames come from configuration (`HostRouting:SiteHosts`, `HostRouting:AppHosts`) and are
validated at startup with `ValidateOnStart()`. Terraform writes them as
`HostRouting__SiteHosts__0` style App Service settings and always appends the App Service default
`*.azurewebsites.net` hostname so platform probes and the deployment version gate keep working.

**Deliberate exception:** `/api/health/live`, `/api/health/ready` and `/info` are *not* host
restricted. App Service health probes and `frasermolyneux/actions/wait-for-version` hit the default
`*.azurewebsites.net` hostname, and these endpoints expose no user data.

## Modular monolith

Compile-time boundaries, no MediatR/CQRS indirection. Dependencies point inwards only.

| Project | Responsibility | May reference |
| --- | --- | --- |
| `MX.TripSideKick.Domain` | Aggregates, value objects, invariants. Noda Time for dates, `decimal` + ISO 4217 for money, UUIDv7 ids, `rowversion` for concurrency. | (nothing) |
| `MX.TripSideKick.Application` | Feature-oriented application services and the repository/identity **interfaces** they depend on. | Domain |
| `MX.TripSideKick.Infrastructure` | EF Core `DbContext`, cache-first repository implementations, Azure SDK clients. | Application, Domain |
| `MX.TripSideKick.Web` | Host: Razor Pages, versioned controllers, middleware, composition root. | Application, Infrastructure |
| `MX.TripSideKick.Web.Tests` | xUnit unit + in-process pipeline tests. | Web (and transitively the rest) |

Controllers stay thin: they translate HTTP to an application-service call and back. Repository
interfaces live in `Application/Trips/ITripRepository.cs`; implementations are cache-first
(`IMemoryCache` in front of EF Core) per the org repository pattern.

## Versioned API

Segment-based versioning: controllers are decorated `[Route("v1/<resource>")]` and the runtime
OpenAPI document for each version is served at `/swagger/{documentName}/openapi.json` (app hosts
only). Adding `v2` means adding a `Controllers/V2` folder and a second `AddOpenApi("v2")` call — the
`v1` contract is never mutated in place.

## Data and storage

Terraform provisions the full data footprint now, but **this slice does not read or write it**:

* **Azure SQL** — Entra-only authentication (`azuread_authentication_only = true`, no SQL logins).
  The `TripSideKickDbContext` and `SqlTripRepository` exist, but are only registered when
  `Sql:ConnectionString` is present. With no connection string the DI graph falls back to
  `EmptyTripRepository`, so startup and readiness never touch SQL.
* **Blob Storage** — private containers `documents` and `dataprotection`. Public blob access is
  disabled and shared keys are disabled; access is `DefaultAzureCredential` (managed identity) only.
* **Data Protection** — keys persist to the `dataprotection` container when
  `BlobStorage:ServiceUri` is configured, so App Service scale-out and restarts do not invalidate
  antiforgery tokens or cookies. Locally it falls back to the file-system key ring.

## Health endpoints

Exactly two, per the org standard — no `/health` or `/healthz` aliases:

* `GET /api/health/live` — process liveness only (`Predicate = _ => false`). This is the App Service
  `health_check_path`.
* `GET /api/health/ready` — readiness. Deliberately **not** gated on SQL or Blob Storage in this
  slice; dependency checks will be registered with the `ready` tag when the data slice lands.

## Observability

* Server: `Microsoft.ApplicationInsights.AspNetCore` plus the shared
  `MX.Observability.ApplicationInsights.AspNetCore` package (`AddObservability()`).
* Browser: `@microsoft/applicationinsights-web`, initialised from the connection string returned by
  `GET /v1/client-config` so the same build artefact can be promoted between environments.
* Azure diagnostics for the App Service, SQL database, Key Vault and blob service are routed to the
  shared `platform-monitoring` Log Analytics workspace.
* **Never emit PII** — no trip content, document contents, booking references or email addresses in
  telemetry, log messages or exception data.

## Web security baseline

Established now, tightened as features arrive:

* Strict host validation (above) and HTTPS redirection + HSTS outside Development.
* `SecurityHeadersMiddleware` emits CSP, `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Cross-Origin-Opener-Policy` and `Permissions-Policy`. The CSP already allows
  the Google Maps and Application Insights ingestion origins the app will need.
* Same-origin secure cookies (`CookiePolicyOptions`) and antiforgery via the `X-CSRF-TOKEN` header
  with a `__Host-` prefixed cookie.
* Fixed-window rate limiting (300 requests/minute/IP) as a global limiter.
* Forwarded headers configured for the App Service reverse proxy.
