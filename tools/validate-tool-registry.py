#!/usr/bin/env python3
"""Validate tool-registry.yaml — the Development Harness tool exposure manifest
(PNL-007 / ADR-0039). Deterministic, read-only, no model inference.

Checked invariants:
  1. structure      — version == 1; tools is a non-empty mapping; names match
                      ^[a-z][a-z0-9-]*$;
  2. vocabulary     — backing / invocation / posture / status within frozen
                      enums; surfaces non-empty subset of the known set;
  3. binding fields — deterministic-script requires entry; model-procedure
                      requires skillRef (with SKILL.md); capability backing
                      requires capabilityRef (vocabulary-only this round);
  4. references     — entry / outputSchema / skillRef paths exist (when the
                      rule applies); requiredCapability must be declared in
                      model-routing.yaml; consumes targets must be registered
                      tools;
  5. status rule    — implemented requires its invocation binding to exist on
                      disk; planned does not.

Exit codes: 0 valid · 1 invalid or unreadable.
Usage: python3 tools/validate-tool-registry.py   (from the repository root)
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "tool-registry.yaml"
MODEL_ROUTING = ROOT / "model-routing.yaml"

BACKINGS = {"harness-native", "capability"}
INVOCATIONS = {"deterministic-script", "model-procedure"}
POSTURES = {"read-only", "local-write", "external-effect"}
SURFACES = {"cli", "coder", "workbench"}
STATUSES = {"implemented", "planned"}
REQUIRED_FIELDS = ("summary", "backing", "invocation", "requiredCapability", "posture", "surfaces", "status")
NAME_RE = re.compile(r"^[a-z][a-z0-9-]*$")


def load_yaml(path: Path):
    with path.open(encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def main() -> int:
    if not REGISTRY.is_file():
        print(f"ERROR registry missing: {REGISTRY}")
        return 1
    try:
        registry = load_yaml(REGISTRY)
    except yaml.YAMLError as error:
        print(f"ERROR registry unreadable YAML: {error}")
        return 1
    if not isinstance(registry, dict) or registry.get("version") != 1:
        print("ERROR registry: top-level 'version: 1' required")
        return 1
    tools = registry.get("tools")
    if not isinstance(tools, dict) or not tools:
        print("ERROR registry: 'tools' must be a non-empty mapping")
        return 1

    try:
        routing = load_yaml(MODEL_ROUTING)
        capabilities = set((routing.get("capabilities") or {}).keys())
    except (OSError, yaml.YAMLError) as error:
        print(f"ERROR model-routing.yaml unreadable: {error}")
        return 1
    if not capabilities:
        print("ERROR model-routing.yaml: no capabilities declared")
        return 1

    violations: list[str] = []
    for name in sorted(tools):
        tool = tools[name]
        where = f"tools.{name}"
        if not NAME_RE.match(name):
            violations.append(f"{where}: name must match ^[a-z][a-z0-9-]*$")
        if not isinstance(tool, dict):
            violations.append(f"{where}: entry must be a mapping")
            continue
        for field_name in REQUIRED_FIELDS:
            if tool.get(field_name) in (None, "", [], {}):
                violations.append(f"{where}: required field '{field_name}' missing")
        if tool.get("backing") not in BACKINGS:
            violations.append(f"{where}.backing: must be one of {sorted(BACKINGS)}")
        if tool.get("invocation") not in INVOCATIONS:
            violations.append(f"{where}.invocation: must be one of {sorted(INVOCATIONS)}")
        if tool.get("posture") not in POSTURES:
            violations.append(f"{where}.posture: must be one of {sorted(POSTURES)}")
        if tool.get("status") not in STATUSES:
            violations.append(f"{where}.status: must be one of {sorted(STATUSES)}")
        surfaces = tool.get("surfaces")
        if isinstance(surfaces, list) and surfaces:
            unknown = [s for s in surfaces if s not in SURFACES]
            if unknown:
                violations.append(f"{where}.surfaces: unknown surfaces {sorted(unknown)}; known = {sorted(SURFACES)}")
        elif surfaces is not None:
            violations.append(f"{where}.surfaces: must be a non-empty list")

        implemented = tool.get("status") == "implemented"
        invocation = tool.get("invocation")

        if invocation == "deterministic-script":
            entry = tool.get("entry")
            if not entry:
                violations.append(f"{where}: deterministic-script requires 'entry'")
            elif implemented and not (ROOT / entry).is_file():
                violations.append(f"{where}.entry: file does not exist: {entry}")
        elif invocation == "model-procedure":
            skill_ref = tool.get("skillRef")
            if not skill_ref:
                violations.append(f"{where}: model-procedure requires 'skillRef'")
            elif not (ROOT / skill_ref / "SKILL.md").is_file():
                violations.append(f"{where}.skillRef: SKILL.md not found under {skill_ref}")

        if tool.get("backing") == "capability":
            capability_ref = tool.get("capabilityRef")
            if not capability_ref:
                violations.append(f"{where}: capability backing requires 'capabilityRef' (vocabulary-only; resolution deferred per ADR-0026)")

        output_schema = tool.get("outputSchema")
        if output_schema and not (ROOT / output_schema).is_file():
            violations.append(f"{where}.outputSchema: file does not exist: {output_schema}")

        capability = tool.get("requiredCapability")
        if capability and capability not in capabilities:
            violations.append(f"{where}.requiredCapability: '{capability}' not declared in model-routing.yaml capabilities {sorted(capabilities)}")

        consumes = tool.get("consumes")
        if consumes is not None:
            if not isinstance(consumes, list) or not consumes:
                violations.append(f"{where}.consumes: must be a non-empty list when present")
            else:
                for target in consumes:
                    if target not in tools:
                        violations.append(f"{where}.consumes: '{target}' is not a registered tool")

    if violations:
        for violation in violations:
            print(f"VIOLATION {violation}")
        print(f"FAILED tool-registry.yaml with {len(violations)} violation(s)")
        return 1
    print(f"PASSED tool-registry.yaml ({len(tools)} tool(s): {', '.join(sorted(tools))})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
