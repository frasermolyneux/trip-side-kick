import json
import os
from pathlib import Path
import runpy
import unittest
from unittest.mock import Mock, patch


GATE = runpy.run_path(str(Path(__file__).with_name("require-quality.py")))["wait_for_quality"]
SHA = "a" * 40
REPOSITORY = "owner/repository"
QUALITY = {
    "run_started_at": "2026-10-27T18:00:00Z", "run_attempt": 1,
    "id": 10, "head_sha": SHA, "event": "push", "head_branch": "main",
    "path": ".github/workflows/codequality.yml",
    "repository": {"full_name": REPOSITORY}, "status": "completed", "conclusion": "success",
}
CHECK = {
    "id": 20, "head_sha": SHA, "name": "workload-contract",
    "app": {"id": 15368, "slug": "github-actions"},
    "status": "completed", "conclusion": "success",
    "details_url": f"https://github.com/{REPOSITORY}/actions/runs/99",
    "external_id": "trusted-workload-contract:99",
}
PRODUCER = {
    "run_started_at": "2026-10-27T18:00:00Z", "run_attempt": 1,
    "id": 99, "event": "workflow_run", "path": ".github/workflows/validate-workload-contract.yml",
    "display_title": f"Validate workload contract {SHA}",
    "head_branch": "main", "repository": {"full_name": REPOSITORY},
    "status": "completed", "conclusion": "success",
}
JOB = {"id": 30, "name": f"workload-contract ({SHA})", "status": "completed", "conclusion": "success"}


