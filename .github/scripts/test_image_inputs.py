import importlib.util
from pathlib import Path
import unittest
from unittest.mock import Mock, patch


SPEC = importlib.util.spec_from_file_location("image_inputs", Path(__file__).with_name("image-inputs.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
BEFORE, AFTER = "a" * 40, "b" * 40


class ImageInputTests(unittest.TestCase):
    def test_multi_commit_push_uses_complete_range_not_final_commit(self):
        with patch("subprocess.run", return_value=Mock(returncode=0)), \
                patch("subprocess.check_output", return_value="src/changed.cs\nREADME.md\n") as diff:
            self.assertTrue(MODULE.should_publish({"before": BEFORE, "after": AFTER}, AFTER, r"^src/"))
        diff.assert_called_once_with(["git", "diff", "--name-only", BEFORE, AFTER], text=True)

    def test_digest_only_promotion_does_not_create_publication_loop(self):
        with patch("subprocess.run", return_value=Mock(returncode=0)), \
                patch("subprocess.check_output", return_value="workload.json\n"):
            self.assertFalse(MODULE.should_publish({"before": BEFORE, "after": AFTER}, AFTER, r"^src/"))

    def test_missing_pre_push_history_never_silently_skips_images(self):
        with patch("subprocess.run", return_value=Mock(returncode=1)):
            self.assertTrue(MODULE.should_publish({"before": BEFORE, "after": AFTER}, AFTER, r"^src/"))

    def test_initial_push_publishes_without_a_parent(self):
        self.assertTrue(MODULE.should_publish({"before": "0" * 40, "after": AFTER}, AFTER, r"^src/"))

    def test_other_source_push_metadata_cannot_skip_publication(self):
        with self.assertRaisesRegex(ValueError, "validated source"):
            MODULE.should_publish({"before": BEFORE, "after": AFTER}, "c" * 40, r"^src/")


if __name__ == "__main__":
    unittest.main()
