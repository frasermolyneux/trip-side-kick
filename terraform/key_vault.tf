resource "azurerm_key_vault" "kv" {
  name                = local.key_vault_name
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  tenant_id           = data.azuread_client_config.current.tenant_id

  tags = merge(local.tags, { Purpose = "workload-secrets" })

  soft_delete_retention_days = 90
  purge_protection_enabled   = true
  rbac_authorization_enabled = true

  sku_name = "standard"

  # checkov:skip=CKV2_AZURE_32:The off-Azure host uses its fixed public egress allowlist rather than Private Link.
  # checkov:skip=CKV_AZURE_189:The public endpoint is required for off-Azure hydration, restricted by RBAC and host IPv4.
  network_acls {
    bypass         = "AzureServices"
    default_action = "Deny"
    ip_rules       = var.runtime_host_ipv4_cidrs
  }
  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_role_assignment" "deploy_kv_secrets_officer" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azuread_client_config.current.object_id
}
