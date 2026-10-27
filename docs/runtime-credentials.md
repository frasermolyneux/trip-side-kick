# Production runtime credentials

The retained application registration keeps its workforce/B2B audience and user
flow. The server authenticates the confidential web client with a certificate,
not the retired App Service's managed identity or a client secret.

`Runtime Infrastructure` plans the complete Production Terraform configuration
on infrastructure pull requests. Its input-free manual dispatch applies only
approved `main`; it never publishes application code or creates Development.
Review the full plan before dispatch. Retain the legacy resources until the
server, authentication and isolated restores have passed acceptance.

PR plans are redacted log output only: no binary plan/state artifact is
published. The shared setup still enforces the committed CLI/provider pins.
The approved-main apply creates and consumes its saved plan in one isolated
job, without uploading it for repository-wide download.

After applying the reviewed vault/host permissions, dispatch:

```powershell
gh workflow run bootstrap-runtime.yml --repo frasermolyneux/trip-side-kick --ref main
gh workflow run renew-origin-tls.yml --repo frasermolyneux/trip-side-kick --ref main
```

The first workflow discovers the exact registration and vault from the approved
runtime example and validation policy, checks ownership/tenant/audience, and
creates a database password only if missing. It generates an ephemeral RSA key,
appends the public certificate, authenticates it against Entra, and only then
publishes a base64 passwordless PFX. Private keys never enter Terraform state,
Git, command-line values or artifacts. Management-plane secret publication
does not require opening the default-deny vault to hosted runner addresses.
The host reads through its exact-vault Arc role and fixed egress allowlist.

The first configuration publication explicitly enables
`Sql:InitializeOnStartup` for the fresh PostgreSQL schema. Repeated bootstrap
runs preserve this pending state, even if host activation has not happened.
After verifying the actual host's readiness and deployed version, dispatch
`complete-runtime-bootstrap.yml` to record completion and restore `false`.
This explicit action changes only configuration, not database or certificate
credentials. Subsequent bootstrap runs retain the completed state.
Existing database passwords are never implicitly
rotated. An unchanged reviewed configuration creates no new secret version.
Trusted Caddy addresses are resolved and injected only by the host.

Credential and TLS workflows are manual-only during initial rollout. Enable
their weekly renewal schedules in a reviewed post-acceptance change.
Certificate checks renew 180-day Entra certificates within 30 days of expiry.
Unrelated public keys and unexpired previous certificates are retained;
only this workflow's expired keys are eligible for removal after seven days.
The independent origin workflow requests 365-day Origin CA certificates for
the exact brochure/app apex and www hostnames and renews within 30 days. It
does not modify DNS or routes. A separate exact-zone
`CLOUDFLARE_ORIGIN_API_TOKEN` is supplied by the workload catalog.

Missing established credentials, unexpected ownership, disabled secrets,
expiring database passwords or changed
certificate bindings stop publication and require explicit recovery. A failed
bootstrap can leave an unused public certificate; retry safely without
deleting existing keys. Verify actual browser sign-in/sign-out and user-flow
behavior before revoking the legacy managed-identity trust or retiring Azure.
