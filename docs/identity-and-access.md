# Identity and access

The app surface uses the retained workforce Microsoft Entra B2B registration.
The BFF performs OpenID Connect authorization-code sign-in with PKCE and a
certificate-backed confidential client. Tokens stay server-side; the browser
receives a secure, HttpOnly, `__Host-` session cookie. Data Protection keys
persist on the runtime keyring mount. See
[runtime credentials](runtime-credentials.md) for bootstrap and recovery.

The brochure surface is anonymous. Auth endpoints and the API are restricted
to configured app hosts. API challenges return 401/403 rather than redirecting
fetch requests to Entra. Mutating API requests use antiforgery protection.

Authorization uses the stable Entra object ID (`ICurrentUser.SubjectId`), not
email or display name. Invitation acceptance separately checks the invited
address against the signed-in user's verified email. Do not log email,
display names, invitation links or trip content.

## Manual admin runbook

The retained registration and redirect URIs are defined in `terraform/identity.tf`
and `terraform/locals.tf`. Self-service sign-up configuration and conditional
access are tenant-admin responsibilities, not application deployment steps.
Before accepting a rollout, verify the existing user flow is attached to the
registration, its intended identity providers are enabled, and tenant
collaboration and conditional-access policies permit the intended users.
Exercise sign-up, sign-in and sign-out with the real provider. Do not weaken
tenant security policies or grant additional permissions without review.

If a flow must be created, a tenant administrator can use Entra admin center
under External Identities > User flows, select the intended providers and
attach the retained registration. Confirm the flow's application and provider
settings before testing. Tenant-wide policy changes require separate review.

Local test authentication is opt-in and Development-only; it must not be
enabled on a public host. Production never enables it, including when
`WEBSITE_INSTANCE_ID` is absent. See [testing](testing.md).
