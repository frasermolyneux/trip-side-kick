# Cloudflare is authoritative for tripsidekick.app and tripsidekick.net; there is no Azure DNS zone
# for this workload. Records are created DNS-only (proxied = false) because:
#   * App Service validates custom hostnames by resolving them directly to Azure, and
#   * App Service managed certificates cannot be issued or renewed through the Cloudflare proxy.
# Turning the orange cloud on would move TLS termination to Cloudflare and break managed-certificate
# renewal, so it stays off unless the workload adopts Cloudflare origin certificates.
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
  ttl     = 300
  content = azurerm_linux_web_app.app.custom_domain_verification_id
  comment = "trip-side-kick ${var.environment} App Service custom domain verification"
}

resource "cloudflare_dns_record" "web_app" {
  for_each = var.custom_domains

  zone_id = data.cloudflare_zone.zones[each.value.zone].zone_id
  name    = local.cloudflare_record_names[each.key]
  type    = "CNAME"
  ttl     = 300
  content = azurerm_linux_web_app.app.default_hostname
  proxied = false
  comment = "trip-side-kick ${var.environment} App Service ${each.value.surface} surface"

  depends_on = [
    cloudflare_dns_record.app_service_verification
  ]
}
