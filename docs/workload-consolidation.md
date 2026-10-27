# Constrained runtime migration

The application targets the existing `ns346663` constrained runtime: .NET 10 Razor
brochure (`tripsidekick.net`) and React PWA/BFF (`tripsidekick.app`), with a dedicated
PostgreSQL 18 database. No hosted Development environment is introduced.

**Deployment is not authorized by this source change.** `workload.json` contains
deliberately nonexistent zero digests. Publication and a digest-only promotion PR
must precede separately reviewed host activation. Public routing, the platform's
`platform-ingress` network mapping, secrets, certificates, backups, DNS, registry
permissions, and legacy retirement remain platform/operator responsibilities.
No Terraform resources or state are removed or changed here; destroy workflows
and retained Entra/vault definitions remain available for coordinated retirement.
Legacy Azure apply/deploy workflows now fail closed on manual dispatch and have
no schedule or automatic triggers. Development destruction is manual-only; its
capability and Terraform definitions are retained. PR labels cannot re-enable
legacy deployments.

## Separately reviewed Production DNS cutover

`terraform/tfvars/prd.tfvars` explicitly selects `runtime_origin_ipv4` for the
constrained host. All four public app/brochure apex and www records become
proxied A records. `platform-connectivity` retains ownership of both managed
zones and their strict origin TLS baseline; no zone-wide TLS setting is changed
by this workload. The host already has separate Origin CA certificates and
canonical www redirects.

Keep this cutover held until actual off-server database, document and keyring
restores pass. Real delegated sign-in/sign-out and Trip B2B sign-up remain
operator acceptance gates before Azure retirement. Azure compute, SQL/storage,
Entra registration, existing DNS verification and Google ownership are retained.
Setting `runtime_origin_ipv4` back to null restores DNS-only Azure routing in a
reviewed rollback; existing managed Azure certificates cannot renew while
Cloudflare proxying is enabled. Do not treat legacy Azure cleanup as collateral
to the DNS change.

## Runtime contract

| Service | UID:GID | Writable mount IDs and paths | Networks |
|---|---|---|---|
| `trip-side-kick-web` | `1654:1654` | `keyring` → `/app/state/keys`; `documents` → `/app/state/documents` | `database`, `public-proxy` |
| `trip-side-kick-postgres` | `999:1654` | `postgres` → `/var/lib/postgresql/data` | private `database` only |

Both roots are read-only, capabilities dropped by the platform, with bounded
resources and `/tmp` tmpfs. Images declare no `VOLUME` metadata. The private
database has **no host port or ingress**. Host policy supplies the public-proxy
network's mapping to `platform-ingress`; application images do not manage hosts.
Precreate/mount directories with the specified owners and minimum permissions.
The keyring contains sensitive signing/encryption material; restrict it to the
web UID and include it in encrypted backups, together with the document directory.
The document mount is reserved for document persistence; no unrelated upload
feature or object-storage migration is introduced.

Text secrets:

* `runtime-configuration` → `/run/secrets/runtime-configuration` (web only).
* `client-certificate` → `/run/secrets/client-certificate` (web only): base64 of a
  passwordless PFX with private key, registered on the existing workforce B2B app.
  The application loads it with `EphemeralKeySet`; it never writes the private key
  to the filesystem or returns access/refresh tokens to the browser.
* `database-password` → `/run/secrets/database-password` (both services): a
  nonempty, high-entropy single-line password. Mount readable by the service UID
  or approved shared group, never world-readable in production.

`deployment/runtime.example.json` is the authoritative configuration shape, with
no secret values. `TRIPSIDEKICK_RUNTIME_FILE` pins the JSON file; a pinned missing
file fails startup. It is loaded before service composition; environment settings
may override individual keys. Do not mount source, inject credentials into the
manifest, or copy live runtime JSON into the repository.

`Sql:ConnectionString` holds only host/database/user settings. `Sql:PasswordFile`
adds the password using `NpgsqlConnectionStringBuilder`; error detail is disabled.
The database and application/logical-backup role are **`tripsidekick`**. Bootstrap
creates **`tripsidekick_admin`**, accepted only on loopback; the application role
is a database owner, not a superuser, role creator or replication user. Bootstrap
reuses the password secret but does not permit remote administrative login.
Password rotation is a separate operator action, never an application deployment.

The runtime adapter must populate `ForwardedHeaders:KnownProxies` with the exact
Caddy IP. Existing framework loopback trust remains; no proxy/network allowlists
are cleared, forwarded host is never trusted, and forwarding is limited to one hop.
Without that explicit IP, external forwarded HTTPS/IP headers are ignored.

Host allowlists still separate brochure routes/assets from app API/PWA assets.
`Health:Host` must exactly match a configured app/site host. The exec probes call
loopback HTTP:8080 with that Host and never follow redirects:

* `dotnet MX.TripSideKick.Web.dll --healthcheck`: liveness.
* `dotnet MX.TripSideKick.Web.dll --readiness`: database/schema readiness.

