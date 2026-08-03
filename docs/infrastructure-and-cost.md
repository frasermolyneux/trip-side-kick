# Infrastructure and Cost

Everything is Terraform (`terraform/`), applied per environment from CI with OIDC federated
credentials. There are **two environments only**: `dev` (Development) and `prd` (Production), both
in **swedencentral**.

## Remote state consumed

| State | Provides |
| --- | --- |
| `platform-workloads` | Resource groups, Terraform backends and the workload service principal (consumed indirectly — the RG is looked up by name) |
| `platform-hosting` | The shared Linux App Service plan (`app_service_plans["default"]`) and hosting resource group |
| `platform-monitoring` | The shared Log Analytics workspace (`log_analytics.id`) |

This workload **never creates an App Service plan or a Log Analytics workspace.**

## Resources created per environment

| Resource | Name pattern | Notes |
| --- | --- | --- |
| Linux Web App | `app-trip-side-kick-<env>-swedencentral-<id>` | On the shared plan, .NET 10, system-assigned identity, `https_only`, TLS 1.2 min, health check `/api/health/live`, `WEBSITE_RUN_FROM_PACKAGE = 1` (seeded here, then owned by the deployment workflow via `lifecycle.ignore_changes`) |
| Custom hostname bindings + managed certificates | per `custom_domains` entry | Free App Service managed certificates |
| Cloudflare DNS records | `<host>` CNAME + `asuid.<host>` TXT | See [DNS and Custom Domains](dns-and-custom-domains.md) |
| Application Insights | `ai-trip-side-kick-<env>-swedencentral` | Workspace-based, pointed at the shared `platform-monitoring` workspace |
| Key Vault | `kv-<id>` | RBAC authorisation, soft delete + purge protection, public network access on (no VNet in this slice) |
| Storage account | `sttripsidekic<env><id>` | StorageV2, LRS, TLS 1.2, `allow_nested_items_to_be_public = false`, `shared_access_key_enabled = false`, OAuth-only. Containers `documents` and `dataprotection`, both private. The workload name is truncated to 11 characters to stay inside the 24-character storage account name limit |
| SQL logical server | `sql-trip-side-kick-<env>-swedencentral-<id>` | v12, Entra-only auth (`azuread_authentication_only = true`), no SQL logins, `AllowAllAzureServices` firewall rule |
| SQL database | `sqldb-trip-side-kick-<env>` | See tier below |
| Diagnostic settings | — | App Service, SQL database, Key Vault and the blob service all ship to the shared workspace |
| Role assignments | — | Web app identity: `Key Vault Secrets User`, `Storage Blob Data Contributor`. Deploy SP: `Storage Blob Data Contributor` (needed because shared keys are disabled and Terraform creates containers over the data plane) |

## SQL tier and cost

**Chosen tier: `GP_S_Gen5_1` — General Purpose Serverless, Gen5, 1 vCore max**, with
`min_capacity = 0.5`, `auto_pause_delay_in_minutes = 60`, `max_size_gb = 32`,
`storage_account_type = "Local"`, `zone_redundant = false`. Identical in both environments.

Why serverless rather than Basic DTU:

* Auto-pause after 60 minutes idle means a personal project pays almost nothing overnight and at
  weekends — the dominant cost becomes storage, not compute.
* vCore-based General Purpose is the tier EF Core 10 features and future scale actually want; Basic
  caps at 2 GB and 5 DTU, which is a dead end.
* Both environments use the same tier, so a Development problem reproduces in Production.

### Estimated monthly cost (swedencentral, pay-as-you-go, £ approximate)

| Component | Development | Production |
| --- | --- | --- |
| SQL compute (serverless, auto-paused) | ~£2–8 — depends entirely on active hours; a genuinely idle DB approaches £0 | ~£10–35 if the app is warm during waking hours |
| SQL storage (32 GB provisioned, billed on used GB) | ~£2–4 | ~£2–4 |
| Storage account (LRS, low volume) | <£1 | <£1 |
| Key Vault (RBAC, few operations) | <£1 | <£1 |
| Application Insights (ingestion) | <£1 at skeleton volumes | £1–5 |
| App Service | £0 — shared `platform-hosting` B2 plan, already paid for | £0 |
| Managed certificates, Cloudflare DNS | £0 | £0 |
| **Total** | **~£5–15/month** | **~£15–45/month** |

> ⚠️ **Cost flag.** These are estimates, not a quote — serverless SQL is billed per vCore-second and
> the range is dominated by how long the database stays un-paused. Validate against Azure Cost
> Management after the first full billing week. `destroy-development.yml` runs nightly at 23:55 UTC
> specifically to keep the Development bill near zero; the Development environment is expected to be
> ephemeral.
>
> If the Development bill is still uncomfortable, the cheapest alternative is `Basic` (2 GB, ~£4/month
> flat) — change `sql_database.sku_name` in `terraform/tfvars/dev.tfvars`; `locals.is_serverless_sql`
> already nulls the serverless-only arguments for non-serverless SKUs.

## Secrets posture

* No client secrets anywhere. Azure auth is OIDC federated credentials in CI and system-assigned
  managed identity at runtime.
* No connection strings in App Service settings — the SQL connection string is Entra-authenticated
  and the storage account is reached with `DefaultAzureCredential` over `BlobStorage:ServiceUri`.
* Storage shared keys are disabled outright (`shared_access_key_enabled = false`).
* The only secret material in the pipeline is `CLOUDFLARE_API_KEY` (a scoped Cloudflare API token)
  and `SONAR_TOKEN`, both GitHub-managed.
* Key Vault is provisioned but currently holds nothing — it exists so the identity and data slices
  have a home for anything that genuinely cannot be a managed identity.

## Known gaps for later slices

* No VNet integration or Private Link; SQL uses the "allow Azure services" firewall rule and the
  storage account and Key Vault allow public network access. Revisit if the workload ever holds data
  that justifies the extra plan cost.
* The App Service managed identity still needs a contained database user
  (`CREATE USER [...] FROM EXTERNAL PROVIDER`) — see the TODO in `terraform/sql.tf`.
* No Entra External ID resources — see [Identity and Access](identity-and-access.md).
