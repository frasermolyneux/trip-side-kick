# Keep the live constrained-origin records; only legacy Azure verification is retired.
locals {
  # Cloudflare wants the apex record named after the zone itself; subdomains use the FQDN.
  cloudflare_record_names = {
    for key, domain in var.custom_domains : key => domain.hostname
  }
}

resource "cloudflare_dns_record" "web_app" {
  for_each = var.custom_domains

  zone_id = data.cloudflare_zone.zones[each.value.zone].zone_id
  name    = local.cloudflare_record_names[each.key]
  type    = "A"
  ttl     = 1
  content = var.runtime_origin_ipv4
  proxied = true
  comment = "trip-side-kick ${var.environment} constrained origin ${each.value.surface} surface"
}
