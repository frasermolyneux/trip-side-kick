# Identity and Access

## Status: IMPLEMENTED (dev), pending one-time tenant admin actions

Real sign-in is wired end-to-end for the **app** surface: Entra External ID (B2B collaboration +
self-service sign-up) app registration provisioned via Terraform, an ASP.NET Core BFF that
terminates OpenID Connect authorization-code + PKCE server-side, and a React shell that talks only
to the BFF's `/v1/auth/*` endpoints. **The dev deploy will not work until the tenant admin performs
the one-time actions in [Manual admin runbook](#manual-admin-runbook) below** — several of them
require directory-admin privileges no workload identity holds, by design (`standards.oidc-and-secrets`,
`tenant.identity`).

## Target model

| Concern | Decision |
| --- | --- |
| Home tenant | **Molyneux.IO** workforce tenant (`e56a6947-bb9a-4a6e-846a-1f118d1c3a14`) — the same tenant the platform workloads live in |
| Customer identity | **Microsoft Entra External ID** with **B2B collaboration + self-service sign-up** |
| MVP sign-in methods | **Email one-time passcode** and **personal Microsoft accounts** |
| Later | Google as an additional federated provider once the sign-up user flow is proven |
| Protocol | OpenID Connect authorization code + PKCE, terminated **server-side** in the BFF |
| Token handling | Tokens never reach the browser. The SPA authenticates with the same-origin session cookie; the BFF holds tokens |
| Cookie | `__Host-` prefixed, `Secure`, `HttpOnly`, `SameSite=Lax`, backed by the blob-persisted Data Protection key ring |
| Secrets | **None.** The App Service's system-assigned managed identity is a federated identity credential on the app registration (Microsoft.Identity.Web `ClientCredentials` `SourceType=SignedAssertionFromManagedIdentity`) |

Authorisation is per-trip: a trip has an owner and a set of collaborators. `ICurrentUser.SubjectId` is
the stable object identifier that trip membership is keyed on. Never key membership on email
address — it is mutable and it is PII.

## Surface implications

Only the **app** surface (`tripsidekick.app`, `dev.tripsidekick.app`) authenticates. The brochure
surface (`tripsidekick.net`) stays fully anonymous and cacheable — the sign-in link there is an
outbound link to the app host, not an auth-protected route. `/v1/auth/*` is mapped by the same
`MapControllers().RequireHost(appHosts)` call as every other v1 controller in `Program.cs`, so it is
unreachable (404) on the brochure host; see `HostRoutingTests`.

## What was built

| Area | What |
| --- | --- |
| `terraform/identity.tf` | `azuread_application.app_sign_in` (confidential web client, redirect URIs + front-channel logout URL), `azuread_service_principal.app_sign_in`, `azuread_application_federated_identity_credential.app_sign_in_managed_identity` (trusts the App Service's system-assigned managed identity — no secret, no certificate) |
| `terraform/locals.tf` | `identity_app_hostnames`, `identity_redirect_uris` (per-environment app hostnames + the default `azurewebsites.net` hostname + `https://localhost:7207/signin-oidc` for dev), `identity_logout_url` |
| `terraform/web_app.tf` | `AzureAd__Instance/TenantId/ClientId/CallbackPath/SignedOutCallbackPath/ClientCredentials__0__SourceType` app settings — authority/client id/user-flow, never a secret |
| `terraform/outputs.tf` | `identity_app_client_id`, `identity_app_object_id`, `identity_tenant_id` |
| `terraform/scripts/configure-external-id-sign-up.sh` | Idempotent Graph automation for what's covered by the granted permissions (see below); documents the remaining manual step |
| `.github/workflows/deploy-dev.yml`, `deploy-prd.yml` | Runs the script above as its own step (own `azure/login`, no new secrets) after the Terraform apply, because the shared `terraform-plan-and-apply` composite only authenticates the Terraform providers, not the Azure CLI |
| `src/MX.TripSideKick.Web/Program.cs` | `AddAuthentication().AddMicrosoftIdentityWebApp(...).EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches()` (forces the authorization-code + PKCE response type so the token endpoint - and the managed-identity credential - is actually exercised; the default sign-in-only wiring resolves to an implicit `id_token`-only flow that never redeems a code), cookie (`__Host-tsk-auth`) and OIDC (`SaveTokens=false`) options, `UseAuthentication()`/`UseAuthorization()` between `UseRouting()`/the rate limiter and endpoint mapping. `CookiePolicyOptions.MinimumSameSitePolicy` is deliberately left unset so the OIDC handler's own `SameSite=None` nonce/correlation cookies survive the identity provider's cross-site POST back to `/signin-oidc` |
| `src/MX.TripSideKick.Web/Controllers/V1/AuthController.cs` | `/v1/auth/login`, `/v1/auth/logout`, `/v1/auth/me` — open-redirect-safe, never returns a token |
| `src/MX.TripSideKick.Web/Hosting/HttpContextCurrentUser.cs` | Claims-backed `ICurrentUser`: `SubjectId` = `oid`, `DisplayName` = `name` claim (PII, never logged) |
| `src/MX.TripSideKick.Web/Controllers/V1/ClientConfigController.cs` | `SignInEnabled: true`, plus `LoginUrl`/`LogoutUrl` — no client id, authority or token ever exposed to the SPA |
| `src/MX.TripSideKick.Web/Controllers/V1/StatusController.cs` | Reports the real authenticated state |
| `src/MX.TripSideKick.Web/ClientApp/src/App.tsx` | Real signed-in/signed-out shell: Sign in → `/v1/auth/login`, signed-in view shows `DisplayName` from `/v1/auth/me`, Sign out → `/v1/auth/logout` |

### Entra object provisioning mechanism (per object)

| Entra object | Mechanism | Why |
| --- | --- | --- |
| App registration (`azuread_application`) | Native `azuread` Terraform resource | Fully supported |
| Service principal | Native `azuread` Terraform resource | Fully supported |
| Federated identity credential (managed-identity trust) | Native `azuread` Terraform resource | Fully supported; confirmed via provider docs that `subject` accepts a system-assigned managed identity's principal id, not only user-assigned |
| `authenticationFlowsPolicy.selfServiceSignUp` (tenant-wide toggle) | `az rest` in `configure-external-id-sign-up.sh`, run as its own CI step under the workload's federated identity | `azuread` ~3.9 has no resource for this policy; the granted `Policy.ReadWrite.AuthenticationFlows` permission covers the Graph call directly |
| Email OTP / Microsoft account identity providers | **No provisioning needed** — both are built-in, zero-configuration, on-by-default providers for a workforce tenant. The script only verifies their presence via `GET /identity/identityProviders` (covered by `IdentityProvider.ReadWrite.All`) | Nothing to create |
| Self-service sign-up user flow (`authenticationEventsFlow`, subtype `externalUsersSelfServiceSignUpEventsFlow`) attaching the providers + the app registration | **Documented manual step** (see runbook) | Requires Graph permission `EventListener.ReadWrite.All`, which the workload does **not** hold. `IdentityUserFlow.ReadWrite.All` (granted) only covers the legacy, B2C-only `b2cUserFlows` API — a different Graph resource this workload does not use. The `azuread` provider also has no resource for this object. The exact Graph payload to automate this once the permission gap is closed is documented as a comment in the script |

## Manual admin runbook

These are one-time actions only the Molyneux.IO tenant admin can perform. **Do them before the first
`deploy-dev` run** — without them, sign-in will fail even though Terraform applies cleanly.

1. **Grant admin consent for the workload's Graph application permissions.**
   - Portal: Entra admin center → **App registrations** → the trip-side-kick workload service
     principal (the one platform-workloads created, not `app-sign-in` from this slice) → **API
     permissions** → **Grant admin consent for Molyneux.IO**.
   - Verify: the same page should show a green check against `Application.ReadWrite.OwnedBy`,
     `Policy.ReadWrite.AuthenticationFlows`, `IdentityUserFlow.ReadWrite.All`,
     `IdentityProvider.ReadWrite.All` with status "Granted for Molyneux.IO".
   - Without this, `configure-external-id-sign-up.sh`'s `az rest` calls return `403 Authorization_RequestDenied`.

2. **Confirm external collaboration / self-service sign-up is not blocked tenant-wide.**
   - Portal: Entra admin center → **External Identities** → **External collaboration settings**.
     Confirm "Guest invite restrictions" and "Collaboration restrictions" do not deny the domains
     consumer email OTP / MSA sign-ins will present (typically no domain restriction is needed for
     MVP — email OTP and MSA sign-ups are not tied to a verified domain).
   - `configure-external-id-sign-up.sh` sets `policies/authenticationFlowsPolicy` →
     `selfServiceSignUp.isEnabled = true` automatically on first `deploy-dev` run — verify it stuck
     via `az rest --method GET --uri https://graph.microsoft.com/v1.0/policies/authenticationFlowsPolicy`
     (should show `"selfServiceSignUp": { "isEnabled": true }`).

3. **Rule out Conditional Access / Security Defaults blocking consumer sign-ups.**
   - Portal: Entra admin center → **Protection** → **Conditional Access** → **Policies**. If Security
     Defaults is enabled (Properties → Manage Security Defaults), it enforces MFA for all users,
     which self-service sign-up consumers cannot satisfy the same way workforce members can. Either
     keep Security Defaults off (if already off for this tenant) or add a Conditional Access
     exclusion / dedicated policy for the guest/consumer user type used by External ID self-service
     sign-up.
   - Verify: attempt the manual sign-up flow (step 4) succeeds without an unexpected MFA challenge
     that a consumer identity cannot complete.

4. **Create the self-service sign-up user flow** (blocked from automation — see the mechanism table
   above).
   - Portal: Entra admin center → **External Identities** → **User flows** → **New user flow**.
   - Identity providers: **Email One-Time Passcode**, **Microsoft Account**.
   - User attributes: defaults are fine for MVP (email address collected automatically).
   - Applications: attach the app registration whose client id is `terraform output identity_app_client_id`
     (run from `terraform/` after `deploy-dev` applies) for the relevant environment.
   - Verify: the user flow's **Applications** blade lists the app registration; **Identity providers**
     lists both Email OTP and Microsoft account as enabled.

5. **First sign-in smoke test** (after the app is deployed): browse to
   `https://dev.tripsidekick.app`, click **Sign in**, and complete an email OTP sign-up followed
   (separately) by a personal Microsoft account sign-in. Confirm `GET /v1/auth/me` reflects
   `isAuthenticated: true` and a display name for each.

## Non-negotiables

* **No client secrets, ever.** The federated identity credential trusting the App Service's
  system-assigned managed identity is the only credential; Key Vault-backed certificate remains
  available as a documented fallback (`terraform/key_vault.tf`) but was not needed.
* **No tokens in the browser** — no access tokens in `localStorage`, `sessionStorage` or JS-readable
  cookies. `Microsoft.Identity.Web`'s `OpenIdConnectOptions.SaveTokens` is `false`.
* **No PII in telemetry** — never log email addresses, trip content or booking references.
  `ICurrentUser.SubjectId` (an opaque object id) is the only identity value safe to attach to a
  trace; `ICurrentUser.DisplayName` is PII and must never reach telemetry or logs.

## Residual risks / follow-ups

* **`EventListener.ReadWrite.All` permission gap.** Automating the user-flow creation itself (rather
  than just the tenant-wide policy toggle) needs this permission granted to the workload service
  principal plus a corresponding admin-consent grant. Until then, step 4 of the runbook above is a
  recurring manual action for any *new* environment (dev already done covers dev; prd will need its
  own user flow attached to the prd app registration).
* **prd-specific steps.** This slice targets dev end-to-end; `identity.tf`/`locals.tf` are already
  parameterised per `var.environment`, so a prd app registration + federated credential will be
  created automatically on a prd apply, but the manual runbook (steps 2–4) needs repeating once for
  prd's own user flow and Conditional Access scope.
* **Google deferral.** Explicitly out of scope for MVP; would be a second `identityProviders` entry
  (this one *does* need real provisioning, unlike Email OTP/MSA) plus attaching it to the user flow —
  can reuse the same manual-step pattern until the permission gap is closed.
* **Guest vs. self-service-sign-up nuance.** B2B guests (directly invited corporate collaborators)
  and self-service sign-up consumers both land as guest objects in the directory but follow different
  invitation paths; this slice only wires the self-service sign-up path. Direct B2B invites are a
  follow-up for whichever slice needs to invite a specific collaborator by email.
* **Trip-membership authorization hook-in.** The trips-core slice should authorize collaborator
  access by comparing `ICurrentUser.SubjectId` against a trip's owner/collaborator object ids — never
  by email.

