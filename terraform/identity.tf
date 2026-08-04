# Entra External ID (B2B collaboration + self-service sign-up) for the app surface.
#
# This is a *customer*-facing app registration - distinct from the workload service principal that
# platform-workloads provisions for Terraform/CI. It lives in the same Molyneux.IO workforce tenant
# (docs/identity-and-access.md) because "External ID in a workforce tenant" keeps B2B guests and
# self-service sign-up consumers as directory objects in this tenant rather than a separate CIAM
# tenant - no second tenant, no cross-tenant app registration, no separate authority host.
#
# Credential: NO client secret and NO certificate. The federated identity credential below lets the
# App Service's system-assigned managed identity mint the client assertion Microsoft.Identity.Web
# needs for the authorization-code exchange (ClientCredentials SourceType =
# SignedAssertionFromManagedIdentity, see Program.cs). This satisfies standards.oidc-and-secrets:
# federated credentials only, never a stored secret.
resource "azuread_application" "app_sign_in" {
  display_name     = "${var.workload}-${var.environment}-app-sign-in"
  sign_in_audience = "AzureADMyOrg"

  owners = [data.azuread_client_config.current.object_id]

  web {
    redirect_uris = local.identity_redirect_uris
    logout_url    = local.identity_logout_url

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = true
    }
  }

  tags = ["trip-side-kick", var.environment]
}

# Enterprise application object for the registration above. Not strictly required for the OIDC
# authorization-code flow itself, but it is what the self-service sign-up user flow (configured via
# Graph in identity_graph_automation.tf) associates itself with, and it is what shows up under
# Entra ID > Enterprise applications for the tenant admin to inspect/troubleshoot sign-ins.
resource "azuread_service_principal" "app_sign_in" {
  client_id = azuread_application.app_sign_in.client_id
  owners    = [data.azuread_client_config.current.object_id]
}

# Federated identity credential: trusts the App Service's system-assigned managed identity as a
# workload identity, so it can request a signed assertion from Microsoft Entra ID instead of a
# stored secret/certificate being needed by Microsoft.Identity.Web's confidential-client credential.
# See https://learn.microsoft.com/entra/workload-id/workload-identity-federation-config-app-trust-managed-identity.
resource "azuread_application_federated_identity_credential" "app_sign_in_managed_identity" {
  application_id = azuread_application.app_sign_in.id
  display_name   = "web-app-managed-identity"
  description    = "Lets the trip-side-kick App Service's system-assigned managed identity authenticate as this app's confidential client - no secret, no certificate."

  audiences = ["api://AzureADTokenExchange"]
  issuer    = "https://login.microsoftonline.com/${data.azuread_client_config.current.tenant_id}/v2.0"
  subject   = azurerm_linux_web_app.app.identity[0].principal_id
}
