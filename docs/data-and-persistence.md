# Data and Persistence

## Status: IMPLEMENTED (dev + prd via Terraform; DB user + migrations run on next `deploy-dev`/`deploy-prd`)

This slice (Trips + Membership/Roles) makes Azure SQL real: `SqlTripRepository` and friends replace
`EmptyTripRepository` as soon as `Sql:ConnectionString` configuration is present, and EF Core owns
the schema for Trips, Memberships, Travellers and Invitations.

## Why a CI step, not a Terraform resource

`terraform/sql.tf` provisions the Entra-only Azure SQL logical server and database. It deliberately
stops there: creating the **contained database user** for the App Service's data-access managed
identity (a `CREATE USER … WITH SID … TYPE = E` statement) is T-SQL, not an ARM or Graph operation,
so it cannot be an `azurerm`/`azuread` Terraform resource, and applying EF Core migrations is likewise
outside Terraform's remit. This mirrors the existing `configure-external-id-sign-up.sh` pattern
(`docs/identity-and-access.md`): a **discrete CI step**, authenticated via its own `azure/login`
under the workload's federated identity, runs after `terraform apply`.

## Why a dedicated user-assigned identity (and SID, not `FROM EXTERNAL PROVIDER`)

The App Service uses a **dedicated user-assigned managed identity** (`terraform/managed_identity.tf`)
for SQL data access — separate from its system-assigned identity, which continues to back the Entra
External ID sign-in federated credential (`terraform/identity.tf`).

