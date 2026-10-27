#!/usr/bin/env python3

import argparse
import json
import os
import re
import runpy
import subprocess
import sys
import time
from pathlib import Path


wait_contract = runpy.run_path(str(Path(__file__).with_name("require-quality.py")))["wait_for_contract"]


COPILOT_LOGINS = {
    "copilot-pull-request-reviewer",
    "copilot-pull-request-reviewer[bot]",
}
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
IMAGE_PATTERN = re.compile(r"^[a-z0-9.-]+/[a-z0-9/_-]+@sha256:[0-9a-f]{64}$")


def run(*args: str, capture: bool = False) -> str:
    result = subprocess.run(
        args,
        check=True,
        text=True,
        stdout=subprocess.PIPE if capture else None,
    )
    return result.stdout.strip() if capture else ""


def gh_json(*args: str) -> object:
    return json.loads(run("gh", *args, capture=True))


def is_ancestor(ancestor: str, descendant: str) -> bool:
    return (
        subprocess.run(
            ["git", "merge-base", "--is-ancestor", ancestor, descendant],
            check=False,
        ).returncode
        == 0
    )


def apply_updates(document: dict, updates: dict[str, str]) -> bool:
    services = {service["name"]: service for service in document["services"]}
    missing = sorted(set(updates) - set(services))
    if missing:
        raise RuntimeError(f"Unknown workload services: {', '.join(missing)}")
    changed = False
    for name, image in updates.items():
        current_image = services[name]["image"]
        current_prefix, separator, _ = current_image.partition("@sha256:")
        next_prefix, next_separator, _ = image.partition("@sha256:")
        if not separator or not next_separator or current_prefix != next_prefix:
            raise RuntimeError(
                f"Image update for {name} must retain registry and repository."
            )
        if current_image != image:
            services[name]["image"] = image
            changed = True
    return changed


def has_newer_image_inputs(source_sha: str, image_input_pattern: str) -> bool:
    run("git", "fetch", "origin", "main:refs/remotes/origin/main")
    run("git", "merge-base", "--is-ancestor", source_sha, "origin/main")
    changed_files = run(
        "git",
        "log",
        "--first-parent",
        "--diff-merges=first-parent",
        "--format=",
        "--name-only",
        f"{source_sha}..origin/main",
        capture=True,
    ).splitlines()
    result = subprocess.run(
        ["grep", "--extended-regexp", "--quiet", image_input_pattern],
        input="\n".join(changed_files),
        text=True,
        check=False,
    )
    if result.returncode not in {0, 1}:
        raise RuntimeError("The image input pattern is not a valid POSIX ERE.")
    return result.returncode == 0


def has_newer_service_images(source_sha: str, services: set[str]) -> bool:
    commits = run(
        "git",
        "rev-list",
        "--first-parent",
        f"{source_sha}..origin/main",
        "--",
        "workload.json",
        capture=True,
    ).splitlines()
    for commit in commits:
        before = json.loads(
            run("git", "show", f"{commit}^1:workload.json", capture=True)
        )
        after = json.loads(run("git", "show", f"{commit}:workload.json", capture=True))
        before_services = {service["name"]: service for service in before["services"]}
        after_services = {service["name"]: service for service in after["services"]}
        missing = sorted(
            services - before_services.keys() | services - after_services.keys()
        )
        if missing:
            raise RuntimeError(
                f"Updated workload services are missing from history: {', '.join(missing)}"
            )
        if any(
            before_services[name]["image"] != after_services[name]["image"]
            for name in services
        ):
            return True
    return False


def wait_for_quality(workflow: str, branch: str, head_sha: str) -> None:
    existing_runs = gh_json(
        "run",
        "list",
        "--workflow",
        workflow,
        "--branch",
        branch,
        "--event",
        "workflow_dispatch",
        "--limit",
        "100",
        "--json",
        "databaseId",
    )
    existing_ids = {item["databaseId"] for item in existing_runs}
    run("gh", "workflow", "run", workflow, "--ref", branch)
    for _ in range(60):
        runs = gh_json(
            "run",
            "list",
            "--workflow",
            workflow,
            "--branch",
            branch,
            "--event",
            "workflow_dispatch",
            "--limit",
            "10",
            "--json",
            "databaseId,headSha",
        )
        matching = [
            item
            for item in runs
            if item["headSha"] == head_sha and item["databaseId"] not in existing_ids
        ]
        if matching:
            run("gh", "run", "watch", str(matching[0]["databaseId"]), "--exit-status")
            return
        time.sleep(2)
    raise RuntimeError("Timed out waiting for the promotion Quality run.")


def wait_for_pull_request_quality(
    workflow: str, branch: str, head_sha: str
) -> None:
    for _ in range(60):
        runs = gh_json(
            "run",
            "list",
            "--workflow",
            workflow,
            "--branch",
            branch,
            "--event",
            "pull_request",
            "--limit",
            "10",
            "--json",
            "databaseId,headSha",
        )
        matching = [item for item in runs if item["headSha"] == head_sha]
        if matching:
            run("gh", "run", "watch", str(matching[0]["databaseId"]), "--exit-status")
            return
        time.sleep(2)
    raise RuntimeError("Timed out waiting for pull-request Quality.")


