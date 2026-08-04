resource "azurerm_linux_web_app" "app" {
  name = local.web_app_name
  tags = local.tags

  resource_group_name = local.platform_hosting_app_service_plan.resource_group_name
  location            = local.platform_hosting_app_service_plan.location

  service_plan_id = local.platform_hosting_app_service_plan.id

  https_only = true

  identity {
    type = "SystemAssigned"
  }

  # checkov:skip=CKV_AZURE_222:This is a public-facing brochure/PWA site by design (see
  # docs/architecture-overview.md); disabling public network access needs a VNet/Private Link front
  # door, which is out of scope for this MVP (docs/infrastructure-and-cost.md).
  # checkov:skip=CKV_AZURE_13:Identity is intentionally stubbed for this slice - see
  # docs/identity-and-access.md and the `IDENTITY STUB` / `TODO (identity slice)` markers. App Service
  # Authentication (Easy Auth) is deferred to the identity slice.
  # checkov:skip=CKV_AZURE_17:No mTLS requirement for this public site; incoming client certificates
  # would break normal browser/PWA traffic.
  # checkov:skip=CKV_AZURE_88:The app is stateless and deployed via `WEBSITE_RUN_FROM_PACKAGE`; it has
  # no need for a persistent Azure Files mount.
  site_config {
    application_stack {
      dotnet_version = "10.0"
    }

    always_on           = true
    ftps_state          = "Disabled"
    http2_enabled       = true
    minimum_tls_version = "1.2"

    health_check_path                 = "/api/health/live"
    health_check_eviction_time_in_min = 5
  }

  # Diagnostic-only logging: written to the App Service's own log storage, not exposed to end users.
  logs {
    detailed_error_messages = true
    failed_request_tracing  = true

    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }
  }

  app_settings = merge(local.host_routing_app_settings, {
    "APPLICATIONINSIGHTS_CONNECTION_STRING"       = azurerm_application_insights.ai.connection_string
    "ApplicationInsights__ClientConnectionString" = azurerm_application_insights.ai.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION"  = "~3"
    "ASPNETCORE_ENVIRONMENT"                      = var.environment == "prd" ? "Production" : "Development"
    "WEBSITE_RUN_FROM_PACKAGE"                    = "1"

    "HostRouting__RedirectWwwToApex" = "true"

    "BlobStorage__ServiceUri"                  = azurerm_storage_account.data.primary_blob_endpoint
    "BlobStorage__DocumentsContainerName"      = local.blob_container_names.documents
    "BlobStorage__DataProtectionContainerName" = local.blob_container_names.dataprotection

    # Managed-identity SQL auth - no secret, no connection string password. The system-assigned
    # identity is granted EXACTLY db_datareader/db_datawriter by a CI step after apply (see
    # terraform/scripts/configure-sql-data-access.ps1 and docs/data-and-persistence.md); until that
    # step has run for a brand-new environment, SqlTripRepository etc. will fail on first use, but
    # startup and health/readiness never crash - see SqlReadinessHealthCheck.
    "Sql__ConnectionString" = "Server=tcp:${azurerm_mssql_server.sql.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.db.name};Authentication=Active Directory Managed Identity;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"

    "KeyVault__Uri" = azurerm_key_vault.kv.vault_uri

    # Entra External ID (B2B collaboration + self-service sign-up), app surface only. No client
    # secret is ever set here - Microsoft.Identity.Web authenticates the confidential client with a
    # signed assertion from the App Service's own system-assigned managed identity, trusted via the
    # federated identity credential in identity.tf.
    "AzureAd__Instance"                         = "https://login.microsoftonline.com/"
    "AzureAd__TenantId"                         = data.azuread_client_config.current.tenant_id
    "AzureAd__ClientId"                         = azuread_application.app_sign_in.client_id
    "AzureAd__CallbackPath"                     = "/signin-oidc"
    "AzureAd__SignedOutCallbackPath"            = "/signout-callback-oidc"
    "AzureAd__ClientCredentials__0__SourceType" = "SignedAssertionFromManagedIdentity"
  })

  lifecycle {
    ignore_changes = [
      # Set by the deployment workflow when it pushes the published package.
      app_settings["WEBSITE_RUN_FROM_PACKAGE"]
    ]
  }
}

# First-apply propagation race: App Service validates the custom hostname binding by resolving the
# asuid TXT record (and the CNAME) directly, and that lookup can run before Cloudflare has propagated
# the just-created records. The wait matches the records' TTL (local.custom_domain_dns_ttl_seconds,
# dns_records.tf) so the two can't drift out of sync - see docs/dns-and-custom-domains.md. A re-run
# remains the ultimate fallback (every resource here is idempotent), but the goal is first-apply
# success. Only created when there are custom domains to bind, so environments/configs without any
# (var.custom_domains empty) don't pay the wait for nothing.
resource "time_sleep" "wait_for_dns_propagation" {
  count = length(var.custom_domains) > 0 ? 1 : 0

  create_duration = "${local.custom_domain_dns_ttl_seconds}s"

  depends_on = [
    cloudflare_dns_record.app_service_verification,
    cloudflare_dns_record.web_app
  ]
}

resource "azurerm_app_service_custom_hostname_binding" "domains" {
  for_each = var.custom_domains

  hostname            = each.value.hostname
  app_service_name    = azurerm_linux_web_app.app.name
  resource_group_name = azurerm_linux_web_app.app.resource_group_name

  depends_on = [
    time_sleep.wait_for_dns_propagation
  ]
}

resource "time_sleep" "wait_for_hostname_binding" {
  create_duration = "60s"

  depends_on = [
    azurerm_app_service_custom_hostname_binding.domains
  ]
}

# App Service managed certificates are free and auto-renew, but they can only be issued while the
# hostname resolves straight to Azure - hence the DNS-only Cloudflare records.
resource "azurerm_app_service_managed_certificate" "domains" {
  for_each = var.custom_domains

  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.domains[each.key].id

  depends_on = [
    time_sleep.wait_for_hostname_binding
  ]
}

resource "azurerm_app_service_certificate_binding" "domains" {
  for_each = var.custom_domains

  hostname_binding_id = azurerm_app_service_custom_hostname_binding.domains[each.key].id
  certificate_id      = azurerm_app_service_managed_certificate.domains[each.key].id
  ssl_state           = "SniEnabled"
}
