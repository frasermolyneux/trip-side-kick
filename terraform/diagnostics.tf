locals {
  diagnostic_targets = {
    web_app       = azurerm_linux_web_app.app.id
    key_vault     = azurerm_key_vault.kv.id
    sql_database  = azurerm_mssql_database.db.id
    storage_blobs = "${azurerm_storage_account.data.id}/blobServices/default"
  }
}

resource "azurerm_monitor_diagnostic_setting" "workload" {
  for_each = local.diagnostic_targets

  name                       = "diag-to-platform-monitoring"
  target_resource_id         = each.value
  log_analytics_workspace_id = local.platform_monitoring_workspace_id

  enabled_log {
    category_group = "allLogs"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}

# azurerm_mssql_server_extended_auditing_policy.sql (sql.tf) uses log_monitoring_enabled = true, which
# per the azurerm provider docs requires the server's "master" database to have a diagnostic setting
# forwarding AuditEvent logs to Azure Monitor - otherwise no audit events are actually delivered.
resource "azurerm_monitor_diagnostic_setting" "sql_server_audit" {
  name                       = "diag-to-platform-monitoring"
  target_resource_id         = "${azurerm_mssql_server.sql.id}/databases/master"
  log_analytics_workspace_id = local.platform_monitoring_workspace_id

  enabled_log {
    category = "AuditEvent"
  }
}
