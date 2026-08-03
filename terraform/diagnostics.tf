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
