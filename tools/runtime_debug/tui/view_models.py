"""TUI view models — pure, stdlib-only derivation of UI data from Query Core.

The TUI shell (app.py, textual) renders ONLY these models and collects input;
it never reimplements correlation, pruning, or analysis. All logic stays in
`runtime_debug.query` / `runtime_debug.sources`. Keep this module free of any
UI-framework import so it stays unit-testable without textual.
"""

from __future__ import annotations

from .. import query
from ..sources import bundle as bundle_source


def open_run(bundle_dir: str) -> dict:
    """One run view from a bundle directory (AssetRef index + stored facts)."""
    bundle = bundle_source.read_bundle(bundle_dir)
    assets = query.assets(bundle)
    return {
        "bundleId": bundle.capture_session_id,
        "traceId": bundle.trace_id,
        "scenarioId": bundle.scenario_id,
        "assetCount": assets["count"],
        "assets": assets["assets"],
        "recordCount": len(bundle.records),
        "hasTrace": bundle.trace is not None,
        "terminal": {
            "finalState": bundle.manifest.get("finalState"),
            "runtimeOutcome": bundle.manifest.get("runtimeOutcome"),
            "runtimeSucceeded": bundle.manifest.get("runtimeSucceeded"),
        },
    }


def tree_view(tree_result: dict | None) -> list[dict]:
    """Flatten a Core tree result (causal or execution) into depth-annotated
    rows for a tree widget. Deterministic pre-order."""
    if tree_result is None or tree_result.get("roots") is None:
        return []
    rows = []
    stack = [(node, 0) for node in reversed(tree_result.get("roots", []))]
    while stack:
        node, depth = stack.pop()
        rows.append({
            "depth": depth,
            "spanId": node.get("spanId"),
            "name": node.get("name") or node.get("stage"),
            "layer": node.get("layer"),
            "component": node.get("component"),
            "outcome": node.get("outcome") or node.get("status"),
            "startOffsetNs": node.get("startOffsetNs"),
            "durationNs": node.get("durationNs"),
            "summary": node.get("summary"),
            "observationSeq": node.get("observationSeq"),
            "frameAssetRefs": node.get("frameAssetRefs"),
            "actionKind": node.get("actionKind"),
        })
        for child in reversed(node.get("children") or []):
            stack.append((child, depth + 1))
    return rows


def filter_state(layers: str = "", components: str = "", names: str = "",
                 only_errors: bool = False,
                 time_from: int | None = None, time_to: int | None = None) -> dict:
    """Deterministic construction of execution-tree/causal-tree parameters."""
    split = lambda text: [s.strip() for s in text.split(",") if s.strip()]
    return {
        "hideLayers": split(layers),
        "hideComponents": split(components),
        "hideNames": split(names),
        "onlyErrors": only_errors,
        "timeFrom": time_from,
        "timeTo": time_to,
    }


def diagnosis_view(packet: dict | None = None, bundle_dir: str | None = None) -> dict:
    """Diagnosis panel: stored facts from a packet (terminal-chain) plus, when a
    bundle is given, the first mechanically failed execution span. Never
    computes semantics — surfaces what the Core already projected."""
    view = {"terminal": None, "chain": [], "storedDiagnostics": {},
            "failedSpans": []}
    if packet is not None:
        chain = query.terminal_chain(packet)
        view["terminal"] = chain.get("terminalState")
        view["chain"] = chain.get("chain") or []
        view["storedDiagnostics"] = chain.get("storedDiagnostics") or {}
    if bundle_dir is not None:
        bundle = bundle_source.read_bundle(bundle_dir)
        tree = query.execution_tree(bundle, only_errors=True)
        if tree.get("roots") is not None:
            view["failedSpans"] = [
                row for row in tree_view(tree) if row["outcome"] in ("FAILED", "CANCELLED")]
    return view