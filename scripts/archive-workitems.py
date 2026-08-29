#!/usr/bin/env python3
"""archive-workitems — change 归档后把关联 workitem 标为 archived。

按 change_set_id 匹配 docs/work/active/workitems/*.json；幂等。

用法:
  python3 scripts/archive-workitems.py --list                 # 列出 change_set_id -> workitems
  python3 scripts/archive-workitems.py CS-XXX --dry-run       # 预览
  python3 scripts/archive-workitems.py CS-XXX                 # 标 archived
"""
import argparse
import glob
import json
import sys
from pathlib import Path

WORKITEMS = Path(__file__).resolve().parents[1] / "docs" / "work" / "active" / "workitems"


def load_all():
    items = []
    for path in sorted(glob.glob(str(WORKITEMS / "WI-*.json"))):
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        items.append((Path(path).name, data))
    return items


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("change_set_id", nargs="?", default=None)
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    items = load_all()
    if args.list:
        by_change = {}
        for name, data in items:
            by_change.setdefault(data.get("change_set_id", "?"), []).append(
                "%s(%s)" % (name, data.get("status", "?")))
        for cid in sorted(by_change):
            print("%-60s %s" % (cid, ", ".join(by_change[cid])))
        return 0
    if not args.change_set_id:
        ap.error("需要 change_set_id（或 --list）")

    matched = [it for it in items
               if it[1].get("change_set_id") == args.change_set_id]
    if not matched:
        print("无匹配 workitem: %s" % args.change_set_id)
        return 1
    for name, data in matched:
        action = "would mark" if args.dry_run else "marking"
        if data.get("status") == "archived" and not args.dry_run:
            print("already archived: %s" % name)
            continue
        print("%s archived: %s" % (action, name))
        if not args.dry_run:
            data["status"] = "archived"
            path = WORKITEMS / name
            tmp = path.with_suffix(".json.tmp")
            tmp.write_text(json.dumps(
                data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            tmp.replace(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())