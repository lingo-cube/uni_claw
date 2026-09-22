#!/usr/bin/env python3
"""Guard: invariant-enforcement matrix completeness.

Checks that docs/analysis/invariant-enforcement-matrix.md contains exactly one
row per invariant declared in the architecture baseline:

  - Section 20 "Core Invariants"  -> invariants 1..42
  - Section 24.10 amendment block -> invariants 43..47

Fails (exit 1) on missing rows, duplicate ids, unknown ids, or verdicts
outside the allowed vocabulary. Run from repo root:

    python3 tools/check-invariant-matrix.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASELINE = ROOT / "docs/architecture/product-architecture-baseline-l0-l3.md"
MATRIX = ROOT / "docs/analysis/invariant-enforcement-matrix.md"

ALLOWED_VERDICTS = {"TEST", "STRUCT", "TEST+STRUCT", "PARTIAL", "DEFERRED"}


def baseline_invariant_ids() -> set[int]:
    text = BASELINE.read_text(encoding="utf-8")
    m20 = re.search(r"^## 20\. Core Invariants$(.*?)^## 21\.", text, re.M | re.S)
    if not m20:
        raise SystemExit("FATAL: cannot locate baseline section 20")
    ids: set[int] = set()
    for num in re.findall(r"^(\d{1,2})\.\s", m20.group(1), re.M):
        n = int(num)
        if 1 <= n <= 42:
            ids.add(n)
    for num in re.findall(r"^(4[3-7])\.\s", text, re.M):
        ids.add(int(num))
    return ids


def matrix_rows() -> dict[int, str]:
    text = MATRIX.read_text(encoding="utf-8")
    rows: dict[int, str] = {}
    for m in re.finditer(r"^\|\s*(\d{1,2})\s*\|([^|]*)\|([^|]*)\|", text, re.M):
        n = int(m.group(1))
        verdict = m.group(3).strip()
        if n in rows:
            raise SystemExit(f"FATAL: matrix has duplicate row for invariant {n}")
        rows[n] = verdict
    return rows


def main() -> int:
    base = baseline_invariant_ids()
    rows = matrix_rows()
    errors: list[str] = []

    missing = sorted(base - rows.keys())
    extra = sorted(rows.keys() - base)
    if missing:
        errors.append(f"missing rows for invariants: {missing}")
    if extra:
        errors.append(f"rows with unknown invariant ids: {extra}")
    for n in sorted(rows.keys() & base):
        verdict = rows[n]
        if verdict not in ALLOWED_VERDICTS:
            errors.append(
                f"invariant {n}: verdict '{verdict}' not in {sorted(ALLOWED_VERDICTS)}"
            )

    if errors:
        print("INVARIANT-MATRIX CHECK: FAIL")
        for e in errors:
            print(f"  - {e}")
        return 1
    print(f"INVARIANT-MATRIX CHECK: PASS ({len(rows)} rows = {len(base)} invariants)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