The reason is the contained-user creation. `CREATE USER [name] FROM EXTERNAL PROVIDER` makes Azure SQL
resolve the identity through Microsoft Graph using the **SQL server's own identity**, which then needs
the Entra **Directory Readers** role. This workload's service principal is *not* granted the ability
to assign that role (it isn't in the `platform-workloads` definition, and "Cloud Application
Administrator" doesn't cover directory-role membership), and it also lacks the Graph permission to look
the identity up itself. A **user-assigned** identity sidesteps all of this: Terraform exposes its
`client_id` directly, so the contained user is created **by SID** —
`CREATE USER [name] WITH SID = <client_id-bytes>, TYPE = E` — which performs no directory lookup and
needs no Directory Readers role. (The wider org pattern in `portal-core` grants Directory Readers to a
purpose-built SQL-server identity provisioned by a separate platform stack; trip-side-kick has no such
stack, so the SID form is the self-contained equivalent.)

The workload service principal is deliberately the SQL server's Entra admin
(`azuread_administrator` block in `terraform/sql.tf`), so it is authorized to both create the
contained user **and** apply migrations. The runtime managed identity (the App Service itself)
never gets admin/schema rights — see [Least privilege](#least-privilege) below.

## Mechanism: `terraform/scripts/configure-sql-data-access.ps1`

Runs as the **"Configure SQL data access (contained user + migrations)"** step in both
`.github/workflows/deploy-dev.yml` and `deploy-prd.yml`, immediately after
`terraform-plan-and-apply` and the Entra sign-up configuration step, using the same `azure/login`
session:

1. **Acquire a token** for `https://database.windows.net` via
   `az account get-access-token --resource https://database.windows.net` (no secret — the workload's
   federated OIDC credential from the preceding `azure/login` step).
2. **Generate an idempotent EF Core migrations script** beforehand, in its own workflow step:
   `dotnet tool run dotnet-ef migrations script --idempotent --project src/MX.TripSideKick.Infrastructure -o migrate.sql`.
   The `--idempotent` flag wraps every migration in an `IF NOT EXISTS (SELECT ... FROM
   __EFMigrationsHistory ...)` guard, so re-running the same script against an already-migrated
   database is a safe no-op.
3. **`Invoke-Sqlcmd -AccessToken`** (via the `SqlServer` PowerShell module, installed on first use if
   missing) runs two idempotent operations against the real database, using the token from step 1:
   - `CREATE USER [<identity name>] WITH SID = <sid>, TYPE = E` — guarded by
     `IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ...)`. The user **name** is the
     data-access identity's name (`terraform output sql_data_identity_name`); the **SID** is that
     identity's `client_id` (`terraform output sql_data_identity_client_id`) converted to bytes
     (`System.Guid.ToByteArray()` order), never hardcoded. `TYPE = E` marks it an external (Entra)
     principal.
   - `ALTER ROLE db_datareader/db_datawriter ADD MEMBER [...]` — each guarded by a
     `sys.database_role_members` existence check.
   - Then applies `migrate.sql` from step 2.

Both operations are safe to re-run on every deploy: an already-existing user/role membership is a
no-op, and an already-applied migration is a no-op per its `--idempotent` guard.

```powershell
terraform/scripts/configure-sql-data-access.ps1 `
  -SqlServerFqdn "<terraform output sql_server_fully_qualified_domain_name>" `
  -SqlDatabaseName "<terraform output sql_database_name>" `
  -ManagedIdentityName "<terraform output sql_data_identity_name>" `
  -ManagedIdentityClientId "<terraform output sql_data_identity_client_id>" `
  -MigrationsScriptPath "<path to the --idempotent migrate.sql>"
```

## Least privilege

| Principal | Grants | Why |
| --- | --- | --- |
| Workload service principal (`spn-trip-side-kick-<env>`) | SQL server Entra **admin** (full control) | Must create users and apply schema changes; never used by the running app |
| App Service **data-access user-assigned managed identity** (`id-trip-side-kick-<env>-<location>`) | **`db_datareader` + `db_datawriter` only** | The runtime app reads/writes rows; it never applies migrations or alters schema. No `db_ddladmin`, no `db_owner` |

## Connection string (no secrets)

`terraform/web_app.tf` sets:

```
Sql__ConnectionString = "Server=tcp:<fqdn>,1433;Database=<db>;Authentication=Active Directory Managed Identity;User Id=<data-access identity client_id>;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
```

`Authentication=Active Directory Managed Identity` with `User Id=<client_id>` means the
Microsoft.Data.SqlClient driver acquires its own Entra token via the App Service's **user-assigned**
data-access identity at connection time —
**no password, no secret, ever** (`standards.oidc-and-secrets`). This satisfies the
`terraform/web_app.tf` `TODO(data-slice)` marker that previously left the setting empty.

`InfrastructureServiceCollectionExtensions.cs` registers the real EF Core stack
(`TripSideKickDbContext`, `SqlTripRepository`, `SqlMembershipRepository`, `SqlTravellerRepository`,
`SqlInvitationRepository`) only when `Sql:ConnectionString` is non-empty; otherwise it falls back to
the existing in-memory `Empty*Repository` implementations, so a fresh environment (before this
step has ever run) still starts up cleanly — it just serves no persisted data until the CI step
above has run at least once.

## EF Core model summary

See [Domain and application model](#domain-and-application-model-summary) below for the full
invariants; this section is the persistence mapping only.

| Aggregate | Table | Key mapping notes |
| --- | --- | --- |
| `Trip` | `Trips` | `TripId` (UUIDv7, `Guid` column, generated client-side via `Uuid7.NewUuid7()`), `RowVersion` (SQL Server `rowversion`/`timestamp`, EF `IsRowVersion()`), `TripDates` owned type (`StartDate`/`EndDate` as nullable NodaTime `LocalDate` via `NodaTimeConversions`, plus a `TripDateStatus` enum column), `ReportingCurrency` as a fixed `char(3)` ISO 4217 code, nullable `CoverImageUri` |
| `Membership` | `Memberships` | `MembershipId` (UUIDv7), FK `TripId`, `SubjectId` (the Entra `oid` — **never email**), `MembershipRole` enum column, `RowVersion`. Unique index on `(TripId, SubjectId)` — one membership per trip per identity |
| `Traveller` | `Travellers` | `TravellerId` (UUIDv7), FK `TripId`, `DisplayName`, nullable `LinkedMembershipId` FK (self-contained: a traveller may or may not be linked to an account) |
| `Invitation` | `Invitations` | `InvitationId` (UUIDv7), FK `TripId`, invited email (normalized, **bound** — see below), `MembershipRole` to grant on accept, `InvitationStatus` enum (`Pending`/`Accepted`/`Revoked`), nullable `LinkedTravellerId`, a random, non-guessable acceptance token (used to build the stubbed acceptance link — see [Invitation notifications](#invitation-notifications-the-iinvitationnotifier-stub)), `RowVersion` |
| `ItineraryItem` (Slice 5) | `ItineraryItems` | `ItineraryItemId` (UUIDv7), FK `TripId`, `Title`, nullable `Notes`/`Location`, an `ItinerarySchedule` complex property (`ScheduleStatus` enum + nullable `ScheduledDate`/`ScheduledStartTime`/`ScheduledEndTime`), `ApplicableTravellerIds` as a JSON list column (empty = everyone — see `docs/itinerary-and-travellers.md`), `RowVersion` |
| `ItineraryComment` (Slice 5) | `ItineraryComments` | Append-only. `ItineraryCommentId` (UUIDv7), FK `TripId`, FK `ItineraryItemId`, `AuthorSubjectId` (Entra `oid`), `Body`, `CreatedAt` (`Instant`). No `RowVersion` |
| `TripActivityFeedEntry` (Slice 5) | `TripActivityFeedEntries` | Append-only. `TripActivityFeedEntryId` (UUIDv7), FK `TripId`, `ActorSubjectId`, `EventType` enum, `Summary` (trip content, never PII — see [logging rules](#pii-rules)), `OccurredAt` (`Instant`), nullable FK `ItineraryItemId`. Written inside the same `IUnitOfWork.ExecuteAsync` transaction as the underlying mutation |
| `TripTravellerFilter` (Slice 5) | `TripTravellerFilters` | Per-member persisted filter preference. `TripTravellerFilterId` (UUIDv7), FK `TripId`, FK `MembershipId`, `Mode` enum (`Everyone`/`Me`/`Selected`), `SelectedTravellerIds` as a JSON list, `RowVersion`. Unique index on `(TripId, MembershipId)` |

All dates are NodaTime (`LocalDate`/`Instant` as applicable) via the custom `NodaTimeConversions`
value converters — no `DateTime`/`DateTimeOffset` anywhere in the domain or EF mapping. Money
(`ReportingCurrency`) is a `decimal`-free field this slice (currency code only, no amounts/FX
conversion yet — see the trip-creation journey below); the existing `Common/Money.cs` value object
(decimal + ISO 4217) remains the pattern for the future slice that adds actual costs.

The single migration `20260804144332_InitialTripsMembership` creates the first four tables; the
Slice 5 migration `20260806193446_AddItineraryAndTravellerFilter` adds
`ItineraryItems`, `ItineraryComments`, `TripActivityFeedEntries`, and `TripTravellerFilters`
(with the unique `(TripId, MembershipId)` index on filters). Both are applied by the same
CI-as-admin step described above.

## Domain and application model summary

- **`Trip`** — name (required, only mandatory field), optional destination(s), optional
  `ReportingCurrency` (validated ISO 4217, no FX conversion this slice), `TripDates` modelled as a
  `TripDateStatus` (`Undecided`/`Approximate`/`Confirmed`) plus optional start/end `LocalDate`s —
  never a bare nullable pair, so "the dates aren't decided yet" is an explicit, queryable state
  rather than an inferred one. Only `Confirmed` with both dates set unlocks date-dependent
  capabilities (a future slice); `Undecided`/`Approximate` still allow general planning. Creation is
  online-only (no offline trip creation this slice) and always creates the creator as an **Owner**
  membership **and** an account-linked `Traveller` in the same transaction — removing oneself as a
  traveller (`DELETE .../travellers/self` or similar) does not touch the Owner membership; ownership
  and traveller-presence are independent facts about the same identity.
- **`Membership`** — `SubjectId` + `MembershipRole` (`Viewer`/`Editor`/`Owner`), one row per
  `(TripId, SubjectId)`. `MembershipPolicy` (domain) centralises the authorization rules: only
  Owners can invite/remove/change roles; Editors can mutate trip content but not membership;
  Viewers cannot mutate anything; **the last remaining Owner cannot leave, be removed, or be
  demoted** (`LastOwnerViolationException` → HTTP 409) until a second Owner exists — multiple
  Owners are explicitly allowed and is the only way to hand off/step down safely.
- **`Traveller`** — deliberately minimal this slice: `Id`, `TripId`, `DisplayName`, optional
  `LinkedMembershipId`. No per-activity assignment, no Everyone/Me/selected filter — see
  [Journey 10 deferral](#journey-10-deferral-travellers-filtering) below for how this shape
  accommodates that later without a breaking change.
- **`Invitation`** — invite by email + role, optionally linked to an existing traveller, a new
  linked traveller (created on accept), or a non-travelling planner (access without ever appearing
  in traveller lists). States: `Pending` → `Accepted`/`Revoked` (also resend, which reuses the same
  invitation row/token rather than creating a duplicate). **Binding**: the invitation is bound to
  the invited email address for its entire lifetime, including after the invitee creates an
  account — accept only succeeds when the signed-in user's Entra **verified email** claim matches
  the invited email (case-insensitively); a mismatch throws `InvitationIdentityMismatchException`
  (→ HTTP 403) and never grants access. Accepting a linked invite connects exactly the intended
  traveller record (no duplicate traveller created); accepting a non-travelling-planner invite
  grants a `Membership` with no `Traveller` row at all.

Authorization throughout (API, application services, and the E2E suite) keys **only** on
`ICurrentUser.SubjectId` — the invitation-acceptance email check is the sole place email is ever
compared, and even then only against the *invitation's own bound email*, never used to look up or
authorize unrelated resources (`docs/identity-and-access.md`).

## Invitation notifications: the `IInvitationNotifier` stub

Email delivery is **not available yet** — the `platform-notifications` `tripsidekick.net` sending
domain is a separate, pending piece of shared infrastructure. Rather than block this slice on that
dependency, invitation sending is behind an application-layer abstraction:

```csharp
public interface IInvitationNotifier
{
    Task NotifyCreatedAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task NotifyResentAsync(Invitation invitation, CancellationToken cancellationToken = default);
}
```

The only implementation registered today, `LoggingInvitationNotifier`, does not send anything — it
returns/logs the acceptance link and the API surfaces it directly in the invitation response
(`InvitationResponse.AcceptanceUrl`) and the React `ManageMembersPage` renders it as a
copy/click-through link (`data-testid="invitation-acceptance-link"`), so invitations are fully
testable — manually and in the E2E suite — without any email ever being sent.

**TODO (email-delivery slice):** once `platform-notifications`' `tripsidekick.net` sending domain
exists, add an `MX.Platform.Notifications`-backed `IInvitationNotifier` implementation and register
it in place of `LoggingInvitationNotifier` — no other code should need to change, since callers only
depend on the interface. `LoggingInvitationNotifier`'s log statements never include the invited
email address (PII) or the invitation's raw acceptance token — only the invitation id and trip id.

## Local development

`docker-compose.yml` (repo root) already provisions a local SQL Server 2022 container for
`dotnet run`/`dotnet watch` development, unchanged by this slice. Point
`src/MX.TripSideKick.Web/appsettings.Development.json` (or user-secrets, preferred so nothing is
committed) at it:

```json
{ "Sql": { "ConnectionString": "Server=localhost;Database=TripSideKick;User Id=sa;Password=<compose password>;TrustServerCertificate=true;" } }
```

Apply migrations locally with `dotnet tool restore` (installs the pinned `dotnet-ef` from
`.config/dotnet-tools.json`) then:

```bash
dotnet ef database update --project src/MX.TripSideKick.Infrastructure --startup-project src/MX.TripSideKick.Web
```

`TripSideKickDbContextFactory` (the `IDesignTimeDbContextFactory` `dotnet ef` tooling uses) reads an
optional `TRIPSIDEKICK_MIGRATION_CONNECTION_STRING` environment variable override — used by the
Playwright E2E harness (`tests/e2e/support/migrate.ts`) to point migrations at its ephemeral
Testcontainers database instead of the docker-compose one; the running app itself never reads this
variable, only `dotnet ef` tooling does.

## Health

`/api/health/live` remains liveness-only (process is up), unchanged. `SqlReadinessHealthCheck`
(tagged `ready`) is registered for `/api/health/ready` and executes a trivial `SELECT 1` with a
short timeout; a SQL outage degrades readiness (so App Service/load-balancer probes can react) but
**never throws during startup or brings the process down** — this preserves the existing
"readiness is deliberately shallow" convention (`standards.health-endpoints`) while adding real
signal now that SQL exists.

## Residual risks / follow-ups

- **First-run friction on a brand-new environment.** Until the "Configure SQL data access" CI step
  has run at least once for an environment, the app starts fine (falls back to
  `Empty*Repository` behaviour is *not* what happens once `Sql:ConnectionString` is set — instead,
  `SqlTripRepository` will throw on first real query because the contained user doesn't exist yet).
  In practice this only affects the very first `deploy-dev`/`deploy-prd` run after this PR merges;
  every subsequent deploy is a no-op-safe re-run.
- **Migration ownership.** Migrations are applied by CI-as-admin, deliberately never by the running
  app at startup (no `Database.Migrate()` call in `Program.cs`) — this avoids multiple App Service
  instances racing to apply the same migration and keeps the runtime identity's grants minimal.
- **No Private Link/VNet this slice** — unchanged from the existing "allow Azure services" firewall
  rule documented in `docs/infrastructure-and-cost.md`; revisit if the workload's data
  classification changes.
