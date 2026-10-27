import json
import os
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


def run(*arguments):
    return subprocess.check_output(arguments, text=True).strip()


registry = os.environ["WORKLOAD_ACR_NAME"]
sha = os.environ["SOURCE_SHA"]
if registry != "acr7ca0fd3326df" or not re.fullmatch(r"[0-9a-f]{40}", sha):
    raise SystemExit("Publication requires the reviewed registry and an exact source revision.")
login = json.loads(run("az", "acr", "login", "--name", registry, "--expose-token", "--output", "json"))
refresh_token = login["accessToken"]
subprocess.run(
    ["docker", "login", f"{registry}.azurecr.io", "--username",
     "00000000-0000-0000-0000-000000000000", "--password-stdin"],
    input=refresh_token, text=True, check=True
)
del login
images = {}
for kind in ("web", "postgres"):
    repository = f"workloads/trip-side-kick/{kind}"
    tag = f"{repository}:sha-{sha}"
    token_request = urllib.request.Request(
        f"https://{registry}.azurecr.io/oauth2/token",
        data=urllib.parse.urlencode({
            "grant_type": "refresh_token", "service": f"{registry}.azurecr.io",
            "scope": f"repository:{repository}:pull,push", "refresh_token": refresh_token
        }).encode(),
        method="POST"
    )
    with urllib.request.urlopen(token_request, timeout=30) as response:
        token = json.load(response)["access_token"]
    manifest_request = urllib.request.Request(
        f"https://{registry}.azurecr.io/v2/{repository}/manifests/sha-{sha}",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.v2+json, application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.list.v2+json"
        },
        method="HEAD"
    )
    try:
        with urllib.request.urlopen(manifest_request, timeout=30) as response:
            exists = response.status == 200
            if not exists:
                raise SystemExit("Unexpected registry manifest status; publication stopped.")
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise SystemExit(f"Registry manifest lookup failed with HTTP {error.code}; publication stopped.") from None
        exists = False
    if exists:
        metadata = json.loads(run("az", "acr", "repository", "show", "--name", registry,
                                  "--image", tag, "--output", "json"))
        if any(metadata["changeableAttributes"][key] for key in ("writeEnabled", "deleteEnabled")):
            raise SystemExit("An existing source tag is not immutable; refusing to overwrite it.")
        digest = metadata["digest"]
    else:
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
