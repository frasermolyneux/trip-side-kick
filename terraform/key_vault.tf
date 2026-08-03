resource "azurerm_key_vault" "kv" {
  name                = local.key_vault_name
  location            = data.azurerm_resource_group.rg.location
  resource_group_name = data.azurerm_resource_group.rg.name
  tenant_id           = data.azuread_client_config.current.tenant_id

  tags = local.tags

  soft_delete_retention_days = 90
  purge_protection_enabled   = true
  rbac_authorization_enabled = true

  sku_name = "standard"

  # checkov:skip=CKV2_AZURE_32:No VNet/private endpoint in this slice (docs/infrastructure-and-cost.md).
  # checkov:skip=CKV_AZURE_109:No VNet in this slice (docs/infrastructure-and-cost.md); the web app
  # is not VNet-integrated, so a "Deny" default with only trusted-service bypass would also block the
  # app's own secret reads. Tighten alongside Private Link when the workload justifies its own networking.
  # checkov:skip=CKV_AZURE_189:Same reasoning as CKV_AZURE_109 - disabling public network access requires
  # a private endpoint, which is out of scope for this MVP (docs/infrastructure-and-cost.md).
  network_acls {
    bypass         = "AzureServices"
    default_action = "Allow"
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

# TODO(identity-slice): the Entra External ID application registration and any signing material it
# needs will be provisioned in the identity slice. No client secrets are stored here - the app uses
# its managed identity, and federated credentials are used for anything that needs an assertion.