class PublicationGateTests(unittest.TestCase):
    def test_promotion_head_moving_during_contract_wait_prevents_success(self):
        promotion = runpy.run_path(str(Path(__file__).with_name("promote-workload-images.py")))["main"]
        head = "b" * 40
        prefix = "acr7ca0fd3326df.azurecr.io/workloads/trip-side-kick/web@sha256:"
        before = {"services": [{"name": "trip-side-kick-web", "image": prefix + "1" * 64}]}
        after = {"services": [{"name": "trip-side-kick-web", "image": prefix + "2" * 64}]}

        def commands(*args, **_):
            if args[:2] == ("git", "show"):
                return json.dumps(before if args[2].startswith("origin/main:") else after)
            if args[:2] == ("git", "diff"):
                return "workload.json"
            return "main" if args[:3] == ("gh", "pr", "view") else ""

        def github(*args):
            if args[:2] == ("pr", "list"):
                return [{"number": 1, "headRefOid": head, "baseRefName": "main"}]
            return {"baseRefName": "main", "headRefOid": "c" * 40}

        contract = Mock()
        replacements = {
            "run": commands, "gh_json": github, "is_ancestor": lambda *_: True,
            "has_newer_image_inputs": lambda *_: False,
            "has_newer_service_images": lambda *_: False,
            "wait_for_quality": lambda *_: None,
            "wait_for_pull_request_quality": lambda *_: None,
            "wait_for_copilot_review": lambda *_: None,
            "wait_contract": contract,
        }
        with patch.dict(promotion.__globals__, replacements), \
                patch.dict(os.environ, {"GITHUB_REPOSITORY": REPOSITORY}), \
                patch("sys.argv", ["promote", "--source-sha", SHA,
                                   "--image-input-pattern", ".",
                                   "--update", "trip-side-kick-web=" + prefix + "2" * 64]), \
                patch("builtins.print") as output:
            with self.assertRaisesRegex(RuntimeError, "exact head or base changed"):
                promotion()
            contract.assert_called_once_with(head, REPOSITORY)
            output.assert_not_called()

    def run_gate(self, qualities=None, checks=None, producer=None, jobs=None, newer_producer=None,
                 producer_pages=None, quality_pages=None, quality_after_contract=None,
                 partitioned_producers=None, contract_failure_during_quality_wait=False):
        quality_reads = 0
        partition_reads = 0
        contract_reads = 0

        def output(command, **_):
            nonlocal quality_reads, partition_reads, contract_reads
            path = command[-1]
            if path == f"repos/{REPOSITORY}":
                result = {"default_branch": "main", "created_at": "2026-10-27T00:00:00Z"}
            elif "/actions/workflows/codequality.yml/runs?" in path:
                quality_reads += 1
                result = {"workflow_runs": [QUALITY] if qualities is None else qualities}
                if quality_after_contract is not None and quality_reads > 1:
                    result = {"workflow_runs": quality_after_contract}
                if contract_failure_during_quality_wait and quality_reads == 2:
                    result = {"workflow_runs": [{**QUALITY, "status": "in_progress", "conclusion": None}]}
                if quality_pages is not None:
                    result = {"workflow_runs": quality_pages[int(path.rsplit("page=", 1)[1]) - 1]}
            elif "/check-runs?" in path:
                contract_reads += 1
                result = {"check_runs": [CHECK] if checks is None else checks}
                if (quality_after_contract is not None or contract_failure_during_quality_wait) and contract_reads == 1:
                    result = {"check_runs": [{**CHECK, "status": "in_progress", "conclusion": None}]}
                if contract_failure_during_quality_wait and quality_reads >= 3:
                    result = {"check_runs": [{**CHECK, "conclusion": "failure"}]}
            elif "/actions/workflows/validate-workload-contract.yml/runs?" in path:
                result = {"workflow_runs": [PRODUCER if producer is None else producer]
                          + ([newer_producer] if newer_producer else [])}
                if producer_pages is not None:
                    result = {"workflow_runs": producer_pages[int(path.rsplit("page=", 1)[1]) - 1]}
                if partitioned_producers is not None:
                    result = {"workflow_runs": [], "total_count": 1000}
                    if "&created=" in path:
                        result = {"workflow_runs": partitioned_producers[partition_reads]}
                        partition_reads += 1
            elif "/actions/runs/99/jobs?" in path or "/actions/runs/100/jobs?" in path:
                result = {"jobs": [JOB] if jobs is None else jobs}
            else:
                self.fail(f"Unexpected API request: {path}")
            return json.dumps(result)

        with patch("subprocess.check_output", side_effect=output), patch("time.sleep"):
            GATE(SHA, REPOSITORY)

    def test_exact_quality_and_sha_bound_trusted_contract_allow_publication(self):
        self.run_gate()

    def test_quality_failure_during_contract_wait_is_rechecked_before_publication(self):
        with self.assertRaisesRegex(RuntimeError, "Code Quality.*did not succeed"):
            self.run_gate(quality_after_contract=[{**QUALITY, "conclusion": "failure", "run_attempt": 2}])

    def test_contract_failure_during_quality_wait_cannot_reuse_prior_success(self):
        with self.assertRaisesRegex(RuntimeError, "trusted workload contract did not succeed"):
            self.run_gate(contract_failure_during_quality_wait=True)

    def test_capped_history_is_partitioned_without_losing_older_failed_reruns(self):
        failed = {**PRODUCER, "run_started_at": "2026-10-27T19:00:00Z",
                  "run_attempt": 2, "conclusion": "failure"}
        with self.assertRaisesRegex(RuntimeError, "producer did not succeed"):
            self.run_gate(partitioned_producers=[[failed], [{**PRODUCER, "id": 100}]])
        self.run_gate(partitioned_producers=[
            [{**PRODUCER, "display_title": "Another source"}], [PRODUCER]])

    def test_normalized_display_url_cannot_break_or_replace_explicit_producer_binding(self):
        normalized = {**CHECK, "details_url": f"https://github.com/{REPOSITORY}/runs/20"}
        self.run_gate(checks=[normalized])
        for binding in ("", "trusted-workload-contract:100", "untrusted:99"):
            with self.subTest(binding=binding), self.assertRaisesRegex(RuntimeError, "Untrusted"):
                self.run_gate(checks=[{**normalized, "external_id": binding}])

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

    def test_lower_id_rerun_failure_cannot_be_hidden_by_newer_id_success(self):
        for field in ("contract", "quality"):
            failed = {**(PRODUCER if field == "contract" else QUALITY),
                      "run_started_at": "2026-10-27T19:00:00Z", "run_attempt": 2, "conclusion": "failure"}
            with self.subTest(field=field), self.assertRaisesRegex(RuntimeError, "did not succeed"):
                if field == "contract":
                    self.run_gate(producer=failed, newer_producer={**PRODUCER, "id": 100},
                                  checks=[{**CHECK, "details_url": CHECK["details_url"].replace("/99", "/100")}])
                else:
                    self.run_gate(qualities=[failed, {**QUALITY, "id": 11}])

    def test_latest_failed_rerun_on_later_page_rejects_replayed_success(self):
        first = [{**PRODUCER, "id": 100}] + [
            {**PRODUCER, "id": number, "display_title": "Another source"} for number in range(101, 200)
        ]
        failed = {**PRODUCER, "run_started_at": "2026-10-27T19:00:00Z",
                  "run_attempt": 2, "conclusion": "failure"}
        with self.assertRaisesRegex(RuntimeError, "producer did not succeed"):
            self.run_gate(producer_pages=[first, [failed]],
                          checks=[{**CHECK, "details_url": CHECK["details_url"].replace("/99", "/100")}])

    def test_filtered_api_limit_is_not_mistaken_for_complete_history(self):
        for field in ("quality", "contract"):
            full = [QUALITY if field == "quality" else PRODUCER] * 100
            with self.subTest(field=field), self.assertRaisesRegex(RuntimeError, "Too many"):
                self.run_gate(**{
                    "quality_pages" if field == "quality" else "producer_pages": [full] * 10 + [[]]
                })


if __name__ == "__main__":
    unittest.main()
