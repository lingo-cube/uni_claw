#!/usr/bin/env python3
"""PROTOCOL-MACHINE-001 — protocol state-machine generator (Slice 1).

Single source: schemas/protocol/state-machine.yaml. This tool is the ONLY
sanctioned writer of the generated enforcement artifact consumed by the C#
teeth (tests/UniClaw.Kernel.Tests/Protocol/protocol-machine.generated.json).

Discipline (D12, aligned with the C8 writer protocol):
  --check            freshness (regenerate + byte-compare, no writes) over ALL
                     generated artifacts (C# teeth json + interaction-model
                     steps js + protocol map fragment + per-domain protocol
                     cards) PLUS the Slice 3 graph audit: reachability DFS,
                     per-machine hang whitelist exact match, termination cycle
                     bisection, anchor resolution. Non-zero exit on drift or
                     violation.
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
STEPS_JS = ROOT / "docs" / "design" / "protocol-workflow-steps.generated.js"
PROTOCOL_MAP = (
    ROOT / "docs" / "architecture" / "protocols" / "protocol-map.generated.md"
)
CARDS_DIR = ROOT / "docs" / "protocol-cards"

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
    "contract-view": set(),
    "activation-record": set(),
    "consultation-journal": set(),
}

# Slice 4：交互模型（HTML 27 步）单源。步骤是渲染数据：组/编号/边引用/
# 方向/状态/字段注记全量在此，HTML 手写副本已删除（A5）。
STEP_GROUPS = {"pre", "contract", "observe", "decide", "act", "verify", "terminal"}
STEP_STATUSES = {"keep", "fix", "track"}
FIELD_MARKS = {"必", "选", "准", "系", "—"}
EXPECTED_STEP_COUNT = 27
# st:'fix' 的 fixTag → 致因 change（与 fix_ref 同一解析执法：断链 = RED）。
FIXTAG_CHANGE = {
    "S1": "PROTOCOL-FIX-001",
    "S5": "PROTOCOL-FIX-001",
    "S2": "PROTOCOL-FIX-002",
    "S3": "PROTOCOL-FIX-003",
    "S4": "PROTOCOL-FIX-004",
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
            # G 锚的步骤名在 Slice 4 后迁居生成 js（STEPS 单源），锚住处
            # = HTML 页面 + 生成 steps 数据两者并集。
            if eid.startswith("G") and STEPS_JS.exists():
                haystack += STEPS_JS.read_text(encoding="utf-8")
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

    reasons.extend(validate_steps(doc, edge_ids))
    return reasons


def validate_steps(doc: dict, edge_ids: set[str]) -> list[str]:
    """Slice 4：交互模型 steps 段形状断言（渲染静默坏页面必须被上游拦住）。

    执法面：步骤总数钉扎（27，A1 全量转录）；必填键缺失 = RED；
    mark / st / group 越封闭词汇 = RED；st='fix' 必带 fixTag 且 fix_ref
    解析到致因 change；步骤引用的边 id 必须存在于 edges，且步骤 status
    与所引用边 status 一致（track⇔track、fix⇒keep、keep⇒keep|track）。"""
    reasons: list[str] = []
    im = doc.get("interaction_model")
    if not isinstance(im, dict) or not isinstance(im.get("steps"), list) \
            or not im["steps"]:
        reasons.append("interaction_model: must be a mapping with a non-empty "
                       "'steps' list")
        return reasons

    steps = im["steps"]
    if len(steps) != EXPECTED_STEP_COUNT:
        reasons.append(f"interaction_model: step count {len(steps)} != "
                       f"{EXPECTED_STEP_COUNT} (27 步全量转录，A1)")
    for i, s in enumerate(steps):
        where = f"steps[{i}]"
        if not isinstance(s, dict):
            reasons.append(f"{where}: must be a mapping")
            continue
        label = s.get("no") or s.get("name") or i
        where = f"steps[{label}]"
        for key in ("group", "no", "name", "edge", "st", "fields"):
            if key not in s or s[key] in (None, "", []):
                reasons.append(f"{where}: missing required key {key!r}")
        if s.get("group") not in STEP_GROUPS:
            reasons.append(f"{where}: group {s.get('group')!r} outside "
                           f"{sorted(STEP_GROUPS)}")
        st = s.get("st")
        if st not in STEP_STATUSES:
            reasons.append(f"{where}: st {st!r} outside {sorted(STEP_STATUSES)}")
        # fix 家族键只允许 st='fix' 携带；fix 必须落到致因 change。
        fix_keys = [k for k in ("fixTag", "fix", "fix_ref") if k in s]
        if st == "fix":
            if not fix_keys:
                reasons.append(f"{where}: st='fix' requires fixTag + fix + fix_ref")
            else:
                tag = s.get("fixTag")
                expected_change = FIXTAG_CHANGE.get(tag)
                if expected_change is None:
                    reasons.append(f"{where}: fixTag {tag!r} not in "
                                   f"{sorted(FIXTAG_CHANGE)}")
                elif s.get("fix_ref") != expected_change:
                    reasons.append(f"{where}: fix_ref {s.get('fix_ref')!r} != "
                                   f"fixTag {tag!r} causal change "
                                   f"{expected_change!r}")
                elif not fix_ref_exists(s["fix_ref"]):
                    reasons.append(f"{where}: fix_ref {s['fix_ref']!r} does not "
                                   "resolve to changes/<id>/state.md or an ADR")
        elif fix_keys:
            reasons.append(f"{where}: fix-family keys {fix_keys} only valid "
                           "with st='fix'")
        # 边引用存在性 + 步骤/边 status 一致性。
        ref = s.get("edge_ref")
        if ref is not None:
            if ref not in edge_ids:
                reasons.append(f"{where}: edge_ref {ref!r} not registered in edges")
            else:
                edge_status = next(e["status"] for e in doc["edges"] if e["id"] == ref)
                if st == "track" and edge_status != "track":
                    reasons.append(f"{where}: st='track' cites {ref} with "
                                   f"status={edge_status!r} (步骤/边 status 不一致)")
                elif st == "fix" and edge_status != "keep":
                    reasons.append(f"{where}: st='fix' cites {ref} with "
                                   f"status={edge_status!r} (步骤/边 status 不一致)")
                elif st == "keep" and edge_status not in ("keep", "track"):
                    reasons.append(f"{where}: st='keep' cites {ref} with "
                                   f"status={edge_status!r} (步骤/边 status 不一致)")
        elif "edge_ref" in s:
            reasons.append(f"{where}: edge_ref present but empty")
        fields = s.get("fields")
        if isinstance(fields, list):
            for j, f in enumerate(fields):
                if not isinstance(f, dict):
                    reasons.append(f"{where}.fields[{j}]: must be a mapping")
                    continue
                for key in ("name", "type", "desc", "mark"):
                    if key not in f:
                        reasons.append(f"{where}.fields[{j}]: missing required "
                                       f"key {key!r}")
                if f.get("mark") not in FIELD_MARKS:
                    reasons.append(f"{where}.fields[{j}]: mark "
                                   f"{f.get('mark')!r} outside {sorted(FIELD_MARKS)}")
    return reasons


# ---- Slice 4：三出口（fragment / steps js / 每域卡片）——纯派生，无时间戳 ----

def edge_sort_key(edge_id: str):
    return (edge_id[0], int(edge_id[1:]))


def payload_summary(edge: dict) -> str:
    pref = edge.get("payload_ref") or {}
    if not pref:
        return "—"
    if pref.get("type") == "schema":
        return f"schema {str(pref['name']).rstrip('/').rsplit('/', 1)[-1]}"
    return f"record {pref['name']}"


def edge_guards(doc: dict, edge_ids: set[str]) -> dict[str, list[str]]:
    """转移合法性表反查：每条被引用边的 guard 摘要（机.转移 → guard）。"""
    out: dict[str, list[str]] = {}
    for name in sorted(doc.get("machines") or {}):
        for t in doc["machines"][name].get("transitions") or []:
            ref = (t.get("trigger") or {}).get("edge")
            if ref in edge_ids:
                out.setdefault(ref, []).append(f"{name}.{t['id']}: {t.get('guard', '')}")
    return out


def render_protocol_map(doc: dict) -> str:
    lines = [
        "# Protocol Map（生成产物）",
        "",
        "> 派生自 `schemas/protocol/state-machine.yaml`（PROTOCOL-MACHINE-001 单源）。",
        "> 本文件是生成产物，手改会被 `--check` 漂移检测抓出；协议语义权威是",
        "> COMPONENT-BASELINE 文档 `inter-component-protocol-baseline-l1-l3.md`，",
        "> 本视图不引入边表之外的协议事实。",
        "",
        "## 机器概览（5 台）",
        "",
        "| 机 | initial | 节点数 | 转移数 | 终态 | 悬挂白名单 |",
        "|---|---|---|---|---|---|",
    ]
    machines = doc.get("machines") or {}
    for name in sorted(machines):
        m = machines[name]
        lines.append(
            f"| {name} | {m['initial']} | {len(m.get('nodes') or [])} "
            f"| {len(m.get('transitions') or [])} "
            f"| {', '.join(sorted(m.get('terminal_nodes') or []))} "
            f"| {', '.join(sorted(m.get('hang_whitelist') or [])) or '∅'} |")
    edges = sorted(doc.get("edges") or [], key=lambda e: edge_sort_key(e["id"]))
    lines += [
        "",
        f"## 协议边表（{len(edges)} 条；P## 协议边 / O## wire·宿主边 / G## known gap）",
        "",
        "| id | kind | status | 方向 | 载荷 | 基线锚 |",
        "|---|---|---|---|---|---|",
    ]
    for e in edges:
        anchor = str(e.get("title_anchor") or "").replace("|", "\\|")
        lines.append(
            f"| {e['id']} | {e['kind']} | {e['status']} "
            f"| {e['from']} → {e['to']} | {payload_summary(e)} | {anchor} |")
    lines.append("")
    return "\n".join(lines)


def render_steps_js(doc: dict) -> str:
    """HTML 交互模型渲染数据（<script src> 加载，file:// 可用，不依赖 fetch）。

    键形与页面渲染层契约（g/no/nm/edge/dir/st/fixTag/fix/note/fields[n/t/d/req]）。"""
    steps = []
    for s in (doc.get("interaction_model") or {}).get("steps") or []:
        steps.append({
            "g": s["group"],
            "no": s["no"],
            "nm": s["name"],
            "edge": s["edge"],
            "dir": s.get("dir", ""),
            "st": s["st"],
            **({"fixTag": s["fixTag"]} if "fixTag" in s else {}),
            **({"fix": s["fix"]} if "fix" in s else {}),
            **({"note": s["note"]} if s.get("note") else {}),
            "fields": [
                {"n": f["name"], "t": f["type"], "d": f["desc"], "req": f["mark"]}
                for f in s.get("fields") or []
            ],
        })
    header = ("// 生成产物（PROTOCOL-MACHINE-001 单源："
              "schemas/protocol/state-machine.yaml interaction_model 段）。\n"
              "// 手改会被 tools/gen-protocol-machine.py --check 漂移检测抓出。\n")
    return header + "window.PROTOCOL_STEPS = " + json.dumps(
        steps, ensure_ascii=False, indent=1) + ";\n"


def render_cards(doc: dict) -> dict[str, str]:
    """每域协议卡片（D8 第四出口）：边表按组件过滤的派生视图。"""
    edges = {e["id"]: e for e in doc.get("edges") or []}
    guards = edge_guards(doc, set(edges))
    by_component: dict[str, list[str]] = {}
    for eid in sorted(edges, key=edge_sort_key):
        e = edges[eid]
        by_component.setdefault(e["from"], []).append(eid)
        if e["to"] != e["from"]:
            by_component.setdefault(e["to"], []).append(eid)
    cards: dict[str, str] = {}
    for comp in sorted(by_component):
        incoming = [i for i in by_component[comp] if edges[i]["to"] == comp]
        outgoing = [i for i in by_component[comp] if edges[i]["from"] == comp]
        lines = [
            f"# 协议卡片 · {comp}（生成产物）",
            "",
            "> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001",
            "> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。",
            "",
        ]
        for title, ids, direction in (
                ("出边（本组件 → 对端）", outgoing, True),
                ("入边（对端 → 本组件）", incoming, False)):
            if not ids:
                continue
            lines += [f"## {title}", "",
                      "| id | kind | status | 方向 | 载荷 | 基线锚 |", "|---|---|---|---|---|---|"]
            for eid in ids:
                e = edges[eid]
                arrow = f"{e['from']} → {e['to']}"
                anchor = str(e.get("title_anchor") or "").replace("|", "\\|")
                lines.append(f"| {eid} | {e['kind']} | {e['status']} | {arrow} "
                             f"| {payload_summary(e)} | {anchor} |")
            lines.append("")
        related_guards = [(eid, guards[eid]) for eid in by_component[comp]
                          if eid in guards]
        if related_guards:
            lines += ["## 转移合法性 guard 摘要（引用本组件参与边的转移）", ""]
            for eid, entries in related_guards:
                lines.append(f"- **{eid}**")
                lines.extend(f"  - {entry}" for entry in entries)
            lines.append("")
        cards[f"{comp}.md"] = "\n".join(lines)
    return cards


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
    steps_js = render_steps_js(doc).encode("utf-8")
    protocol_map = render_protocol_map(doc).encode("utf-8")
    cards = {name: body.encode("utf-8")
             for name, body in render_cards(doc).items()}

    targets: list[tuple[Path, bytes]] = [(ARTIFACT, generated),
                                         (STEPS_JS, steps_js),
                                         (PROTOCOL_MAP, protocol_map)]
    targets += [(CARDS_DIR / name, body) for name, body in cards.items()]

    def current(path: Path) -> bytes | None:
        return path.read_bytes() if path.exists() else None

    if args.check:
        if audit_reasons:
            return fail(audit_reasons)
        stale = [path for path, body in targets if current(path) != body]
        if stale:
            for path in stale:
                print(f"FAIL: stale generated artifact: {path.relative_to(ROOT)} — "
                      "rerun tools/gen-protocol-machine.py --change <causal-id>",
                      file=sys.stderr)
            return 1
        print(f"OK: protocol machine fresh ({len(doc['machines'])} machines, "
              f"{sum(len(m['transitions']) for m in doc['machines'].values())} transitions, "
              f"{len(doc.get('edges') or [])} edges, "
              f"{len((doc.get('interaction_model') or {}).get('steps') or [])} steps, "
              f"{len(cards)} domain cards) — graph audit passed: "
              "reachability, hang whitelist exact match, cycle bisection, "
              "anchors (details: --audit-report)")
        return 0

    drifted = [path for path, body in targets if current(path) != body]
    if drifted and not args.change:
        print("FAIL: source semantic diff detected — writing requires "
              "--change <causal change id> (C8 protocol)", file=sys.stderr)
        return 1

    for path, body in targets:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(body)
    stale_cards = [p for p in sorted(CARDS_DIR.glob("*.md"))
                   if (CARDS_DIR / p.name) not in {path for path, _ in targets}]
    for p in stale_cards:
        p.unlink()
    print(f"OK: wrote {len(targets)} generated artifact(s) "
          f"(json + steps js + protocol map + {len(cards)} cards)"
          + (f" (change {args.change})" if args.change else " (pure regeneration)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
