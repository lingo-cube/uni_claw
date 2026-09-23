#!/usr/bin/env python3
"""Scenario Library Coverage Report — SIM-002 G3: coverage truth chain.

`status` is derived from REAL test executions (TRX), never from JSON
self-report. The acceptance this enforces: a scenario reported "passing"
means "at this HEAD, with these certified expectations, its mapped test just
executed and passed".

Truth chain:
  1. test-case ↔ scenario-id mapping lives WITH the tests:
     [Trait("Scenario", "SCN-…")] (C# enforcer: ScenarioCertificationTests).
  2. Mapping discovered from the BINARY via `dotnet test --list-tests
     --filter Scenario=<id>`（TRX 不携带 xUnit traits——vstest 版本局限，
     故映射与结果经 FQN 在工具内 join）。
  3. TRX (dotnet test --logger trx) carries FQN → outcome.
  4. This tool derives each scenario's status from mapped outcomes and
     requires the JSON-declared status to match.

Violations → exit 1:
  - 无结果: declared passing/failing/pending-env but no mapped test result
  - 结果不匹配: JSON status ≠ derived status
  - schema 违规: invalid/missing required fields
  - 陈旧执行: TRX older than any Kernel/Agent source, Simulation.Tests
    source, or scenario JSON (re-run dotnet test)
  - (G2) golden certification violations

Usage:
  python3 tools/scenario-coverage.py [--by-component] [--trx PATH] [--run]
  --run: execute `dotnet test tests/UniClaw.Simulation.Tests` with a TRX
  logger first (fresh execution), then report.
"""
from __future__ import annotations

import json, re, subprocess, sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from scenario_certify import runtime_source_hash, verify_entry

try:
    import jsonschema
except ImportError:  # pragma: no cover
    jsonschema = None

ROOT = Path(__file__).resolve().parent.parent
SCENARIOS = ROOT / "scenarios"
SCHEMA = SCENARIOS / "schema.json"
SIM_TESTS = ROOT / "tests" / "UniClaw.Simulation.Tests"
TRX_DIR = SIM_TESTS / "TestResults"
VALID_STATUS = {"passing", "failing", "pending-env", "not-implemented"}
VALID_SOURCE = {"recorded", "synthetic", "derived-from-doc", "bug-repro", "component-test"}
VALID_REALIZATION = {"real", "double"}

OUTCOME_TO_STATUS = {
    "Passed": "passing",
    "Failed": "failing",
    "NotExecuted": "pending-env",
    "Skipped": "pending-env",
}


def run_tests() -> Path:
    """Execute the simulation suite with a TRX logger; return the TRX path."""
    TRX_DIR.mkdir(parents=True, exist_ok=True)
    name = "coverage.trx"
    result = subprocess.run(
        ["dotnet", "test", str(SIM_TESTS), "--logger", f"trx;LogFileName={name}"],
        cwd=ROOT,
    )
    trx = TRX_DIR / name
    if result.returncode != 0:
        print(f"  FAIL: dotnet test 退出码 {result.returncode}（测试存在失败 = 场景真值，"
              f"以 TRX 派生状态为准继续）")
    if not trx.exists():
        print("  FAIL: dotnet test 未产出 TRX")
        sys.exit(2)
    return trx


def discover_trx(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit)
        if not path.exists():
            print(f"  FAIL: TRX 不存在: {path}")
            sys.exit(2)
        return path
    candidates = sorted(TRX_DIR.glob("*.trx")) if TRX_DIR.exists() else []
    if not candidates:
        print("  FAIL: 无 TRX（先跑 dotnet test --logger trx，或用 --run）")
        sys.exit(2)
    return candidates[-1]


def parse_trx(trx: Path) -> dict[str, str]:
    """TRX → {测试 FQN: outcome}（同名多次执行取最后一次；namespace 无关）。"""
    root = ET.parse(trx).getroot()
    outcomes: dict[str, str] = {}
    for result in root.iter():
        if result.tag.split("}")[-1] != "UnitTestResult":
            continue
        name, outcome = result.get("testName"), result.get("outcome")
        if name and outcome:
            outcomes[name] = outcome
    return outcomes


