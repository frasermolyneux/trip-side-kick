import json
from pathlib import Path
import runpy
import unittest
from unittest.mock import patch


GATE = runpy.run_path(str(Path(__file__).with_name("require-quality.py")))["wait_for_quality"]
SHA = "a" * 40
REPOSITORY = "owner/repository"
QUALITY = {
    "id": 10, "head_sha": SHA, "event": "push", "head_branch": "main",
    "path": ".github/workflows/codequality.yml",
    "repository": {"full_name": REPOSITORY}, "status": "completed", "conclusion": "success",
}
CHECK = {
    "id": 20, "head_sha": SHA, "name": "workload-contract",
    "app": {"id": 15368, "slug": "github-actions"},
    "status": "completed", "conclusion": "success",
    "details_url": f"https://github.com/{REPOSITORY}/actions/runs/99",
}
PRODUCER = {
    "id": 99, "event": "workflow_run", "path": ".github/workflows/validate-workload-contract.yml",
    "display_title": f"Validate workload contract {SHA}",
    "head_branch": "main", "repository": {"full_name": REPOSITORY},
    "status": "completed", "conclusion": "success",
}
JOB = {"id": 30, "name": f"workload-contract ({SHA})", "status": "completed", "conclusion": "success"}


class PublicationGateTests(unittest.TestCase):
    def run_gate(self, qualities=None, checks=None, producer=None, jobs=None, newer_producer=None):
        def output(command, **_):
            path = command[-1]
            if path == f"repos/{REPOSITORY}":
                result = {"default_branch": "main"}
            elif "/actions/workflows/codequality.yml/runs?" in path:
                result = {"workflow_runs": [QUALITY] if qualities is None else qualities}
            elif "/check-runs?" in path:
                result = {"check_runs": [CHECK] if checks is None else checks}
            elif "/actions/workflows/validate-workload-contract.yml/runs?" in path:
                result = {"workflow_runs": [PRODUCER if producer is None else producer]
                          + ([newer_producer] if newer_producer else [])}
            elif "/actions/runs/99/jobs?" in path or "/actions/runs/100/jobs?" in path:
                result = {"jobs": [JOB] if jobs is None else jobs}
            else:
                self.fail(f"Unexpected API request: {path}")
            return json.dumps(result)

        with patch("subprocess.check_output", side_effect=output), patch("time.sleep"):
            GATE(SHA, REPOSITORY)

    def test_exact_quality_and_sha_bound_trusted_contract_allow_publication(self):
        self.run_gate()

    def test_failed_or_wrong_source_quality_and_untrusted_checks_stop_publication(self):
        for variant in (
            {"qualities": [{**QUALITY, "conclusion": "failure"}]},
            {"qualities": [{**QUALITY, "head_sha": "b" * 40}]},
            {"checks": [{**CHECK, "conclusion": "failure"}]},
            {"checks": [{**CHECK, "head_sha": "b" * 40}]},
            {"checks": [{**CHECK, "app": {"id": 999, "slug": "github-actions"}}]},
        ):
            with self.subTest(variant=variant), self.assertRaises(RuntimeError):
                self.run_gate(**variant)

    def test_same_app_check_cannot_borrow_wrong_workflow_or_other_sha_producer(self):
        for variant in (
            {"producer": {**PRODUCER, "path": ".github/workflows/untrusted.yml"}},
            {"producer": {**PRODUCER, "head_branch": "feature/untrusted"}},
            {"jobs": [{**JOB, "name": f"workload-contract ({'b' * 40})"}]},
        ):
            with self.subTest(variant=variant), self.assertRaises(RuntimeError):
                self.run_gate(**variant)

    def test_new_same_app_check_cannot_replay_success_before_newer_producer_failure(self):
        with self.assertRaisesRegex(RuntimeError, "producer did not succeed"):
            self.run_gate(checks=[{**CHECK, "id": 200}],
                          newer_producer={**PRODUCER, "id": 100, "conclusion": "failure"})

    def test_new_same_app_check_must_reference_the_latest_successful_producer(self):
        with self.assertRaisesRegex(RuntimeError, "replays an older producer"):
            self.run_gate(newer_producer={**PRODUCER, "id": 100})


if __name__ == "__main__":
    unittest.main()
