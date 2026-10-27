"""Publish runtime credentials without rotating database passwords or storing keys in state."""

import base64
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import HTTPRedirectHandler, Request, build_opener
import uuid


ROOT = Path(__file__).resolve().parents[2]
ARM = "https://management.azure.com"


class BootstrapError(Exception):
    """An actionable error that does not contain command output or credentials."""


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, new_url):
        return None


def command(*arguments, binary=False):
    result = subprocess.run(arguments, capture_output=True, text=not binary, check=False)
    if result.returncode:
        raise BootstrapError(f"{arguments[0]} failed; output withheld to protect credentials.")
    return result.stdout


def az_json(*arguments):
    output = command("az", *arguments, "--only-show-errors", "--output", "json")
    return json.loads(output) if output.strip() else None


def request(method, url, *, token=None, document=None, missing_ok=False, form=False):
    headers = {"Content-Type": "application/x-www-form-urlencoded" if form else "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    body = None if document is None else (
        urlencode(document).encode() if form else json.dumps(document).encode()
    )
    try:
        with build_opener(NoRedirect).open(
            Request(url, data=body, headers=headers, method=method), timeout=60
        ) as response:
            return json.load(response)
    except HTTPError as error:
        if missing_ok and error.code == 404:
            return None
        raise BootstrapError(f"Credential API failed (HTTP {error.code}); response withheld.") from None
    except URLError:
        raise BootstrapError("Credential API unavailable; retry without removing existing credentials.") from None


def discover():
    policy = json.loads((ROOT / ".github" / "validation" / "policy.json").read_text())
    config = json.loads((ROOT / "deployment" / "runtime.example.json").read_text())
    workload = policy["workloadId"]
    bindings = {entry["id"]: entry for entry in policy["allowedSecrets"]}
    required = ("runtime-configuration", "client-certificate", "database-password")
    vaults = {bindings[name]["vaultName"] for name in required}
    if len(vaults) != 1 or any(bindings[name]["vaultSecret"] != name for name in required):
        raise BootstrapError("Runtime secret bindings differ from the reviewed bootstrap contract.")
    vault = az_json("keyvault", "show", "--name", vaults.pop(),
                    "--resource-group", f"rg-{workload}-prd-swedencentral")
    if any((vault.get("tags") or {}).get(key) != value for key, value in {
        "Workload": workload, "Environment": "prd", "Purpose": "workload-secrets"
    }.items()):
        raise BootstrapError("Runtime vault ownership does not match the reviewed Production workload.")
    app = az_json("ad", "app", "show", "--id", config["AzureAd"]["ClientId"])
    account = az_json("account", "show")
    audience = "AzureADandPersonalMicrosoftAccount" if config["AzureAd"]["TenantId"] == "common" else "AzureADMyOrg"
    if (app["appId"] != config["AzureAd"]["ClientId"]
            or app.get("signInAudience") != audience
            or vault["properties"]["tenantId"] != account["tenantId"]):
        raise BootstrapError("Application audience or vault tenant differs from the reviewed contract.")
    if config["AzureAd"]["TenantId"] != "common" and config["AzureAd"]["TenantId"] != account["tenantId"]:
        raise BootstrapError("Workforce application tenant differs from the deployment tenant.")
    config["ForwardedHeaders"]["KnownProxies"] = []
    token = az_json("account", "get-access-token", "--resource", ARM)["accessToken"]
    return workload, vault["id"], app, account["tenantId"], token, config


def secret_url(vault, name):
    return f"{ARM}{vault}/secrets/{name}?api-version=2023-07-01"


def metadata(vault, name, token, workload):
    existing = request("GET", secret_url(vault, name), token=token, missing_ok=True)
    if existing is not None and (
        (existing.get("tags") or {}).get("managed-by") != workload
        or existing.get("properties", {}).get("attributes", {}).get("enabled") is not True
    ):
        raise BootstrapError(f"Existing {name} has unexpected ownership or is disabled; explicit recovery required.")
    return existing


def publish(vault, name, token, workload, value, *, expiry=None, tags=None):
    request("PUT", secret_url(vault, name), token=token, document={
        "tags": {"managed-by": workload, **(tags or {})},
        "properties": {
            "value": value,
            "contentType": "application/json" if name == "runtime-configuration" else "text/plain",
            "attributes": {"enabled": True, **({"exp": int(expiry.timestamp())} if expiry else {})},
        },
    })


def b64(value):
    return base64.urlsafe_b64encode(value).decode().rstrip("=")


def verify_authentication(app, tenant, certificate, key, now):
    endpoint = f"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token"
    der = command("openssl", "x509", "-in", str(certificate), "-outform", "DER", binary=True)
    # SHA-1 identifies the registered certificate; the assertion itself uses RS256.
    thumbprint = hashlib.sha1(der, usedforsecurity=False).digest()
    header = b64(json.dumps({"alg": "RS256", "typ": "JWT", "x5t": b64(thumbprint)}).encode())
    claims = b64(json.dumps({
        "aud": endpoint, "iss": app["appId"], "sub": app["appId"], "jti": str(uuid.uuid4()),
        "nbf": int(now.timestamp()) - 30, "exp": int(now.timestamp()) + 600,
    }).encode())
    unsigned = f"{header}.{claims}"
    payload = key.parent / "assertion"
    payload.write_text(unsigned, encoding="ascii")
    signature = command("openssl", "dgst", "-sha256", "-sign", str(key), str(payload), binary=True)
    assertion = f"{unsigned}.{b64(signature)}"
    for attempt in range(12):
        try:
            result = request("POST", endpoint, form=True, document={
                "client_id": app["appId"], "scope": "https://graph.microsoft.com/.default",
                "grant_type": "client_credentials",
                "client_assertion_type": "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                "client_assertion": assertion,
            })
            if not result.get("access_token"):
                raise BootstrapError("Certificate authentication returned no access token.")
            return
        except BootstrapError:
            if attempt == 11:
                raise
            time.sleep(5)


