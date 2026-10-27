"""Fail closed unless reviewed workflow producers validated this exact source."""

import json
from datetime import datetime, timedelta, timezone
import os
import re
import subprocess
import time


ACTIONS_APP_ID = 15368
CONTRACT_WORKFLOW = ".github/workflows/validate-workload-contract.yml"


def gh(path):
    environment = os.environ.copy()
    if environment.get("GH_READ_TOKEN"):
        environment["GH_TOKEN"] = environment["GH_READ_TOKEN"]
    return json.loads(subprocess.check_output(["gh", "api", path], text=True, env=environment))


def execution_order(run):
    started = datetime.fromisoformat(run["run_started_at"].replace("Z", "+00:00"))
    return started, run["run_attempt"], run["id"]


def complete_workflow_runs(path, start=None, end=None):
    results = []
    window = "" if start is None else (
        f"&created={start:%Y-%m-%dT%H:%M:%SZ}..{end:%Y-%m-%dT%H:%M:%SZ}")
    for page in range(1, 11):
        response = gh(f"{path}{window}&per_page=100&page={page}")
        if response.get("total_count", 0) >= 1000:
            break
        batch = response["workflow_runs"]
        results.extend(batch)
        if len(batch) < 100:
            return results
    if start is None:
        repository = gh(path.split("/actions/", 1)[0])
        start = datetime.fromisoformat(repository["created_at"].replace("Z", "+00:00"))
        end = datetime.now(timezone.utc).replace(microsecond=0)
    if (end - start).total_seconds() < 1:
        raise RuntimeError("Too many workflow producers in one creation-time second.")
    middle = start + timedelta(seconds=int((end - start).total_seconds()) // 2)
    return (complete_workflow_runs(path, start, middle)
            + complete_workflow_runs(path, middle + timedelta(seconds=1), end))


def latest_contract_producer(sha, repository, default_branch):
    candidates = []
    runs = complete_workflow_runs(
        f"repos/{repository}/actions/workflows/validate-workload-contract.yml/runs"
        f"?event=workflow_run&branch={default_branch}")
    for run in runs:
        if (run.get("event") != "workflow_run" or run.get("path") != CONTRACT_WORKFLOW
                or run.get("head_branch") != default_branch
                or run.get("display_title") != f"Validate workload contract {sha}"
                or run.get("repository", {}).get("full_name") != repository):
            continue
        candidates.append(run)
    if not candidates:
        return None
    run = max(candidates, key=execution_order)
    jobs = gh(f"repos/{repository}/actions/runs/{run['id']}/jobs?filter=latest&per_page=100")["jobs"]
    exact = [job for job in jobs if job["name"] == f"workload-contract ({sha})"]
    return run, max(exact, key=lambda job: job["id"]) if exact else None


def trusted_contract_producer(check, sha, repository, default_branch):
    producer = latest_contract_producer(sha, repository, default_branch)
    if producer is None:
        raise RuntimeError("Trusted workload contract job does not validate this SHA.")
    run, job = producer
    if run["status"] != "completed":
        return False
    if run["conclusion"] != "success":
        raise RuntimeError("Trusted workload contract producer did not succeed.")
    if check.get("external_id") != f"trusted-workload-contract:{run['id']}":
        raise RuntimeError("Untrusted workload contract check replays an older producer.")
    if job is None or job["conclusion"] != "success":
        raise RuntimeError("Trusted workload contract job does not validate this SHA.")
    return True


def latest_quality(sha, repository, default_branch):
    runs = complete_workflow_runs(
        f"repos/{repository}/actions/workflows/codequality.yml/runs?event=push&head_sha={sha}")
    exact = [run for run in runs if run["head_sha"] == sha
             and run.get("event") == "push" and run.get("head_branch") == default_branch
             and run.get("path") == ".github/workflows/codequality.yml"
             and run.get("repository", {}).get("full_name") == repository]
    return max(exact, key=execution_order) if exact else None


def latest_contract(sha, repository):
    checks = []
    for page in range(1, 21):
        batch = gh(f"repos/{repository}/commits/{sha}/check-runs?check_name=workload-contract&filter=all&per_page=100&page={page}")["check_runs"]
        checks.extend(batch)
        if len(batch) < 100:
            break
    else:
        raise RuntimeError("Too many workload contract results.")
    exact = [check for check in checks if check.get("name") == "workload-contract"
             and check.get("head_sha") == sha and check.get("app", {}).get("id") == ACTIONS_APP_ID
             and check.get("app", {}).get("slug") == "github-actions"]
    return max(exact, key=lambda check: check["id"]) if exact else None


def wait_for_success(fetch, label, attest=None):
    for _ in range(120):
        latest = fetch()
        if latest and latest["status"] == "completed":
            if latest["conclusion"] != "success":
                raise RuntimeError(f"{label} did not succeed.")
            if attest is None or attest(latest):
                return
        time.sleep(5)
    raise RuntimeError(f"Timed out waiting for {label}.")


def wait_for_contract(sha, repository):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise RuntimeError("Expected a complete source SHA.")
    default_branch = gh(f"repos/{repository}")["default_branch"]
    wait_for_success(lambda: latest_contract(sha, repository), "Exact-head trusted workload contract",
                     lambda check: trusted_contract_producer(check, sha, repository, default_branch))


def wait_for_quality(sha, repository):
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise RuntimeError("Expected a complete source SHA.")
    default_branch = gh(f"repos/{repository}")["default_branch"]

    def attest_quality(_):
        check = latest_contract(sha, repository)
        if not check or check["status"] != "completed":
            return False
        if check["conclusion"] != "success":
            raise RuntimeError("Exact-head trusted workload contract did not succeed.")
        return trusted_contract_producer(check, sha, repository, default_branch)

    wait_for_success(lambda: latest_quality(sha, repository, default_branch),
                     "Exact-head Code Quality and trusted contract", attest_quality)


if __name__ == "__main__":
    wait_for_quality(os.environ["SOURCE_SHA"], os.environ["GITHUB_REPOSITORY"])
