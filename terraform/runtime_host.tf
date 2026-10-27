variable "runtime_host_principal_id" {
  description = "Azure Arc principal allowed to hydrate Production runtime secrets."
  type        = string
  default     = null

  validation {
    condition     = var.environment != "prd" || can(regex("^[0-9a-fA-F-]{36}$", var.runtime_host_principal_id))
    error_message = "Production requires the approved runtime host principal ID."
  }
}

variable "runtime_host_ipv4_cidrs" {
  description = "Approved fixed IPv4 egress ranges for Production vault hydration."
  type        = list(string)
  default     = []

  validation {
    condition = var.environment != "prd" || (
      length(var.runtime_host_ipv4_cidrs) > 0 &&
      alltrue([for cidr in var.runtime_host_ipv4_cidrs : can(cidrnetmask(cidr)) && endswith(cidr, "/32")])
    )
    error_message = "Production requires explicit, individual IPv4 host addresses."
  }
}

resource "azurerm_role_assignment" "runtime_host_secrets_user" {
  count = var.environment == "prd" ? 1 : 0

  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.runtime_host_principal_id
}