def scenario_test_map(scenario_ids: list[str]) -> dict[str, list[str]]:
    """scenario-id → 测试 FQN 清单（从二进制的 trait 发现；locale 无关）。"""
    mapping: dict[str, list[str]] = {}
    for scenario_id in scenario_ids:
        proc = subprocess.run(
            ["dotnet", "test", str(SIM_TESTS), "--list-tests",
             "--filter", f"Scenario={scenario_id}"],
            cwd=ROOT, capture_output=True, text=True,
        )
        fqns = re.findall(r"^\s+(UniClaw\.\S+)$", proc.stdout, re.M)
        mapping[scenario_id] = [f.strip() for f in fqns]
    return mapping


def freshness_violation(trx: Path) -> str | None:
    """TRX 必须不早于任何影响测试结果的内容（Kernel/Agent 源、仿真测试源、场景库）。"""
    watched: list[Path] = []
    for project in (ROOT / "src" / "UniClaw.Kernel", ROOT / "src" / "UniClaw.Agent"):
        for pattern in ("*.cs", "*.csproj"):
            watched.extend(p for p in project.rglob(pattern)
                           if "/bin/" not in p.as_posix() and "/obj/" not in p.as_posix())
    watched.extend(SIM_TESTS.rglob("*.cs"))
    watched.extend(SCENARIOS.glob("SCN-*.json"))
    newest = max(p.stat().st_mtime for p in watched)
    if trx.stat().st_mtime < newest:
        stale = max(watched, key=lambda p: p.stat().st_mtime)
        return f"TRX 陈旧（{trx.name} 早于 {stale.relative_to(ROOT).as_posix()} 的最后修改）——重跑 dotnet test 或使用 --run"
    return None


def derive_status(outcomes: list[str]) -> tuple[str | None, str | None]:
    if any(o == "Failed" for o in outcomes):
        return "failing", None
    if any(o in ("NotExecuted", "Skipped") for o in outcomes):
        return "pending-env", None
    if outcomes and all(o == "Passed" for o in outcomes):
        return "passing", None
    unknown = sorted(set(outcomes) - set(OUTCOME_TO_STATUS))
    if unknown:
        return None, f"未知 TRX outcome: {unknown}"
    return None, None


def load_schema_violations() -> tuple[dict | None, list[str]]:
    """S5：schema.json 必须是合法 JSON Schema（v2）；加载失败即违规。"""
    try:
        schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    except Exception as e:
        return None, [f"schema.json: 非法 JSON / 无法加载（{e}）——SIM-002 S5"]
    return schema, []


def validate_entry(schema: dict | None, data: dict, filename: str) -> list[str]:
    """S5：条目对 schema.json 的真校验（jsonschema，draft-07）。"""
    if jsonschema is None or schema is None:
        return []
    validator = jsonschema.Draft7Validator(schema)
    return [
        f"{filename}: schema 违规：{'/'.join(str(p) for p in error.absolute_path) or '<root>'}: {error.message}"
        for error in sorted(validator.iter_errors(data), key=lambda e: list(e.absolute_path))
    ]


