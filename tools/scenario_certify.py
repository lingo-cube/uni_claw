#!/usr/bin/env python3
"""SIM-002 G2 (S2): scenario golden certification writer — the ONLY sanctioned
path that writes `certification` blocks into scenarios/*.json.

C8 golden-expectation-update protocol, mechanically enforced:
- expectation migrations must piggyback the causal change (--change <ID>, required);
- certification binds (expectations digest, runtime source hash, causal change, date);
- the test runtime (ScenarioCertification.cs) is Verify-only — it never writes.

Canonical renderings MUST stay byte-identical with the C# side
(tests/UniClaw.Simulation.Tests/ScenarioCertification.cs); the two independent
implementations double as a cross-language consistency check against the same
stamped data.

Usage:
  python3 tools/scenario_certify.py --change SIM-002 --all
  python3 tools/scenario_certify.py --change RUN-004 --scenario SCN-WIFI-001
  python3 tools/scenario_certify.py --check            # verify only, no write
  python3 tools/scenario_certify.py --print-source-hash # print runtimeSourceHash only

SIM-003 G1: runtimeSourceHash hashes CRLF→LF-normalized bytes — the digest is
checkout-invariant (LF and CRLF checkouts of the same content produce the same
hash). .gitattributes eol pins are hygiene, not the correctness mechanism.
SIM-003 G2: certification schema v2 pins TWO digests with separate duties:
  expectationsDigest → what result is expected (cert-exp-v1 canonical)
  executionDigest    → which executable carrier tests it (cert-exec-v1 canonical)
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCENARIOS = ROOT / "scenarios"
SOURCE_PROJECTS = ("src/UniClaw.Kernel", "src/UniClaw.Agent")

EXPECTATIONS_TAG = "cert-exp-v1"
EXECUTION_TAG = "cert-exec-v1"
SOURCE_TAG = "cert-src-v1"
BLOCK_SCHEMA_VERSION = 2
VALID_EXECUTION_KINDS = ("golden-bundle", "none")


# ---- canonical renderings (byte-identical contract with the C# side) ----

def expectations_digest(exp: dict) -> str:
    def opt(value):
        return "-" if value is None else value

    line2 = (
        f"status={exp['status']}"
        f"|classification={opt(exp.get('classification'))}"
        f"|effects={exp['effects']}"
        f"|agentConsultations={exp['agentConsultations']}"
        f"|unconsumedStimuli={exp['unconsumedStimuli']}"
        f"|goalSatisfaction={opt(exp.get('goalSatisfaction'))}"
    )
    return hashlib.sha256((EXPECTATIONS_TAG + "\n" + line2).encode("utf-8")).hexdigest()


def _render_option_value(value) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)


def execution_digest(exec: dict) -> str:
    """cert-exec-v1 canonical rendering（与 C# 侧逐字节一致）：
    kind / carrier（null → "-"）/ options（key 稳定排序，k=v 逗号连接；
    无 options → "-"）。只覆盖路由三元组，不混入期望值。"""
    def opt(value):
        return "-" if value is None else value

    options = exec.get("options") or {}
    rendered_options = ",".join(
        f"{key}={_render_option_value(options[key])}" for key in sorted(options)
    ) or "-"
    line2 = (
        f"kind={exec['kind']}"
        f"|carrier={opt(exec.get('carrier'))}"
        f"|options={rendered_options}"
    )
    return hashlib.sha256((EXECUTION_TAG + "\n" + line2).encode("utf-8")).hexdigest()


def runtime_source_hash() -> str:
    files: list[str] = []
    for project in SOURCE_PROJECTS:
        base = ROOT / project
        for pattern in ("*.cs", "*.csproj"):
            # 仓库相对 POSIX 路径（与 C# 侧 GetRelativePath 一致——认证跨机可复现）
            files.extend(p.relative_to(ROOT).as_posix() for p in base.rglob(pattern))
    files = sorted(
        p for p in files if "/bin/" not in p and "/obj/" not in p
    )
    h = hashlib.sha256()
    h.update((SOURCE_TAG + "\0").encode("utf-8"))
    for rel in files:
        # G1: 文本源统一 CRLF→LF 再哈希——LF/CRLF checkout 同 digest
        data = (ROOT / rel).read_bytes().replace(b"\r\n", b"\n")
        h.update(f"{len(rel)}:{rel}{len(data)}:".encode("utf-8") + data)
    return h.hexdigest()


# ---- verify / certify ------------------------------------------------------

def verify_entry(data: dict, filename: str, source_hash: str) -> list[str]:
    problems: list[str] = []
    if "expectations" not in data:
        return [f"{filename}: 缺 expectations 块"]
    if "execution" not in data or not isinstance(data["execution"], dict):
        return [f"{filename}: 缺 execution 块（SIM-003 G3：execution.kind/carrier/options）"]
    cert = data.get("certification")
    if not isinstance(cert, dict):
        return [f"{filename}: 缺 certification 块（经 scenario_certify.py --change <致因change> 认证）"]

    if cert.get("schemaVersion") != BLOCK_SCHEMA_VERSION:
        problems.append(
            f"{filename}: certification.schemaVersion 须为 {BLOCK_SCHEMA_VERSION}"
            f"（实际 {cert.get('schemaVersion')}；v1 已废弃——经 scenario_certify.py --change SIM-003 迁移）"
        )
    if not cert.get("certifiedByChange"):
        problems.append(f"{filename}: certification.certifiedByChange 缺失（C8：期望改动必须搭乘致因 change）")
    if not cert.get("certifiedAt"):
        problems.append(f"{filename}: certification.certifiedAt 缺失")

    actual = expectations_digest(data["expectations"])
    if cert.get("expectationsDigest") != actual:
        problems.append(
            f"{filename}: expectations 摘要不匹配——期望值在认证后被改动且未重认证"
            f"（经 scenario_certify.py --change <致因change> 重认证）"
        )

    execution = data["execution"]
    if execution.get("kind") not in VALID_EXECUTION_KINDS:
        problems.append(
            f"{filename}: execution.kind 非法 '{execution.get('kind')}'（legal: {'|'.join(VALID_EXECUTION_KINDS)}）"
        )
    elif execution.get("kind") == "golden-bundle" and not execution.get("carrier"):
        problems.append(f"{filename}: execution.kind=golden-bundle 须携带非空 carrier")
    elif execution.get("kind") == "none" and execution.get("carrier"):
        problems.append(f"{filename}: execution.kind=none 不得携带 carrier")

    if "executionDigest" not in cert:
        problems.append(f"{filename}: certification.executionDigest 缺失（SIM-003 G2 v2）")
    elif execution.get("kind") in VALID_EXECUTION_KINDS:
        if cert.get("executionDigest") != execution_digest(execution):
            problems.append(
                f"{filename}: execution 摘要不匹配——执行绑定在认证后被改动且未重认证"
                f"（carrier/options 变更必须搭乘致因 change 经 scenario_certify.py 重认证）"
            )

    if not cert.get("runtimeSourceHash"):
        problems.append(f"{filename}: certification.runtimeSourceHash 缺失")
    elif cert["runtimeSourceHash"] != source_hash:
        problems.append(
            f"{filename}: 运行时源码哈希不匹配——Kernel/Agent 源在认证后变更"
            f"（经 scenario_certify.py --change <致因change> 重认证）"
        )
    return problems


def certify_file(path: Path, change: str, source_hash: str, today: str) -> bool:
    """Write/refresh the certification block. Returns True when content changed."""
    data = json.loads(path.read_text(encoding="utf-8"))
    if "execution" not in data:
        raise SystemExit(
            f"ERROR: {path.name} 缺 execution 块——先补 execution 绑定（SIM-003 G3），再认证"
        )
    cert = {
        "schemaVersion": BLOCK_SCHEMA_VERSION,
        "expectationsDigest": expectations_digest(data["expectations"]),
        "executionDigest": execution_digest(data["execution"]),
        "runtimeSourceHash": source_hash,
        "certifiedByChange": change,
        "certifiedAt": today,
    }
    if data.get("certification") == cert:
        return False
    data["certification"] = cert
    path.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--change", help="致因 change id（认证必带；C8 搭乘协议）")
    parser.add_argument("--scenario", help="单个场景 id，如 SCN-WIFI-001")
    parser.add_argument("--all", action="store_true", help="认证全部场景")
    parser.add_argument("--check", action="store_true", help="仅验证，不写入")
    parser.add_argument("--print-source-hash", action="store_true",
                        help="只打印当前 runtimeSourceHash（M5 跨语言对拍用，不写入）")
    args = parser.parse_args()

    if args.print_source_hash:
        print(runtime_source_hash())
        return 0

    files = sorted(SCENARIOS.glob("SCN-*.json"))
    if args.scenario:
        files = [f for f in files if f.stem == args.scenario]
        if not files:
            print(f"ERROR: 未找到场景 {args.scenario}")
            return 2

    if args.check or not (args.all or args.scenario):
        source_hash = runtime_source_hash()
        problems: list[str] = []
        for f in files:
            problems.extend(verify_entry(json.loads(f.read_text(encoding="utf-8")), f.name, source_hash))
        for p in problems:
            print(f"  FAIL {p}")
        print(f"certification check: {'PASS' if not problems else 'FAIL'} ({len(files)} files, {len(problems)} violations)")
        return 0 if not problems else 1

    if not args.change:
        print("ERROR: 认证必须显式携带致因 change（--change <ID>，C8 搭乘协议）；拒绝无记录重认证")
        return 2

    source_hash = runtime_source_hash()
    today = datetime.date.today().isoformat()
    changed = 0
    for f in files:
        if certify_file(f, args.change, source_hash, today):
            changed += 1
            print(f"  certified {f.name}  (change={args.change})")
    print(f"done: {changed}/{len(files)} certification blocks written/refreshed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
