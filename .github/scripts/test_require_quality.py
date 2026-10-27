import json
import os
from pathlib import Path
import runpy
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).with_name("require-quality.py")
SHA = "a" * 40


class PublicationGateTests(unittest.TestCase):
    def run_gate(self, conclusion="success", app="github-actions", check_sha=SHA, quality_sha=SHA):
        def output(command, **_):
            if command[1] == "run":
                return json.dumps([{"headSha": quality_sha, "status": "completed", "conclusion": "success"}])
            return json.dumps({"check_runs": [{
                "id": 1, "head_sha": check_sha, "app": {"slug": app},
                "status": "completed", "conclusion": conclusion
            }]})

        with patch.dict(os.environ, {"SOURCE_SHA": SHA, "GITHUB_REPOSITORY": "owner/repository"}), \
                patch("subprocess.check_output", side_effect=output), patch("time.sleep"):
            runpy.run_path(str(SCRIPT))

    def test_exact_quality_and_trusted_contract_allow_publication(self):
        self.run_gate()

    def test_failed_contract_blocks_publication(self):
        with self.assertRaisesRegex(SystemExit, "trusted workload contract did not pass"):
            self.run_gate(conclusion="failure")

    def test_other_revision_contract_cannot_authorize_publication(self):
        with self.assertRaisesRegex(SystemExit, "Timed out"):
            self.run_gate(check_sha="b" * 40)

    def test_other_application_cannot_spoof_contract_success(self):
        with self.assertRaisesRegex(SystemExit, "Timed out"):
            self.run_gate(app="untrusted-application")

    def test_other_revision_quality_cannot_authorize_publication(self):
        with self.assertRaisesRegex(SystemExit, "Code Quality"):
            self.run_gate(quality_sha="b" * 40)


if __name__ == "__main__":
    unittest.main()
