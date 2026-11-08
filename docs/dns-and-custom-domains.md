# DNS and custom domains

`tripsidekick.app` and `tripsidekick.net` are Cloudflare-managed zones owned by
`platform-connectivity`. This repository owns the four production DNS records
using a scoped DNS token, supplied as the GitHub environment secret
`CLOUDFLARE_API_KEY`. Terraform creates proxied A records pointing to the
constrained runtime host:

| Hostname | Surface |
| --- | --- |
| `tripsidekick.net` | Brochure site |
| `www.tripsidekick.net` | Brochure site, redirected to apex |
| `tripsidekick.app` | PWA and API |
| `www.tripsidekick.app` | PWA and API, redirected to apex |

The shared Caddy ingress obtains and renews publicly trusted certificates using
its default automatic HTTPS configuration. The application handles www-to-apex
redirects. Cloudflare's Full (strict) origin TLS setting remains unchanged.
Public DNS, Cloudflare and the host firewall must allow at least one default
Caddy ACME challenge to reach Caddy. The actual rollout will establish whether
issuance and subsequent renewal work; failure can interrupt HTTPS.

Platform ingress changes and obsolete credential cleanup require separate
review and rollout. See [runtime credentials](runtime-credentials.md) for the
reported cleanup status and outstanding certificate verification.
