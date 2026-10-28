# Retain the existing workforce/B2B registration and its separately rotated certificates.
resource "azuread_application" "app_sign_in" {
  display_name     = "${var.workload}-${var.environment}-app-sign-in"
  sign_in_audience = "AzureADMyOrg"

  owners = [data.azuread_client_config.current.object_id]

  web {
    redirect_uris = local.identity_redirect_uris
    logout_url    = local.identity_logout_url

    # Authorization-code + PKCE only (Program.cs forces ResponseType=code via
    # EnableTokenAcquisitionToCallDownstreamApi()) - the implicit grant is unnecessary for a
    # confidential-client BFF and left disabled so no client can request an id_token/access_token
    # via the implicit response types even if misconfigured.
    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }

  tags = ["trip-side-kick", var.environment]
  lifecycle {
    prevent_destroy = true
  }
}

resource "azuread_service_principal" "app_sign_in" {
  client_id = azuread_application.app_sign_in.client_id
  owners    = [data.azuread_client_config.current.object_id]
  lifecycle {
    prevent_destroy = true
  }
}
