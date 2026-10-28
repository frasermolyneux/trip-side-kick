# AGENTS.md — trip-side-kick

.NET 10 modular monolith: Razor brochure on tripsidekick.net, React PWA/BFF on
tripsidekick.app. PostgreSQL 18 and constrained containers replace the Azure SQL/
App Service runtime. Read `.github/copilot-instructions.md`,
`docs/workload-consolidation.md` and `docs/identity-and-access.md` first.

## Validation

```powershell
dotnet build src\MX.TripSideKick.slnx
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName!~IntegrationTests'
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName~IntegrationTests'
dotnet format src\MX.TripSideKick.slnx --verify-no-changes
# Client directory: src\MX.TripSideKick.Web\ClientApp
npm ci
npm run build
npm run test
python deployment\build_web_image.py --tag tripsidekick-web:ci
docker build -f deployment\postgres.Dockerfile -t tripsidekick-postgres:ci .
python deployment\test_containers.py
```

Integration/container/browser tests require a responsive Linux Docker daemon.
`TRIPSIDEKICK_TEST_POSTGRES` may select a disposable CI database. Never point
tests at real data. Local backing services: `docker compose up -d postgres`.
Use LF endings, including generated migrations; warnings are errors.

## Boundaries

* Preserve host-aware route and static asset segregation; extend host tests when
  adding routes. Health paths are exactly `/api/health/live` and `/api/health/ready`.
* Preserve PKCE/cookies/no browser tokens, CSRF, optimistic concurrency/ETags,
  unique error translation and serializable last-owner invariants.
* Production must not run with an unconfigured database or ephemeral keyring.
  Initialization is explicit, never `EnsureCreated`/automatic database reset.
* Test-auth is local Development-only, opt-in and never allowed in Production.
* Trust only explicitly supplied reverse-proxy IPs plus framework loopback
  defaults; never clear forwarded-header network/proxy allowlists.
* No secrets, private certificates, live runtime JSON, state, plans, generated
  wwwroot, logs or PII in Git. Brochure assets live in `SiteAssets/`.
* Do not hand-edit `ClientApp/src/api/generated/schema.ts`; use `npm run generate-api`.
* No hosted dev environment, privileged runners/containers, Docker VOLUME
  metadata, host database port, or separate API/SPA deployment.
* `workload.json` zero digests are intentionally nondeployable. No host activation,
  deployment, resource destruction, credential rotation or PR merge as collateral.
* Production Terraform retains protected Entra identity, runtime vault/host RBAC,
  live DNS and Google APIs/key after coordinated legacy Azure retirement.
  Do not mutate state or Google project/billing/APIs/keys as collateral.
  The separately reviewed, fixed-scope `Retire Travel Google Federation`
  operation is authorized only after both Travel owned-stack destroys. It removes
  only retired Travel trust and preserves Trip/platform trust and all other IAM.
* Preserve required CI checks: `dependabot-policy`, `SonarCloud Code Analysis`,
  `build-and-test`, `quality / Code Quality`,
  `devops-secure-scanning / DevOps Secure Scanning`.
* Keep `AddObservability()` for shared telemetry; never emit trip/document/contact
  content, booking references, emails or display names.

## Terraform retained tooling

Select the exact committed `terraform/.terraform-version` CLI before running
offline validation. Commit `.terraform.lock.hcl`; normal init is read-only:

```powershell
terraform -chdir=terraform fmt -check -recursive
terraform -chdir=terraform init -lockfile=readonly -backend=false
terraform -chdir=terraform validate
```

Regenerate provider selections only in a reviewed dependency-update PR.
Do not plan/apply/import/move/remove real state without an explicit operation.
