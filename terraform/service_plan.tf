resource "azurerm_service_plan" "dev" {
  count = var.environment == "dev" ? 1 : 0

  name                = "asp-${var.workload}-dev-${var.location}-default"
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "B1"
  tags                = local.tags
}
