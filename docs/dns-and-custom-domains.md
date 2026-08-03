# DNS and Custom Domains

## Cloudflare, not Azure DNS

`tripsidekick.app` and `tripsidekick.net` are **Cloudflare-managed zones**. There is no Azure DNS
zone for this workload and no `DNS Zone Contributor` role assignment — do not add one.

* Zone registration/ownership lives in `frasermolyneux/platform-connectivity`, which manages the
  zones with per-record resources (no zone-wide pruning), so workload-owned records are safe.
* This repo creates its own records with the Cloudflare Terraform provider (`cloudflare_dns_record`)
  using a **scoped API token** delivered as the GitHub environment secret `CLOUDFLARE_API_KEY` and
  passed to Terraform as `TF_VAR_cloudflare_api_token`.
* Zone IDs are resolved with the `cloudflare_zone` data source rather than remote state — this
  workload is not granted read access to the `platform-connectivity` state.

## Records created per environment

For every entry in the `custom_domains` variable Terraform creates two records:

| Record | Type | Content | Proxied |
| --- | --- | --- | --- |
| `<hostname>` | CNAME | `<web app name>.azurewebsites.net` | No |
| `asuid.<hostname>` | TXT | App Service `custom_domain_verification_id` | n/a |

### Development

| Hostname | Surface |
| --- | --- |
| `dev.tripsidekick.net` | Brochure site |
| `dev.tripsidekick.app` | PWA + `/v1` API |

### Production

| Hostname | Surface | Notes |
| --- | --- | --- |
| `tripsidekick.net` | Brochure site | Apex — relies on Cloudflare CNAME flattening |
| `www.tripsidekick.net` | Brochure site | Bound for TLS; the app `308`s it to the apex |
| `tripsidekick.app` | PWA + API | Apex — relies on Cloudflare CNAME flattening |
| `www.tripsidekick.app` | PWA + API | Bound for TLS; the app `308`s it to the apex |

`www` hostnames are bound to the App Service (so TLS terminates and the redirect can be issued) but
are deliberately **not** added to the `HostRouting` allow lists — `HostSurfaceMiddleware` redirects
them before routing ever sees them.

## Proxied vs DNS-only — decision

**All workload records are created DNS-only (`proxied = false`, grey cloud).**

Rationale:

1. **App Service managed certificates cannot be issued or renewed through the Cloudflare proxy.**
   Azure validates domain ownership and issues the free managed certificate by resolving the
   hostname directly to the App Service. Behind the orange cloud, resolution returns Cloudflare
   anycast addresses and issuance/renewal fails — silently, months later, at renewal time.
2. **Custom hostname binding validation** performs the same direct resolution (CNAME to
   `*.azurewebsites.net`, or the flattened apex A record, plus the `asuid` TXT record). Proxying
   breaks the binding, not just the certificate.
3. TLS is already terminated by App Service with `https_only = true` and TLS 1.2 minimum, so the
   proxy adds no security we do not already have.

Turning the orange cloud on later is only viable if the workload moves to Cloudflare **origin**
certificates with full (strict) encryption — at which point App Service managed certificates must be
removed from the plan. Do not toggle proxy mode in the Cloudflare UI without changing the Terraform
accordingly; the next apply will revert it.

### Apex records

Cloudflare flattens CNAME records at the zone apex, so `tripsidekick.net` and `tripsidekick.app`
resolve to A records pointing at the App Service inbound IP. That satisfies App Service apex
validation when combined with the `asuid` TXT record. If flattening behaviour ever changes, the
fallback is an explicit A record to the App Service inbound IP plus the same TXT record.

## Ordering and timing

Terraform sequences the work so a first apply succeeds without manual intervention:

1. `cloudflare_dns_record.app_service_verification` (TXT) is created first.
2. `cloudflare_dns_record.web_app` (CNAME) depends on the TXT record.
3. `azurerm_app_service_custom_hostname_binding` depends on both.
4. `time_sleep.wait_for_hostname_binding` waits 60s.
5. `azurerm_app_service_managed_certificate` and then
   `azurerm_app_service_certificate_binding` complete the TLS binding.

Certificate issuance can still fail on a *first* apply if Cloudflare has not finished propagating
(records are created with a 300s TTL). Re-running the apply is the supported remedy — every resource
is idempotent.
