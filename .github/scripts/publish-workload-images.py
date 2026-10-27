import json
import os
import re
import subprocess
from pathlib import Path


def run(*arguments):
    return subprocess.check_output(arguments, text=True).strip()


registry = os.environ["WORKLOAD_ACR_NAME"]
sha = os.environ["SOURCE_SHA"]
if registry != "acr7ca0fd3326df" or not re.fullmatch(r"[0-9a-f]{40}", sha):
    raise SystemExit("Publication requires the reviewed registry and an exact source revision.")
login = json.loads(run("az", "acr", "login", "--name", registry, "--expose-token", "--output", "json"))
subprocess.run(
    ["docker", "login", f"{registry}.azurecr.io", "--username",
     "00000000-0000-0000-0000-000000000000", "--password-stdin"],
    input=login["accessToken"], text=True, check=True
)
del login
images = {}
for kind in ("web", "postgres"):
    repository = f"workloads/trip-side-kick/{kind}"
    tag = f"{repository}:sha-{sha}"
    existing = subprocess.run(
        ["az", "acr", "repository", "show", "--name", registry, "--image", tag, "--output", "json"],
        text=True, capture_output=True, check=False
    )
    if existing.returncode == 0:
        metadata = json.loads(existing.stdout)
        if any(metadata["changeableAttributes"][key] for key in ("writeEnabled", "deleteEnabled")):
            raise SystemExit("An existing source tag is not immutable; refusing to overwrite it.")
        digest = metadata["digest"]
    else:
        # Authentication, authorization, connectivity and registry failures must never look absent.
        if "MANIFEST_UNKNOWN" not in existing.stderr:
            raise SystemExit("Could not establish that the source tag is absent; publication stopped.")
        target = f"{registry}.azurecr.io/{tag}"
        dockerfile = "Dockerfile" if kind == "web" else "deployment/postgres.Dockerfile"
        subprocess.run(["docker", "build", "--file", dockerfile, "--tag", target, "."], check=True)
        subprocess.run(["docker", "push", target], check=True)
        digest = run("az", "acr", "repository", "show", "--name", registry, "--image", tag,
                     "--query", "digest", "--output", "tsv")
        subprocess.run(["az", "acr", "repository", "update", "--name", registry, "--image", tag,
                        "--write-enabled", "false", "--delete-enabled", "false", "--output", "none"], check=True)
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise SystemExit("Registry did not return an immutable image digest.")
    subprocess.run(["az", "acr", "repository", "update", "--name", registry,
                    "--image", f"{repository}@{digest}", "--delete-enabled", "false", "--output", "none"], check=True)
    attributes = json.loads(run("az", "acr", "repository", "show", "--name", registry,
                                "--image", tag, "--query", "changeableAttributes", "--output", "json"))
    if attributes["writeEnabled"] or attributes["deleteEnabled"]:
        raise SystemExit("Source tag lock verification failed.")
    images[f"trip-side-kick-{kind}"] = f"{registry}.azurecr.io/{repository}@{digest}"
Path("promotion").mkdir(exist_ok=True)
Path("promotion/payload.json").write_text(json.dumps({"source_sha": sha, "images": images}) + "\n", encoding="utf-8")
