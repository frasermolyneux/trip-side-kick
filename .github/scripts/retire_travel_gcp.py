"""Revoke only the retired repository's federation after its owned stacks are gone."""

import copy
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
from urllib.request import Request, urlopen


PROJECT = "gcp-mx-io-travel-itinerary"
NUMBER = "776197093213"
ACCOUNT = f"terraform-platform@{PROJECT}.iam.gserviceaccount.com"
PROVIDER = f"projects/{NUMBER}/locations/global/workloadIdentityPools/github-actions/providers/github-provider"
PREFIX = f"principalSet://iam.googleapis.com/projects/{NUMBER}/locations/global/workloadIdentityPools/github-actions/attribute.repository/"
TRAVEL = PREFIX + "frasermolyneux/travel-itinerary"
TRIP = PREFIX + "frasermolyneux/trip-side-kick"
OLD = ("assertion.repository == 'frasermolyneux/platform-workloads' || "
       "assertion.repository == 'frasermolyneux/travel-itinerary' || "
       "assertion.repository == 'frasermolyneux/trip-side-kick'")
DESIRED = ("assertion.repository == 'frasermolyneux/platform-workloads' || "
           "assertion.repository == 'frasermolyneux/trip-side-kick'")
MAPPING = {
    "google.subject": "assertion.sub",
    "attribute.repository": "assertion.repository",
    "attribute.repository_owner": "assertion.repository_owner",
}
REPOSITORY = "frasermolyneux/travel-itinerary"
GUARDED_RETIREMENT = "34c7b27ef4d5c594c97885d168bca61c8a154d85"


class GuardFailure(ValueError):
    """An authored prerequisite message safe to emit without service output."""


def github(path):
    token = os.environ.get("GH_TOKEN")
    if not token:
        raise GuardFailure("Read-only retirement evidence access is required.")
    request = Request(
        f"https://api.github.com/repos/{REPOSITORY}/{path}",
        headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json"},
    )
    try:
        with urlopen(request, timeout=30) as response:
            return json.load(response)
    except Exception:
        raise GuardFailure("Retirement evidence could not be verified; output withheld.") from None


def validate_retirements():
    runs = github(
        "actions/workflows/destroy-environment.yml/runs"
        "?branch=main&event=workflow_dispatch&status=success&per_page=30"
    )
    environments = set()
    required = {
        "Stop if retirement would destroy retained Google resources",
        "Apply only the validated retirement plan",
        "Assert workload resource group is empty",
    }
    for run in runs.get("workflow_runs", []):
        if (run.get("conclusion") != "success" or run.get("head_branch") != "main"
                or run.get("event") != "workflow_dispatch"):
            continue
        jobs = github(f"actions/runs/{int(run['id'])}/jobs?filter=latest&per_page=100")
        for job in jobs.get("jobs", []):
            successful = {step["name"] for step in job.get("steps", [])
                          if step.get("conclusion") == "success"}
            if (job.get("name") != "terraform-destroy" or job.get("conclusion") != "success"
                    or not required <= successful):
                continue
            check_id = int(job["check_run_url"].rsplit("/", 1)[-1])
            check = github(f"check-runs/{check_id}")
            environment = (check.get("deployment") or {}).get("environment")
            if (environment not in ("Development", "Production")
                    or check.get("head_sha") != run.get("head_sha")
                    or check.get("conclusion") != "success"):
                continue
            comparison = github(f"compare/{GUARDED_RETIREMENT}...{run['head_sha']}?per_page=1")
            if comparison.get("status") in ("identical", "ahead"):
                environments.add(environment)
        if environments == {"Development", "Production"}:
            return
    raise GuardFailure("Both guarded Travel destroys and empty inventories must succeed first.")


def validate_provider(provider):
    if (provider.get("state") != "ACTIVE"
            or provider.get("oidc", {}).get("issuerUri") != "https://token.actions.githubusercontent.com"
            or provider.get("attributeMapping") != MAPPING
            or provider.get("attributeCondition") not in (OLD, DESIRED)):
        raise GuardFailure("Federation differs from the reviewed retirement contract.")


def retired_policy(policy):
    if not any(
        entry.get("role") == "roles/iam.workloadIdentityUser"
        and "condition" not in entry and TRIP in entry.get("members", [])
        for entry in policy.get("bindings", [])
    ):
        raise GuardFailure("Replacement repository trust is missing.")
    desired = copy.deepcopy(policy)
    desired["bindings"] = [
        {**entry, "members": [member for member in entry["members"] if member != TRAVEL]}
        for entry in desired.get("bindings", [])
        if any(member != TRAVEL for member in entry.get("members", []))
    ]
    return desired


