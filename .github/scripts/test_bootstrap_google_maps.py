import copy
import unittest
from unittest.mock import patch

import bootstrap_google_maps as delivery


class GoogleKeyDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.key = {
            "name": f"projects/776197093213/locations/global/keys/{delivery.KEY}",
            "restrictions": {
                "apiTargets": [{"service": service} for service in delivery.SERVICES],
                "browserKeyRestrictions": {"allowedReferrers": sorted(delivery.REFERRERS)},
            },
        }
        self.enabled = [{"config": {"name": service}} for service in delivery.SERVICES]

    def run_delivery(self, key=None, enabled=None, existing=None):
        with patch.dict(delivery.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(delivery, "discover", return_value=(
                    "trip-side-kick", "/vault", {}, "tenant", "arm-token", {})), \
                patch.object(delivery, "gcloud", side_effect=[
                    self.enabled if enabled is None else enabled,
                    self.key if key is None else key, {"keyString": "disposable-test-key"},
                ]), patch.object(delivery, "metadata", return_value=existing), \
                patch.object(delivery, "publish") as publish:
            delivery.main()
            return publish

    def test_exact_restricted_key_is_published_without_terraform_secret_output(self):
        publish = self.run_delivery()
        self.assertEqual(publish.call_args.args[1], "google-maps-api-key")
        self.assertEqual(publish.call_args.kwargs["tags"]["google-key-id"], delivery.KEY)

    def test_disabled_api_wrong_project_or_broader_restrictions_fail_before_publication(self):
        wrong_name = {**self.key, "name": self.key["name"].replace("776197093213", "123")}
        broad = copy.deepcopy(self.key)
        broad["restrictions"]["browserKeyRestrictions"]["allowedReferrers"].append("https://other.example/*")
        for variant in ({"enabled": []}, {"key": wrong_name}, {"key": broad}):
            with self.subTest(variant=variant), self.assertRaisesRegex(delivery.BootstrapError, "did not verify"):
                self.run_delivery(**variant)

    def test_unchanged_key_is_not_republished_and_foreign_key_is_not_overwritten(self):
        tags = {"google-key-id": delivery.KEY,
                "value-sha256": delivery.hashlib.sha256(b"disposable-test-key").hexdigest()}
        self.run_delivery(existing={"tags": tags}).assert_not_called()
        with self.assertRaisesRegex(delivery.BootstrapError, "explicit recovery"):
            self.run_delivery(existing={"tags": {"google-key-id": "foreign"}})

    def test_non_main_execution_cannot_discover_or_publish(self):
        with patch.dict(delivery.os.environ, {"GITHUB_REF": "refs/heads/untrusted"}), \
                patch.object(delivery, "discover") as discover:
            with self.assertRaisesRegex(delivery.BootstrapError, "approved main"):
                delivery.main()
        discover.assert_not_called()
