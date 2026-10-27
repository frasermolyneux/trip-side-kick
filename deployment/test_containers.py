"""CI-only constrained-runtime smoke tests. All credentials/data are disposable."""
import base64
import json
import os
import shutil
import subprocess
import time
from pathlib import Path


def run(*arguments, capture=False, check=True):
    return subprocess.run(arguments, text=True, check=check,
                          stdout=subprocess.PIPE if capture else None,
                          stderr=subprocess.PIPE if capture else None, timeout=90)


prefix = f"tripsidekick-test-{os.getpid()}"
directory = Path(".container-test").resolve()
network = prefix
database, web = f"{prefix}-postgres", f"{prefix}-web"
volumes = [f"{prefix}-{name}" for name in ("postgres", "keys", "documents")]
try:
    directory.mkdir(mode=0o755)
    configuration = json.loads(Path("deployment/runtime.example.json").read_text())
    configuration["Sql"]["ConnectionString"] = "Host=trip-side-kick-postgres;Database=tripsidekick;Username=tripsidekick"
    configuration["Sql"]["InitializeOnStartup"] = True
    (directory / "runtime-configuration").write_text(json.dumps(configuration))
    (directory / "database-password").write_text("disposable-container-tests-only")
    run("openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1",
        "-subj", "/CN=Disposable CI", "-keyout", str(directory / "key.pem"),
        "-out", str(directory / "cert.pem"), capture=True)
    run("openssl", "pkcs12", "-export", "-passout", "pass:", "-inkey", str(directory / "key.pem"),
        "-in", str(directory / "cert.pem"), "-out", str(directory / "client.pfx"), capture=True)
    (directory / "client-certificate").write_text(base64.b64encode((directory / "client.pfx").read_bytes()).decode())
    for path in directory.iterdir():
        path.chmod(0o644)
    for image in ("tripsidekick-web:ci", "tripsidekick-postgres:ci"):
        metadata = json.loads(run("docker", "image", "inspect", image, capture=True).stdout)[0]
        assert not metadata["Config"].get("Volumes"), f"{image} declares forbidden VOLUME metadata"
        assert metadata["Config"]["User"] in ("1654:1654", "999:1654")
    run("docker", "network", "create", "--internal", network, capture=True)
    for volume in volumes:
        run("docker", "volume", "create", volume, capture=True)
    common = ["--detach", "--read-only", "--cap-drop=ALL", "--security-opt=no-new-privileges",
              "--pids-limit=128", "--memory=768m", "--tmpfs=/tmp:rw,nosuid,noexec,size=64m", "--network", network]
    password_mount = f"type=bind,source={directory / 'database-password'},target=/run/secrets/database-password,readonly"
    run("docker", "run", *common, "--name", database, "--network-alias", "trip-side-kick-postgres",
        "--mount", password_mount, "--mount", f"type=volume,source={volumes[0]},target=/var/lib/postgresql/data",
        "tripsidekick-postgres:ci", capture=True)
    for _ in range(60):
        if run("docker", "exec", database, "pg_isready", "-h", "127.0.0.1",
               "-U", "tripsidekick", "-d", "tripsidekick", capture=True, check=False).returncode == 0:
            break
        time.sleep(1)
    else:
        raise RuntimeError("Non-root PostgreSQL bootstrap did not become ready.")
    run("docker", "run", *common, "--name", web,
        "--mount", password_mount,
        "--mount", f"type=bind,source={directory / 'runtime-configuration'},target=/run/secrets/runtime-configuration,readonly",
        "--mount", f"type=bind,source={directory / 'client-certificate'},target=/run/secrets/client-certificate,readonly",
        "--mount", f"type=volume,source={volumes[1]},target=/app/state/keys",
        "--mount", f"type=volume,source={volumes[2]},target=/app/state/documents",
        "tripsidekick-web:ci", capture=True)
    for _ in range(30):
        if run("docker", "exec", web, "dotnet", "MX.TripSideKick.Web.dll", "--readiness",
               capture=True, check=False).returncode == 0:
            break
        time.sleep(1)
    else:
        raise RuntimeError("Production web startup, migration or readiness failed.")
    assert run("docker", "exec", "--env", "Health__Host=untrusted.invalid", web,
               "dotnet", "MX.TripSideKick.Web.dll", "--healthcheck",
               capture=True, check=False).returncode != 0
    run("docker", "restart", web, capture=True)
    for _ in range(60):
        if run("docker", "exec", web, "dotnet", "MX.TripSideKick.Web.dll", "--readiness",
               capture=True, check=False).returncode == 0:
            break
        time.sleep(1)
    else:
        raise RuntimeError("Idempotent initialization after restart failed.")
    backup = run("docker", "exec", "--env", "PGPASSWORD=disposable-container-tests-only", database,
                 "pg_dump", "--host=127.0.0.1", "--username=tripsidekick", "--dbname=tripsidekick",
                 "--schema-only", capture=True).stdout
    assert 'CREATE TABLE public."Trips"' in backup
    run("docker", "stop", database, capture=True)
    assert run("docker", "exec", web, "dotnet", "MX.TripSideKick.Web.dll", "--readiness",
               capture=True, check=False).returncode != 0
    run("docker", "exec", web, "dotnet", "MX.TripSideKick.Web.dll", "--healthcheck", capture=True)
    print("Non-root read-only images, migration restart, host checks, backup and database-loss probes passed.")
except Exception:
    for container in (web, database):
        logs = run("docker", "logs", "--tail", "30", container, capture=True, check=False)
        print(logs.stdout, logs.stderr)
    raise
finally:
    for container in (web, database):
        run("docker", "rm", "--force", container, capture=True, check=False)
    for volume in volumes:
        run("docker", "volume", "rm", volume, capture=True, check=False)
    run("docker", "network", "rm", network, capture=True, check=False)
    shutil.rmtree(directory, ignore_errors=True)