def gcloud(*arguments):
    result = subprocess.run(
        ["gcloud", *arguments, "--quiet", "--format=json"],
        capture_output=True, text=True, check=False,
    )
    if result.returncode:
        length = (4 if arguments[:2] == ("iam", "workload-identity-pools")
                  else 3 if arguments[0] in ("iam", "billing") else 2)
        operation = " ".join(arguments[:length])
        diagnostic = result.stderr.lower()
        if (any(marker in diagnostic for marker in ("service_disabled", "has not been used"))
                or re.search(r"\bapi(?:\s+\[[^\]\r\n]+\])?\s+(?:is\s+)?(?:not enabled|disabled)\b",
                             diagnostic)):
            category = "requires an enabled API"
        elif any(marker in diagnostic for marker in (
                "invalid_grant", "unauthorized_client", "insufficient authentication scopes",
                "unauthenticated", "invalid authentication credentials", "invalid credentials",
                "access_token_scope_insufficient")):
            category = "authentication rejected"
        elif (any(marker in diagnostic for marker in (
                "permission_denied", "does not have permission", "does not have required permission",
                "does not have sufficient permission", "insufficient permission"))
                or re.search(r"\bpermission\b[^\r\n]*\bdenied\b", diagnostic)):
            category = "permission denied"
        elif any(marker in diagnostic for marker in ("invalid choice", "unrecognized arguments")):
            category = "command unavailable"
        else:
            category = "failed"
        raise GuardFailure(f"Google {operation} {category}; diagnostic output withheld.")
    return json.loads(result.stdout) if result.stdout.strip() else None


def provider():
    return gcloud("iam", "workload-identity-pools", "providers", "describe", "github-provider",
                  f"--project={PROJECT}", "--location=global", "--workload-identity-pool=github-actions")


def retained():
    project = gcloud("projects", "describe", PROJECT)
    billing = gcloud("billing", "projects", "describe", PROJECT)
    services = gcloud("services", "list", "--enabled", f"--project={PROJECT}")
    enabled = {entry["config"]["name"] for entry in services}
    if (str(project.get("projectNumber")) != NUMBER or project.get("lifecycleState") != "ACTIVE"
            or billing.get("billingEnabled") is not True
            or not {"maps-backend.googleapis.com", "places.googleapis.com"} <= enabled):
        raise GuardFailure("Retained project, billing or APIs are not healthy.")
    return billing["billingAccountName"]


def main():
    if (os.environ.get("GITHUB_REF") != "refs/heads/main"
            or os.environ.get("CONFIGURED_PROJECT") != PROJECT
            or os.environ.get("CONFIGURED_PROVIDER") != PROVIDER
            or os.environ.get("CONFIGURED_ACCOUNT") != ACCOUNT):
        raise GuardFailure("Retirement requires the reviewed main/project/account.")
    validate_retirements()
    print("Verified both actual guarded Travel destroys and empty inventories.", flush=True)
    billing = retained()
    before = provider()
    validate_provider(before)
    policy = gcloud("iam", "service-accounts", "get-iam-policy", ACCOUNT, f"--project={PROJECT}")
    desired = retired_policy(policy)
    if desired != policy:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "policy.json"
            path.write_text(json.dumps(desired), encoding="utf-8")
            path.chmod(0o600)
            gcloud("iam", "service-accounts", "set-iam-policy", ACCOUNT, str(path),
                   f"--project={PROJECT}")
    if before["attributeCondition"] != DESIRED:
        gcloud("iam", "workload-identity-pools", "providers", "update-oidc", "github-provider",
               f"--project={PROJECT}", "--location=global",
               "--workload-identity-pool=github-actions", f"--attribute-condition={DESIRED}")
    after = provider()
    validate_provider(after)
    policy = gcloud("iam", "service-accounts", "get-iam-policy", ACCOUNT, f"--project={PROJECT}")
    if (after["attributeCondition"] != DESIRED or retired_policy(policy) != policy
            or retained() != billing):
        raise GuardFailure("Retirement verification failed.")
    print("Verified retained project/billing/APIs and Trip trust; retired only Travel federation.")


if __name__ == "__main__":
    try:
        main()
    except GuardFailure as error:
        raise SystemExit(str(error)) from None
    except (ValueError, RuntimeError, KeyError, TypeError):
        raise SystemExit("Google federation retirement stopped; output withheld. Review before retrying.") from None
