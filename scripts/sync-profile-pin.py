#!/usr/bin/env python3
"""sync-profile-pin — 维护 .dsh/profile-adapter/profile-source.yaml 的 source_revision
（规则文件集内容指纹，lockfile 模式）。

默认模式: 把两处 pin（宽松字段 + JSON 块）原子、幂等地更新为当前指纹。
--check:   只报告（不写盘），供 scripts/verify-before-commit.sh 调用。
"""
import argparse
import importlib.util
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
YAML_PATH = ROOT / ".dsh" / "profile-adapter" / "profile-source.yaml"
PIN_DIRS = (".ai/profiles", ".ai/schemas", "tools/agent_profile_validator.py")

SPEC = importlib.util.spec_from_file_location(
    "dsh_profile_adapter", ROOT / "tools" / "dsh_profile_adapter.py")
adapter = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(adapter)


def pin_files_changed():
    proc = subprocess.run(
        ["git", "diff", "--name-only", "HEAD", "--"] + list(PIN_DIRS),
        cwd=str(ROOT), capture_output=True, text=True)
    return bool(proc.stdout.strip())


def read_pin(text):
    m = re.search(r"(?m)^\s*source_revision:\s*([0-9a-fA-F]{40,64})\s*$", text)
    return m.group(1) if m else ""


def sync_text(text, fingerprint):
    text = re.sub(r"(?m)^(\s*source_revision:\s*)[0-9a-fA-F]{40,64}\s*$",
                  r"\g<1>" + fingerprint, text)
    text = re.sub(r'("source_revision":\s*")[0-9a-fA-F]{40,64}(")',
                  r"\g<1>" + fingerprint + r"\g<2>", text)
    return text


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="只报告状态（PIN/YAML_PIN/FILES_CHANGED），不写盘")
    args = ap.parse_args()

    fingerprint = adapter.profile_source_fingerprint(ROOT)
    text = YAML_PATH.read_text(encoding="utf-8")
    pin = read_pin(text)
    changed = pin_files_changed()

    if args.check:
        print("PIN=%s" % fingerprint)
        print("YAML_PIN=%s" % pin)
        print("FILES_CHANGED=%s" % ("yes" if changed else "no"))
        return 0

    if pin == fingerprint:
        print("pin already in sync: %s" % fingerprint)
        return 0
    tmp_path = YAML_PATH.with_name(YAML_PATH.name + ".tmp")
    tmp_path.write_text(sync_text(text, fingerprint), encoding="utf-8")
    tmp_path.replace(YAML_PATH)
    print("pin synced: %s -> %s%s" % (
        pin or "(none)", fingerprint,
        " (pin files changed in worktree)" if changed else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())