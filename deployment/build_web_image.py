"""Compute NBGV metadata from the real checkout without copying Git history into images."""
import argparse
import json
from pathlib import Path
import re
import subprocess


def version_arguments(properties):
    names = {
        "BUILD_VERSION": "Version",
        "ASSEMBLY_VERSION": "AssemblyVersion",
        "FILE_VERSION": "AssemblyFileVersion",
        "INFORMATIONAL_VERSION": "AssemblyInformationalVersion",
        "SOURCE_REVISION": "GitCommitId"
    }
    if not re.fullmatch(r"[0-9a-f]{40}", properties["GitCommitId"]):
        raise ValueError("A real source revision is required.")
    arguments = []
    for argument, property_name in names.items():
        value = properties[property_name]
        if not value or not re.fullmatch(r"[0-9A-Za-z.+-]+", value):
            raise ValueError(f"Invalid build metadata: {property_name}")
        arguments.extend(["--build-arg", f"{argument}={value}"])
    return arguments


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", required=True)
    options = parser.parse_args()
    project = "src/MX.TripSideKick.Web/MX.TripSideKick.Web.csproj"
    subprocess.run(["dotnet", "restore", project, "-p:SkipClientBuild=true"], check=True)
    result = subprocess.check_output([
        "dotnet", "msbuild", project, "-target:GetBuildVersion",
        "-getProperty:Version,AssemblyVersion,AssemblyFileVersion,AssemblyInformationalVersion,GitCommitId",
        "-p:SkipClientBuild=true", "-nologo", "-verbosity:quiet"
    ], text=True)
    metadata = json.loads(result)["Properties"]
    subprocess.run(["docker", "build", "--tag", options.tag,
                    *version_arguments(metadata), "."], check=True)
    Path("artifacts").mkdir(exist_ok=True)
    Path("artifacts/build-metadata.json").write_text(json.dumps(metadata), encoding="utf-8")


if __name__ == "__main__":
    main()
