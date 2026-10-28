import copy
import json
from pathlib import Path
import unittest
from unittest import mock

import retire_travel_gcp as retirement


class RetirementTests(unittest.TestCase):
    def test_only_retired_member_is_removed_and_retry_is_idempotent(self):
        policy = {"etag": "current", "version": 3, "bindings": [
            {"role": "roles/iam.workloadIdentityUser", "members": [
                retirement.TRAVEL, retirement.TRIP, retirement.PREFIX + "frasermolyneux/platform-workloads"
            ]},
            {"role": "roles/viewer", "condition": {"expression": "true"}, "members": ["user:other"]},
            {"role": "roles/viewer", "members": [retirement.TRAVEL]},
        ]}
        original = copy.deepcopy(policy)
        desired = retirement.retired_policy(policy)
        self.assertEqual(policy, original)
        self.assertEqual(desired["etag"], "current")
        self.assertEqual(desired["version"], 3)
        self.assertEqual(desired["bindings"][1], policy["bindings"][1])
        self.assertEqual(desired["bindings"][0]["members"], policy["bindings"][0]["members"][1:])
        self.assertEqual(len(desired["bindings"]), 2)
        self.assertEqual(retirement.retired_policy(desired), desired)

    def test_missing_or_conditional_replacement_trust_fails_closed(self):
        for binding in (
            {"role": "roles/iam.workloadIdentityUser", "members": [retirement.TRAVEL]},
            {"role": "roles/iam.workloadIdentityUser", "members": [retirement.TRIP],
             "condition": {"expression": "true"}},
        ):
            with self.assertRaises(ValueError):
                retirement.retired_policy({"bindings": [binding]})

    def test_only_reviewed_provider_and_idempotent_conditions_are_allowed(self):
        valid = {"state": "ACTIVE", "oidc": {"issuerUri": "https://token.actions.githubusercontent.com"},
                 "attributeMapping": retirement.MAPPING, "attributeCondition": retirement.OLD}
        retirement.validate_provider(valid)
        retirement.validate_provider({**valid, "attributeCondition": retirement.DESIRED})
        for key, value in (("state", "DISABLED"), ("oidc", {"issuerUri": "https://unexpected"}),
                           ("attributeMapping", {}), ("attributeCondition", "true")):
            with self.subTest(key=key), self.assertRaises(ValueError):
                retirement.validate_provider({**valid, key: value})


