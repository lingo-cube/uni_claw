#!/usr/bin/env python3
"""PROTOCOL-MACHINE-001 — protocol state-machine generator (Slice 1).

Single source: schemas/protocol/state-machine.yaml. This tool is the ONLY
sanctioned writer of the generated enforcement artifact consumed by the C#
teeth (tests/UniClaw.Kernel.Tests/Protocol/protocol-machine.generated.json).

Discipline (D12, aligned with the C8 writer protocol):
  --check            freshness (regenerate + byte-compare, no writes) PLUS the
                     Slice 3 graph audit: reachability DFS, per-machine hang
                     whitelist exact match, termination cycle bisection,
                     anchor resolution. Non-zero exit on drift or violation.
  --audit-report     print the evidence-friendly audit report (hang lists,
                     cycle classification, measure annotations) and exit.
  (writer mode)      regenerate and write. If the artifact would change and no
                     --change <ID> is given, refuse (semantic diff must
                     piggyback its causal change); pure regeneration (no
                     byte diff) never requires --change.
  Meta-validation always runs first: closed vocabularies (kind / status /
  measure / await), required keys, self-loop liveness dichotomy (progress
  loops must carry a well-founded measure; silent loops must declare
  await: external and may not fake progress).

Determinism: output bytes are a pure function of the YAML — stable sort order,
no timestamps, LF, trailing newline.

Usage:
  python3 tools/gen-protocol-machine.py --check
  python3 tools/gen-protocol-machine.py --change PROTOCOL-MACHINE-001
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "schemas" / "protocol" / "state-machine.yaml"
ARTIFACT = (
    ROOT / "tests" / "UniClaw.Kernel.Tests" / "Protocol"
    / "protocol-machine.generated.json"
)

VERSION = 1
MEASURES = {"steps", "rounds", "defer-rounds", "cap-remaining"}
AWAITS = {"external"}
KINDS = {"edge", "gap"}
STATUSES = {"keep", "fix", "track"}
EDGE_ID_PREFIXES = ("P", "O", "G")
COMPONENTS = {
    "uni-agent", "uni-kernel", "run-model", "control-loop", "assurance",
    "effect-boundary", "capability-plane", "evidence-ledger", "world-model",
    "memory-system", "user-session", "simulation-host",
}
WORKSPACE_SCHEMAS = ROOT / "schemas" / "workspace"
BASELINE = (
    ROOT / "docs" / "architecture" / "protocols"
    / "inter-component-protocol-baseline-l1-l3.md"
)
INTERACTION_MODEL = ROOT / "docs" / "design" / "protocol-workflow-model-v0.1.html"

# A2/R1：每机悬挂白名单的期望值（精确匹配——多、少、失真都是 RED）。
EXPECTED_HANG_WHITELIST: dict[str, set[str]] = {
    "run": set(),
    "binding": set(),
    "judgment": set(),
    "attempt": set(),
    "contract-view": {"admitted"},  # Deferred ⑰（activation seam 未闭合）
}


def anchor_target(edge_id: str) -> Path:
    """D6 断锚解析目标：P## → 协议基线；G## → 交互模型（gap 锚住在 HTML
    步骤表）；O## → 无散文锚住处，逐字执法走 payload schema $id（见下）。"""
    if edge_id.startswith("P"):
        return BASELINE
    return INTERACTION_MODEL


def anchor_resolves(anchor: str, haystack: str) -> bool:
    """逐字命中，或命中前括号注释前的锚头（G 锚是 YAML 侧组合串：
    「任务定义（交互模型前奏 0a：…）」在 HTML 里是分字段的步骤名）。"""
    if anchor in haystack:
        return True
    head = anchor.split("（", 1)[0].strip()
    return bool(head) and head in haystack


