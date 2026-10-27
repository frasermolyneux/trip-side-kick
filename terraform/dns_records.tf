# Cloudflare is authoritative for tripsidekick.app and tripsidekick.net; there is no Azure DNS zone
# for this workload. Production explicitly switches to the constrained origin with
# Cloudflare Origin CA TLS. platform-connectivity owns both zones' strict TLS baseline.
# Retain Azure verification/bindings/certificates for rollback until separate retirement;
# managed Azure certificates cannot renew while their records are proxied.
locals {
  # Cloudflare wants the apex record named after the zone itself; subdomains use the FQDN.
  cloudflare_record_names = {
    for key, domain in var.custom_domains : key => domain.hostname
  }
}

resource "cloudflare_dns_record" "app_service_verification" {
  for_each = var.custom_domains

  zone_id = data.cloudflare_zone.zones[each.value.zone].zone_id
  name    = "asuid.${local.cloudflare_record_names[each.key]}"
  type    = "TXT"
  ttl     = local.custom_domain_dns_ttl_seconds
  content = azurerm_linux_web_app.app.custom_domain_verification_id
  comment = "trip-side-kick ${var.environment} App Service custom domain verification"
}

resource "cloudflare_dns_record" "web_app" {
  for_each = var.custom_domains

  zone_id = data.cloudflare_zone.zones[each.value.zone].zone_id
  name    = local.cloudflare_record_names[each.key]
  type    = var.runtime_origin_ipv4 == null ? "CNAME" : "A"
  ttl     = var.runtime_origin_ipv4 == null ? local.custom_domain_dns_ttl_seconds : 1
  content = var.runtime_origin_ipv4 == null ? azurerm_linux_web_app.app.default_hostname : var.runtime_origin_ipv4
  proxied = var.runtime_origin_ipv4 != null
  comment = "trip-side-kick ${var.environment} ${var.runtime_origin_ipv4 == null ? "App Service" : "constrained origin"} ${each.value.surface} surface"

  depends_on = [
    cloudflare_dns_record.app_service_verification
  ]
}
