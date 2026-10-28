locals {
  resource_group_name = "rg-${var.workload}-${var.environment}-${var.location}"

  platform_monitoring_workspace_id = data.terraform_remote_state.platform_monitoring.outputs.log_analytics.id
  key_vault_name                   = "kv-${random_id.environment_id.hex}"

  # Cloudflare zones referenced by the configured custom domains.
  cloudflare_zone_names = toset([for domain in var.custom_domains : domain.zone])

  primary_domains = { for key, domain in var.custom_domains : key => domain if !domain.redirect }

  identity_app_hostnames = [for domain in local.primary_domains : domain.hostname if domain.surface == "app"]

  identity_redirect_uris = [for hostname in local.identity_app_hostnames : "https://${hostname}/signin-oidc"]
  identity_logout_url    = "https://${local.identity_app_hostnames[0]}/signout-callback-oidc"

  tags = merge(var.tags, {
    Environment = var.environment
    Workload    = var.workload
    DeployedBy  = "GitHub-Terraform"
    Git         = "https://github.com/frasermolyneux/trip-side-kick"
  })
}
