# AGENTS.md — trip-side-kick

Trip Side Kick: an ASP.NET Core (.NET 10) modular monolith serving a Razor Pages brochure site and a React + TypeScript PWA / versioned `/v1` API from one Azure App Service, provisioned with Terraform and fronted by Cloudflare DNS.

Execution brief for coding agents. See `.github/copilot-instructions.md` for architecture/domain detail, `docs/architecture-overview.md` for the host-surface split, and `docs/identity-and-access.md` before touching anything auth-shaped — identity is **stubbed**. `AddObservability()` (`MX.Observability.ApplicationInsights.AspNetCore`) is the shared telemetry package; don't hand-roll initialisers.

## Build / test / run commands

```bash
# .NET (repo root) — warnings are errors
dotnet build src/MX.TripSideKick.sln
dotnet test  src/MX.TripSideKick.sln
dotnet test  src/MX.TripSideKick.sln --filter "FullyQualifiedName~HostRoutingTests"   # single class
dotnet test  src/MX.TripSideKick.sln --filter "FullyQualifiedName!~IntegrationTests"  # CI's filter

# Format gate — run from src/, exactly as CI does
cd src && dotnet format "." --verify-no-changes

# Client (src/MX.TripSideKick.Web/ClientApp)
npm ci && npm run build && npm run test

# Terraform (terraform/) — offline validation only; plan/apply needs CI credentials
terraform fmt -check -recursive
terraform init -backend=false
terraform validate

# Local run
dotnet run --project src/MX.TripSideKick.Web    # https://localhost:7207 = app, http://127.0.0.1:5207 = site
```

Run only the block(s) your change touches. LF endings only (`.gitattributes` / `.editorconfig`) — `dotnet format --verify-no-changes` fails on CRLF, the most common Windows-authored CI break.

## Do NOT

- ❌ Client secrets, connection strings, or hard-coded subscription IDs/GUIDs. Auth is OIDC + managed identity only.
- ❌ Change resource naming/tagging conventions.
- ❌ Assume tools/SDKs beyond what `copilot-setup-steps.yml` provisions.
- ❌ Add an Azure DNS zone or `DNS Zone Contributor` role — DNS is **Cloudflare**, not Azure.
- ❌ Create an App Service plan or Log Analytics workspace — both come from platform remote state.
- ❌ Implement auth opportunistically — identity needs Graph permissions this workload lacks; keep `IDENTITY STUB` / `TODO (identity slice)` markers unless that's the assigned task.
- ❌ Add MediatR/CQRS or a separate API/SPA deployment — one App Service, two host surfaces.
- ❌ Add `/health` or `/healthz` aliases — exactly `/api/health/live` and `/api/health/ready`.
- ❌ Put `IntegrationTests` in a test FQN unless genuinely expensive — CI filters that string out.
- ❌ Commit `src/MX.TripSideKick.Web/wwwroot/` (generated, git-ignored) — brochure assets belong in `SiteAssets/`.
- ❌ Hand-edit `ClientApp/src/api/generated/schema.ts` — regenerate via `npm run generate-api`.
- ❌ Log or trace PII: trip content, document contents, booking references, emails, display names.
