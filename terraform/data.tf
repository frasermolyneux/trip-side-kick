data "azuread_client_config" "current" {}

data "azurerm_resource_group" "rg" {
  name = local.resource_group_name
}

# Zone IDs are resolved through the scoped Cloudflare token rather than platform-connectivity
# remote state; this workload is not granted read access to that state.
data "cloudflare_zone" "zones" {
  for_each = local.cloudflare_zone_names

  filter = {
    name = each.value
  }
}
