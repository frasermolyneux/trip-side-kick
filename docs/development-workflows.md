# Development workflows

Use the SDK selected by `global.json`, Node as configured in the CI workflows,
and Docker for database, container and browser integration tests. Terraform
changes use the CLI selected by `terraform/.terraform-version` and the
committed provider lock file.

## Build and test

From the repository root:

```powershell
dotnet build src\MX.TripSideKick.slnx
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName!~IntegrationTests'
dotnet format src\MX.TripSideKick.slnx --verify-no-changes
```

The web project builds the client bundle through MSBuild. Use
`-p:SkipClientBuild=true` only for backend-focused work. For client-only work,
run `npm ci`, `npm run build` and `npm run test` in
`src\MX.TripSideKick.Web\ClientApp`. Generated `wwwroot/` is not committed;
brochure assets belong in `SiteAssets/`.

See [testing](testing.md) for integration, browser and image checks.

## Run locally

Start the disposable database with `docker compose up -d postgres`. Set
`Sql__ConnectionString` to the host, database, user and disposable password
specified in `docker-compose.yml`, then opt in to initialization with
`Sql__InitializeOnStartup=true` and run:

```powershell
dotnet run --project src\MX.TripSideKick.Web
```

The launch profile supplies local host routing: `localhost` and
`app.localhost` are app hosts; `127.0.0.1` and `site.localhost` are site
hosts. See `Properties/launchSettings.json` for ports. A local Development
instance may start without a database, but readiness is unhealthy.

For client hot reload, run `npm run dev` in the client directory alongside the
web host. Vite proxies `/v1` and `/api` to the local host.

## Delivery and infrastructure

Open a pull request against `main`. Required checks and test conventions are
listed in [AGENTS.md](../AGENTS.md); workflow definitions specify their
current triggers. Hosted Development and legacy Azure deployment workflows
are disabled. Image publication and digest-only promotion do not activate the
host; see [runtime delivery](workload-consolidation.md#local-development-and-validation).

For offline Terraform validation use `terraform fmt -check -recursive`,
`terraform init -lockfile=readonly -backend=false` and `terraform validate`
from `terraform/`. Do not plan or mutate real state without an explicit
operation. The reviewed Runtime Infrastructure workflow handles Production
plans and manual approved-main apply.
