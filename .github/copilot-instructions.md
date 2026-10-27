# Copilot Instructions — trip-side-kick

Read `AGENTS.md` for commands and guardrails and `docs/workload-consolidation.md`
for the authoritative constrained-runtime/CI contract.

## Architecture

One .NET 10 modular monolith serves two strictly isolated host surfaces:

* `tripsidekick.net`: anonymous Razor brochure pages and `SiteAssets/`.
* `tripsidekick.app`: React PWA bundle in generated `wwwroot/`, `/v1` BFF/API,
  OpenAPI and Entra workforce B2B sign-in.

`HostSurfaceMiddleware` runs before routing, rejects unknown hosts and canonicalizes
www to apex. Endpoint `.RequireHost(...)` and host-scoped static middleware must
remain paired. Health/info are reachable on any configured host, not arbitrary
hosts. Do not place host arrays in appsettings.Development.json.

Dependencies point inward: Domain → Application interfaces/services →
Infrastructure EF repositories → Web composition. No MediatR/CQRS or independent
SPA/API deployment. Noda Time dates, decimal/ISO currencies, UUIDv7 identifiers.

## Persistence/runtime

PostgreSQL 18 via Npgsql replaces SQL Server. Fresh initial migrations are
intentional; SQL Server migration history is not reused. PostgreSQL defaults and
update triggers generate byte-array concurrency values used by existing ETags.
Translate unique violations and serialization/deadlock conflicts into existing
domain errors; retain serializable transactions for cross-row owner invariants.

`Sql:PasswordFile` supplies credentials independently of the nonsecret connection
settings. Missing configuration outside Development fails startup. Readiness
requires actual DB connectivity and no pending migrations. Initialization is
explicit via `--initialize-database` or `Sql:InitializeOnStartup`.

`deployment/runtime.example.json` describes configuration loaded before composition
from `TRIPSIDEKICK_RUNTIME_FILE`. Local Data Protection keys and documents are
persistent mounts; the Entra certificate is a base64 PFX text secret loaded with
ephemeral private-key storage. PKCE, HttpOnly secure cookies, antiforgery and
server-only tokens remain intact. Test-auth must never work in Production,
regardless of WEBSITE_INSTANCE_ID.

Forwarded host is not trusted. Preserve explicit `ForwardedHeaders:KnownProxies`
and framework loopback defaults; never clear trust allowlists.

## Build/delivery

MSBuild owns the React bundle (`npm ci`/`npm run build`) and publishes generated
assets. `-p:SkipClientBuild=true` is only for backend-focused validation/tooling.
EF/container tests have `IntegrationTests` FQNs and are run explicitly in the
PostgreSQL CI job rather than the shared action's inexpensive test filter.
Warnings are errors, LF endings required.

Production image publication is exact-source quality-gated and immutable.
Promotion PRs change digests only, retain exact-head quality/Copilot review
guards, and do not auto-merge or deploy. Initial zero digests gate activation.
Legacy apply/deploy triggers are disabled. Terraform/destroy/identity/vault
resources remain unchanged until coordinated platform retirement; no GCP changes.
Do not create a hosted dev environment.

Terraform CLI/provider selections are committed in `.terraform-version` and
`.terraform.lock.hcl`; normal initialization uses `-lockfile=readonly`.
Provider regeneration belongs only in reviewed dependency updates; never commit
state, plans or `.terraform/`.
