import json
import os
import subprocess
import time

sha = os.environ["SOURCE_SHA"]
for _ in range(120):
    runs = json.loads(subprocess.check_output([
        "gh", "run", "list", "--workflow", "codequality.yml", "--commit", sha,
        "--event", "push", "--json", "headSha,status,conclusion", "--limit", "20"
    ], text=True))
    exact = [run for run in runs if run["headSha"] == sha]
    if exact and exact[0]["status"] == "completed":
        if exact[0]["conclusion"] != "success":
            raise SystemExit("Exact-revision Code Quality did not pass.")
        break
    time.sleep(5)
else:
    raise SystemExit("Timed out waiting for exact-revision Code Quality.")

for _ in range(120):
    checks = json.loads(subprocess.check_output([
        "gh", "api",
        f"repos/{os.environ['GITHUB_REPOSITORY']}/commits/{sha}/check-runs?check_name=workload-contract&filter=latest"
    ], text=True))["check_runs"]
    exact = [check for check in checks if check["head_sha"] == sha and check["app"]["slug"] == "github-actions"]
    latest = max(exact, key=lambda check: check["id"]) if exact else None
    if latest and latest["status"] == "completed":
        if latest["conclusion"] != "success":
            raise SystemExit("Exact-revision trusted workload contract did not pass.")
        break
    time.sleep(5)
else:
    raise SystemExit("Timed out waiting for exact-revision trusted workload contract.")
