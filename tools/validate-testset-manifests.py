#!/usr/bin/env python3
"""Validate the repository's temporary, logical test-set catalog manifests."""

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = (
    ROOT / "testsets/android-settings/manifest.json",
    ROOT / "testsets/workspace-contract/manifest.json",
    ROOT / "testsets/simulation-baseline/manifest.json",
)
REF_RE = re.compile(r"^(project|testset|task|fixture|acceptance)/[a-z0-9][a-z0-9._/-]*$")
REVISION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$")
SCENARIO_RE = re.compile(r"^SCN-[A-Z0-9][A-Z0-9-]*$")
REQUIRED = ("schemaVersion", "projectRef", "testSetRef", "version", "displayName", "tasks", "fixtures")


def fail(path: Path, message: str) -> None:
    try:
        label = path.relative_to(ROOT)
    except ValueError:
        label = path
    raise ValueError(f"{label}: {message}")


def check_ref(path: Path, field: str, value: object, kind: str) -> None:
    if not isinstance(value, str) or not REF_RE.fullmatch(value) or not value.startswith(kind + "/"):
        fail(path, f"{field} must be a logical {kind} reference")
    if value.startswith(("/", "./", "../")) or "\\" in value or ".." in value:
        fail(path, f"{field} must not contain a physical path")


def check_manifest(path: Path) -> None:
    try:
        data = json.loads(path.read_text())
    except (OSError, json.JSONDecodeError) as exc:
        fail(path, f"invalid JSON: {exc}")
    if not isinstance(data, dict):
        fail(path, "manifest must be an object")
    missing = [field for field in REQUIRED if field not in data]
    if missing:
        fail(path, "missing required fields: " + ", ".join(missing))
    if data["schemaVersion"] != "testset-manifest.v1":
        fail(path, "schemaVersion must be testset-manifest.v1")
    if data["version"] != "default":
        fail(path, "version must be default")
    if not isinstance(data["displayName"], str) or not data["displayName"].strip():
        fail(path, "displayName must be non-empty")
    check_ref(path, "projectRef", data["projectRef"], "project")
    check_ref(path, "testSetRef", data["testSetRef"], "testset")
    if "sourceRevision" in data and (
        not isinstance(data["sourceRevision"], str)
        or not REVISION_RE.fullmatch(data["sourceRevision"])
    ):
        fail(path, "sourceRevision must be a logical revision identifier, not a path")

    tasks = data["tasks"]
    fixtures = data["fixtures"]
    if not isinstance(tasks, list) or not tasks:
        fail(path, "tasks must be a non-empty array")
    if not isinstance(fixtures, list):
        fail(path, "fixtures must be an array")
    fixture_refs = []
    for fixture in fixtures:
        if not isinstance(fixture, dict) or set(("fixtureRef", "kind", "description")) - fixture.keys():
            fail(path, "each fixture requires fixtureRef, kind, and description")
        check_ref(path, "fixtureRef", fixture["fixtureRef"], "fixture")
        fixture_refs.append(fixture["fixtureRef"])
    if len(fixture_refs) != len(set(fixture_refs)):
        fail(path, "duplicate fixtureRef")

    task_refs = []
    for task in tasks:
        if not isinstance(task, dict) or set(("taskRef", "displayName", "fixtureRefs", "acceptanceRefs")) - task.keys():
            fail(path, "each task requires taskRef, displayName, fixtureRefs, and acceptanceRefs")
        check_ref(path, "taskRef", task["taskRef"], "task")
        task_refs.append(task["taskRef"])
        # SIM-006 A3: optional mechanical scenario linkage. The scenario id is
        # resolved against the scenario library on disk — unknown references
        # fail closed instead of silently passing.
        scenario_refs = task.get("scenarioRefs", [])
        if not isinstance(scenario_refs, list) or not all(
            isinstance(ref, str) for ref in scenario_refs
        ):
            fail(path, f"{task['taskRef']} scenarioRefs must be an array of scenario ids")
        for ref in scenario_refs:
            if not SCENARIO_RE.fullmatch(ref):
                fail(path, f"{task['taskRef']} scenarioRefs entry '{ref}' must be a scenario id (SCN-*)")
            if not (ROOT / "scenarios" / f"{ref}.json").exists():
                fail(path, f"{task['taskRef']} references unknown scenario {ref}")
        if "testSetRefs" in task or "testLayer" in task:
            fail(path, f"{task['taskRef']} uses reserved unmapped fields (testSetRefs/testLayer)")
        if not isinstance(task["fixtureRefs"], list) or not task["fixtureRefs"]:
            fail(path, f"{task['taskRef']} must reference at least one fixture")
        for ref in task["fixtureRefs"]:
            check_ref(path, "fixtureRefs[]", ref, "fixture")
            if ref not in fixture_refs:
                fail(path, f"{task['taskRef']} references unknown fixture {ref}")
        if not isinstance(task["acceptanceRefs"], list) or not task["acceptanceRefs"]:
            fail(path, f"{task['taskRef']} must reference at least one acceptance")
        for ref in task["acceptanceRefs"]:
            check_ref(path, "acceptanceRefs[]", ref, "acceptance")
    if len(task_refs) != len(set(task_refs)):
        fail(path, "duplicate taskRef")


def main() -> int:
    manifests = tuple(Path(arg).resolve() for arg in sys.argv[1:]) or MANIFESTS
    try:
        for manifest in manifests:
            check_manifest(manifest)
    except ValueError as exc:
        print(f"FAIL: {exc}")
        return 1
    print(f"PASSED {len(manifests)} test-set manifest(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
