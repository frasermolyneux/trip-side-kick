resource "azurerm_mssql_server" "sql" {
  name                = local.sql_server_name
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  version             = "12.0"

  minimum_tls_version = "1.2"
  # checkov:skip=CKV_AZURE_113:No VNet/private endpoint in this slice (docs/infrastructure-and-cost.md);
  # the App Service reaches the database over the Azure backbone via the "allow Azure services" rule below.
  # checkov:skip=CKV2_AZURE_45:Same reasoning as CKV_AZURE_113 - no VNet/Private Link in this slice.
  # checkov:skip=CKV2_AZURE_2:SQL Vulnerability Assessment needs a storage account key/SAS, which
  # conflicts with `shared_access_key_enabled = false` on the storage account (storage_account.tf);
  # server-level auditing goes to Log Analytics instead (see azurerm_mssql_server_extended_auditing_policy).
  # checkov:skip=CKV_AZURE_24:Retention is governed by the shared Log Analytics workspace's retention
  # policy (platform-monitoring), not a per-resource blob storage container - storage-based auditing
  # would need a shared storage account key, which conflicts with `shared_access_key_enabled = false`.
  public_network_access_enabled = true

  # No SQL logins exist: Entra ID is the only authentication path.
  azuread_administrator {
    login_username              = "sp-${var.workload}-${var.environment}"
    object_id                   = data.azuread_client_config.current.object_id
    tenant_id                   = data.azuread_client_config.current.tenant_id
    azuread_authentication_only = true
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.tags
}

# The App Service runs on the shared plan without VNet integration, so the database is reached over
# the Azure backbone using the "allow Azure services" pseudo-rule. Tighten this to Private Link when
# the workload justifies its own networking.
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  # checkov:skip=CKV2_AZURE_34:Deliberate "AllowAllAzureServices" rule - see comment above and
  # docs/infrastructure-and-cost.md; there is no VNet/Private Link in this slice to scope it further.
  name             = "AllowAllAzureServices"
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Server-level auditing to Log Analytics. Storage-based auditing (and the SQL Vulnerability Assessment
# below) would need a shared storage account key/SAS, which conflicts with the storage account's
# `shared_access_key_enabled = false` posture (docs/infrastructure-and-cost.md) - so audit events go
# straight to the shared Log Analytics workspace instead of blob storage.
resource "azurerm_mssql_server_extended_auditing_policy" "sql" {
  server_id              = azurerm_mssql_server.sql.id
  log_monitoring_enabled = true
}

resource "azurerm_mssql_database" "db" {
  name      = local.sql_database_name
  server_id = azurerm_mssql_server.sql.id

  collation   = "SQL_Latin1_General_CP1_CI_AS"
  sku_name    = var.sql_database.sku_name
  max_size_gb = var.sql_database.max_size_gb

  min_capacity                = local.is_serverless_sql ? var.sql_database.min_capacity : null
  auto_pause_delay_in_minutes = local.is_serverless_sql ? var.sql_database.auto_pause_delay_in_minutes : null

  # checkov:skip=CKV_AZURE_229:Zone redundancy adds cost that isn't justified for this MVP tier
  # (docs/infrastructure-and-cost.md documents `zone_redundant = false` as a deliberate default).
  # checkov:skip=CKV_AZURE_224:The Ledger feature (cryptographic proof of data integrity) isn't needed
  # for this workload's data classification; revisit if a compliance requirement emerges.
  zone_redundant       = var.sql_database.zone_redundant
  storage_account_type = var.sql_database.storage_account_type

  tags = local.tags
}

# The contained database user for the App Service's data-access managed identity
# (a dedicated user-assigned identity - managed_identity.tf; CREATE USER [<identity name>] WITH SID =
# <client_id-bytes>, TYPE = E, granted EXACTLY db_datareader + db_datawriter) is created by a CI step,
# not Terraform - it is a T-SQL operation and this stack never opens a SQL connection. Creating it by
# SID (rather than FROM EXTERNAL PROVIDER) means the server identity never needs the Entra "Directory
# Readers" role. Migrations are applied the same way, under the workload service principal (the SQL
# AAD admin above), never by the low-privilege runtime identity. See
# terraform/scripts/configure-sql-data-access.ps1, docs/data-and-persistence.md, and the
# "Configure SQL data access" step in .github/workflows/deploy-dev.yml / deploy-prd.yml.
