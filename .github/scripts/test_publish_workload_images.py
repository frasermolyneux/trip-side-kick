import json
import os
from pathlib import Path
import runpy
import unittest
from unittest.mock import MagicMock, patch
from urllib.error import HTTPError


SCRIPT = Path(__file__).with_name("publish-workload-images.py")
DIGEST = "sha256:" + "1" * 64


class PublicationTests(unittest.TestCase):
    def run_publisher(self, status, token_failure=False):
        response = MagicMock()
        response.status = status
        response.read.return_value = json.dumps({"access_token": "disposable-test-token"}).encode()
        response.__enter__.return_value = response
        failure = HTTPError("https://registry.invalid/", status, "test", {}, None)
        requests = [failure] if token_failure else [
            response, failure if status >= 400 else response,
            response, failure if status >= 400 else response
        ]

        def output(*arguments, **_):
            command = arguments[0]
            if "login" in command:
                return json.dumps({"accessToken": "disposable-test-token"})
            if "--query" in command:
                return DIGEST if "digest" in command else json.dumps(
                    {"writeEnabled": False, "deleteEnabled": False})
            return json.dumps({
                "digest": DIGEST,
                "changeableAttributes": {"writeEnabled": False, "deleteEnabled": False}
            })

        with patch.dict(os.environ, {"WORKLOAD_ACR_NAME": "acr7ca0fd3326df", "SOURCE_SHA": "a" * 40}), \
                patch("subprocess.check_output", side_effect=output), \
                patch("subprocess.run") as execute, \
                patch("urllib.request.urlopen", side_effect=requests), \
                patch("pathlib.Path.mkdir"), patch("pathlib.Path.write_text"):
            self.execute = execute
            runpy.run_path(str(SCRIPT))
        return execute

    def builds(self):
        return [call for call in self.execute.call_args_list
                if call.args[0][:2] == ["docker", "build"] or call.args[0][1] == "deployment/build_web_image.py"]

    def test_only_missing_manifests_build_both_images(self):
        self.run_publisher(404)
        self.assertEqual(2, len(self.builds()))

    def test_existing_immutable_manifests_are_reused(self):
        self.run_publisher(200)
        self.assertEqual([], self.builds())

    def test_registry_failures_cannot_be_treated_as_missing(self):
        for status in (401, 403, 429, 500):
            with self.subTest(status=status), self.assertRaises(SystemExit):
                self.run_publisher(status)
            self.assertEqual([], self.builds())
            self.assertEqual(1, self.execute.call_count)

    def test_token_failure_cannot_be_treated_as_missing(self):
        with self.assertRaises(HTTPError):
            self.run_publisher(403, token_failure=True)
        self.assertEqual([], self.builds())

    def test_unexpected_success_status_fails_closed(self):
        with self.assertRaises(SystemExit):
            self.run_publisher(201)
        self.assertEqual([], self.builds())


if __name__ == "__main__":
    unittest.main()
