"""Verify retained Google capability and securely deliver its key without logging it."""

import hashlib
import json
import os

from bootstrap_runtime import BootstrapError, command, discover, metadata, publish


PROJECT = "gcp-mx-io-travel-itinerary"
KEY = "maps-trip-side-kick-prd-31aac1a62737"
SERVICES = {"maps-backend.googleapis.com", "places.googleapis.com"}
REFERRERS = {
    "https://tripsidekick.app/*", "https://www.tripsidekick.app/*",
    "https://tripsidekick.net/*", "https://www.tripsidekick.net/*",
}


def gcloud(*arguments):
    return json.loads(command("gcloud", *arguments, f"--project={PROJECT}", "--format=json"))


def main():
    if os.environ.get("GITHUB_REF") != "refs/heads/main":
        raise BootstrapError("Google key delivery is restricted to approved main.")
    workload, vault, _, _, token, _ = discover()
    if workload != "trip-side-kick":
        raise BootstrapError("Unexpected workload for retained Google capability.")
    enabled = {entry["config"]["name"] for entry in gcloud("services", "list", "--enabled")}
    key = gcloud("services", "api-keys", "describe", KEY, "--location=global")
    restrictions = key.get("restrictions", {})
    if (not SERVICES <= enabled or key.get("deleteTime")
            or key.get("name") != f"projects/776197093213/locations/global/keys/{KEY}"
            or {target["service"] for target in restrictions.get("apiTargets", [])} != SERVICES
            or set(restrictions.get("browserKeyRestrictions", {}).get("allowedReferrers", [])) != REFERRERS):
        raise BootstrapError("Retained APIs or exact replacement key restrictions did not verify.")
    value = gcloud("services", "api-keys", "get-key-string", KEY, "--location=global").get("keyString")
    if not isinstance(value, str) or not value.strip() or "\n" in value or "\r" in value:
        raise BootstrapError("Google returned an invalid replacement key; no secret published.")
    tags = {"google-key-id": KEY, "value-sha256": hashlib.sha256(value.encode()).hexdigest()}
    existing = metadata(vault, "google-maps-api-key", token, workload)
    if existing is not None and (existing.get("tags") or {}).get("google-key-id") != KEY:
        raise BootstrapError("Existing Google key has unknown ownership; explicit recovery required.")
    if existing is None or any((existing.get("tags") or {}).get(name) != tag for name, tag in tags.items()):
        publish(vault, "google-maps-api-key", token, workload, value, tags=tags)
        print("Verified APIs/restrictions and securely published the replacement Google key.")
    else:
        print("Verified APIs/restrictions; retained unchanged Google key secret.")


if __name__ == "__main__":
    try:
        main()
    except BootstrapError as error:
        raise SystemExit(str(error)) from None
