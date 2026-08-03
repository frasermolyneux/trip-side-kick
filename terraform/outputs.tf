output "resource_group_name" {
  value = data.azurerm_resource_group.rg.name
}

output "web_app_name" {
  value = azurerm_linux_web_app.app.name
}

output "web_app_resource_group_name" {
  value = azurerm_linux_web_app.app.resource_group_name
}

output "web_app_default_hostname" {
  value = azurerm_linux_web_app.app.default_hostname
}

output "site_hostnames" {
  description = "Hostnames serving the Razor Pages brochure surface."
  value       = [for domain in local.primary_domains : domain.hostname if domain.surface == "site"]
}

output "app_hostnames" {
  description = "Hostnames serving the React PWA and the versioned API."
  value       = [for domain in local.primary_domains : domain.hostname if domain.surface == "app"]
}

output "application_insights_name" {
  value = azurerm_application_insights.ai.name
}

output "key_vault_name" {
  value = azurerm_key_vault.kv.name
}

output "storage_account_name" {
  value = azurerm_storage_account.data.name
}

output "sql_server_fully_qualified_domain_name" {
  value = azurerm_mssql_server.sql.fully_qualified_domain_name
}

output "sql_database_name" {
  value = azurerm_mssql_database.db.name
}
