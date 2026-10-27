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

    def test_missing_established_credentials_are_not_recreated(self):
        existing = {"properties": {"attributes": {}}}
        for password, certificate in ((None, existing), (existing, None)):
            with self.subTest(password=password, certificate=certificate), \
                    patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                    patch.object(bootstrap, "discover", return_value=self.discovered), \
                    patch.object(bootstrap, "metadata", side_effect=[existing, password, certificate]), \
                    patch.object(bootstrap, "rotate_certificate") as rotate, \
                    patch.object(bootstrap, "publish") as publish:
                with self.assertRaisesRegex(bootstrap.BootstrapError, "explicit recovery"):
                    bootstrap.main()
                rotate.assert_not_called()
                publish.assert_not_called()

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

    def test_pending_initialization_survives_repeated_bootstrap_before_activation(self):
        config = {"Sql": {"InitializeOnStartup": False}}
        pending = {"Sql": {"InitializeOnStartup": True}}
        digest = bootstrap.hashlib.sha256(bootstrap.json.dumps(pending, sort_keys=True).encode()).hexdigest()
        existing = {
            "tags": {"configuration-sha256": digest, "schema-initialization": "pending"},
            "properties": {"attributes": {}},
        }
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(bootstrap, "discover", return_value=(*self.discovered[:-1], config)), \
                patch.object(bootstrap, "metadata", return_value=existing), \
                patch.object(bootstrap, "rotate_certificate"), \
                patch.object(bootstrap, "publish") as publish:
            bootstrap.main()
        publish.assert_not_called()
        self.assertTrue(config["Sql"]["InitializeOnStartup"])

    def test_explicit_completion_only_publishes_configuration_and_persists_across_retries(self):
        config = {"Sql": {"InitializeOnStartup": False}}
        existing = {"tags": {"schema-initialization": "pending"}, "properties": {"attributes": {}}}
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(bootstrap, "discover", side_effect=lambda: (
                    *self.discovered[:-1], {"Sql": {"InitializeOnStartup": False}}
                )), \
                patch.object(bootstrap, "metadata", return_value=existing) as metadata, \
                patch.object(bootstrap, "rotate_certificate") as rotate, \
                patch.object(bootstrap, "publish") as publish:
            bootstrap.main(complete_initialization=True)
            self.assertEqual(metadata.call_args_list[0].args[1], "runtime-configuration")
            self.assertEqual(metadata.call_count, 1)
            self.assertEqual(publish.call_count, 1)
            self.assertEqual(bootstrap.json.loads(publish.call_args.args[4]), config)
            existing["tags"] = publish.call_args.kwargs["tags"]
            self.assertEqual(existing["tags"]["schema-initialization"], "complete")
            publish.reset_mock()
            bootstrap.main()
            publish.assert_not_called()
        rotate.assert_called_once()

    def test_completion_cannot_create_or_adopt_unknown_initialization_state(self):
        for existing in (None, {"tags": {}, "properties": {"attributes": {}}}):
            with self.subTest(existing=existing), \
                    patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                    patch.object(bootstrap, "discover", return_value=(
                        *self.discovered[:-1], {"Sql": {"InitializeOnStartup": False}}
                    )), \
                    patch.object(bootstrap, "metadata", return_value=existing), \
                    patch.object(bootstrap, "rotate_certificate") as rotate, \
                    patch.object(bootstrap, "publish") as publish:
                with self.assertRaises(bootstrap.BootstrapError):
                    bootstrap.main(complete_initialization=True)
                rotate.assert_not_called()
                publish.assert_not_called()

    def test_discovery_uses_the_committed_runtime_inputs(self):
        config = bootstrap.json.loads((bootstrap.ROOT / "deployment/runtime.example.json").read_text())
        policy = bootstrap.json.loads((bootstrap.ROOT / ".github/validation/policy.json").read_text())
        tenant = config["AzureAd"]["TenantId"]
        vault = {
            "id": "/vault",
            "tags": {"Workload": policy["workloadId"], "Environment": "prd", "Purpose": "workload-secrets"},
            "properties": {"tenantId": tenant},
        }
        with patch.object(bootstrap, "az_json", side_effect=[
            vault, {"id": "app-object", "appId": config["AzureAd"]["ClientId"], "signInAudience": "AzureADMyOrg"},
            {"tenantId": tenant}, {"accessToken": "token"},
        ]):
            discovered = bootstrap.discover()
        self.assertEqual(discovered[0], policy["workloadId"])
        self.assertEqual(discovered[1], vault["id"])
        self.assertEqual(discovered[-1]["ForwardedHeaders"]["KnownProxies"], [])

    def test_non_main_execution_cannot_discover_or_mutate_credentials(self):
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/feature/untrusted"}), \
                patch.object(bootstrap, "discover") as discover:
            with self.assertRaisesRegex(bootstrap.BootstrapError, "approved main"):
                bootstrap.main()
        discover.assert_not_called()

    def test_unchanged_reviewed_configuration_does_not_create_versions_or_restarts(self):
        digest = bootstrap.hashlib.sha256(bootstrap.json.dumps(
            self.discovered[-1], sort_keys=True).encode()).hexdigest()
        existing = {"tags": {"configuration-sha256": digest}, "properties": {"attributes": {}}}
        with patch.dict(bootstrap.os.environ, {"GITHUB_REF": "refs/heads/main"}), \
                patch.object(bootstrap, "discover", return_value=self.discovered), \
                patch.object(bootstrap, "metadata", return_value=existing), \
                patch.object(bootstrap, "rotate_certificate"), \
                patch.object(bootstrap, "publish") as publish:
            bootstrap.main()
        publish.assert_not_called()

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

    def test_due_owned_certificate_renews_after_binding_validation(self):
        self.exercise_new_certificate(renewal=True)

    def test_revoked_foreign_or_shortened_registration_cannot_restore_access(self):
        expiry = self.now + timedelta(days=90)
        existing = {
            "tags": {"application-id": self.app["appId"], "application-object-id": self.app["id"],
                     "credential-key-id": "owned-key"},
            "properties": {"attributes": {"exp": int(expiry.timestamp())}},
        }
        for registered in (
            [],
            [{"keyId": "owned-key", "displayName": "foreign", "endDateTime": expiry.isoformat()}],
            [{"keyId": "owned-key", "displayName": "workload-ns346663-owned",
              "endDateTime": (expiry - timedelta(days=1)).isoformat()}],
        ):
            with self.subTest(registered=registered), \
                    patch.object(bootstrap, "metadata", return_value=existing), \
                    patch.object(bootstrap, "az_json", return_value=registered), \
                    patch.object(bootstrap, "command") as command, \
                    patch.object(bootstrap, "publish") as publish:
                with self.assertRaisesRegex(bootstrap.BootstrapError, "explicit recovery"):
                    bootstrap.rotate_certificate("workload", "/vault", self.app, "tenant", "token", self.now)
                command.assert_not_called()
                publish.assert_not_called()

    def exercise_new_certificate(self, authentication_error=None, renewal=False):
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

        registered = [{"keyId": "unrelated", "displayName": "another-owner", "endDateTime": "2020-01-01T00:00:00Z"}]
        existing = None
        if renewal:
            expiry = self.now + timedelta(days=10)
            registered.append({
                "keyId": "old-key", "displayName": "workload-ns346663-old", "endDateTime": expiry.isoformat(),
            })
            existing = {
                "tags": {"application-id": self.app["appId"], "application-object-id": self.app["id"],
                         "credential-key-id": "old-key"},
                "properties": {"attributes": {"exp": int(expiry.timestamp())}},
            }
        az = Mock(side_effect=[
            registered,
            None,
            [{"keyId": "new-key", "displayName": "workload-ns346663-fresh"}],
        ])
        calls.attach_mock(Mock(side_effect=authentication_error), "authenticate")
        calls.attach_mock(Mock(), "publish")
        with patch.object(bootstrap, "metadata", return_value=existing), \
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
