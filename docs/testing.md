# Testing

## Status: VERIFIED GREEN — unit, integration, and Playwright E2E all executed and passing locally with Docker

Trip Side Kick's Trips + Membership/Roles slice (Journeys 1 & 2) is covered at three levels:
xUnit unit tests, xUnit + Testcontainers SQL Server integration tests, and a hermetic Playwright
role-based E2E suite. **Nothing in any of the three levels talks to Azure, real Entra, or any
external network at test-run time** — see each section below for exactly how.

All three tiers were executed for real (Docker became available partway through this slice's
authoring) and are green as of this branch:

- **Unit tests**: 149/149 passing (`dotnet test --filter "FullyQualifiedName!~IntegrationTests"`).
- **Integration tests**: all passing against a real Testcontainers SQL Server 2022 container.
- **Playwright E2E**: 13/13 specs passing (~33s once the app/DB are up).

### Bugs found and fixed while getting these tiers green

Running the suites for real (rather than relying on "authored, compiles, reviewed") surfaced several
genuine defects that static review had missed — evidence for why executing all three tiers, not just
authoring them, is a hard requirement of this slice:

1. **SQL options read too early.** `InfrastructureServiceCollectionExtensions` originally read
   `Sql:ConnectionString` eagerly at service-registration time, before `WebApplicationFactory`'s
   config overrides applied in integration tests — so tests always saw "no SQL configured" and got
   `EmptyTripRepository`. Fixed by switching to lazy `IOptions<SqlOptions>` resolution.
2. **Anonymous `/v1` requests attempted a real network call.** `DefaultChallengeScheme` was still
   `OpenIdConnect` even for API routes, so an anonymous `GET /v1/trips` triggered the real OIDC
   handler's metadata discovery fetch (over the network) instead of a clean 401. Fixed by adding an
   `AddPolicyScheme` (`ApiChallengeScheme`) as the new `DefaultChallengeScheme`, forwarding
   `/v1`-prefixed paths to the Cookie scheme (whose `OnRedirectToLogin`/`OnRedirectToAccessDenied`
   return 401/403 directly) and everything else to OpenIdConnect.
3. **Missing `Microsoft.EntityFrameworkCore.Design` reference** in the startup project
   (`MX.TripSideKick.Web.csproj`) — `dotnet-ef` needs it present in the startup project to build the
   migration model; without it, `dotnet ef database update` failed.
4. **Wrong working directory for the spawned E2E app process** (`tests/e2e/support/appProcess.ts`) —
   the child `dotnet` process needs its `cwd` set to the published app's own directory so
   `ContentRootPath` (and therefore `SiteAssets`) resolves correctly.
5. **Health-check probe followed HTTPS redirects into an untrusted dev-cert TLS failure** —
   `waitForHealthy` now uses `redirect: 'manual'` and accepts any 2xx/3xx response as proof of
   liveness, instead of following the redirect and failing TLS validation against the local
   self-signed dev certificate.
6. **CSP `style-src` silently broke every MUI dropdown/popover in the entire app** — the most
   significant finding. MUI's Popper-based components (`Select`, `Menu`, `Autocomplete`, `Tooltip`,
   `Dialog`, `Snackbar` transitions) position and animate themselves by writing directly to
   `element.style` via JavaScript. CSP nonces only cover `<style>`/`<link>` elements, **not** inline
   `style` attribute mutations performed via JS, so with `style-src 'self'` (no `'unsafe-inline'`)
   the browser silently blocked every one of these writes — no console error the user would
   necessarily notice, but every MUI `<Select>`/menu/popover/tooltip in the app was non-functional.
   This was a genuine, previously-undiscovered **production bug** affecting real users in dev/prd
   today, not an E2E-only artifact — only surfaced because the E2E suite actually drives a real
   browser against the real CSP header. Fixed in
   `src/MX.TripSideKick.Web/Hosting/SecurityHeadersOptions.cs` by adding `'unsafe-inline'` to
   `style-src` (a standard, low-risk mitigation for MUI/emotion-based apps — inline `style`
   attributes carry far lower XSS risk than inline `<script>`, which nonces still fully protect).