def anchor_o_slug_ok(edge: dict) -> tuple[bool, str]:
    """O## 锚执法（D5/D7）：wire 边无散文锚住处，双方契约 schema 即
    canonical 锚——锚头「O# · <slug>」的 slug 必须逐字等于 payload_ref
    schema $id 的末段。slug 漂移 / payload_ref 缺失 / 非 schema = 断锚。"""
    head = str(edge.get("title_anchor") or "").split("（", 1)[0].strip()
    slug = head.split("·", 1)[1].strip() if "·" in head else ""
    pref = edge.get("payload_ref") or {}
    expected = str(pref.get("name") or "").rstrip("/").rsplit("/", 2)[-2]
    if pref.get("type") != "schema" or not expected:
        return False, f"O-edge anchor requires payload_ref type=schema, got {pref!r}"
    if slug != expected:
        return False, f"anchor slug {slug!r} != payload schema $id tail {expected!r}"
    return True, ""


def workspace_schema_ids() -> dict[str, str]:
    """D7：schemas/workspace 的 $id 清单 → 文件名（归属执法 ①② 的基准）。"""
    import json as _json

    ids: dict[str, str] = {}
    for path in sorted(WORKSPACE_SCHEMAS.glob("*.json")):
        try:
            schema = _json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            raise RuntimeError(f"unreadable workspace schema {path.name}: {exc}")
        sid = schema.get("$id")
        if sid:
            ids[sid] = path.name
    return ids


def fix_ref_exists(fix_ref: str) -> bool:
    """D11：fix_ref 必须解析到存在的 change（state.md）或 ADR 文件。"""
    if (ROOT / "changes" / fix_ref / "state.md").exists():
        return True
    return any(p.name.startswith(fix_ref) for p in (ROOT / "docs" / "adr").glob("*.md"))


# ---- Slice 3：图审计（纯函数，不写文件、无时间戳） ----

def elementary_cycles(name: str, machine: dict) -> list[list[dict]]:
    """枚举有向图全部初等环（节点 ≤ 10，穷举 DFS 足够）。

    返回环的边列表；同一节点集只报一次（以最小节点为起点规范化）。"""
    nodes = sorted(machine.get("nodes") or [])
    adj: dict[str, list[dict]] = {n: [] for n in nodes}
    for t in machine.get("transitions") or []:
        if t.get("from") in adj and t.get("to") in adj:
            adj[t["from"]].append(t)

    found: dict[tuple[str, ...], list[dict]] = {}
    for start in nodes:
        stack: list[dict] = []

        def dfs(current: str) -> None:
            for t in adj[current]:
                if t["to"] == start:
                    # 路径节点 = start + stack 各边的 to；start 为环内最小
                    # 节点（下方 to > start 剪枝），路径即规范化键。
                    path = tuple([start] + [e["to"] for e in stack])
                    found.setdefault(path, stack + [t])
                elif t["to"] > start and t["to"] not in [e["to"] for e in stack] \
                        and t["to"] != current:
                    stack.append(t)
                    dfs(t["to"])
                    stack.pop()

        dfs(start)
    return list(found.values())


