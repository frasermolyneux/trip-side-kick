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
# requires the server's "master" database to have a diagnostic setting forwarding audit logs to Azure
# Monitor - otherwise no audit events are actually delivered. The category name is "SQLSecurityAuditEvents"
# (see https://learn.microsoft.com/azure/azure-monitor/reference/supported-logs/microsoft-sql-servers-databases-logs
# and https://learn.microsoft.com/azure/azure-sql/database/audit-log-format#log-analytics, which documents
# audit events landing in the AzureDiagnostics table under the SQLSecurityAuditEvents category). "AuditEvent"
# was the legacy classic-diagnostics category name and is rejected by the current API for this target.
resource "azurerm_monitor_diagnostic_setting" "sql_server_audit" {
  name                       = "diag-to-platform-monitoring"
  target_resource_id         = "${azurerm_mssql_server.sql.id}/databases/master"
  log_analytics_workspace_id = local.platform_monitoring_workspace_id

  enabled_log {
    category = "SQLSecurityAuditEvents"
  }
}
