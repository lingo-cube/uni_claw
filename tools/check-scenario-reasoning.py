#!/usr/bin/env python3
"""Guard: scenario-reasoning document structural integrity.

Checks docs/design/scenario-reasoning-v0.1.md for:

  1. Scenario rows (| SR-xxx | ... |) count within 100..200, ids unique;
  2. Every scenario row carries an anchor verdict token (✅ / ◐ / D / NB);
  3. Group headers G1..G13 and class sections C1..C13 all present;
  4. Every class section contains the four required reasoning elements:
     走法 / 数学 / 哲学 / 判定.

Run from repo root:  python3 tools/check-scenario-reasoning.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOC = ROOT / "docs/design/scenario-reasoning-v0.1.md"

VERDICT_TOKENS = ("✅", "◐", "D（", "D ", "D|", "NB")
REQUIRED_ELEMENTS = ("走法", "数学", "哲学", "判定")
GROUPS = [f"G{i}" for i in range(1, 14)]
CLASSES = [f"C{i}" for i in range(1, 14)]


def main() -> int:
    text = DOC.read_text(encoding="utf-8")
    errors: list[str] = []

    rows = re.findall(r"^\|\s*(SR-\d{3})\s*\|.+$", text, re.M)
    ids = [r[0] if isinstance(r, tuple) else r for r in rows]
    # re.findall with one group returns strings
    ids = re.findall(r"^\|\s*(SR-\d{3})\s*\|", text, re.M)
    full_rows = re.findall(r"^\|\s*(SR-\d{3})\s*\|.*\|\s*(.*?)\s*\|\s*$", text, re.M)

    n = len(ids)
    if not (100 <= n <= 200):
        errors.append(f"scenario count {n} outside 100..200")
    if len(set(ids)) != n:
        dupes = sorted({i for i in ids if ids.count(i) > 1})
        errors.append(f"duplicate scenario ids: {dupes}")

    for sid, last_cell in full_rows:
        if not any(tok in last_cell for tok in ("✅", "◐", "NB", "D")):
            errors.append(f"{sid}: anchor cell lacks verdict token (last cell: {last_cell!r})")

    for g in GROUPS:
        if f"### {g} " not in text and f"## {g}" not in text:
            errors.append(f"missing group header {g}")
    for c in CLASSES:
        if f"### {c} " not in text:
            errors.append(f"missing class section {c}")
            continue
        seg = re.search(
            rf"^### {c} .+?(?=^### C\d+ |\Z)", text, re.M | re.S
        )
        if seg:
            for el in REQUIRED_ELEMENTS:
                if f"**{el}**" not in seg.group(0):
                    errors.append(f"class {c}: missing element **{el}**")

    if errors:
        print("SCENARIO-REASONING CHECK: FAIL")
        for e in errors:
            print(f"  - {e}")
        return 1
    print(
        f"SCENARIO-REASONING CHECK: PASS "
        f"({n} scenarios, 13 groups, 13 classes, 4 elements each)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
