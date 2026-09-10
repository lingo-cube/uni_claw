#!/usr/bin/env python3
"""Validate .dsh/model-bindings.yaml against model-routing.yaml (MRB-001).

Deterministic contract check:
  1. every tier in model-routing.yaml has a binding entry
  2. every binding entry maps to a declared tier (no extras / typos)
  3. each binding has primary {provider, model}; fallback optional but,
     if present, must carry both fields
Exit 0 = pass; non-zero with reasons = fail.
"""
from __future__ import annotations

import sys
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parent.parent
ROUTING = REPO / "model-routing.yaml"
BINDINGS = REPO / ".dsh" / "model-bindings.yaml"


def fail(reasons: list[str]) -> int:
    for r in reasons:
        print(f"FAIL: {r}", file=sys.stderr)
    return 1


def main() -> int:
    reasons: list[str] = []
    routing = yaml.safe_load(ROUTING.read_text(encoding="utf-8"))
    bindings_doc = yaml.safe_load(BINDINGS.read_text(encoding="utf-8"))

    tiers = set(routing.get("tiers") or {})
    entries = bindings_doc.get("bindings") or {}
    if not tiers:
        reasons.append("model-routing.yaml declares no tiers")
    if not entries:
        reasons.append(".dsh/model-bindings.yaml declares no bindings")

    missing = tiers - set(entries)
    if missing:
        reasons.append(f"tiers without binding: {sorted(missing)}")
    extra = set(entries) - tiers
    if extra:
        reasons.append(f"bindings for undeclared tiers: {sorted(extra)}")

    for tier, entry in sorted(entries.items()):
        if not isinstance(entry, dict):
            reasons.append(f"{tier}: binding must be a mapping")
            continue
        for slot in ("primary", "fallback"):
            if slot in entry and entry[slot] is not None:
                target = entry[slot]
                if (
                    not isinstance(target, dict)
                    or not target.get("provider")
                    or not target.get("model")
                ):
                    reasons.append(f"{tier}.{slot}: needs provider + model")
        if "primary" not in entry or entry.get("primary") is None:
            reasons.append(f"{tier}: primary binding is required")

    if reasons:
        return fail(reasons)

    for tier in sorted(tiers):
        p = entries[tier]["primary"]
        line = f"{tier}: {p['provider']}/{p['model']}"
        fb = entries[tier].get("fallback")
        if fb:
            line += f" (fallback {fb['provider']}/{fb['model']})"
        print(f"OK: {line}")
    print(f"validate-model-bindings: {len(tiers)} tier bindings pass")
    return 0


if __name__ == "__main__":
    sys.exit(main())
