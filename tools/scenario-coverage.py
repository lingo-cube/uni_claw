#!/usr/bin/env python3
"""Scenario Library Coverage Report.

Scans scenarios/*.json and aggregates capability × status × source.
Independent of any document; srRef shown when present (source lineage).

Usage: python3 tools/scenario-coverage.py [--by-component]
"""
from __future__ import annotations
import json, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCENARIOS = ROOT / "scenarios"
VALID_STATUS = {"passing", "failing", "pending-env", "not-implemented"}
VALID_SOURCE = {"recorded", "generated", "derived-from-doc", "bug-repro", "component-test"}


def load() -> list[dict]:
    entries = []
    for f in sorted(SCENARIOS.glob("*.json")):
        if f.name == "schema.json":
            continue
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
            if data.get("status") not in VALID_STATUS:
                print(f"  WARN: {f.name} invalid status '{data.get('status')}'")
            if data.get("source") not in VALID_SOURCE:
                print(f"  WARN: {f.name} invalid source '{data.get('source')}'")
            entries.append(data)
        except Exception as e:
            print(f"  ERROR: {f.name}: {e}")
    return entries


def main() -> int:
    by_component = "--by-component" in sys.argv
    entries = load()
    if not entries:
        print("No scenarios found.")
        return 1

    print(f"{'='*60}")
    print(f" Scenario Library Coverage Report")
    print(f"{'='*60}")
    print(f" Total: {len(entries)} scenarios\n")

    # By status
    print("── By Status ──")
    for status in sorted(VALID_STATUS):
        count = sum(1 for e in entries if e["status"] == status)
        pct = count * 100 // len(entries) if entries else 0
        bar = "█" * (count * 20 // len(entries)) if entries else ""
        print(f"  {status:16} {count:3}  {bar} {pct}%")
    passing = sum(1 for e in entries if e["status"] == "passing")
    print(f"\n  Pass rate: {passing}/{len(entries)} = {passing*100//len(entries)}%")

    # By capability
    print(f"\n── By Capability ──")
    caps: dict[str, list[dict]] = {}
    for e in entries:
        caps.setdefault(e["capability"], []).append(e)
    for cap in sorted(caps):
        items = caps[cap]
        p = sum(1 for e in items if e["status"] == "passing")
        print(f"  {cap:30} {p}/{len(items)} passing")

    # By source
    print(f"\n── By Source ──")
    for src in sorted(VALID_SOURCE):
        items = [e for e in entries if e["source"] == src]
        if items:
            sr_refs = [e["srRef"] for e in items if e.get("srRef")]
            ref_note = f" (refs: {', '.join(sr_refs)})" if sr_refs else ""
            print(f"  {src:20} {len(items):3}{ref_note}")

    if by_component:
        print(f"\n── By Component ──")
        comps: dict[str, int] = {}
        for e in entries:
            for c in e.get("components", []):
                comps[c] = comps.get(c, 0) + 1
        for comp in sorted(comps, key=comps.get, reverse=True):
            print(f"  {comp:25} {comps[comp]:3} scenarios")

    # Pending items
    pending = [e for e in entries if e["status"] in ("failing", "pending-env", "not-implemented")]
    if pending:
        print(f"\n── Attention ({len(pending)}) ──")
        for e in pending:
            print(f"  {e['id']:20} {e['status']:16} {e['name']}")

    print(f"\n{'='*60}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
