terraform {
  required_version = ">= 1.15.6"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.1.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.9.0"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.23.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.7"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.14.0"
    }
  }

  backend "azurerm" {}
}

provider "azurerm" {
  subscription_id = var.subscription_id

  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }

    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }

  storage_use_azuread = true
}

provider "azuread" {}

# DNS for tripsidekick.app / tripsidekick.net is Cloudflare, not Azure DNS. The scoped API token is
# injected by the workflow as TF_VAR_cloudflare_api_token from the GitHub environment secret
# CLOUDFLARE_API_KEY.
provider "cloudflare" {
  api_token = var.cloudflare_api_token
}