7. **A test bug, not an app bug**: the "mismatched identity cannot accept" E2E test asserted on
   `page.request.get(...)` (the mismatched identity's own session) instead of `ownerPage.request.get(...)`
   — since that identity is correctly refused membership, its own request to list members correctly
   403s, and the test's `.find(...)` on a non-array problem-details body threw. Fixed by querying
   through the Owner's session, which is the actual member entitled to list members.

**Debugging tip for future contributors:** when a Playwright locator (e.g. `getByRole('option', ...)`)
mysteriously never resolves against what looks like correct application code, attach temporary
`page.on('console', ...)` / `page.on('pageerror', ...)` listeners and re-run — this is what surfaced
the CSP violations above; Playwright's own trace/screenshot output does not always make console-level
errors obvious.

## 1. Unit tests

`src/MX.TripSideKick.Web.Tests/Domain/*` and `Application/*` (xUnit + Moq, no I/O of any kind).
Covers:

- **Trip** — required-name-only creation, `TripDates`/`TripDateStatus` transitions
  (Undecided → Approximate → Confirmed and back), currency validation (`IsoCurrencyCodes`), the
  creator-becomes-Owner-and-Traveller invariant, and that removing oneself as a traveller never
  touches the Owner membership.
- **`MembershipPolicy`** — the full Owner/Editor/Viewer action matrix (who may invite, remove,
  change roles, edit content) and **last-owner protection** (`LastOwnerViolationException` when the
  only remaining Owner tries to leave/be removed/be demoted; allowed once a second Owner exists).
- **`Invitation`** — state transitions (Pending → Accepted/Revoked), resend behaviour, and the
  **email-binding/claim rule**: `Accept` only succeeds when the caller's email matches the
  invitation's bound email (case-insensitive), throwing `InvitationIdentityMismatchException`
  otherwise; linking to an existing/new traveller vs. a non-travelling planner.
- **`Traveller`** — linkage to a membership, and that a non-travelling-planner invitation never
  creates a traveller row.
- Application services (`TripPlanningService`, `MembershipService`, `MembershipAccessService`,
  `InvitationService`, `TravellerService`) — orchestration logic with mocked repositories/`IUnitOfWork`/`IClock`/`IInvitationNotifier`, verifying the right domain methods are invoked and the
  right exceptions propagate.

Run:

```bash
dotnet test src/MX.TripSideKick.sln --filter "FullyQualifiedName!~IntegrationTests"
```

(This is also exactly what `build-and-test`/`pr-verify` CI runs — see
[CI integration](#4-ci-integration) below.)

## 2. Integration tests (Testcontainers SQL Server)

`src/MX.TripSideKick.Web.Tests/Integration/*IntegrationTests.cs`, using a single shared
`SqlServerContainerFixture` (xUnit collection fixture) that starts one ephemeral
`mcr.microsoft.com/mssql/server:2022-latest` container per test run and applies the real EF Core
migrations to it once. **Deliberately named with the literal substring `IntegrationTests`** — per
`AGENTS.md`, the shared `dotnet-web-ci` CI action filters that string out of its default
`dotnet test` run (`--filter "FullyQualifiedName!~IntegrationTests"`), because these tests are
genuinely expensive (real SQL Server startup) and need Docker; the unit-test run above stays fast
and dependency-free.

Reuses the existing `TripSideKickApplicationFactory` (`WebApplicationFactory`) + `TestAuthHandler`
(`X-Test-Subject-Id`/`X-Test-Email`/`X-Test-Display-Name` headers) so tests authenticate as
arbitrary subjects without any real cookie/OIDC flow — the same pattern the identity slice
introduced, extended here rather than reinvented.

Covers, against a real SQL Server:

- `TripLifecycleIntegrationTests` — create-trip end-to-end (owner membership + traveller created in
  the same transaction), update-trip with `If-Match`/ETag, and an **explicit 409** on a stale ETag
  (no silent last-write-wins).
- `MembershipRoleMatrixIntegrationTests` — the Owner/Editor/Viewer allowed/denied action matrix
  exercised through the real API pipeline (authorization handler + controllers), and **last-owner
  protection** (409 on demote/remove of the sole Owner).
- `InvitationAcceptanceIntegrationTests` — invite → accept with a **matching** email (succeeds,
  correct role granted, correct traveller linkage) vs. a **mismatched** email (403, no membership
  created); resend reuses the same invitation row.

Run (requires Docker):

```bash
dotnet test src/MX.TripSideKick.sln --filter "FullyQualifiedName~IntegrationTests"
```

## 3. Playwright role-based E2E suite

### Why `tests/e2e/` at the repo root

Chosen over `ClientApp/e2e` or a `MX.TripSideKick.Web.Tests`-adjacent folder because this suite:

- drives the **fully built, published app** (real Kestrel process, real host-routing middleware,
  real EF Core against a real — if ephemeral — SQL Server) rather than the React app alone, so it
  belongs outside `ClientApp` (which is purely the Vite/React project);
- is a genuinely separate toolchain (Node/Playwright/Testcontainers-for-Node, its own
  `package.json`/`tsconfig.json`/lockfile) from both the .NET test project and the React client, so
  a repo-root `tests/` sibling to `src/` keeps it discoverable without entangling either existing
  project's build;
- exercises the **real host-routing split** (app vs. brochure hosts) as its own dedicated spec
  (`host-routing.spec.ts`), which only makes sense against a real running server, not a component
  test.

### Deterministic test auth — the security-sensitive control

`src/MX.TripSideKick.Web/Hosting/TestAuthEndpoints.cs` maps `GET /testauth/signin?sub=...&email=...&name=...`
and `POST /testauth/signout` **only when both**:

1. `IWebHostEnvironment.IsDevelopment()` is true, **and**
2. `TestAuth:Enabled` (bound from `TestAuthOptions`) is explicitly `true`.

Neither is ever true in a deployed environment: `ASPNETCORE_ENVIRONMENT=Production` is set for both
the dev and prd App Services (`terraform/web_app.tf` — "dev" there means the pre-production
deployment slot, not the ASP.NET Core `Development` environment), and `TestAuth__Enabled` is never
set by any Terraform app setting. **Both conditions must independently fail closed** — a single
misconfiguration (e.g. someone accidentally setting `TestAuth__Enabled=true` in prod app settings)
still cannot expose the endpoint, because the environment check also has to pass.

On success, it signs the caller in via the **same** cookie authentication scheme
(`CookieAuthenticationDefaults.AuthenticationScheme`) and the same claim shapes
(`oid`/`name`/`email`) that `HttpContextCurrentUser` reads in production — so every downstream
behaviour (authorization, `ICurrentUser.SubjectId`, invitation email-matching) is exercised exactly
as it would be for a real signed-in user; only the *sign-in* step is faked.

**The proof test:** `src/MX.TripSideKick.Web.Tests/Hosting/TestAuthEndpointsTests.cs` asserts the
endpoint 404s in three scenarios — flag unset (any environment), flag `true` but environment not
`Development`, and (as a positive control) flag `true` **and** environment `Development` actually
maps and works — so a regression in either half of the gate is caught immediately. This is part of
the fast unit-test run (`dotnet test --filter "FullyQualifiedName!~IntegrationTests"`), so it runs
on every PR without needing Docker.

The Playwright harness opts in explicitly and only for its own spawned app process
(`tests/e2e/support/appProcess.ts`'s `startApp`): `ASPNETCORE_ENVIRONMENT=Development` and
`TestAuth__Enabled=true` are set as env vars on that one child process only — never anywhere near a
real deployment.

### Self-contained data store

`tests/e2e/support/sqlContainer.ts` starts an ephemeral
`mcr.microsoft.com/mssql/server:2022-latest` Testcontainers container per run (same image as
`docker-compose.yml` and the .NET integration tests' fixture), then
`tests/e2e/support/migrate.ts` applies the real EF Core migrations to it via
`dotnet ef database update` (pointed at the container via `TripSideKickDbContextFactory`'s
`TRIPSIDEKICK_MIGRATION_CONNECTION_STRING` env-var override — the running app itself never reads
this variable). No Azure SQL, no seeded cloud state, no shared state between runs. All test data
(trips, memberships, invitations) is created **through the real API** during the specs themselves,
via `/testauth/signin` + normal `/v1` calls — never by talking to the database directly or through
a special seeding-only backdoor.

There is currently **no documented non-Docker fallback** (e.g. SQL LocalDB) for this suite — Docker
is a hard prerequisite for `npm run test:e2e` today, consistent with the identical prerequisite the
.NET integration tests already have. `ubuntu-latest` GitHub-hosted runners have Docker preinstalled,
so this is a no-op prerequisite in CI; a contributor working locally without Docker cannot run this
suite (they can still run unit tests and rely on CI for the rest) — flagged as a residual risk
below.

### Coverage: roles and journeys

| Spec | Coverage |
| --- | --- |
| `tests/e2e/tests/host-routing.spec.ts` | An unrecognised `Host` header yields 400 on **any** path (not just app routes); operational endpoints (`/api/health/live`) work regardless of host; `/v1/trips` returns 401 on the app host vs. 400 on an unrecognised host — proves the host-routing split is real, not just unit-tested |
| `tests/e2e/tests/anonymous.spec.ts` | `GET /v1/trips` → 401 with no session; navigating to `/trips` triggers the app's `RequireAuth` redirect to `/v1/auth/login` — intercepted via `page.route()` so the suite never actually reaches real Entra (see [hermeticity mitigation](#hermeticity-mitigation-the-anonymous-redirect-risk)) |
| `tests/e2e/tests/journey-1-and-2.spec.ts` | Full serial journey: **Owner** creates a trip (asserts required-name-only creation, the "dates not confirmed" banner, and the manage-members entry point) → invites an **Editor**, a **Viewer**, and a mismatched-identity invitee, extracting each stubbed acceptance link (`data-testid="invitation-acceptance-link"`'s `href`) → **Editor accepts** and role is verified as `1` (Editor) → **Viewer accepts** and role is verified as `0` (Viewer) → a **mismatched identity** attempts to accept the third invite and is refused (`accept-invitation-error` visible; never appears in the members list) → **Editor edits trip content successfully but cannot manage membership** (no `manage-members-link`; direct API `PUT .../members/{id}/role` → 403) → **Viewer is fully read-only** (no `edit-trip-name`/`manage-members-link`; direct API `PUT /v1/trips/{tripId}` → 403) → **last-owner protection** in both UI (no `leave-trip-button` success path / no `remove-member-{id}` for the sole owner) and API (`PUT .../role` and `DELETE .../members/{id}` on the last Owner both → 409) |

Every identity uses its own isolated `browser.newContext()` (its own cookie jar), since
`/testauth/signin` is cookie-based — reusing one context across identities would silently mix
sessions. Assertions combine **UI state** (button/element presence, banners, role select values)
**and** **direct `/v1` API calls** via Playwright's `page.request` (which automatically shares
cookies with its parent context) to prove the actual authorization outcome, not just what the UI
happens to hide.

### Hermeticity mitigation: the anonymous-redirect risk

The React `RequireAuth` wrapper redirects an unauthenticated visitor to `/v1/auth/login`, which in
production 302s onward to the real Entra authorize endpoint. If a spec merely clicked through and
let that redirect run to completion, it would attempt a real network call to
`login.microsoftonline.com` — breaking hermeticity and likely hanging/failing in an offline CI
runner. `anonymous.spec.ts` avoids this by installing a `page.route('**/v1/auth/login**', ...)`
interceptor **before** navigating, which fulfils the request with a stub response instead of letting
it reach the real handler — the redirect *attempt* (and therefore the `RequireAuth` behaviour) is
still fully exercised and asserted; only the real Entra hop is short-circuited.

### 4. CI integration

A new `e2e-tests` job was added to **both** `.github/workflows/build-and-test.yml` (push to
`agents/**`/`feature/**`/etc.) and `.github/workflows/pr-verify.yml` (pull requests), gated on the
existing `build-and-test` job succeeding first. It needs **zero secrets**:

1. `actions/setup-dotnet` (10.0.x) + `dotnet tool restore` (the pinned `dotnet-ef`).
2. `actions/setup-node` (22.x, npm-cached on `tests/e2e/package-lock.json`) + `npm ci` in
   `tests/e2e/`.
3. `actions/cache` for `~/.cache/ms-playwright`, then `npx playwright install --with-deps chromium`
   (Chromium only — keeps the job fast; the suite doesn't need cross-browser coverage for an
   internal role-matrix check).
4. `dotnet dev-certs https` so Kestrel has a certificate to bind its HTTPS endpoint with.
5. `npm run test:e2e` — this single command is `global-setup.ts` doing everything: start the SQL
   container (Docker is preinstalled on `ubuntu-latest`), apply migrations, publish the app (no
   artifact reuse from the `build-and-test` job in this first cut — see
   [residual risks](#residual-risks--follow-ups) — so this job does its own `dotnet publish`),
   start it, wait for `/api/health/live`, then run every spec.
6. Always uploads the Playwright HTML report as a build artifact (`if: always()`), so a failure's
   trace/screenshots are inspectable without re-running.

**Branch-ruleset action needed:** this is a **new** job/check name (`e2e-tests`) that did not exist
before this slice. If it should become a required status check on `main` (recommended, given it's
first-class coverage for this slice), **the orchestrator needs to add it to the branch protection
ruleset** — this agent cannot and did not modify branch rulesets.

### Local DX: the single command

```bash
cd tests/e2e
npm ci
npx playwright install --with-deps chromium   # once, or after a Playwright version bump
npm run test:e2e
```

Prerequisites (all already used elsewhere in this repo, no new tooling introduced):

- **Docker** running locally (Testcontainers talks to the local Docker daemon; Podman works too via
  `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE`, same as the .NET integration tests).
- **.NET 10 SDK** (per `global.json`) + `dotnet tool restore` at the repo root (installs
  `dotnet-ef`).
- **`dotnet dev-certs https`** run at least once (standard ASP.NET Core local HTTPS prerequisite,
  unrelated to this slice).
- **Node 22.x** + the `tests/e2e/` npm dependencies (`npm ci`).

`npm run test:e2e` is exactly `playwright test`, which triggers `global-setup.ts`: start SQL
container → migrate → ensure dev cert → publish (or reuse `E2E_APP_PUBLISH_DIR` if set) → start app
→ wait healthy → run specs → tear down (stop app, stop container) — identical sequence locally and
in CI.

## 4. Client component tests (Vitest + Testing Library + MSW)

Unchanged mechanism from the identity slice, extended with new specs for every new screen:
`CreateTripPage`, `TripsListPage`, `TripDashboardPage`, `ManageMembersPage`,
`AcceptInvitationPage` — each mocks the generated `openapi-fetch` client via MSW, asserting loading/
error/success states and the `data-testid`s the Playwright suite also relies on. Run via
`npm run test` in `src/MX.TripSideKick.Web/ClientApp` (also what CI's `build-and-test` job runs).

## Residual risks / follow-ups

- **No artifact reuse between `build-and-test` and `e2e-tests`.** The E2E job publishes the app a
  second time rather than downloading the `MX.TripSideKick.Web` artifact the `build-and-test`
  job/composite action already produces — simpler and decoupled from that composite action's
  internals for this first cut, at the cost of a slower job. `tests/e2e/support/appProcess.ts`
  already supports an `E2E_APP_PUBLISH_DIR` override, so wiring artifact download+reuse later is a
  small, isolated follow-up.
- **No local non-Docker fallback** for the E2E suite (unlike the task's suggested "SQL LocalDB"
  option) — not implemented in this pass; a contributor without Docker can still run unit tests
  locally and rely on CI for integration/E2E coverage.
- **Chromium-only** in CI — sufficient for an internal role-matrix suite; add Firefox/WebKit
  projects later only if a real cross-browser bug surfaces.
- **New required check.** If `e2e-tests` should gate merges to `main`, the branch ruleset needs an
  orchestrator-side update — not done as part of this PR.
- **`npm audit` findings (documented, not fixed this pass):**
  - `tests/e2e/`: 4 vulnerabilities (3 moderate, 1 high) — all transitive, via `testcontainers`'s
    `dockerode`/`undici`/`uuid` dependencies. `npm audit fix --force` would downgrade/upgrade across
    a `testcontainers` major version (breaking change) purely to satisfy dev/test-only tooling that
    never runs in production and never handles untrusted external input (it only talks to the local
    Docker daemon). Accepted as low risk for now; revisit when bumping `testcontainers` deliberately.
  - `src/MX.TripSideKick.Web/ClientApp/`: 2 high-severity findings, both the same underlying advisory
    (`react-router`/`react-router-dom` RSC-mode CSRF bypass). This app does **not** use React Server
    Components / RSC mode (it's a client-only SPA served as static files), so the advisory's attack
    surface does not apply here. `npm audit fix --force` would pull a breaking `react-router-dom`
    major bump. Accepted as low risk for now; revisit at the next planned React Router upgrade.
