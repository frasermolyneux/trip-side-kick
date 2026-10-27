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
  # checkov:skip=CKV_AZURE_109:Development retains legacy App Service access until retirement; Production is default-deny.
  # checkov:skip=CKV_AZURE_189:The public endpoint is required for off-Azure hydration, restricted by RBAC and host IPv4.
  network_acls {
    bypass         = "AzureServices"
    default_action = var.environment == "prd" ? "Deny" : "Allow"
    ip_rules       = var.environment == "prd" ? var.runtime_host_ipv4_cidrs : []
  }
}

resource "azurerm_role_assignment" "deploy_kv_secrets_officer" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azuread_client_config.current.object_id
}

resource "azurerm_role_assignment" "web_app_kv_secrets_user" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.app.identity[0].principal_id
}
