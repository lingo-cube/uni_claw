#!/usr/bin/env python3
"""finalize-change — OpenSpec change 收尾闭环。

规则：tasks.md 全部勾选 → [归档] → 投影再生 → [workitem 联动] → 提示 verify。
消除"新 change 忘同步投影 / 归档忘标 workitem"的机械遗漏（regen 与
archive-workitems 的编排入口，供 .ai/openspec-workflow 与 agent 调用）。

用法:
  python3 scripts/finalize-change.py <change_name>                  # 活跃度收尾（regen 投影）
  python3 scripts/finalize-change.py <change_name> --archive        # 归档（git mv + regen）
  python3 scripts/finalize-change.py <change_name> --archive --workitem CS-XXX
  python3 scripts/finalize-change.py <change_name> --dry-run        # 预览
"""
import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHANGES = ROOT / "openspec" / "changes"
ARCHIVE = CHANGES / "archive"
REGEN = ROOT / "scripts" / "regenerate-projections.py"
ARCHIVE_WORKITEMS = ROOT / "scripts" / "archive-workitems.py"


def run(cmd, cwd=None):
    proc = subprocess.run(cmd, cwd=cwd or str(ROOT),
                          capture_output=True, text=True)
    if proc.returncode != 0:
        print("  ! %s 退出码 %d: %s" % (" ".join(cmd), proc.returncode,
                                       proc.stderr.strip()[-300:]), file=sys.stderr)
    return proc.returncode


def tasks_completion(name):
    tasks = CHANGES / name / "tasks.md"
    if not tasks.is_file():
        return None, "tasks.md 缺失"
    text = tasks.read_text(encoding="utf-8")
    done = len(re.findall(r"^- \[x\]", text, re.M))
    total = len(re.findall(r"^- \[.\]", text, re.M))
    return (done, total), None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("change_name")
    ap.add_argument("--archive", action="store_true",
                    help="把 change 归档到 openspec/changes/archive/（git mv）")
    ap.add_argument("--workitem", default=None,
                    help="按 change_set_id 把关联 workitem 标 archived")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    src = CHANGES / args.change_name
    if not (src / "proposal.md").is_file():
        print("ERROR: 找不到 active change: %s" % args.change_name)
        return 1

    status = tasks_completion(args.change_name)
    if status[1]:
        print("[check] WARNING %s" % status[1])
    else:
        done, total = status[0]
        print("[check] tasks.md 完成度: %d/%d%s" % (
            done, total, "" if done == total else "（未完项检测到 — 仍可收尾，但可能过早）"))
        if done != total and not args.archive:
            print("       注意: 未全部勾选。若为新 change 收尾（非归档），继续。")

    if args.archive:
        dst = ARCHIVE / args.change_name
        if dst.exists():
            print("ERROR: archive 已存在同名 change: %s" % args.change_name)
            return 1
        if args.dry_run:
            print("[archive] (dry-run) 将 git mv %s -> archive/%s" % (
                args.change_name, args.change_name))
        else:
            src.rename(dst)  # 同文件系统 rename ≈ git mv；再交由 git 追踪
            print("[archive] moved: openspec/changes/archive/%s" % args.change_name)

    print("[projection] %s" % ("(dry-run) 预览" if args.dry_run else "regenerate..."))
    regen_cmd = [sys.executable, str(REGEN)] + (["--dry-run"] if args.dry_run else [])
    if run(regen_cmd) != 0:
        print("WARNING: 投影再生失败，请检查输出（不阻断）")

    if args.workitem:
        cmd = [sys.executable, str(ARCHIVE_WORKITEMS), args.workitem]
        if args.dry_run:
            cmd.append("--dry-run")
        print("[workitem] %s" % " ".join(cmd))
        if run(cmd) != 0:
            print("WARNING: workitem 联动失败（change_set_id 无匹配？）")

    print("\n下一步: git add -A && bash scripts/verify-before-commit.sh && git commit"
          + ("  （dry-run 未写盘）" if args.dry_run else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())