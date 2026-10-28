import copy
import unittest

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


if __name__ == "__main__":
    unittest.main()
