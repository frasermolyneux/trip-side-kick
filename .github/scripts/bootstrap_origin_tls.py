"""Renew exact-host Origin CA credentials without changing DNS or ingress."""

from datetime import datetime, timedelta, timezone
import os
from pathlib import Path
import re
import tempfile

from bootstrap_runtime import BootstrapError, command, discover, metadata, publish, request


def approved_hostnames(config):
    routing = config.get("HostRouting")
    hosts = (routing["AppHosts"] + routing["SiteHosts"] if routing
             else config["AllowedHosts"].split(";"))
    if not hosts or any(not re.fullmatch(r"[a-z0-9][a-z0-9.-]+\.[a-z]{2,}", host) for host in hosts):
        raise BootstrapError("Origin TLS requires explicit approved public DNS hostnames.")
    return tuple(sorted({name for host in hosts for name in (host, "www." + host)
                         if not name.startswith("www.www.")}))


def new_certificate(hostnames, token):
    with tempfile.TemporaryDirectory(prefix="consolidated-origin-tls-") as directory:
        root = Path(directory)
        root.chmod(0o700)
        key, csr, certificate = root / "key.pem", root / "request.pem", root / "certificate.pem"
        command("openssl", "ecparam", "-name", "prime256v1", "-genkey", "-noout", "-out", str(key))
        key.chmod(0o600)
        command("openssl", "req", "-new", "-sha256", "-key", str(key), "-out", str(csr),
                "-subj", "/CN=" + hostnames[0],
                "-addext", "subjectAltName=" + ",".join("DNS:" + name for name in hostnames))
        issued = request("POST", "https://api.cloudflare.com/client/v4/certificates", token=token, document={
            "csr": csr.read_text(),
            "hostnames": list(hostnames),
            "request_type": "origin-ecc",
            "requested_validity": 365,
        })
        if issued.get("success") is not True:
            raise BootstrapError("Cloudflare did not issue an Origin CA certificate.")
        issued = issued["result"]
        certificate.write_text(issued["certificate"], encoding="ascii")
        if command("openssl", "pkey", "-in", str(key), "-check", "-noout").strip() != "Key is valid":
            raise BootstrapError("Origin TLS private key failed its consistency check.")
        command("openssl", "verify", "-partial_chain", "-trusted", str(certificate),
                "-purpose", "sslserver", str(certificate))
        public = command("openssl", "x509", "-in", str(certificate), "-pubkey", "-noout")
        if public != command("openssl", "pkey", "-in", str(key), "-pubout"):
            raise BootstrapError("Origin certificate does not match the locally generated key.")
        names = re.findall(r"\bDNS:([^,\s]+)", command(
            "openssl", "x509", "-in", str(certificate), "-noout", "-ext", "subjectAltName"))
        if {name.lower() for name in names} != set(hostnames):
            raise BootstrapError("Origin certificate does not cover exactly the approved hostnames.")
        command("openssl", "x509", "-in", str(certificate), "-noout", "-checkend", str(60 * 86400))
        expiry = datetime.strptime(command("openssl", "x509", "-in", str(certificate), "-enddate", "-noout")
                                  .strip().removeprefix("notAfter="), "%b %d %H:%M:%S %Y %Z").replace(tzinfo=timezone.utc)
        return certificate.read_text() + "\n" + key.read_text(), expiry, issued["id"]


def renew(workload, vault, token, config, now):
    hostnames = approved_hostnames(config)
    existing = metadata(vault, "origin-tls", token, workload)
    if existing is not None:
        expiry = existing["properties"]["attributes"].get("exp")
        if ((existing.get("tags") or {}).get("certificate-hostnames") != ",".join(hostnames)
                or not isinstance(expiry, int)):
            raise BootstrapError("Existing origin certificate has unexpected hostname/lifecycle metadata.")
        if expiry > int((now + timedelta(days=30)).timestamp()):
            print("Retained origin certificate; renewal is not due.")
            return
    bundle, expiry, certificate_id = new_certificate(hostnames, os.environ["CLOUDFLARE_ORIGIN_API_TOKEN"])
    publish(vault, "origin-tls", token, workload, bundle, expiry=expiry,
            tags={"certificate-hostnames": ",".join(hostnames), "origin-certificate-id": certificate_id})
    print("Published validated origin certificate; no DNS or ingress was changed.")


if __name__ == "__main__":
    try:
        if os.environ.get("GITHUB_REF") != "refs/heads/main":
            raise BootstrapError("Origin TLS bootstrap is restricted to approved main.")
        workload, vault, _, _, token, config = discover()
        renew(workload, vault, token, config, datetime.now(timezone.utc))
    except BootstrapError as error:
        raise SystemExit(str(error)) from None
