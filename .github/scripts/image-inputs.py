import json
import os
from pathlib import Path
import re
import subprocess


def should_publish(metadata, source_sha, pattern):
    if not pattern:
        raise ValueError("Image input pattern must not be empty.")
    if set(metadata) != {"before", "after"} or metadata["after"] != source_sha:
        raise ValueError("Push metadata does not match the validated source revision.")
    if any(not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{40}", value)
           for value in metadata.values()):
        raise ValueError("Push revisions must be exact commit IDs.")
    before, after = metadata["before"], metadata["after"]
    if before == "0" * 40:
        return True
    available = subprocess.run(["git", "cat-file", "-e", f"{before}^{{commit}}"],
                               capture_output=True, check=False)
    if available.returncode != 0:
        return True
    files = subprocess.check_output(["git", "diff", "--name-only", before, after], text=True)
    return any(re.search(pattern, filename) for filename in files.splitlines())


if __name__ == "__main__":
    metadata = json.loads(Path("artifacts/publication-inputs/push-range.json").read_text(encoding="utf-8"))
    pattern = Path(".github/image-input-pattern.txt").read_text(encoding="utf-8").strip()
    print("true" if should_publish(metadata, os.environ["SOURCE_SHA"], pattern) else "false")
