#!/usr/bin/env python3
"""PROTOCOL-MACHINE-001 — protocol state-machine generator (Slice 1).

Single source: schemas/protocol/state-machine.yaml. This tool is the ONLY
sanctioned writer of the generated enforcement artifact consumed by the C#
teeth (tests/UniClaw.Kernel.Tests/Protocol/protocol-machine.generated.json).

Discipline (D12, aligned with the C8 writer protocol):
  --check            regenerate and byte-compare (freshness / drift detection);
                     exit non-zero on any drift. No writes.
  (writer mode)      regenerate and write. If the artifact would change and no
                     --change <ID> is given, refuse (semantic diff must
                     piggyback its causal change); pure regeneration (no
                     byte diff) never requires --change.
  Meta-validation always runs first: closed vocabularies (kind / status /
  measure / await), required keys, self-loop liveness dichotomy (progress
  loops must carry a well-founded measure; silent loops must declare
  await: external and may not fake progress).

Determinism: output bytes are a pure function of the YAML — stable sort order,
no timestamps, LF, trailing newline.

Usage:
  python3 tools/gen-protocol-machine.py --check
  python3 tools/gen-protocol-machine.py --change PROTOCOL-MACHINE-001
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "schemas" / "protocol" / "state-machine.yaml"
ARTIFACT = (
    ROOT / "tests" / "UniClaw.Kernel.Tests" / "Protocol"
    / "protocol-machine.generated.json"
)

VERSION = 1
MEASURES = {"steps", "rounds", "defer-rounds", "cap-remaining"}
AWAITS = {"external"}
KINDS = {"edge", "gap"}
STATUSES = {"keep", "fix", "track"}
EDGE_ID_PREFIXES = ("P", "O", "G")
COMPONENTS = {
    "uni-agent", "uni-kernel", "run-model", "control-loop", "assurance",
    "effect-boundary", "capability-plane", "evidence-ledger", "world-model",
    "memory-system", "user-session",
}


def fail(reasons: list[str]) -> int:
    for r in reasons:
        print(f"FAIL: {r}", file=sys.stderr)
    return 1


def validate(doc: dict) -> list[str]:
    reasons: list[str] = []

    if doc.get("version") != VERSION:
        reasons.append(f"version must be {VERSION}, got {doc.get('version')!r}")
    machines = doc.get("machines")
    if not isinstance(machines, dict) or not machines:
        reasons.append("machines: must be a non-empty mapping")
        return reasons

    edge_ids: set[str] = set()
    for entry in doc.get("edges") or []:
        eid = entry.get("id") if isinstance(entry, dict) else None
        if not eid:
            reasons.append("edges: entry without id")
            continue
        if eid[0] not in EDGE_ID_PREFIXES or not eid[1:].isdigit():
            reasons.append(f"edges.{eid}: id must match P##/O##/G## namespace")
        if entry.get("kind") not in KINDS:
            reasons.append(f"edges.{eid}: kind must be one of {sorted(KINDS)}")
        if entry.get("status") not in STATUSES:
            reasons.append(f"edges.{eid}: status must be one of {sorted(STATUSES)}")
        if entry.get("status") == "fix" and not entry.get("fix_ref"):
            reasons.append(f"edges.{eid}: status=fix requires fix_ref (change id)")
        if entry.get("from") not in COMPONENTS:
            reasons.append(f"edges.{eid}: from {entry.get('from')!r} outside closed component vocabulary")
        if entry.get("to") not in COMPONENTS:
            reasons.append(f"edges.{eid}: to {entry.get('to')!r} outside closed component vocabulary")
        pref = entry.get("payload_ref")
        if not isinstance(pref, dict) or not pref.get("name"):
            reasons.append(f"edges.{eid}: payload_ref requires type + name")
        edge_ids.add(eid)

    for name, machine in sorted(machines.items()):
        nodes = set(machine.get("nodes") or [])
        if not nodes:
            reasons.append(f"{name}.nodes: must be a non-empty list")
            continue
        initial = machine.get("initial")
        if initial not in nodes:
            reasons.append(f"{name}.initial: {initial!r} not a node")
        transitions = machine.get("transitions") or []
        if not transitions:
            reasons.append(f"{name}.transitions: must be a non-empty list")
            continue
        seen_ids: set[str] = set()
        for t in transitions:
            tid = t.get("id")
            if not tid:
                reasons.append(f"{name}: transition without id")
                continue
            if tid in seen_ids:
                reasons.append(f"{name}.{tid}: duplicate transition id")
            seen_ids.add(tid)
            where = f"{name}.{tid}"
            for key in ("from", "to", "trigger", "guard", "emits"):
                if key not in t:
                    reasons.append(f"{where}: missing required key {key!r}")
            if t.get("from") not in nodes:
                reasons.append(f"{where}: from {t.get('from')!r} not a node")
            if t.get("to") not in nodes:
                reasons.append(f"{where}: to {t.get('to')!r} not a node")
            trigger = t.get("trigger")
            if not isinstance(trigger, dict) or not trigger.get("event"):
                reasons.append(f"{where}: trigger requires event")
            edge = (trigger or {}).get("edge")
            if edge is not None and edge not in edge_ids:
                reasons.append(f"{where}: trigger.edge {edge!r} not registered in edges")
            if not isinstance(t.get("emits"), list):
                reasons.append(f"{where}: emits must be a list (may be empty)")

            self_loop = t.get("from") == t.get("to")
            measure = t.get("measure")
            await_ = t.get("await")
            # D10: progress measures annotate cycle-forming edges (a cycle may
            # span several states); silent waiting must be a self-loop.
            if measure is not None:
                if measure not in MEASURES:
                    reasons.append(f"{where}: measure {measure!r} outside {sorted(MEASURES)}")
                if await_ is not None:
                    reasons.append(f"{where}: silent loop must not carry a measure (D10)")
            if await_ is not None:
                if not self_loop:
                    reasons.append(f"{where}: await: external is only valid on a self-loop")
                if await_ not in AWAITS:
                    reasons.append(f"{where}: await {await_!r} outside {sorted(AWAITS)}")
                if measure is not None:
                    reasons.append(f"{where}: silent loop must not carry a measure (D10)")
            if self_loop and measure is None and await_ is None:
                reasons.append(
                    f"{where}: self-loop must carry exactly one of "
                    "measure (progress loop) or await: external (silent loop)")
    return reasons


def render(doc: dict) -> str:
    """Deterministic JSON projection consumed by the C# teeth."""
    payload = {
        "version": doc["version"],
        "source": "schemas/protocol/state-machine.yaml",
        "machines": {},
        "edges": [
            {
                "id": e["id"],
                "kind": e["kind"],
                "status": e["status"],
                "from": e["from"],
                "to": e["to"],
                "titleAnchor": e.get("title_anchor"),
                "payloadRef": {
                    "type": e["payload_ref"]["type"],
                    "name": e["payload_ref"]["name"],
                },
            }
            for e in sorted(doc.get("edges") or [], key=lambda e: e["id"])
        ],
    }
    for name in sorted(doc["machines"]):
        machine = doc["machines"][name]
        payload["machines"][name] = {
            "initial": machine["initial"],
            "terminalNodes": list(machine.get("terminal_nodes") or []),
            "absorbing": list(machine.get("absorbing") or []),
            "nodes": sorted(machine["nodes"]),
            "transitions": [
                {
                    "id": t["id"],
                    "from": t["from"],
                    "to": t["to"],
                    "trigger": {
                        "event": t["trigger"]["event"],
                        "edge": t["trigger"].get("edge"),
                    },
                    "guard": t["guard"],
                    "emits": sorted(t["emits"]),
                    "measure": t.get("measure"),
                    "await": t.get("await"),
                }
                for t in sorted(machine["transitions"], key=lambda t: t["id"])
            ],
        }
    return json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="verify-only: regenerate and byte-compare, no write")
    parser.add_argument("--change", metavar="ID",
                        help="causal change id, required when writing over a diff")
    args = parser.parse_args()

    doc = yaml.safe_load(SOURCE.read_text(encoding="utf-8"))
    reasons = validate(doc)
    if reasons:
        return fail(reasons)

    generated = render(doc).encode("utf-8")
    existing = ARTIFACT.read_bytes() if ARTIFACT.exists() else None

    if args.check:
        if existing is None:
            print(f"FAIL: artifact missing: {ARTIFACT}", file=sys.stderr)
            return 1
        if existing != generated:
            print("FAIL: protocol-machine.generated.json is stale — rerun "
                  "tools/gen-protocol-machine.py --change <causal-id>", file=sys.stderr)
            return 1
        print(f"OK: protocol machine fresh ({len(doc['machines']['run']['transitions'])} "
              f"run transitions, {len(doc.get('edges') or [])} edges)")
        return 0

    if existing != generated and not args.change:
        print("FAIL: source semantic diff detected — writing requires "
              "--change <causal change id> (C8 protocol)", file=sys.stderr)
        return 1

    ARTIFACT.parent.mkdir(parents=True, exist_ok=True)
    ARTIFACT.write_bytes(generated)
    print(f"OK: wrote {ARTIFACT.relative_to(ROOT)}"
          + (f" (change {args.change})" if args.change else " (pure regeneration)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
