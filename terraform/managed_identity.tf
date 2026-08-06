# User-assigned managed identity the App Service uses for Azure SQL data access.
#
# Why a user-assigned identity (rather than the App Service's system-assigned identity) for SQL:
# the contained database user has to be created as a discrete T-SQL step (this stack never opens a
# SQL connection). Creating it for a *system-assigned* identity would require either
# `CREATE USER ... FROM EXTERNAL PROVIDER` - which needs the SQL server's own identity to hold the
# Entra "Directory Readers" role (not granted to, and not self-grantable by, this workload) - or a
# Microsoft Graph lookup of the identity's appId (a Graph permission the workload service principal
# does not hold). A user-assigned identity exposes its `client_id` directly from Terraform, so the
# contained user can be created deterministically by SID
# (`CREATE USER [name] WITH SID = <client_id-bytes>, TYPE = E`) with no directory lookup and no extra
# Entra role. See terraform/scripts/configure-sql-data-access.ps1 and docs/data-and-persistence.md.
#
# The App Service keeps its system-assigned identity as well (see web_app.tf) - that one backs the
# Entra External ID sign-in federated credential (identity.tf) and the Key Vault / Blob Storage RBAC.
resource "azurerm_user_assigned_identity" "sql_data" {
  name                = local.sql_data_identity_name
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location
  tags                = local.tags
}