Only the existing `/api/health/live` and `/api/health/ready` routes bypass HTTPS
redirection; host validation still runs first. Database loss leaves liveness
healthy but returns readiness 503. Missing database configuration outside local
Development fails startup. Production also requires absolute persistent state
directories. Test-auth remains Development-only, explicit opt-in, and unavailable
in Production regardless of `WEBSITE_INSTANCE_ID`.

## Fresh database and explicit initialization

There is no meaningful production data to transfer. SQL Server migrations have
been replaced with a fresh PostgreSQL initial migration; **do not run it against
an existing SQL Server or try to preserve its migration history**.

Initialization is off by default. On the single production application instance:

1. Provision the private database and persistent state mounts through the platform.
2. Set `Sql:InitializeOnStartup` to `true` explicitly for bootstrap/approved schema
   updates, or run the image once with `--initialize-database` in the same runtime.
   The command applies migrations and exits without serving traffic.
3. EF Core's PostgreSQL migration lock serializes migration runners. Applying the
   same migration repeatedly is safe; no `EnsureCreated`, database reset, automatic
   destructive migration, or background migration worker is used.
4. Confirm readiness and the application version; set the flag back to `false`.

Readiness requires a working connection and no pending migrations. Keep one
production instance; schema changes require reviewed compatibility and backups.
PostgreSQL generates opaque 16-byte versions on inserts and refreshes them through
database triggers on updates. Existing byte-array HTTP ETags/If-Match semantics
are unchanged. Unique violation `23505` retains domain conflict translation;
serializable transactions preserve last-owner invariants, with serialization/
deadlock races reported as 409 rather than unsafe transaction replay.

Logical backups can use `pg_dump --username=tripsidekick --dbname=tripsidekick`
inside the private runtime with the password supplied from the mounted secret,
not command-line arguments. Platform-owned backup automation must encrypt,
retain off-host copies, and test restore alongside keyring/document recovery.

## Local development and validation

```powershell
docker compose up -d postgres
$env:Sql__ConnectionString = 'Host=127.0.0.1;Database=tripsidekick;Username=tripsidekick;Password=local-development-only'
$env:Sql__InitializeOnStartup = 'true'
dotnet run --project src\MX.TripSideKick.Web
dotnet test src\MX.TripSideKick.slnx
```

The compose password is disposable local-only data. Local Development may run
without a database for deterministic empty-repository tests, but readiness is
then unhealthy. Never select `Development` or enable `TestAuth` on a public host.

Integration tests start disposable PostgreSQL 18 via Testcontainers, or accept
`TRIPSIDEKICK_TEST_POSTGRES` pointing **only to a disposable CI database**. The
fixture applies real migrations twice. Existing lifecycle, ETag, itinerary,
membership and invitation tests now exercise PostgreSQL; focused provider tests
cover database-generated concurrency, duplicate errors, serializable rollback,
and database readiness/outages.

```powershell
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName!~IntegrationTests'
dotnet test src\MX.TripSideKick.slnx --filter 'FullyQualifiedName~IntegrationTests'
dotnet ef migrations has-pending-model-changes --project src\MX.TripSideKick.Infrastructure --startup-project src\MX.TripSideKick.Web
python deployment\build_web_image.py --tag tripsidekick-web:ci
docker build -f deployment\postgres.Dockerfile -t tripsidekick-postgres:ci .
python deployment\test_containers.py
```

The image smoke test checks non-root/read-only execution, absent image volumes,
explicit migrations/restart, configured health hosts, logical backup and database
loss. Browser journeys retain deterministic local test-auth and use PostgreSQL.
Build and Test/PR Verify run both hermetic tests and the PostgreSQL/container
workflow; Code Quality and secure scanning retain existing required check names.

Production image publication follows successful exact-source Build and Test
**and** Code Quality and the trusted `workload-contract` check, and locks source-SHA tags plus digest deletion in the
reviewed ACR. Promotion changes only the two digest fields, retains the existing
stale-source/exact-head Quality/Copilot review guards, and **does not auto-merge**.
No image publication or promotion workflow deploys or mutates a host.
The source build records the complete main push range, so multi-commit/rebase
pushes cannot hide image changes behind a final documentation-only commit.
Unavailable pre-push history conservatively publishes; digest-only promotions
still do not cause image-publication loops.

Web images must use `deployment/build_web_image.py`: it computes real NBGV
assembly/file/informational versions from the checkout and passes them explicitly
to the image build. Missing metadata fails the Dockerfile; `.git` never enters
the image context or runtime.

PR jobs never receive GitHub App credentials. A separate default-branch
`workflow_run` validates only the triggering revision's `workload.json` as data,
using the reviewed default-branch policy and pinned private-platform compiler.
It never checks out or executes PR code, including for forks and Dependabot.
The read-only private-platform token is restricted to that trusted workflow.
This workflow becomes active after the source PR merges; initial source review
uses the recorded local pinned-compiler validation. Repository GitHub App
enrollment is required before publication; failure cannot silently skip the gate.

Existing Google Maps configuration is unchanged; GCP retention, rotation and
provider/federation/catalog changes are coordinated elsewhere.
