resource "azurerm_mssql_server" "sql" {
  name                = local.sql_server_name
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  version             = "12.0"

  minimum_tls_version           = "1.2"
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
  name             = "AllowAllAzureServices"
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_database" "db" {
  name      = local.sql_database_name
  server_id = azurerm_mssql_server.sql.id

  collation   = "SQL_Latin1_General_CP1_CI_AS"
  sku_name    = var.sql_database.sku_name
  max_size_gb = var.sql_database.max_size_gb

  min_capacity                = local.is_serverless_sql ? var.sql_database.min_capacity : null
  auto_pause_delay_in_minutes = local.is_serverless_sql ? var.sql_database.auto_pause_delay_in_minutes : null

  zone_redundant       = var.sql_database.zone_redundant
  storage_account_type = var.sql_database.storage_account_type

  tags = local.tags
}

# TODO(data-slice): create the contained database user for the App Service managed identity
# (CREATE USER [<web app name>] FROM EXTERNAL PROVIDER) and grant it db_datareader/db_datawriter.
# That is a T-SQL operation and is intentionally not performed by this walking skeleton, which never
# opens a SQL connection.