def wait_for_copilot_review(owner: str, name: str, number: int, head_sha: str) -> None:
    query = """
      query(
        $owner:String!,
        $name:String!,
        $number:Int!,
        $reviewAfter:String,
        $threadAfter:String
      ) {
        repository(owner:$owner, name:$name) {
          pullRequest(number:$number) {
            id
            reviews(first:100, after:$reviewAfter) {
              nodes { author { login } commit { oid } state submittedAt }
              pageInfo { hasNextPage endCursor }
            }
            reviewRequests(first:100) {
              nodes {
                requestedReviewer {
                  ... on Bot { login }
                  ... on User { login }
                }
              }
            }
            reviewThreads(first:100, after:$threadAfter) {
              nodes { isResolved }
              pageInfo { hasNextPage endCursor }
            }
          }
        }
      }
    """

    def fetch_review_state(
        review_after: str | None = None,
        thread_after: str | None = None,
    ) -> dict:
        arguments = [
            "api",
            "graphql",
            "-f",
            f"query={query}",
            "-f",
            f"owner={owner}",
            "-f",
            f"name={name}",
            "-F",
            f"number={number}",
        ]
        if review_after:
            arguments.extend(["-f", f"reviewAfter={review_after}"])
        if thread_after:
            arguments.extend(["-f", f"threadAfter={thread_after}"])
        return gh_json(*arguments)

    review_requested = False
    for _ in range(120):
        response = fetch_review_state()
        pull_request = response["data"]["repository"]["pullRequest"]
        reviews = list(pull_request["reviews"]["nodes"])
        review_page_info = pull_request["reviews"]["pageInfo"]
        while review_page_info["hasNextPage"]:
            response = fetch_review_state(
                review_after=review_page_info["endCursor"]
            )
            review_connection = response["data"]["repository"]["pullRequest"][
                "reviews"
            ]
            reviews.extend(review_connection["nodes"])
            review_page_info = review_connection["pageInfo"]
        matching_reviews = [
            review["author"]
            and review["author"]["login"] in COPILOT_LOGINS
            and review["commit"]
            and review["commit"]["oid"] == head_sha
            for review in reviews
        ]
        matching_reviews = [
            review
            for review, matches in zip(reviews, matching_reviews)
            if matches
        ]
        pending_copilot_review = any(
            request["requestedReviewer"]
            and request["requestedReviewer"].get("login") in COPILOT_LOGINS
            for request in pull_request["reviewRequests"]["nodes"]
        )
        if not matching_reviews and pending_copilot_review:
            review_requested = True
        elif not matching_reviews and not review_requested:
            mutation = """
              mutation($pullRequestId:ID!) {
                requestReviewsByLogin(input:{
                  pullRequestId:$pullRequestId,
                  botLogins:["copilot-pull-request-reviewer"],
                  union:true
                }) {
                  pullRequest { id }
                }
              }
            """
            gh_json(
                "api",
                "graphql",
                "-f",
                f"query={mutation}",
                "-f",
                f"pullRequestId={pull_request['id']}",
            )
            review_requested = True
        reviewed = False
        if matching_reviews:
            latest_review = max(
                matching_reviews,
                key=lambda review: review["submittedAt"],
            )
            if latest_review["state"] not in {"APPROVED", "COMMENTED"}:
                raise RuntimeError(
                    "The latest exact-head Copilot review is not mergeable."
                )
            reviewed = True
        if reviewed:
            threads = list(pull_request["reviewThreads"]["nodes"])
            page_info = pull_request["reviewThreads"]["pageInfo"]
            while page_info["hasNextPage"]:
                response = fetch_review_state(thread_after=page_info["endCursor"])
                review_threads = response["data"]["repository"]["pullRequest"][
                    "reviewThreads"
                ]
                threads.extend(review_threads["nodes"])
                page_info = review_threads["pageInfo"]
            unresolved = [
                thread for thread in threads if not thread["isResolved"]
            ]
            if unresolved:
                raise RuntimeError(
                    "Copilot found unresolved promotion review comments; "
                    "the pull request remains open."
                )
            return
        time.sleep(5)
    raise RuntimeError(
        "Timed out waiting for Copilot review; the promotion pull request remains open."
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--image-input-pattern", required=True)
    parser.add_argument("--workflow", default="quality.yml")
    parser.add_argument("--update", action="append", required=True)
    args = parser.parse_args()

    if not SHA_PATTERN.fullmatch(args.source_sha):
        raise RuntimeError("The validated source revision must be a full commit SHA.")

    updates: dict[str, str] = {}
    for value in args.update:
        service, separator, image = value.partition("=")
        if not separator or not service or not IMAGE_PATTERN.fullmatch(image):
            raise RuntimeError(f"Invalid service image update: {value}")
        updates[service] = image

    repository = os.environ["GITHUB_REPOSITORY"]
    owner, name = repository.split("/", 1)
    manifest_path = Path("workload.json")
    branch = f"automation/promote-{args.source_sha[:12]}"
    existing = gh_json(
        "pr",
        "list",
        "--repo",
        repository,
        "--head",
        branch,
        "--state",
        "open",
        "--json",
        "number,headRefOid,baseRefName",
    )
    if existing and existing[0]["baseRefName"] != "main":
        raise RuntimeError("The existing promotion pull request must target main.")
    if has_newer_image_inputs(
        args.source_sha, args.image_input_pattern
    ) or has_newer_service_images(args.source_sha, set(updates)):
        if existing:
            run(
                "gh",
                "pr",
                "close",
                str(existing[0]["number"]),
                "--repo",
                repository,
                "--delete-branch",
                "--comment",
                "Closed automatically because a newer image-input or service-image revision superseded it.",
            )
        print("A newer image-input or service-image revision supersedes this promotion.")
        return
    manifest = json.loads(
        run("git", "show", "origin/main:workload.json", capture=True)
    )
    if not apply_updates(manifest, updates):
        if existing:
            run(
                "gh",
                "pr",
                "close",
                str(existing[0]["number"]),
                "--repo",
                repository,
                "--delete-branch",
                "--comment",
                "Closed automatically because the published digest is already promoted.",
            )
        print("The published image digest is already promoted on main.")
        return

    if existing:
        number = existing[0]["number"]
        head_sha = existing[0]["headRefOid"]
        run("git", "fetch", "origin", f"refs/heads/{branch}")
        changed_files = run(
            "git", "diff", "--name-only", "origin/main...FETCH_HEAD", capture=True
        ).splitlines()
        if changed_files != ["workload.json"]:
            raise RuntimeError("The existing promotion branch contains unexpected changes.")
        branch_manifest = json.loads(
            run("git", "show", "FETCH_HEAD:workload.json", capture=True)
        )
        if branch_manifest != manifest or not is_ancestor("origin/main", "FETCH_HEAD"):
            run("git", "config", "user.name", "github-actions[bot]")
            run(
                "git",
                "config",
                "user.email",
                "41898282+github-actions[bot]@users.noreply.github.com",
            )
            run("git", "switch", "-C", branch, "origin/main")
            manifest_path.write_text(
                json.dumps(manifest, indent=2, ensure_ascii=True) + "\n",
                encoding="utf-8",
            )
            run("git", "add", "workload.json")
            run("git", "commit", "-m", f"chore: promote image for {args.source_sha[:12]}")
            head_sha = run("git", "rev-parse", "HEAD", capture=True)
            run("git", "push", "--force", "origin", f"HEAD:refs/heads/{branch}")
    else:
        run("git", "config", "user.name", "github-actions[bot]")
        run(
            "git",
            "config",
            "user.email",
            "41898282+github-actions[bot]@users.noreply.github.com",
        )
        run("git", "switch", "-C", branch, "origin/main")
        manifest_path.write_text(
            json.dumps(manifest, indent=2, ensure_ascii=True) + "\n",
            encoding="utf-8",
        )
        run("git", "add", "workload.json")
        run("git", "commit", "-m", f"chore: promote image for {args.source_sha[:12]}")
        head_sha = run("git", "rev-parse", "HEAD", capture=True)
        run("git", "push", "--force", "origin", f"HEAD:refs/heads/{branch}")
        run(
            "gh",
            "pr",
            "create",
            "--repo",
            repository,
            "--base",
            "main",
            "--head",
            branch,
            "--title",
            f"Promote image for {args.source_sha[:12]}",
            "--body",
            (
                "Automated immutable image promotion for validated source "
                f"`{args.source_sha}`.\n\n"
                "The pull request updates only exact image digests in "
                "`workload.json`. Exact-head Quality and Copilot review must "
                "complete without findings. Manual merge and separate host activation are required."
            ),
        )
        number = int(
            run(
                "gh",
                "pr",
                "view",
                branch,
                "--repo",
                repository,
                "--json",
                "number",
                "--jq",
                ".number",
                capture=True,
            )
        )

    wait_for_quality(args.workflow, branch, head_sha)
    wait_for_pull_request_quality(args.workflow, branch, head_sha)
    wait_for_copilot_review(owner, name, number, head_sha)
    wait_contract(head_sha, repository)
    if has_newer_image_inputs(
        args.source_sha, args.image_input_pattern
    ) or has_newer_service_images(args.source_sha, set(updates)):
        run(
            "gh",
            "pr",
            "close",
            str(number),
            "--repo",
            repository,
            "--delete-branch",
            "--comment",
            "Closed automatically because a newer image-input or service-image revision superseded it.",
        )
        print(f"Closed superseded image promotion pull request #{number}.")
        return
    base_ref = run(
        "gh",
        "pr",
        "view",
        str(number),
        "--repo",
        repository,
        "--json",
        "baseRefName",
        "--jq",
        ".baseRefName",
        capture=True,
    )
    if base_ref != "main":
        raise RuntimeError("The promotion pull request no longer targets main.")
    print(f"Image promotion pull request #{number} passed exact-head quality and review; awaiting manual merge.")


if __name__ == "__main__":
    try:
        main()
    except (KeyError, OSError, RuntimeError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
