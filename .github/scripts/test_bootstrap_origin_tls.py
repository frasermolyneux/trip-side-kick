from datetime import datetime, timedelta, timezone
from pathlib import Path
import unittest
from unittest.mock import patch

import bootstrap_origin_tls as tls


class OriginTlsTests(unittest.TestCase):
    def test_exact_hosts_derive_from_both_application_configuration_shapes(self):
        self.assertEqual(tls.approved_hostnames({"AllowedHosts": "tiles.example;www.tiles.example"}),
                         ("tiles.example", "www.tiles.example"))
        self.assertEqual(tls.approved_hostnames({"HostRouting": {
            "AppHosts": ["app.example"], "SiteHosts": ["site.example"],
        }}), ("app.example", "site.example", "www.app.example", "www.site.example"))
        with self.assertRaises(tls.BootstrapError):
            tls.approved_hostnames({"AllowedHosts": "*.example"})

    def test_valid_existing_origin_certificate_does_not_issue_or_publish(self):
        now = datetime.now(timezone.utc)
        existing = {
            "tags": {"certificate-hostnames": "tiles.example,www.tiles.example"},
            "properties": {"attributes": {"exp": int((now + timedelta(days=90)).timestamp())}},
        }
        with patch.object(tls, "metadata", return_value=existing), \
                patch.object(tls, "new_certificate") as issue, patch.object(tls, "publish") as publish:
            tls.renew("workload", "/vault", "token", {"AllowedHosts": "tiles.example"}, now)
        issue.assert_not_called()
        publish.assert_not_called()

    def test_unexpected_existing_hostname_binding_requires_explicit_recovery(self):
        existing = {
            "tags": {"certificate-hostnames": "another.example"},
            "properties": {"attributes": {"exp": 1}},
        }
        with patch.object(tls, "metadata", return_value=existing), \
                patch.object(tls, "new_certificate") as issue, patch.object(tls, "publish") as publish:
            with self.assertRaisesRegex(tls.BootstrapError, "unexpected hostname"):
                tls.renew("workload", "/vault", "token", {"AllowedHosts": "tiles.example"},
                          datetime.now(timezone.utc))
        issue.assert_not_called()
        publish.assert_not_called()

    def test_wrong_private_key_or_extra_san_never_returns_a_bundle(self):
        for wrong_key in (True, False):
            with self.subTest(wrong_key=wrong_key):
                def fake_command(*arguments, **kwargs):
                    if arguments[1] == "ecparam":
                        Path(arguments[arguments.index("-out") + 1]).write_text("private-key")
                    elif arguments[1] == "req":
                        Path(arguments[arguments.index("-out") + 1]).write_text("public-csr")
                    elif arguments[1] == "pkey":
                        return "Key is valid" if "-check" in arguments else ("wrong-public" if wrong_key else "public")
                    elif arguments[1] == "x509":
                        return "public" if "-pubkey" in arguments else "DNS:tiles.example, DNS:unexpected.example"
                    return ""

                with patch.object(tls, "command", side_effect=fake_command), patch.object(
                    tls, "request", return_value={"success": True, "result": {
                        "certificate": "public-certificate", "id": "issued-id",
                    }}
                ):
                    with self.assertRaisesRegex(tls.BootstrapError, "does not match|exactly the approved"):
                        tls.new_certificate(("tiles.example",), "token")


if __name__ == "__main__":
    unittest.main()
