#!/usr/bin/env python3
"""Restore the pinned pure build into a NEW directory and repeat the two bounded test selections."""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tarfile
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main():
    if len(sys.argv) != 2:
        raise SystemExit("usage: reproduce.py <new-empty-directory>")
    target = Path(sys.argv[1]).resolve()
    target.mkdir(parents=True, exist_ok=False)
    binding = json.loads((HERE / "build-binding-v6.json").read_text())
    with tarfile.open(HERE / binding["base_source_archive"]) as archive:
        for item in archive:
            relative = Path(item.name)
            if not item.isfile() or relative.is_absolute() or ".." in relative.parts:
                raise ValueError("invalid archived input")
            path = target / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            with archive.extractfile(item) as source, path.open("wb") as output:
                shutil.copyfileobj(source, output)
    for relative in binding["overlay_paths"]:
        shutil.copy2(HERE / binding["overlay_source_root"] / relative, target / relative)
    for relative, expected in binding["inputs_sha256"].items():
        if hashlib.sha256((target / relative).read_bytes()).hexdigest() != expected:
            raise ValueError("input mismatch: " + relative)
    env = dict(os.environ, DSH_TEST_ADB_ENV="0", UNICLAW_SIM037_LIVE="0")
    cases = [
        ("Kernel", "AuxiliaryObservationIsolationTests|StorageClosureAsyncTests|PageObservationExecutionTests|KernelRuntimeSurfaceWhitelistTests"),
        ("Host", "AuxiliaryArtifactIsolationTests|StorageClosureFileTests|RunResourceAccountingTests"),
    ]
    for project, names in cases:
        file = "tests/UniClaw." + project + ".Tests/UniClaw." + project + ".Tests.csproj"
        subprocess.run(["dotnet", "restore", file, "--ignore-failed-sources"], cwd=target, env=env, check=True)
        expression = "|".join("FullyQualifiedName~" + name for name in names.split("|"))
        subprocess.run(["dotnet", "test", file, "--no-restore", "-m:1", "-nr:false", "--filter", expression,
                        "--logger", "trx;LogFileName=" + project.lower() + ".trx", "--results-directory", str(target / "results")],
                       cwd=target, env=env, check=True)
    print("Repeated bounded pure-candidate tests; original evidence unchanged.")


if __name__ == "__main__":
    main()
