#!/usr/bin/env bash
# Idempotent Microsoft Graph configuration for Entra External ID self-service sign-up.
#
# Runs as a discrete CI step (not a Terraform provisioner) because:
#   1. hashicorp/azuread ~> 3.9 has no resource for authenticationFlowsPolicy,
#      authenticationEventsFlow (the actual self-service sign-up user flow object), or attaching
#      identity providers to a flow - the provider's coverage stops at azuread_application /
#      azuread_service_principal / azuread_application_federated_identity_credential (see
#      terraform/identity.tf).
#   2. The shared terraform-plan-and-apply composite authenticates only the azurerm/azuread
#      Terraform providers via ARM_* OIDC env vars; it never runs `az login`, so a Terraform
#      local-exec provisioner in this stack would have no Graph token to call `az rest` with. This
#      script therefore runs as its own workflow step, after its own `azure/login`, using the same
#      federated workload identity (no secrets - see standards.oidc-and-secrets).
#
# Scope actually covered by the Graph application permissions granted to this workload
# (Policy.ReadWrite.AuthenticationFlows, IdentityProvider.ReadWrite.All):
#   - Enable "self-service sign-up via user flows" tenant-wide (authenticationFlowsPolicy).
#   - Confirm the built-in Email OTP and Microsoft account identity providers are present (both are
#     zero-configuration, on-by-default providers for a workforce tenant - see
#     docs/identity-and-access.md and the manual runbook it links to).
#
# NOT covered, and therefore NOT attempted here: creating/updating the actual self-service sign-up
# user flow (a Microsoft Graph `authenticationEventsFlow` of subtype
# `externalUsersSelfServiceSignUpEventsFlow`) and attaching identity providers/the app registration
# to it. Both operations require the `EventListener.ReadWrite.All` Graph application permission,
# which platform-workloads has not granted to this workload (only
# `IdentityUserFlow.ReadWrite.All` is granted, and that permission covers the older, B2C-only
# `b2cUserFlows` API - a different Graph resource this workload does not use). See the manual
# runbook in docs/identity-and-access.md for the precise portal steps, and the commented Graph
# payload below for what to automate here once/if that permission is added.
set -euo pipefail

if ! command -v az >/dev/null 2>&1; then
  echo "az CLI is required" >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required (used to parse Graph JSON responses without adding a jq dependency)" >&2
  exit 1
fi

echo "Enabling tenant-wide self-service sign-up (authenticationFlowsPolicy)..."

current_policy=$(az rest --method GET --uri "https://graph.microsoft.com/v1.0/policies/authenticationFlowsPolicy")
already_enabled=$(echo "$current_policy" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("selfServiceSignUp", {}).get("isEnabled", False))')

if [ "$already_enabled" = "True" ]; then
  echo "selfServiceSignUp.isEnabled is already true - nothing to do."
else
  az rest --method PATCH \
    --uri "https://graph.microsoft.com/v1.0/policies/authenticationFlowsPolicy" \
    --headers "Content-Type=application/json" \
    --body '{"selfServiceSignUp": {"isEnabled": true}}'
  echo "selfServiceSignUp.isEnabled set to true."
fi

echo "Checking built-in identity providers (Email OTP, Microsoft account)..."
providers=$(az rest --method GET --uri "https://graph.microsoft.com/v1.0/identity/identityProviders")
echo "$providers" | python3 -c '
import json, sys
data = json.load(sys.stdin)
ids = {p.get("identityProviderType") or p.get("type") for p in data.get("value", [])}
for expected in ("EmailOTP", "MicrosoftAccount"):
    status = "present" if expected in ids else "NOT FOUND (verify tenant settings - see docs/identity-and-access.md)"
    print(f"  {expected}: {status}")
'

cat <<'EOF'

Remaining manual step (needs EventListener.ReadWrite.All, not granted to this workload - see
docs/identity-and-access.md "Manual admin runbook"):

  Entra admin center > External Identities > User flows > New user flow
    - Identity providers: Email One-Time Passcode, Microsoft Account
    - Applications: attach the app registration from `terraform output identity_app_client_id`

Equivalent Graph payload for future automation once the permission gap is closed:

  POST https://graph.microsoft.com/v1.0/identity/authenticationEventsFlows
  {
    "@odata.type": "#microsoft.graph.externalUsersSelfServiceSignUpEventsFlow",
    "displayName": "trip-side-kick-<environment>-sign-up",
    "onInteractiveAuthFlowStart": {
      "@odata.type": "#microsoft.graph.onInteractiveAuthFlowStartExternalUsersSelfServiceSignUp",
      "isSignUpAllowed": true
    },
    "onAuthenticationMethodLoadStart": {
      "@odata.type": "#microsoft.graph.onAuthenticationMethodLoadStartExternalUsersSelfServiceSignUp",
      "identityProviders": [
        { "id": "EmailOTP-OAUTH" },
        { "id": "MicrosoftAccount-OAUTH" }
      ]
    }
  }

  Then associate the app registration with the flow:
  POST https://graph.microsoft.com/v1.0/identity/authenticationEventsFlows/{flowId}/applications/$ref
  { "@odata.id": "https://graph.microsoft.com/v1.0/applications/{app-object-id}" }
EOF
