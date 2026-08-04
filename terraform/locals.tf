locals {
  resource_group_name = "rg-${var.workload}-${var.environment}-${var.location}"

  platform_hosting_app_service_plan = data.terraform_remote_state.platform_hosting.outputs.app_service_plans["default"]
  platform_monitoring_workspace_id  = data.terraform_remote_state.platform_monitoring.outputs.log_analytics.id

  web_app_name      = "app-${var.workload}-${var.environment}-${var.location}-${random_id.environment_id.hex}"
  key_vault_name    = "kv-${random_id.environment_id.hex}"
  app_insights_name = "ai-${var.workload}-${var.environment}-${var.location}"
  sql_server_name   = "sql-${var.workload}-${var.environment}-${var.location}-${random_id.environment_id.hex}"
  sql_database_name = "sqldb-${var.workload}-${var.environment}"

  storage_account_prefix = substr(replace(var.workload, "-", ""), 0, 11)
  storage_account_name   = lower("st${local.storage_account_prefix}${var.environment}${random_id.storage.hex}")

  blob_container_names = {
    documents      = "documents"
    dataprotection = "dataprotection"
  }

  # Cloudflare zones referenced by the configured custom domains.
  cloudflare_zone_names = toset([for domain in var.custom_domains : domain.zone])

  # Single source of truth for the custom-domain DNS record TTL (dns_records.tf) so the
  # first-apply propagation wait (time_sleep.wait_for_dns_propagation in web_app.tf) can't drift
  # out of sync with it.
  custom_domain_dns_ttl_seconds = 300

  # www aliases exist purely so the app can 308 them to the apex host; they are bound to the App
  # Service (so TLS terminates) but are never added to the host-routing allow lists.
  primary_domains = { for key, domain in var.custom_domains : key => domain if !domain.redirect }

  host_routing = {
    site_hosts = concat(
      [for domain in local.primary_domains : domain.hostname if domain.surface == "site"],
      ["${local.web_app_name}.azurewebsites.net"]
    )
    app_hosts = concat(
      [for domain in local.primary_domains : domain.hostname if domain.surface == "app"],
      ["${local.web_app_name}.azurewebsites.net"]
    )
  }

  # HostRouting__SiteHosts__0 = ... style App Service settings.
  host_routing_app_settings = merge(
    { for index, host in local.host_routing.site_hosts : "HostRouting__SiteHosts__${index}" => host },
    { for index, host in local.host_routing.app_hosts : "HostRouting__AppHosts__${index}" => host }
  )

  is_serverless_sql = startswith(var.sql_database.sku_name, "GP_S_")

  tags = merge(var.tags, {
    Environment = var.environment
    Workload    = var.workload
    DeployedBy  = "GitHub-Terraform"
    Git         = "https://github.com/frasermolyneux/trip-side-kick"
  })
}
