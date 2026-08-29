#!/usr/bin/env python3
"""regenerate-projections — 机械再生 current-gates / latest 的派生投影。

只重写无权威派生部分（成员表、计数、GeneratedAt）；Gate Annotations、
Source precedence 等手工/权威文本原样保留。规则见
docs/work/active/current-gates.md 文件头 GenerationRule。

用法:
  python3 scripts/regenerate-projections.py --dry-run    # 只打印 diff
  python3 scripts/regenerate-projections.py              # 写盘（幂等）
"""
import argparse
import difflib
import datetime
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHANGES = ROOT / "openspec" / "changes"
GATES = ROOT / "docs" / "work" / "active" / "current-gates.md"
LATEST = ROOT / "docs" / "snapshots" / "latest.md"
TODAY = datetime.date.today().isoformat()

ACTIVE_HEADER = "| Change | Source reference |\n|---|---|\n"
ARCHIVE_HEADER = "| Archived change | Source reference |\n|---|---|\n"
TASK_PROGRESS_NOTE = (
    "Task progress is read from each linked `tasks.md`; this projection does not\n"
    "maintain a second aggregate completion/graduation status.\n"
)
COUNT_HEADER = "| Lifecycle view | Count |\n|---|---:|\n"


def collect():
    active_dirs = sorted(
        d for d in os.listdir(CHANGES)
        if d != "archive" and (CHANGES / d / "proposal.md").is_file())
    archived_dirs = sorted(
        d for d in os.listdir(CHANGES / "archive")
        if (CHANGES / "archive" / d).is_dir())
    active_rows = []
    for name in active_dirs:
        active_rows.append(
            "| `%s` | [proposal](../../../openspec/changes/%s/proposal.md) · "
            "[tasks](../../../openspec/changes/%s/tasks.md) |\n"
            % (name, name, name))
    archive_rows = [
        "| `%s` | [archive](../../../openspec/changes/archive/%s/) |\n"
        % (name, name) for name in archived_dirs]
    return active_dirs, archived_dirs, active_rows, archive_rows


def regenerate_gates(text, n_active, n_archived, active_rows, archive_rows):
    out = []
    # 1) 头部计数与生成日期
    text = re.sub(r"GeneratedAt(?::|: )`[0-9-]+`",
                  "GeneratedAt: `%s`" % TODAY, text, count=1)
    text = re.sub(r"ActiveChangeCount(?::|: )`\d+`",
                  "ActiveChangeCount: `%d`" % n_active, text, count=1)
    text = re.sub(r"ArchivedChangeCount(?::|: )`\d+`",
                  "ArchivedChangeCount: `%d`" % n_archived, text, count=1)

    # 2) active 成员表（标题 + 表头 + 行 + Task progress 注记），保留 Gate Annotations
    start = text.index("## Generated Active Change Membership")
    end = text.index("## Gate Annotations")
    block = ("## Generated Active Change Membership — %d\n\n"
             % n_active + ACTIVE_HEADER + "".join(active_rows) +
             "\n" + TASK_PROGRESS_NOTE + "\n")
    text = text[:start] + block + text[end:]

    # 3) archived 表（标题 + 表头 + 行），保留 Count check 及其后
    start = text.index("## Historical Archived")
    end = text.index("## Count check")
    block = ("## Historical Archived — %d\n\n" % n_archived +
             ARCHIVE_HEADER + "".join(archive_rows) + "\n")
    text = text[:start] + block + text[end:]

    # 4) Count check 表
    text = re.sub(r"\| Current Active \| \d+ \|",
                  "| Current Active | %d |" % n_active, text, count=1)
    text = re.sub(r"\| Historical Archived \| \d+ \|",
                  "| Historical Archived | %d |" % n_archived, text, count=1)
    return text


def regenerate_latest(text, n_active, n_archived):
    text = re.sub(r"GeneratedAt(?::|: )`[0-9-]+`",
                  "GeneratedAt: `%s`" % TODAY, text, count=1)
    text = re.sub(r"ActiveChangeCount(?::|: )`\d+`",
                  "ActiveChangeCount: `%d`" % n_active, text, count=1)
    text = re.sub(r"ArchivedChangeCount(?::|: )`\d+`",
                  "ArchivedChangeCount: `%d`" % n_archived, text, count=1)
    text = re.sub(r"lists \d+ Current Active changes and \d+ Historical Archived changes",
                  "lists %d Current Active changes and %d Historical Archived changes"
                  % (n_active, n_archived), text, count=1)
    return text


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true",
                    help="只打印与现有文件的 diff，不写盘")
    args = ap.parse_args()

    active_dirs, archived_dirs, active_rows, archive_rows = collect()
    n_active, n_archived = len(active_dirs), len(archived_dirs)
    gates_new = regenerate_gates(
        GATES.read_text(encoding="utf-8"), n_active, n_archived,
        active_rows, archive_rows)
    latest_new = regenerate_latest(
        LATEST.read_text(encoding="utf-8"), n_active, n_archived)

    changed = False
    for path, new in ((GATES, gates_new), (LATEST, latest_new)):
        old = path.read_text(encoding="utf-8")
        if old == new:
            print("unchanged: %s" % path.relative_to(ROOT))
            continue
        changed = True
        print("CHANGED: %s" % path.relative_to(ROOT))
        if args.dry_run:
            sys.stdout.writelines(difflib.unified_diff(
                old.splitlines(keepends=True), new.splitlines(keepends=True),
                fromfile=str(path), tofile=str(path)))
        else:
            path.write_text(new, encoding="utf-8")
    if changed and args.dry_run:
        print("\n[dry-run] 未写盘 — 移除 --dry-run 落盘")
    return 0 if not changed else 0


if __name__ == "__main__":
    sys.exit(main())