def audit_graph(doc: dict) -> tuple[list[str], list[str]]:
    """可达性 + 悬挂白名单精确匹配 + 终止性环二分 + 断锚解析。

    返回 (violations, report)；violations 非空 = --check RED。"""
    reasons: list[str] = []
    report: list[str] = ["== graph audit =="]

    machines = doc.get("machines") or {}
    edges = doc.get("edges") or []

    # ---- ④ 断锚解析（A4）----
    unresolved = 0
    for e in sorted(edges, key=lambda e: e["id"]):
        anchor = e.get("title_anchor")
        eid = e["id"]
        if not anchor:
            reasons.append(f"edges.{eid}: title_anchor missing (断锚)")
            unresolved += 1
            continue
        if eid.startswith("O"):
            ok, detail = anchor_o_slug_ok(e)
            if not ok:
                reasons.append(f"edges.{eid}: title_anchor does not resolve "
                               f"against its payload schema contract (断锚): {detail}")
                unresolved += 1
            continue
        target = anchor_target(eid)
        try:
            haystack = target.read_text(encoding="utf-8")
        except OSError as exc:
            reasons.append(f"edges.{eid}: anchor target unreadable "
                           f"{target.name}: {exc}")
            unresolved += 1
            continue
        if not anchor_resolves(anchor, haystack):
            reasons.append(f"edges.{eid}: title_anchor does not resolve in "
                           f"{target.name} (断锚): {anchor!r}")
            unresolved += 1
    report.append(f"anchors: {len(edges) - unresolved}/{len(edges)} resolved "
                  f"(P## → {BASELINE.name}; O## → payload schema $id; "
                  f"G## → {INTERACTION_MODEL.name})")

    # ---- ①②③ 每机审计 ----
    for name in sorted(machines):
        machine = machines[name]
        nodes = set(machine.get("nodes") or [])
        transitions = machine.get("transitions") or []
        terminal_nodes = set(machine.get("terminal_nodes") or [])
        report.append(f"-- machine {name} ({len(nodes)} nodes, "
                      f"{len(transitions)} transitions) --")

        # ① 可达性：从 initial DFS；hang_whitelist 节点 = 显式登记的外部
        # 接缝入口（如 contract-view 的 admitted，Deferred ⑰），作为次级
        # DFS 根——否则白名单悬挂与其余节点可达性互斥。其余不可达 = RED。
        adj: dict[str, set[str]] = {n: set() for n in nodes}
        for t in transitions:
            if t.get("from") in adj:
                adj[t["from"]].add(t.get("to"))
        initial = machine.get("initial")
        whitelist = set(machine.get("hang_whitelist") or [])
        reachable: set[str] = set()
        roots = [n for n in [initial, *sorted(whitelist)] if n in nodes]
        frontier = list(roots)
        while frontier:
            cur = frontier.pop()
            if cur in reachable:
                continue
            reachable.add(cur)
            frontier.extend(adj[cur] - reachable)
        unreachable = sorted(nodes - reachable)
        if unreachable and not whitelist:
            reasons.append(f"{name}: unreachable nodes from initial "
                           f"{initial!r} (DFS): {unreachable}")
        report.append(f"reachability: {len(reachable)}/{len(nodes)} reachable "
                      f"(roots: initial + hang_whitelist seam entries)"
                      + (f"; UNREACHABLE {unreachable}" if unreachable else "")
                      + ("; seam-pending machine (non-empty hang_whitelist) — "
                         "unreachable downstream tolerated, pending seam closure"
                         if unreachable and whitelist else ""))

        # ② 悬挂：白名单精确匹配（期望值 + 双向）。
        outgoing = {t.get("from") for t in transitions}
        hanging = sorted(nodes - terminal_nodes - outgoing)
        whitelist = set(machine.get("hang_whitelist") or [])
        expected = EXPECTED_HANG_WHITELIST.get(name)
        if expected is not None and whitelist != expected:
            reasons.append(f"{name}.hang_whitelist: exact match against the "
                           f"per-machine expected value failed — declared "
                           f"{sorted(whitelist)}, expected {sorted(expected)}")
        distort = sorted(whitelist - set(hanging))
        if distort:
            reasons.append(f"{name}.hang_whitelist: entries that do NOT hang "
                           f"(白名单失真): {distort}")
        outside = sorted(set(hanging) - whitelist)
        if outside:
            reasons.append(f"{name}: nodes without outgoing transition "
                           f"(not terminal, not in hang_whitelist): {outside}")
        report.append(f"hang: {hanging or '{}'}; whitelist {sorted(whitelist) or '{}'}")

        # ③ 终止性：环二分（D10）。
        measure_summary: list[str] = []
        for t in sorted(transitions, key=lambda t: t["id"]):
            if t.get("measure"):
                measure_summary.append(f"{t['id']}:{t['measure']}")
            elif t.get("await"):
                measure_summary.append(f"{t['id']}:await:{t['await']}")
        cycles = elementary_cycles(name, machine)
        progress = silent = 0
        for cycle in cycles:
            ids = sorted(t["id"] for t in cycle)
            measures = {t["measure"] for t in cycle if t.get("measure")}
            awaits = {t["id"] for t in cycle if t.get("await")}
            label = " -> ".join([cycle[0]["from"]] + [t["to"] for t in cycle])
            if awaits:
                silent += 1
                if measures:
                    reasons.append(
                        f"{name}: silent cycle must not carry a measure "
                        f"(伪装进展, D10): {ids} measures={sorted(measures)}")
                report.append(f"cycle [silent/await]: {label} ({', '.join(ids)})")
            else:
                progress += 1
                if not measures:
                    reasons.append(
                        f"{name}: progress cycle without a well-founded "
                        f"measure (终止性, D10): {ids} — {label}")
                else:
                    bad = measures - MEASURES
                    if bad:
                        reasons.append(f"{name}: cycle measure {sorted(bad)} "
                                       f"outside closed vocabulary: {ids}")
                report.append(f"cycle [progress, measure {sorted(measures) or '∅'}]: "
                              f"{label} ({', '.join(ids)})")
        report.append(f"termination: {progress} progress cycle(s), "
                      f"{silent} silent cycle(s); measures/awaits: "
                      f"{measure_summary or '{}'}")
    return reasons, report


