import importlib.util
from datetime import datetime, timedelta, timezone
from pathlib import Path
import unittest
from unittest.mock import Mock, patch


spec = importlib.util.spec_from_file_location("bootstrap_runtime", Path(__file__).with_name("bootstrap_runtime.py"))
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)


class RuntimeBootstrapTests(unittest.TestCase):
    def setUp(self):
        self.app = {"id": "application-object", "appId": "application-client"}
        self.now = datetime(2026, 10, 27, tzinfo=timezone.utc)
        self.discovered = ("workload", "/vault", self.app, "tenant", "arm-token", {"reviewed": True})

    def test_existing_database_password_is_never_replaced(self):
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(bootstrap, "discover", return_value=self.discovered), \
                patch.object(bootstrap, "metadata", return_value={"properties": {"attributes": {}}}), \
                patch.object(bootstrap, "rotate_certificate") as rotate, \
                patch.object(bootstrap, "publish") as publish:
            bootstrap.main()
        rotate.assert_called_once()
        self.assertEqual(publish.call_count, 1)
        self.assertEqual(publish.call_args.args[1], "runtime-configuration")

    def test_expiring_database_password_requires_explicit_recovery(self):
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(bootstrap, "discover", return_value=self.discovered), \
                patch.object(bootstrap, "metadata", return_value={"properties": {"attributes": {"exp": 1}}}), \
                patch.object(bootstrap, "rotate_certificate") as rotate, \
                patch.object(bootstrap, "publish") as publish:
            with self.assertRaisesRegex(bootstrap.BootstrapError, "coordinated rotation"):
                bootstrap.main()
        publish.assert_not_called()
        rotate.assert_not_called()

    def test_initial_trip_configuration_enables_schema_bootstrap_once(self):
        config = {"Sql": {"InitializeOnStartup": False}}
        discovered = (*self.discovered[:-1], config)
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(bootstrap, "discover", return_value=discovered), \
                patch.object(bootstrap, "metadata", return_value=None), \
                patch.object(bootstrap, "rotate_certificate"), \
                patch.object(bootstrap, "publish") as publish:
            bootstrap.main()
        published = bootstrap.json.loads(publish.call_args.args[4])
        self.assertTrue(published["Sql"]["InitializeOnStartup"])
        self.assertEqual(publish.call_args_list[0].args[1], "database-password")

    def test_non_main_execution_cannot_discover_or_mutate_credentials(self):
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/feature/untrusted"}), \
                patch.object(bootstrap, "discover") as discover:
            with self.assertRaisesRegex(bootstrap.BootstrapError, "approved main"):
                bootstrap.main()
        discover.assert_not_called()

    def test_foreign_or_disabled_secret_is_not_adopted(self):
        for tags, enabled in (({"managed-by": "other"}, True), ({"managed-by": "workload"}, False)):
            with self.subTest(tags=tags, enabled=enabled), patch.object(
                bootstrap, "request", return_value={
                    "tags": tags, "properties": {"attributes": {"enabled": enabled}}
                }
            ):
                with self.assertRaisesRegex(bootstrap.BootstrapError, "explicit recovery"):
                    bootstrap.metadata("/vault", "database-password", "token", "workload")

    def test_registered_valid_certificate_is_retained_without_reset(self):
        expiry = self.now + timedelta(days=90)
        existing = {
            "tags": {"application-id": self.app["appId"], "application-object-id": self.app["id"],
                     "credential-key-id": "owned-key"},
            "properties": {"attributes": {"exp": int(expiry.timestamp())}},
        }
        with patch.object(bootstrap, "metadata", return_value=existing), \
                patch.object(bootstrap, "az_json", return_value=[{
                    "keyId": "owned-key", "displayName": "workload-ns346663-owned",
                    "endDateTime": expiry.isoformat(),
                }]) as az, patch.object(bootstrap, "publish") as publish:
            bootstrap.rotate_certificate("workload", "/vault", self.app, "tenant", "token", self.now)
        self.assertEqual(az.call_count, 1)
        publish.assert_not_called()

    def test_failed_authentication_never_publishes_private_material(self):
        self.exercise_new_certificate(authentication_error=bootstrap.BootstrapError("authentication rejected"))

    def test_new_certificate_authenticates_before_publication_and_keeps_unrelated_keys(self):
        self.exercise_new_certificate()

    def exercise_new_certificate(self, authentication_error=None):
        calls = Mock()

        def fake_command(*arguments, **kwargs):
            if arguments[1:3] == ("req", "-x509"):
                Path(arguments[arguments.index("-keyout") + 1]).write_text("private-key")
                Path(arguments[arguments.index("-out") + 1]).write_text("public-certificate")
            elif arguments[1] == "x509":
                return "notAfter=Apr 25 00:00:00 2027 GMT\n"
            elif arguments[1] == "pkcs12":
                Path(arguments[arguments.index("-out") + 1]).write_bytes(b"private-pfx")
            return ""

        az = Mock(side_effect=[
            [{"keyId": "unrelated", "displayName": "another-owner", "endDateTime": "2020-01-01T00:00:00Z"}],
            None,
            [{"keyId": "new-key", "displayName": "workload-ns346663-fresh"}],
        ])
        calls.attach_mock(Mock(side_effect=authentication_error), "authenticate")
        calls.attach_mock(Mock(), "publish")
        with patch.object(bootstrap, "metadata", return_value=None), \
                patch.object(bootstrap, "az_json", az), \
                patch.object(bootstrap, "command", side_effect=fake_command), \
                patch.object(bootstrap.uuid, "uuid4", return_value="fresh"), \
                patch.object(bootstrap, "verify_authentication", calls.authenticate), \
                patch.object(bootstrap, "publish", calls.publish):
            if authentication_error:
                with self.assertRaisesRegex(bootstrap.BootstrapError, "authentication rejected"):
                    bootstrap.rotate_certificate("workload", "/vault", self.app, "tenant", "token", self.now)
                calls.publish.assert_not_called()
            else:
                bootstrap.rotate_certificate("workload", "/vault", self.app, "tenant", "token", self.now)
                self.assertEqual([call[0] for call in calls.mock_calls], ["authenticate", "publish"])
                self.assertEqual(calls.publish.call_args.kwargs["tags"]["credential-key-id"], "new-key")
        self.assertTrue(all("delete" not in call.args for call in az.call_args_list))
        self.assertIn("--append", az.call_args_list[1].args)


if __name__ == "__main__":
    unittest.main()
