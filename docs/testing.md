# Testing

Commands below run from the repository root unless noted. SDK and tool
versions come from the repository configuration and workflow files.

## Fast checks

```powershell
dotnet build src\MX.TripSideKick.slnx
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName!~IntegrationTests'
dotnet format src\MX.TripSideKick.slnx --verify-no-changes
```

Run `npm run test` in `src\MX.TripSideKick.Web\ClientApp` for client component
tests. The shared CI action excludes `IntegrationTests` from its fast test run;
the PostgreSQL job runs them separately.

## PostgreSQL integration and browser tests

Integration tests use a disposable PostgreSQL container, or
`TRIPSIDEKICK_TEST_POSTGRES` when explicitly set to a disposable CI database.
Never point tests at real data. A responsive Linux Docker daemon is required
for Testcontainers:

```powershell
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName~IntegrationTests'
```

The Playwright suite in `tests\e2e` starts its own disposable PostgreSQL
container, applies migrations and runs the published application with
Development-only test authentication. It covers host isolation, membership
and itinerary journeys without signing in to real Entra. Install the directory's
dependencies and Chromium, then run `npm run test:e2e` there. The setup also
restores .NET tools and dependencies and ensures a development certificate.

Test-auth requires explicit opt-in and Development; it is unavailable in
Production regardless of `WEBSITE_INSTANCE_ID`. Never enable it on a public
host.

## Container and CI checks

The PostgreSQL CI workflow runs the integration and browser tests, checks for
pending model changes, builds both images and exercises the constrained
container runtime. To reproduce the image checks locally, follow
[runtime validation](workload-consolidation.md#local-development-and-validation).
The workflow definitions are the source for current triggers and steps.