def fail(reasons: list[str]) -> int:
    for r in reasons:
        print(f"FAIL: {r}", file=sys.stderr)
    return 1


def validate(doc: dict) -> list[str]:
    reasons: list[str] = []

    if doc.get("version") != VERSION:
        reasons.append(f"version must be {VERSION}, got {doc.get('version')!r}")
    machines = doc.get("machines")
    if not isinstance(machines, dict) or not machines:
        reasons.append("machines: must be a non-empty mapping")
        return reasons

    schema_ids = workspace_schema_ids()
    referenced_schema_ids: set[str] = set()
    edge_ids: set[str] = set()
    gap_ids: set[str] = set()
    for entry in doc.get("edges") or []:
        eid = entry.get("id") if isinstance(entry, dict) else None
        if not eid:
            reasons.append("edges: entry without id")
            continue
        if eid[0] not in EDGE_ID_PREFIXES or not eid[1:].isdigit():
            reasons.append(f"edges.{eid}: id must match P##/O##/G## namespace")
        if entry.get("kind") not in KINDS:
            reasons.append(f"edges.{eid}: kind must be one of {sorted(KINDS)}")
        if entry.get("status") not in STATUSES:
            reasons.append(f"edges.{eid}: status must be one of {sorted(STATUSES)}")
        if entry.get("status") == "fix":
            fix_ref = entry.get("fix_ref")
            if not fix_ref:
                reasons.append(f"edges.{eid}: status=fix requires fix_ref (change id)")
            elif not fix_ref_exists(fix_ref):
                reasons.append(f"edges.{eid}: fix_ref {fix_ref!r} does not resolve to "
                               "changes/<id>/state.md or an ADR file")
        if entry.get("from") not in COMPONENTS:
            reasons.append(f"edges.{eid}: from {entry.get('from')!r} outside closed component vocabulary")
        if entry.get("to") not in COMPONENTS:
            reasons.append(f"edges.{eid}: to {entry.get('to')!r} outside closed component vocabulary")
        pref = entry.get("payload_ref")
        if entry.get("kind") == "gap":
            gap_ids.add(eid)
            if pref is not None:
                reasons.append(f"edges.{eid}: gap edges must not carry payload_ref "
                               "(gap 只进渲染与审计，不进转移合法性)")
        else:
            if pref is None:
                # payload_ref 缺席仅允许 status: track（边尚无真实 realization；
                # kind=edge 且已落地的边必须指明载荷目标）。
                if entry.get("status") != "track":
                    reasons.append(f"edges.{eid}: kind=edge with status keep|fix "
                                   "requires payload_ref with type + name")
            elif not isinstance(pref, dict) or not pref.get("type") or not pref.get("name"):
                reasons.append(f"edges.{eid}: kind=edge requires payload_ref with type + name")
            else:
                if pref["type"] == "schema":
                    if pref["name"] not in schema_ids:
                        reasons.append(
                            f"edges.{eid}: payload_ref schema {pref['name']!r} is not "
                            "a $id of any schemas/workspace/*.json (悬空 ref)")
                    else:
                        referenced_schema_ids.add(pref["name"])
                elif pref["type"] == "record":
                    # python 侧只查形状；反射存在性由 C# 牙执法（D12 分层）。
                    if "." not in str(pref["name"]):
                        reasons.append(f"edges.{eid}: record payload_ref must be a "
                                       f"full type name, got {pref['name']!r}")
                else:
                    reasons.append(f"edges.{eid}: payload_ref type must be record|schema, "
                                   f"got {pref['type']!r}")
        edge_ids.add(eid)

    # D7 ①：无主 schema = RED —— 每个 workspace $id 必须被 ≥1 条边引用。
    for sid in sorted(set(schema_ids) - referenced_schema_ids):
        reasons.append(f"ownership: workspace schema {sid} ({schema_ids[sid]}) "
                       "is not referenced by any edge (无主 schema)")

    for name, machine in sorted(machines.items()):
        nodes = set(machine.get("nodes") or [])
        if not nodes:
            reasons.append(f"{name}.nodes: must be a non-empty list")
            continue
        initial = machine.get("initial")
        if initial not in nodes:
            reasons.append(f"{name}.initial: {initial!r} not a node")
        terminal_nodes = set(machine.get("terminal_nodes") or [])
        if not terminal_nodes:
            reasons.append(f"{name}.terminal_nodes: must be declared and non-empty "
                           "(Slice 3 图审计输入，不可缺席)")
        for key in ("terminal_nodes", "absorbing", "hang_whitelist"):
            declared = set(machine.get(key) or [])
            outside = declared - nodes
            if outside:
                reasons.append(f"{name}.{key}: {sorted(outside)} not a node")
        absorbing = set(machine.get("absorbing") or [])
        if absorbing - terminal_nodes:
            reasons.append(f"{name}.absorbing: {sorted(absorbing - terminal_nodes)} "
                           "must be declared terminal")
        transitions = machine.get("transitions") or []
        if not transitions:
            reasons.append(f"{name}.transitions: must be a non-empty list")
            continue
        outgoing: set[str] = set()
        seen_ids: set[str] = set()
        for t in transitions:
            tid = t.get("id")
            if not tid:
                reasons.append(f"{name}: transition without id")
                continue
            if tid in seen_ids:
                reasons.append(f"{name}.{tid}: duplicate transition id")
            seen_ids.add(tid)
            where = f"{name}.{tid}"
            for key in ("from", "to", "trigger", "guard", "emits"):
                if key not in t:
                    reasons.append(f"{where}: missing required key {key!r}")
            if t.get("from") not in nodes:
                reasons.append(f"{where}: from {t.get('from')!r} not a node")
            if t.get("to") not in nodes:
                reasons.append(f"{where}: to {t.get('to')!r} not a node")
            trigger = t.get("trigger")
            if not isinstance(trigger, dict) or not trigger.get("event"):
                reasons.append(f"{where}: trigger requires event")
            edge = (trigger or {}).get("edge")
            if edge is not None:
                if edge not in edge_ids:
                    reasons.append(f"{where}: trigger.edge {edge!r} not registered in edges")
                elif edge in gap_ids:
                    # D11：gap 永不进转移合法性——挂上转移表 = RED。
                    reasons.append(f"{where}: trigger.edge {edge!r} is kind: gap — "
                                   "gaps must never enter transition legality")
            if not isinstance(t.get("emits"), list):
                reasons.append(f"{where}: emits must be a list (may be empty)")
            outgoing.add(t.get("from"))

            self_loop = t.get("from") == t.get("to")
            measure = t.get("measure")
            await_ = t.get("await")
            # D10: progress measures annotate cycle-forming edges (a cycle may
            # span several states); silent waiting must be a self-loop.
            if measure is not None:
                if measure not in MEASURES:
                    reasons.append(f"{where}: measure {measure!r} outside {sorted(MEASURES)}")
                if await_ is not None:
                    reasons.append(f"{where}: silent loop must not carry a measure (D10)")
            if await_ is not None:
                if not self_loop:
                    reasons.append(f"{where}: await: external is only valid on a self-loop")
                if await_ not in AWAITS:
                    reasons.append(f"{where}: await {await_!r} outside {sorted(AWAITS)}")
                if measure is not None:
                    reasons.append(f"{where}: silent loop must not carry a measure (D10)")
            if self_loop and measure is None and await_ is None:
                reasons.append(
                    f"{where}: self-loop must carry exactly one of "
                    "measure (progress loop) or await: external (silent loop)")

        # 形状：吸收态不得有出边；非终态且未列入悬挂白名单的节点必须有出路。
        if absorbing & outgoing:
            reasons.append(f"{name}: absorbing nodes must have no outgoing "
                           f"transitions: {sorted(absorbing & outgoing)}")
        hanging = nodes - terminal_nodes - set(machine.get("hang_whitelist") or []) - outgoing
        if hanging:
            reasons.append(f"{name}: nodes without outgoing transition (not terminal, "
                           f"not in hang_whitelist): {sorted(hanging)}")
    return reasons


