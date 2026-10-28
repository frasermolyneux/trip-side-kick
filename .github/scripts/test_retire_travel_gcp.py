import copy
import json
from pathlib import Path
import unittest
from unittest import mock

import retire_travel_gcp as retirement


class RetirementTests(unittest.TestCase):
    def test_google_cli_human_readable_errors_map_to_fixed_private_categories(self):
        for diagnostic, category in (
            ("does not have permission: Cloud Billing API has not been used", "requires an enabled API"),
            ("SERVICE_DISABLED", "requires an enabled API"),
            ("does not have permission to access projects", "permission denied"),
            ("unauthorized_client", "authentication rejected"),
            ("invalid choice: billing", "command unavailable"),
            ("unexpected service response", "failed"),
        ):
            result = mock.Mock(returncode=1, stderr=diagnostic + " private-canary", stdout="")
            with self.subTest(category=category), mock.patch.object(
                    retirement.subprocess, "run", return_value=result):
                with self.assertRaises(retirement.GuardFailure) as failure:
                    retirement.gcloud("billing", "projects", "describe", retirement.PROJECT)
                self.assertEqual(str(failure.exception),
                                 f"Google billing projects describe {category}; diagnostic output withheld.")
                self.assertNotIn("private-canary", str(failure.exception))

    def test_google_failure_identifies_only_operation_and_withholds_raw_diagnostics(self):
        for operation in (
            ("projects", "describe", retirement.PROJECT),
            ("billing", "projects", "describe", retirement.PROJECT),
            ("iam", "service-accounts", "set-iam-policy", retirement.ACCOUNT, "private-policy.json"),
            ("iam", "workload-identity-pools", "providers", "update-oidc", "github-provider"),
        ):
            result = mock.Mock(returncode=1, stderr="PERMISSION_DENIED private-canary", stdout="")
            with self.subTest(operation=operation), mock.patch.object(
                    retirement.subprocess, "run", return_value=result):
                with self.assertRaises(retirement.GuardFailure) as failure:
                    retirement.gcloud(*operation)
                self.assertIn("permission denied", str(failure.exception))
                self.assertIn("Google " + operation[0], str(failure.exception))
                self.assertNotIn("private-canary", str(failure.exception))
                self.assertNotIn("private-policy.json", str(failure.exception))

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
        evidence = mock.patch.object(retirement, "validate_retirements")
        self.evidence = evidence.start()
        self.addCleanup(evidence.stop)
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

    def test_incomplete_retirements_do_not_contact_google(self):
        self.evidence.side_effect = ValueError("Production retirement has not completed.")
        with self.assertRaises(ValueError):
            retirement.main()
        self.cloud.assert_not_called()

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


class RetirementEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.environments = {1: "Development", 2: "Production"}
        self.apply = "success"
        self.inventory = "success"
        self.comparison = "identical"
        self.check_sha = retirement.GUARDED_RETIREMENT
        api = mock.patch.object(retirement, "github", side_effect=self.github)
        api.start()
        self.addCleanup(api.stop)

    def github(self, path):
        if path.startswith("actions/workflows/"):
            return {"workflow_runs": [
                {"id": number, "head_sha": retirement.GUARDED_RETIREMENT,
                 "head_branch": "main", "event": "workflow_dispatch", "conclusion": "success"}
                for number in (1, 2)
            ]}
        if path.startswith("actions/runs/"):
            number = int(path.split("/")[2])
            return {"jobs": [{"name": "terraform-destroy", "conclusion": "success",
                              "check_run_url": f"https://api.github.com/check-runs/{number}",
                              "steps": [
                                  {"name": "Stop if retirement would destroy retained Google resources",
                                   "conclusion": "success"},
                                  {"name": "Apply only the validated retirement plan",
                                   "conclusion": self.apply},
                                  {"name": "Assert workload resource group is empty",
                                   "conclusion": self.inventory},
                              ]}]}
        if path.startswith("check-runs/"):
            number = int(path.split("/")[1])
            return {"deployment": {"environment": self.environments[number]},
                    "head_sha": self.check_sha, "conclusion": "success"}
        if path.startswith("compare/"):
            return {"status": self.comparison}
        self.fail("Unexpected evidence request.")

    def test_both_actual_guarded_apply_and_inventory_successes_are_required(self):
        retirement.validate_retirements()
        for attribute, value in (("apply", "skipped"), ("inventory", "failure"),
                                 ("comparison", "behind"), ("check_sha", "unexpected"),
                                 ("environments", {1: "Development", 2: "Development"})):
            original = getattr(self, attribute)
            with self.subTest(attribute=attribute):
                setattr(self, attribute, value)
                try:
                    with self.assertRaises(ValueError):
                        retirement.validate_retirements()
                finally:
                    setattr(self, attribute, original)


if __name__ == "__main__":
    unittest.main()
