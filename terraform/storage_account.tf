resource "azurerm_storage_account" "data" {
  name                = local.storage_account_name
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location

  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"

  min_tls_version                   = "TLS1_2"
  https_traffic_only_enabled        = true
  allow_nested_items_to_be_public   = false
  public_network_access_enabled     = true
  shared_access_key_enabled         = false
  default_to_oauth_authentication   = true
  infrastructure_encryption_enabled = true

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    container_delete_retention_policy {
      days = 7
    }
  }

  tags = local.tags
}

# The deployment identity needs data-plane access before it can create containers, because shared
# keys are disabled on the account.
resource "azurerm_role_assignment" "deploy_storage_blob_data_contributor" {
  scope                = azurerm_storage_account.data.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = data.azuread_client_config.current.object_id
}

resource "time_sleep" "wait_for_storage_rbac" {
  create_duration = "60s"

  depends_on = [
    azurerm_role_assignment.deploy_storage_blob_data_contributor
  ]
}

resource "azurerm_storage_container" "containers" {
  for_each = local.blob_container_names

  name                  = each.value
  storage_account_id    = azurerm_storage_account.data.id
  container_access_type = "private"

  depends_on = [
    time_sleep.wait_for_storage_rbac
  ]
}

resource "azurerm_role_assignment" "web_app_storage_blob_data_contributor" {
  scope                = azurerm_storage_account.data.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.app.identity[0].principal_id
}
