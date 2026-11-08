#!/usr/bin/env bash
# Legacy Graph helper, not invoked by current deployment workflows. It can change
# the tenant-wide self-service sign-up policy; do not run without separate review
# and authorization. It does not create or attach the user flow. See
# docs/identity-and-access.md for current verification guidance.
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
