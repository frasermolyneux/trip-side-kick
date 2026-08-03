# Development Workflows

## Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | 10.0.100+ | Pinned by `global.json` (`rollForward: latestFeature`) |
| Node.js | 22.x LTS | npm 10+; `package-lock.json` is committed |
| Terraform | 1.15.6+ | Only needed for infrastructure changes |
| Docker | any recent | Optional: local SQL Server + Azurite via `docker compose` |

## Build, test, format

All .NET commands run from the repository root unless stated otherwise.

```bash
# Restore + build the whole solution (warnings are errors)
dotnet build src/MX.TripSideKick.sln

# Run every test
dotnet test src/MX.TripSideKick.sln

# Run a single test class
dotnet test src/MX.TripSideKick.sln --filter "FullyQualifiedName~HostRoutingTests"

# Formatting gate (must be clean; CI runs the same command)
cd src && dotnet format "." --verify-no-changes
```

Client commands run from `src/MX.TripSideKick.Web/ClientApp`:

```bash
npm ci          # install exactly what the lockfile says
npm run build   # type-check (tsc -b) + Vite production build into ../wwwroot
npm run test    # Vitest + Testing Library + MSW
npm run dev     # Vite dev server on http://localhost:5173, proxying /v1 and /api to the host
```

`dotnet build` on `MX.TripSideKick.Web` runs `npm ci` (when `node_modules` is missing) and
`npm run build` through MSBuild targets, so a plain `dotnet build`/`dotnet publish` always produces a
complete artefact. Set `-p:SkipClientBuild=true` to opt out when iterating on server code only.

> `wwwroot/` is **generated output** and is git-ignored. Brochure-site static assets live in
> `ClientApp/public/` and are copied verbatim by Vite.

## Running locally

```bash
dotnet run --project src/MX.TripSideKick.Web
```

`Properties/launchSettings.json` supplies the local host-routing allow lists, because in Azure the
same values arrive as App Service settings:

| URL | Surface |
| --- | --- |
| `https://localhost:7207` | app surface — React PWA + `/v1` API |
| `http://127.0.0.1:5207` | site surface — Razor Pages brochure |
| `https://app.localhost:7207` | app surface (named alias) |
| `https://site.localhost:7207` | site surface (named alias) |

Any other `Host` header returns `400 Unrecognised host.` — that is the host-routing gate doing its
job, not a bug.

For a hot-reloading client, run `npm run dev` alongside `dotnet run` and browse to
`http://localhost:5173`; the Vite dev server proxies `/v1` and `/api` to the ASP.NET Core host.

### Local backing services

```bash
docker compose up -d      # SQL Server 2022 on localhost:1433, Azurite blob on localhost:10000
docker compose down -v    # tear down including volumes
```

Neither service is required by the app today — the walking skeleton starts and reports healthy with
no SQL connection string and no blob endpoint.

## Terraform

```bash
cd terraform
terraform fmt -check -recursive
terraform init -backend=false
terraform validate
```

A real `plan`/`apply` needs Azure OIDC credentials and the Cloudflare token, so it only runs in CI.

## Branch strategy and CI/CD

* Work on a branch (`feature/**`, `bugfix/**`, `hotfix/**`, `agents/**`) and open a PR into `main`.
* `main` is protected. Required status checks: `dependabot-policy`, `SonarCloud Code Analysis`,
  `build-and-test`, `quality / Code Quality`, `devops-secure-scanning / DevOps Secure Scanning`.

| Workflow | Trigger | What it does |
| --- | --- | --- |
| `build-and-test.yml` | push to `feature/**`, `bugfix/**`, `hotfix/**`, `agents/**` | Client tests, .NET build/test/format, dev Terraform plan when `terraform/**` changed |
| `pr-verify.yml` | pull request | `build-and-test` job (required check) plus label-driven Terraform jobs |
| `codequality.yml` | PR, push to `main`, Monday 08:30 UTC | SonarCloud analysis, DevOps secure scanning, dependency review |
| `dependabot-automerge.yml` | pull request | `dependabot-policy` required check; auto-merges compliant Dependabot PRs |
| `deploy-dev.yml` | manual | Full build → Terraform apply → App Service deploy to Development |
| `deploy-prd.yml` | push to `main`, manual, Thursday 05:00 UTC | Development then Production apply + deploy |
| `destroy-development.yml` | daily 23:55 UTC, manual | Tears the Development environment down overnight to control cost |
| `destroy-environment.yml` | manual | Targeted teardown of `dev` or `prd` |

### PR labels

| Label | Effect |
| --- | --- |
| *(none)* | Terraform **plan** against Development |
| `deploy-dev` | Terraform **plan + apply** against Development, then deploy the App Service and wait for `/info` to report the built version |
| `run-prd-plan` | Terraform **plan** against Production |

`destroy-development.yml` runs nightly, so a Development environment created by a `deploy-dev` label
is expected to disappear overnight. Re-apply the label (or re-run `deploy-dev.yml`) to bring it back.

## Conventions worth knowing

* **Warnings are errors** (`TreatWarningsAsErrors`, `CodeAnalysisTreatWarningsAsErrors`). Analyzer
  severities live in `.editorconfig`.
* **LF line endings everywhere** (`.gitattributes` + `.editorconfig`). `dotnet format` fails on CRLF.
* **Versioning** is Nerdbank.GitVersioning (`version.json`); the build version is surfaced at `/info`
  and in the brochure footer.
* Test fully-qualified names must **not** contain `IntegrationTests` unless they are genuinely
  expensive — the shared CI action filters that string out of `dotnet test`.
