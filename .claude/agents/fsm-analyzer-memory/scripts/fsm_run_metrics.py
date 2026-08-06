#!/usr/bin/env python3
"""Extract FSM/depth/scroll metrics from a UniClaw run directory.

Purpose: cross-run comparison for depth-guard / scroll-policy analysis
(D-G11 / D-3 P3 / enumerate_first_level). Answers: how many scrolls happened,
on which frames (by nodeId), how many child_depth_limit_skipped decisions,
steps consumed, entries observed/visited/ignored, FSM transition sequence.

Input:  --run <dir>  (run directory containing result.json + trace/<runId>/trace.jsonl)
Output: stdout — key=value metrics + per-nodeId scroll/decision table (or --json)
Exit codes: 0 = success, 1 = no trace/result found, 2 = usage error

Example:
    python3 fsm_run_metrics.py --run artifacts/runs/.../20260805T152708137Z-bc37815245f6462
    python3 fsm_run_metrics.py --run <dir> --json   # JSON output for scripting
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

SCROLL_PREFIX = "scroll_"
DEPTH_GUARD_DECISION = "child_depth_limit_skipped"


def find_trace_jsonl(run_dir: Path) -> Path | None:
    """Locate the real trace.jsonl (inner V2 layout: trace/<runId>/trace.jsonl)."""
    candidates = [run_dir / "trace" / "trace.jsonl", run_dir / "trace.jsonl"]
    if (run_dir / "trace").is_dir():
        for sub in (run_dir / "trace").iterdir():
            if sub.is_dir() and (sub / "trace.jsonl").is_file():
                candidates.insert(0, sub / "trace.jsonl")
    for c in candidates:
        if c.is_file():
            return c
    return None


def extract(run_dir: Path):
    result = {}
    result_path = run_dir / "result.json"
    if result_path.is_file():
        d = json.loads(result_path.read_text())
        for k in ("status", "completionReason", "stepsConsumed", "scrollsConsumed",
                  "discoveredEntries", "visitedEntries", "durationMs"):
            result[k] = d.get(k)

    trace_path = find_trace_jsonl(run_dir)
    if trace_path is None:
        return result, None, None, None, None

    decisions = Counter()
    scroll_by_node = Counter()
    depth_skip_by_node = Counter()
    scroll_spans = 0
    entry_counts = Counter()
    fsm_transitions = []
    max_step = 0
    ai_calls = 0
    errors = 0

    with trace_path.open() as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                d = json.loads(line)
            except json.JSONDecodeError:
                continue
            rt = d.get("record_type")
            if rt == "execution":
                st = d.get("spanType")
                ctx = d.get("context") or {}
                step = ctx.get("stepNumber") or 0
                max_step = max(max_step, step)
                if st == "stateDecision":
                    action = d.get("action") or "?"
                    decisions[action] += 1
                    node = ctx.get("nodeId") or "?"
                    if action.startswith(SCROLL_PREFIX):
                        scroll_by_node[node] += 1
                    if action == DEPTH_GUARD_DECISION:
                        depth_skip_by_node[node] += 1
            elif rt == "span":
                st = d.get("spanType") or ""
                if st == "action.scroll":
                    scroll_spans += 1
                elif st.startswith("entry."):
                    entry_counts[st] += 1
            elif rt == "state_transition":
                fsm_transitions.append((d.get("fromState"), d.get("toState")))
            elif rt == "error":
                errors += 1
            elif rt == "ai_call":
                ai_calls += 1

    result["traceMaxStep"] = max_step
    result["scrollSpans"] = scroll_spans
    result["scrollDecisions"] = sum(
        v for k, v in decisions.items() if k.startswith(SCROLL_PREFIX))
    result["depthLimitSkipped"] = decisions.get(DEPTH_GUARD_DECISION, 0)
    result["aiCalls"] = ai_calls
    result["errors"] = errors
    for k in ("entry.observed", "entry.visited", "entry.ignored", "entry.generate", "entry.skipped"):
        result[k] = entry_counts.get(k, 0)
    result["fsmTransitions"] = len(fsm_transitions)

    return result, dict(decisions), dict(scroll_by_node), dict(depth_skip_by_node), fsm_transitions


def main():
    ap = argparse.ArgumentParser(
        description="Extract FSM/depth/scroll metrics from a run directory.")
    ap.add_argument("--run", required=True, help="Run directory")
    ap.add_argument("--json", action="store_true", help="Emit JSON instead of key=value")
    args = ap.parse_args()

    run_dir = Path(args.run)
    if not run_dir.is_dir():
        print(f"error: run directory not found: {args.run}", file=sys.stderr)
        sys.exit(2)

    result, decisions, scroll_by_node, depth_skip_by_node, fsm = extract(run_dir)
    if result is None or "traceMaxStep" not in result:
        print(f"error: no trace.jsonl found under {args.run}", file=sys.stderr)
        sys.exit(1)

    if args.json:
        print(json.dumps({
            "metrics": result,
            "decisions": dict(decisions),
            "scrollByNode": dict(sorted(scroll_by_node.items(), key=lambda kv: -kv[1])),
            "depthSkipByNode": dict(sorted(depth_skip_by_node.items(), key=lambda kv: -kv[1])),
        }, ensure_ascii=False, indent=1))
        sys.exit(0)

    print(f"run={run_dir.name}")
    for k, v in result.items():
        print(f"  {k}={v}")
    if scroll_by_node:
        print("  scrolls by node (top 15):")
        for node, cnt in sorted(scroll_by_node.items(), key=lambda kv: -kv[1])[:15]:
            print(f"    {cnt:3d}  {node}")
    if depth_skip_by_node:
        print("  child_depth_limit_skipped by node (top 10):")
        for node, cnt in sorted(depth_skip_by_node.items(), key=lambda kv: -kv[1])[:10]:
            print(f"    {cnt:3d}  {node}")
    sys.exit(0)


if __name__ == "__main__":
    main()