def rotate_certificate(workload, vault, app, tenant, token, now):
    prefix = f"{workload}-ns346663-"
    registered = az_json("ad", "app", "credential", "list", "--id", app["id"], "--cert")
    existing = metadata(vault, "client-certificate", token, workload)
    if existing is not None:
        tags = existing.get("tags") or {}
        expiry = existing["properties"]["attributes"].get("exp")
        if (tags.get("application-id") != app["appId"] or tags.get("application-object-id") != app["id"]
                or not isinstance(expiry, int) or not tags.get("credential-key-id")):
            raise BootstrapError("Existing certificate has unexpected application/lifecycle metadata.")
        if expiry > int((now + timedelta(days=30)).timestamp()) and any(
            entry["keyId"] == tags["credential-key-id"]
            and (entry.get("displayName") or "").startswith(prefix)
            and datetime.fromisoformat(entry["endDateTime"].replace("Z", "+00:00")).timestamp() >= expiry
            for entry in registered
        ):
            print("Retained certificate; renewal is not due.")
            return
    with tempfile.TemporaryDirectory(prefix=prefix) as directory:
        root = Path(directory)
        root.chmod(0o700)
        key, certificate, pfx = root / "key.pem", root / "certificate.pem", root / "certificate.pfx"
        command("openssl", "req", "-x509", "-newkey", "rsa:3072", "-sha256", "-nodes",
                "-keyout", str(key), "-out", str(certificate), "-days", "180",
                "-subj", f"/CN={workload}", "-addext", "basicConstraints=critical,CA:FALSE",
                "-addext", "keyUsage=critical,digitalSignature", "-addext", "extendedKeyUsage=clientAuth")
        key.chmod(0o600)
        expiry = datetime.strptime(command("openssl", "x509", "-in", str(certificate), "-enddate", "-noout")
                                  .strip().removeprefix("notAfter="), "%b %d %H:%M:%S %Y %Z").replace(tzinfo=timezone.utc)
        name = prefix + str(uuid.uuid4())
        az_json("ad", "app", "credential", "reset", "--id", app["id"], "--append",
                "--cert", f"@{certificate}", "--display-name", name, "--end-date", expiry.strftime("%Y-%m-%dT%H:%M:%SZ"))
        for attempt in range(12):
            keys = az_json("ad", "app", "credential", "list", "--id", app["id"], "--cert")
            matching = [entry for entry in keys if entry.get("displayName") == name]
            if matching or attempt == 11:
                break
            time.sleep(5)
        if len(matching) != 1:
            raise BootstrapError("New public certificate registration did not verify; no private key published.")
        verify_authentication(app, tenant, certificate, key, now)
        command("openssl", "pkcs12", "-export", "-out", str(pfx), "-inkey", str(key),
                "-in", str(certificate), "-passout", "pass:")
        pfx.chmod(0o600)
        publish(vault, "client-certificate", token, workload, base64.b64encode(pfx.read_bytes()).decode(),
                expiry=expiry, tags={"application-id": app["appId"], "application-object-id": app["id"],
                                     "credential-key-id": matching[0]["keyId"]})
    for entry in registered:
        if ((entry.get("displayName") or "").startswith(prefix)
                and datetime.fromisoformat(entry["endDateTime"].replace("Z", "+00:00")) < now - timedelta(days=7)):
            az_json("ad", "app", "credential", "delete", "--id", app["id"], "--key-id", entry["keyId"], "--cert")
    print("Published authenticated certificate; unrelated and unexpired public keys retained.")


def main():
    if os.environ.get("GITHUB_REF") != "refs/heads/main":
        raise BootstrapError("Runtime bootstrap is restricted to approved main.")
    workload, vault, app, tenant, token, config = discover()
    password = metadata(vault, "database-password", token, workload)
    existing = metadata(vault, "runtime-configuration", token, workload)
    metadata(vault, "client-certificate", token, workload)
    if password is None:
        publish(vault, "database-password", token, workload, secrets.token_urlsafe(48))
        print("Created database password.")
    elif password["properties"]["attributes"].get("exp") is not None:
        raise BootstrapError("Database password is expiring; explicit coordinated rotation required.")
    else:
        print("Retained database password.")
    rotate_certificate(workload, vault, app, tenant, token, datetime.now(timezone.utc))
    if existing is None and "Sql" in config:
        config["Sql"]["InitializeOnStartup"] = True
    digest = hashlib.sha256(json.dumps(config, sort_keys=True).encode()).hexdigest()
    if existing is None or (existing.get("tags") or {}).get("configuration-sha256") != digest:
        publish(vault, "runtime-configuration", token, workload, json.dumps(config),
                tags={"configuration-sha256": digest})
        print("Published reviewed runtime configuration; trusted Caddy addresses are hydrated only on the host.")
    else:
        print("Retained unchanged runtime configuration.")


if __name__ == "__main__":
    try:
        main()
    except BootstrapError as error:
        raise SystemExit(str(error)) from None
