# Identity and Access

## Status: STUBBED

This walking skeleton has **no authentication**. Nothing signs in, no authentication scheme is
registered, and no Entra resources are provisioned. Everything below the "Target model" heading is
the plan for the identity slice; everything under "What exists today" is the deliberately inert
scaffolding that slice will replace.

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
| Secrets | **None.** Federated credentials / managed identity only, per `standards.oidc-and-secrets` |

Authorisation is per-trip: a trip has an owner and a set of collaborators. `ICurrentUser.UserId` is
the stable object identifier that trip membership is keyed on. Never key membership on email
address — it is mutable and it is PII.

## Surface implications

Only the **app** surface (`tripsidekick.app`, `dev.tripsidekick.app`) will ever authenticate. The
brochure surface (`tripsidekick.net`) stays fully anonymous and cacheable — the sign-in link there is
an outbound link to the app host, not an auth-protected route. The host-routing split described in
[Architecture Overview](architecture-overview.md) is what keeps that boundary honest.

## What exists today (the stubs)

| Location | Stub |
| --- | --- |
| `src/MX.TripSideKick.Application/Abstractions/ICurrentUser.cs` | The abstraction application services already depend on: `UserId`, `IsAuthenticated`. Marked `IDENTITY STUB` |
| `src/MX.TripSideKick.Web/Hosting/AnonymousCurrentUser.cs` | The only implementation. Always returns `IsAuthenticated = false` and a null `UserId`. Carries the `TODO (identity slice)` marker |
| `src/MX.TripSideKick.Web/Program.cs` (~line 39) | `IDENTITY STUB` comment marking exactly where `AddAuthentication().AddMicrosoftIdentityWebApp(...)` and `UseAuthentication()/UseAuthorization()` slot in |
| `src/MX.TripSideKick.Web/Program.cs` (~line 53) | Cookie policy already configured secure/same-origin so the identity slice does not have to revisit it |
| `src/MX.TripSideKick.Web/Controllers/V1/StatusController.cs` | Reports the anonymous identity so the split is visible end-to-end; `TODO (identity slice)` |
| `src/MX.TripSideKick.Web/Controllers/V1/ClientConfigController.cs` | Returns `SignInEnabled: false`. The identity slice flips this and adds the authority/client id |
| `src/MX.TripSideKick.Web/ClientApp/src/App.tsx` | Renders the "signed-out placeholder" panel; `IDENTITY STUB` comment marks the block to replace |
| `terraform/key_vault.tf` (~line 33) | `TODO(identity-slice)` — where External ID signing material would live if any is ever needed |
| `terraform/web_app.tf` (~line 47) | `TODO(identity-slice)` — where the External ID authority, client id and user-flow app settings go |

Antiforgery, the secure cookie policy, HTTPS redirection, HSTS, the CSP and the Data Protection key
ring are **already wired** — they are prerequisites for cookie auth, not part of it, so the identity
slice inherits a correct baseline rather than retrofitting one.

## What the identity slice must do

1. Provision the Entra External ID tenant resources (app registration, self-service sign-up user
   flow, email OTP + Microsoft account providers). **This needs Graph permissions that the workload
   identity does not currently hold** — granting them is an admin action outside this repo.
2. Add `Microsoft.Identity.Web` to `MX.TripSideKick.Web`, register the OIDC + cookie schemes at the
   marked point in `Program.cs`, and add `UseAuthentication()`/`UseAuthorization()` between
   `UseRouting()` and endpoint mapping.
3. Add `/v1/auth/login`, `/v1/auth/logout` and `/v1/auth/me` BFF endpoints on the app hosts only.
4. Replace `AnonymousCurrentUser` with a claims-backed `HttpContextCurrentUser`.
5. Flip `ClientConfigResponse.SignInEnabled` to true and replace the SPA placeholder with the real
   signed-in/signed-out shell.
6. Add the External ID app settings in `terraform/web_app.tf` and remove the two
   `TODO(identity-slice)` markers.
7. Extend `HostRoutingTests` to prove auth endpoints are unreachable on the brochure host.

## Non-negotiables

* **No client secrets, ever.** Federated credentials only.
* **No tokens in the browser** — no access tokens in `localStorage`, `sessionStorage` or JS-readable
  cookies.
* **No PII in telemetry** — never log email addresses, display names, trip content or booking
  references. `ICurrentUser.UserId` (an opaque object id) is the only identity value safe to attach
  to a trace.
