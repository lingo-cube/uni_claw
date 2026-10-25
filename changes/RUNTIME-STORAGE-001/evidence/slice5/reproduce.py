#!/usr/bin/env python3
"""Rebuild the frozen Control and Plan async candidate in a fresh isolated output directory."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
output = Path(sys.argv[1]).resolve()
if output.exists() and (not output.is_dir() or any(output.iterdir())):
    raise SystemExit("Output must be an empty directory")
output.mkdir(parents=True, exist_ok=True)
source = output / "source"
source.mkdir()
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
metadata = json.loads((HERE / "baseline-archive.json").read_text())
archive_path = HERE / "baseline-entry-source.tar.gz"
assert sha(archive_path) == metadata["sha256"]
with tarfile.open(archive_path, "r:gz") as archive:
    for member in archive.getmembers():
        relative = Path(member.name)
        if not member.isfile() or relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Invalid archived source member: " + member.name)
        target = source / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        with archive.extractfile(member) as incoming, target.open("wb") as outgoing:
            shutil.copyfileobj(incoming, outgoing)
entry = json.loads((HERE / "entry.json").read_text())
for name, digest in entry["inputs"].items():
    assert sha(source / name) == digest, name
candidate = json.loads((HERE / "candidate-inputs-v1.json").read_text())["files"]
for name, digest in candidate.items():
    frozen = HERE / "candidate-source-v1" / name
    assert sha(frozen) == digest, name
    target = source / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(frozen, target)
commands = json.loads((HERE / "regression-selection.json").read_text())
environment = dict(os.environ, UNICLAW_STORAGE_BARRIER_EXPORT=str(output / "barriers"), UNICLAW_CONTROL_PLAN_ASYNC_EXPORT=str(output / "control-plan"), UNICLAW_WORLD_DELTA_EXPORT=str(output / "world-size"))
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results = {}
for name, project, selected in commands:
    command = ["dotnet", "test", project, "--logger", "trx;LogFileName=" + name + ".trx", "--results-directory", str(output)]
    if selected:
        command += ["--filter", selected]
    with (output / (name + ".log")).open("w") as log:
        result = subprocess.run(command, cwd=source, env=environment, stdout=log, stderr=subprocess.STDOUT)
    rows = ET.parse(output / (name + ".trx")).findall(".//t:UnitTestResult", ns)
    results[name] = {"exitCode": result.returncode, "total": len(rows),
        "passed": sum(r.attrib["outcome"] == "Passed" for r in rows),
        "failed": [r.attrib["testName"] for r in rows if r.attrib["outcome"] == "Failed"]}
(output / "summary.json").write_text(json.dumps(results, indent=2) + "\n")
print(json.dumps(results, indent=2))