def load(trx_outcomes: dict[str, str], test_map: dict[str, list[str]]) -> tuple[list[dict], list[str]]:
    entries = []
    violations: list[str] = []
    source_hash = runtime_source_hash()
    schema, schema_problems = load_schema_violations()
    violations.extend(schema_problems)
    for f in sorted(SCENARIOS.glob("*.json")):
        if f.name == "schema.json":
            continue
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
        except Exception as e:
            violations.append(f"{f.name}: JSON 解析失败（{e}）")
            continue
        # S5：schema v2 真校验（结构 / 枚举 / 必填 / additionalProperties）
        violations.extend(validate_entry(schema, data, f.name))
        # 语义级执法（schema 之外的策略）
        if data.get("status") not in VALID_STATUS:
            violations.append(f"{f.name}: invalid status '{data.get('status')}'")
        if data.get("source") not in VALID_SOURCE:
            violations.append(f"{f.name}: invalid source '{data.get('source')}'（S6：generated 已更名 synthetic）")
        for field in ("agentDecisionRealization", "goalEvaluationRealization"):
            if data.get(field) not in VALID_REALIZATION:
                violations.append(f"{f.name}: {field} 缺失或非法值 '{data.get(field)}'（legal: real|double）")
        # S8/C9：场景库 fail-closed——敏感内容评审未 cleared 的条目不得留在库内
        security = data.get("security") or {}
        if security.get("sensitiveReview") != "cleared":
            violations.append(
                f"{f.name}: security.sensitiveReview='{security.get('sensitiveReview')}'"
                "（C9：库内条目必须 cleared；未评审录制不入库）")
        violations.extend(verify_entry(data, f.name, source_hash))

        # 真值链：TRX 派生 status vs JSON 声明
        scenario_id = data.get("id", f.stem)
        mapped = test_map.get(scenario_id, [])
        outcomes = [trx_outcomes[fqn] for fqn in mapped if fqn in trx_outcomes]
        missing_tests = [fqn for fqn in mapped if fqn not in trx_outcomes]
        for fqn in missing_tests:
            violations.append(
                f"{f.name}: 映射测试在 TRX 中无结果（{fqn}——TRX 陈旧或与映射枚举不同源）")
        derived, problem = derive_status(outcomes)
        if problem:
            violations.append(f"{f.name}: {problem}")
        elif derived is None:
            if data.get("status") != "not-implemented" or mapped:
                violations.append(
                    f"{f.name}: 无结果——status='{data.get('status')}' 但没有测试承载"
                    f"（[Trait(\"Scenario\", \"{scenario_id}\")] 缺失或 TRX 陈旧）"
                )
        elif data.get("status") != derived:
            violations.append(
                f"{f.name}: 结果不匹配——JSON 声明 '{data.get('status')}'，"
                f"真实执行派生 '{derived}'（outcomes={outcomes}）"
            )
        data["_derived"] = derived or "not-implemented"
        entries.append(data)
    return entries, violations


def main() -> int:
    args = sys.argv[1:]
    by_component = "--by-component" in args
    do_run = "--run" in args
    trx_arg = None
    if "--trx" in args:
        trx_arg = args[args.index("--trx") + 1]

    trx = run_tests() if do_run else discover_trx(trx_arg)
    trx_outcomes = parse_trx(trx)
    scenario_ids = [p.stem for p in sorted(SCENARIOS.glob("SCN-*.json"))]
    test_map = scenario_test_map(scenario_ids)
    entries, violations = load(trx_outcomes, test_map)

    stale = freshness_violation(trx)
    if stale:
        violations.append(stale)

    if not entries:
        print("No scenarios found.")
        return 1

    print(f"{'='*60}")
    print(f" Scenario Library Coverage Report (truth: {trx.name})")
    print(f"{'='*60}")
    print(f" Total: {len(entries)} scenarios\n")

    print("── By Status (derived from TRX) ──")
    for status in sorted(VALID_STATUS):
        count = sum(1 for e in entries if e["_derived"] == status)
        pct = count * 100 // len(entries) if entries else 0
        bar = "█" * (count * 20 // len(entries)) if entries else ""
        print(f"  {status:16} {count:3}  {bar} {pct}%")
    passing = sum(1 for e in entries if e["_derived"] == "passing")
    print(f"\n  Pass rate: {passing}/{len(entries)} = {passing*100//len(entries)}%")

    print(f"\n── By Capability ──")
    caps: dict[str, list[dict]] = {}
    for e in entries:
        caps.setdefault(e["capability"], []).append(e)
    for cap in sorted(caps):
        items = caps[cap]
        p = sum(1 for e in items if e["_derived"] == "passing")
        print(f"  {cap:30} {p}/{len(items)} passing")

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

    pending = [e for e in entries if e["_derived"] in ("failing", "pending-env", "not-implemented")]
    if pending:
        print(f"\n── Attention ({len(pending)}) ──")
        for e in pending:
            print(f"  {e['id']:20} {e['_derived']:16} {e['name']}")

    print(f"\n── Golden Certification (SIM-002 G2) ──")
    certified_by = {}
    for e in entries:
        cert = e.get("certification") or {}
        if cert.get("certifiedByChange"):
            certified_by[cert["certifiedByChange"]] = certified_by.get(cert["certifiedByChange"], 0) + 1
    for change, count in sorted(certified_by.items()):
        print(f"  certified by {change}: {count} scenarios")

    if violations:
        print(f"\n  FAIL ({len(violations)} violations):")
        for v in violations:
            print(f"    {v}")
    else:
        print(f"  all {len(entries)} entries certified; truth chain verified against {trx.name}")

    print(f"\n{'='*60}")
    return 0 if not violations else 1


if __name__ == "__main__":
    sys.exit(main())