class EntryPointTests(unittest.TestCase):
    def setUp(self):
        environment = mock.patch.dict(retirement.os.environ, {
            "GITHUB_REF": "refs/heads/main",
            "CONFIGURED_PROJECT": retirement.PROJECT,
            "CONFIGURED_PROVIDER": retirement.PROVIDER,
            "CONFIGURED_ACCOUNT": retirement.ACCOUNT,
        }, clear=True)
        environment.start()
        self.addCleanup(environment.stop)
        self.project = {"projectNumber": retirement.NUMBER, "lifecycleState": "ACTIVE"}
        self.billing = {"billingEnabled": True, "billingAccountName": "billingAccounts/retained"}
        self.services = [{"config": {"name": service}} for service in (
            "maps-backend.googleapis.com", "places.googleapis.com"
        )]
        self.provider = {
            "state": "ACTIVE", "oidc": {"issuerUri": "https://token.actions.githubusercontent.com"},
            "attributeMapping": retirement.MAPPING, "attributeCondition": retirement.OLD,
        }
        self.policy = {"etag": "current", "bindings": [
            {"role": "roles/iam.workloadIdentityUser", "members": [
                retirement.TRAVEL, retirement.TRIP,
                retirement.PREFIX + "frasermolyneux/platform-workloads",
            ]},
            {"role": "roles/viewer", "members": ["user:unrelated"]},
        ]}
        self.writes = []
        self.fail_provider_update = False
        cloud = mock.patch.object(retirement, "gcloud", side_effect=self.gcloud)
        self.cloud = cloud.start()
        self.addCleanup(cloud.stop)

    def gcloud(self, *arguments):
        if arguments[:2] == ("projects", "describe"):
            return copy.deepcopy(self.project)
        if arguments[:3] == ("billing", "projects", "describe"):
            return copy.deepcopy(self.billing)
        if arguments[:2] == ("services", "list"):
            return copy.deepcopy(self.services)
        if arguments[:4] == ("iam", "workload-identity-pools", "providers", "describe"):
            return copy.deepcopy(self.provider)
        if arguments[:3] == ("iam", "service-accounts", "get-iam-policy"):
            return copy.deepcopy(self.policy)
        if arguments[:3] == ("iam", "service-accounts", "set-iam-policy"):
            desired = json.loads(Path(arguments[4]).read_text(encoding="utf-8"))
            self.assertEqual(desired["etag"], self.policy["etag"])
            self.policy = {**desired, "etag": "updated"}
            self.writes.append("iam")
            return copy.deepcopy(self.policy)
        if arguments[:4] == ("iam", "workload-identity-pools", "providers", "update-oidc"):
            if self.fail_provider_update:
                self.fail_provider_update = False
                raise RuntimeError("Simulated provider update failure.")
            self.assertEqual(arguments[-1], "--attribute-condition=" + retirement.DESIRED)
            self.provider["attributeCondition"] = retirement.DESIRED
            self.writes.append("provider")
            return copy.deepcopy(self.provider)
        self.fail("Unexpected Google operation.")

    def test_wrong_entry_context_does_not_contact_google(self):
        for key in ("GITHUB_REF", "CONFIGURED_PROJECT", "CONFIGURED_PROVIDER", "CONFIGURED_ACCOUNT"):
            with self.subTest(key=key), mock.patch.dict(retirement.os.environ, {key: "unexpected"}):
                with self.assertRaises(ValueError):
                    retirement.main()
                self.cloud.assert_not_called()

    def test_failed_preflight_never_writes(self):
        cases = [
            ("project", {"projectNumber": "unexpected", "lifecycleState": "ACTIVE"}),
            ("billing", {**self.billing, "billingEnabled": False}),
            ("services", []),
            ("provider", {**self.provider, "attributeCondition": "true"}),
            ("policy", {"etag": "current", "bindings": []}),
        ]
        for attribute, invalid in cases:
            original = getattr(self, attribute)
            with self.subTest(attribute=attribute):
                setattr(self, attribute, invalid)
                try:
                    with self.assertRaises(ValueError):
                        retirement.main()
                    self.assertEqual(self.writes, [])
                finally:
                    setattr(self, attribute, original)

    def test_success_preserves_unrelated_policy_and_retry_does_not_write(self):
        expected = retirement.retired_policy(self.policy)
        retirement.main()
        self.assertEqual(self.policy["bindings"], expected["bindings"])
        self.assertEqual(self.writes, ["iam", "provider"])
        retirement.main()
        self.assertEqual(self.writes, ["iam", "provider"])

    def test_retry_after_iam_success_and_provider_failure_keeps_trip_trusted(self):
        expected = retirement.retired_policy(self.policy)
        self.fail_provider_update = True
        with self.assertRaises(RuntimeError):
            retirement.main()
        self.assertEqual(self.policy["bindings"], expected["bindings"])
        self.assertEqual(self.provider["attributeCondition"], retirement.OLD)
        self.assertEqual(self.writes, ["iam"])
        retirement.main()
        self.assertEqual(self.writes, ["iam", "provider"])
        self.assertEqual(self.policy["bindings"], expected["bindings"])
        self.assertEqual(self.provider["attributeCondition"], retirement.DESIRED)

    def test_retry_after_provider_already_updated_only_removes_old_member(self):
        self.provider["attributeCondition"] = retirement.DESIRED
        retirement.main()
        self.assertEqual(self.writes, ["iam"])
        self.assertEqual(retirement.retired_policy(self.policy), self.policy)


if __name__ == "__main__":
    unittest.main()
