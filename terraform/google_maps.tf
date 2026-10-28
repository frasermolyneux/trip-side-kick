resource "google_project_service" "maps_backend" {
  count              = var.environment == "prd" ? 1 : 0
  project            = "gcp-mx-io-travel-itinerary"
  service            = "maps-backend.googleapis.com"
  disable_on_destroy = false

  lifecycle {
    prevent_destroy = true
  }
}

resource "google_project_service" "places_api" {
  count              = var.environment == "prd" ? 1 : 0
  project            = "gcp-mx-io-travel-itinerary"
  service            = "places.googleapis.com"
  disable_on_destroy = false

  lifecycle {
    prevent_destroy = true
  }
}

resource "google_project_service" "billing_api" {
  count              = var.environment == "prd" ? 1 : 0
  project            = "gcp-mx-io-travel-itinerary"
  service            = "cloudbilling.googleapis.com"
  disable_on_destroy = false

  lifecycle {
    prevent_destroy = true
  }
}

resource "google_apikeys_key" "maps" {
  count        = var.environment == "prd" ? 1 : 0
  name         = "maps-trip-side-kick-prd-31aac1a62737"
  display_name = "Trip Side Kick Production Maps capability"
  project      = "gcp-mx-io-travel-itinerary"

  restrictions {
    api_targets {
      service = "maps-backend.googleapis.com"
    }
    api_targets {
      service = "places.googleapis.com"
    }
    browser_key_restrictions {
      allowed_referrers = [
        "https://tripsidekick.app/*",
        "https://www.tripsidekick.app/*",
        "https://tripsidekick.net/*",
        "https://www.tripsidekick.net/*",
      ]
    }
  }

  depends_on = [
    google_project_service.maps_backend,
    google_project_service.places_api,
  ]
  lifecycle {
    prevent_destroy = true
  }
}
