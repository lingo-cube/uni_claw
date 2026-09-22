#!/usr/bin/env python3
"""Generate changes/INDEX.md — index of open (non-closed) changes.

Derived artifact: do not hand-edit. Regenerate with:

    python3 tools/gen-open-changes.py

GATE-001 P-E' hygiene item (open-gates generated index). Consistency-check
enforcement is deliberately deferred to phase 2 per GATE-001 scope.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CHANGES = ROOT / "changes"
INDEX = CHANGES / "INDEX.md"
HEADER_RE = re.compile(r"^lifecycle_state:\s*(.*)$", re.M)


def parse_state(path: Path) -> dict[str, str] | None:
    text = path.read_text(encoding="utf-8")
    m_head = HEADER_RE.search(text)
    if not m_head:
        return None
    m_title = re.match(r"^#\s+(.+)$", text, re.M)
    fields: dict[str, str] = {}
    first_value = ""
    for seg in m_head.group(1).split("·"):
        seg = seg.split("#", 1)[0].strip()
        if not seg:
            continue
        if ":" in seg:
            k, v = seg.split(":", 1)
            fields[k.strip()] = v.strip()
        elif not first_value:
            # "lifecycle_state: closed · disposition: ..." — the value itself
            # precedes the first '·', so the leading segment has no ':'.
            first_value = seg
    lifecycle = fields.get("lifecycle_state") or first_value
    if not lifecycle:
        return None
    return {
        "title": (m_title.group(1).strip() if m_title else path.parent.name).replace("|", "/"),
        "lifecycle": lifecycle,
        "disposition": fields.get("disposition", "?"),
        "depth": fields.get("depth", "?"),
    }


def main() -> int:
    rows: list[tuple[str, dict[str, str]]] = []
    total = 0
    for d in sorted(CHANGES.iterdir()):
        if not d.is_dir():
            continue
        state = d / "state.md"
        if not state.exists():
            continue
        total += 1
        info = parse_state(state)
        if info is None or info["lifecycle"] == "closed":
            continue
        rows.append((d.name, info))

    lines = [
        "# changes/INDEX.md — open changes（生成物，勿手改）",
        "",
        "> 派生索引：列出所有 lifecycle_state ≠ closed 的 change。",
        "> 再生：`python3 tools/gen-open-changes.py`（GATE-001 P-E′；一致性执法二期）。",
        f"> 统计：{total} changes · {len(rows)} open。",
        "",
        "| change | lifecycle_state | disposition | depth | title |",
        "|---|---|---|---|---|",
    ]
    for name, i in rows:
        lines.append(
            f"| {name} | {i['lifecycle']} | {i['disposition']} | {i['depth']} | {i['title']} |"
        )
    INDEX.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{INDEX.relative_to(ROOT)}: {total} changes, {len(rows)} open")
    return 0


if __name__ == "__main__":
    sys.exit(main())
