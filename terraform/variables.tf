variable "workload" {
  description = "Workload name; matches the platform-workloads workload definition."
  type        = string
  default     = "trip-side-kick"
}

variable "environment" {
  description = "Environment short name (dev or prd)."
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region for workload resources."
  type        = string
  default     = "swedencentral"
}

variable "subscription_id" {
  description = "Subscription that hosts the workload resource group."
  type        = string
}

variable "platform_monitoring_state" {
  description = "Backend config for platform-monitoring remote state (shared Log Analytics workspace)."
  type = object({
    resource_group_name  = string
    storage_account_name = string
    container_name       = string
    key                  = string
    subscription_id      = string
    tenant_id            = string
  })
}

variable "platform_hosting_state" {
  description = "Backend config for platform-hosting remote state (shared Linux App Service plan)."
  type = object({
    resource_group_name  = string
    storage_account_name = string
    container_name       = string
    key                  = string
    subscription_id      = string
    tenant_id            = string
    use_oidc             = bool
  })
}

variable "cloudflare_api_token" {
  description = "Scoped Cloudflare API token (Zone:Read + DNS:Edit on the tripsidekick zones)."
  type        = string
  sensitive   = true
}

variable "custom_domains" {
  description = <<-EOT
    Public hostnames bound to the App Service, keyed by a stable identifier.

      surface  - "site" for the brochure surface, "app" for the PWA/API surface.
      hostname - fully qualified hostname to bind.
      zone     - Cloudflare zone that owns the record.
      redirect - true for www aliases that only exist to redirect to the apex host.
  EOT

  type = map(object({
    surface  = string
    hostname = string
    zone     = string
    redirect = optional(bool, false)
  }))

  validation {
    condition     = alltrue([for domain in var.custom_domains : contains(["site", "app"], domain.surface)])
    error_message = "custom_domains[*].surface must be either \"site\" or \"app\"."
  }

  validation {
    condition     = alltrue([for domain in var.custom_domains : endswith(domain.hostname, domain.zone)])
    error_message = "custom_domains[*].hostname must belong to the declared Cloudflare zone."
  }
}

variable "sql_database" {
  description = <<-EOT
    Azure SQL database sizing. Serverless (GP_S_*) SKUs auto-pause when idle, which keeps a
    low-traffic personal project close to storage-only cost. See docs/infrastructure-and-cost.md.
  EOT

  type = object({
    sku_name                    = string
    max_size_gb                 = number
    min_capacity                = optional(number)
    auto_pause_delay_in_minutes = optional(number)
    zone_redundant              = optional(bool, false)
    storage_account_type        = optional(string, "Local")
  })

  default = {
    sku_name                    = "GP_S_Gen5_1"
    max_size_gb                 = 32
    min_capacity                = 0.5
    auto_pause_delay_in_minutes = 60
  }
}

variable "app_insights_sampling_percentage" {
  description = "Application Insights ingestion sampling percentage."
  type        = number
  default     = 25
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
  default     = {}
}