def render(doc: dict) -> str:
    """Deterministic JSON projection consumed by the C# teeth."""
    payload = {
        "version": doc["version"],
        "source": "schemas/protocol/state-machine.yaml",
        "machines": {},
        "edges": [
            {
                "id": e["id"],
                "kind": e["kind"],
                "status": e["status"],
                "from": e["from"],
                "to": e["to"],
                "titleAnchor": e.get("title_anchor"),
                "payloadRef": (
                    {
                        "type": e["payload_ref"]["type"],
                        "name": e["payload_ref"]["name"],
                    }
                    if e.get("payload_ref")
                    else None
                ),
            }
            for e in sorted(doc.get("edges") or [], key=lambda e: e["id"])
        ],
    }
    for name in sorted(doc["machines"]):
        machine = doc["machines"][name]
        payload["machines"][name] = {
            "initial": machine["initial"],
            "terminalNodes": list(machine.get("terminal_nodes") or []),
            "absorbing": list(machine.get("absorbing") or []),
            "hangWhitelist": list(machine.get("hang_whitelist") or []),
            "nodes": sorted(machine["nodes"]),
            "transitions": [
                {
                    "id": t["id"],
                    "from": t["from"],
                    "to": t["to"],
                    "trigger": {
                        "event": t["trigger"]["event"],
                        "edge": t["trigger"].get("edge"),
                    },
                    "guard": t["guard"],
                    "emits": sorted(t["emits"]),
                    "measure": t.get("measure"),
                    "await": t.get("await"),
                }
                for t in sorted(machine["transitions"], key=lambda t: t["id"])
            ],
        }
    return json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="verify-only: freshness + graph audit, no write")
    parser.add_argument("--audit-report", action="store_true",
                        help="print the evidence-friendly graph audit report")
    parser.add_argument("--change", metavar="ID",
                        help="causal change id, required when writing over a diff")
    args = parser.parse_args()

    doc = yaml.safe_load(SOURCE.read_text(encoding="utf-8"))
    reasons = validate(doc)
    if reasons:
        return fail(reasons)

    audit_reasons, audit_report = audit_graph(doc)

    if args.audit_report:
        print("\n".join(audit_report))
        return 1 if audit_reasons else 0

    generated = render(doc).encode("utf-8")
    existing = ARTIFACT.read_bytes() if ARTIFACT.exists() else None

    if args.check:
        if audit_reasons:
            return fail(audit_reasons)
        if existing is None:
            print(f"FAIL: artifact missing: {ARTIFACT}", file=sys.stderr)
            return 1
        if existing != generated:
            print("FAIL: protocol-machine.generated.json is stale — rerun "
                  "tools/gen-protocol-machine.py --change <causal-id>", file=sys.stderr)
            return 1
        print(f"OK: protocol machine fresh ({len(doc['machines'])} machines, "
              f"{sum(len(m['transitions']) for m in doc['machines'].values())} transitions, "
              f"{len(doc.get('edges') or [])} edges) — graph audit passed: "
              "reachability, hang whitelist exact match, cycle bisection, "
              "anchors (details: --audit-report)")
        return 0

    if existing != generated and not args.change:
        print("FAIL: source semantic diff detected — writing requires "
              "--change <causal change id> (C8 protocol)", file=sys.stderr)
        return 1

    ARTIFACT.parent.mkdir(parents=True, exist_ok=True)
    ARTIFACT.write_bytes(generated)
    print(f"OK: wrote {ARTIFACT.relative_to(ROOT)}"
          + (f" (change {args.change})" if args.change else " (pure regeneration)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
