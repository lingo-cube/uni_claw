#!/usr/bin/env python3
"""检测 FSM 执行中的循环模式：同状态重复 ≥N 次无进展、短周期震荡、entry/step 停滞。

用法:
  python3 fsm_cycle_detector.py --run <runDir>                        # 自动定位 run.log
  python3 fsm_cycle_detector.py --log <run.log>                        # 从 run.log 检测
  python3 fsm_cycle_detector.py --trace <trace.jsonl>                  # 从 trace.jsonl 检测
  python3 fsm_cycle_detector.py --log <run.log> --threshold 5           # 自定义阈值（默认 5）
  python3 fsm_cycle_detector.py --log <run.log> --json                  # JSON 输出

检测规则:
  1. 同状态连续出现 ≥ threshold 次 → stuck_state (卡死)
  2. 短周期循环 (≤4 状态) 重复 ≥ threshold/2 次 → short_cycle
  3. 同状态连续出现且 step 不递增 → no_progress (停滞)
  4. ErrorHandling 连续出现 ≥ threshold 次 → error_loop (错误循环，对应 ErrorLoopAnalyzer)

输入:
  与 fsm_transition_path.py 相同的 run.log / trace.jsonl 格式

退出码: 0=分析完成（即使发现问题）, 1=未找到 FSM 转移, 2=用法错误
"""

import argparse
import json
import os
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

# ---------------------------------------------------------------------------
# Parsers (shared with fsm_transition_path.py)
# ---------------------------------------------------------------------------

LOG_RE = re.compile(
    r"\[(?P<time>[^\]]+)\] \[t=(?P<trace>[^\]]*)\] \[s=(?P<span>[^\]]*)\] "
    r"\[(?P<level>\w+\s*)\] (?P<category>\S+): (?P<msg>.+)"
)
FSM_RE = re.compile(r"FSM (?P<from>\w+)→(?P<to>\w+) step=(?P<step>\d+)")


def parse_runlog(path: str) -> list[dict]:
    transitions = []
    with open(path) as f:
        for line in f:
            line = line.strip()
            lm = LOG_RE.match(line)
            if not lm:
                continue
            d = lm.groupdict()
            fm = FSM_RE.match(d["msg"])
            if not fm:
                continue
            fd = fm.groupdict()
            transitions.append({
                "time": d["time"],
                "span_id": d["span"],
                "from_state": fd["from"],
                "to_state": fd["to"],
                "step": int(fd["step"]),
            })
    return transitions


def parse_trace_jsonl(path: str) -> list[dict]:
    transitions = []
    with open(path) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except json.JSONDecodeError:
                continue
            rt = rec.get("record_type", "")
            if rt in ("StateTransition", "state_transition"):
                transitions.append({
                    "time": rec.get("timestamp", rec.get("time", "")),
                    "span_id": rec.get("span_id", rec.get("spanId", "")),
                    "from_state": rec.get("from_state", rec.get("fromState", rec.get("FromState", ""))),
                    "to_state": rec.get("to_state", rec.get("toState", rec.get("ToState", ""))),
                    "step": rec.get("step", rec.get("step_number", rec.get("StepNumber", 0))),
                })
            elif rt == "span" and "FromState" in rec:
                transitions.append({
                    "time": rec.get("timestamp", ""),
                    "span_id": rec.get("span_id", rec.get("spanId", "")),
                    "from_state": rec.get("FromState", ""),
                    "to_state": rec.get("ToState", ""),
                    "step": rec.get("StepNumber", 0),
                })
    return transitions


def resolve_input(run_dir: str, log_path: str, trace_path: str) -> list[dict]:
    if log_path:
        return parse_runlog(log_path)
    if trace_path:
        return parse_trace_jsonl(trace_path)
    if run_dir:
        p = Path(run_dir)
        for cand in p.rglob("run.log"):
            result = parse_runlog(str(cand))
            if result:
                return result
        for cand in p.rglob("trace.jsonl"):
            result = parse_trace_jsonl(str(cand))
            if result:
                return result
    return []


# ---------------------------------------------------------------------------
# Cycle detection
# ---------------------------------------------------------------------------

@dataclass
class CycleFinding:
    kind: str                        # stuck_state | short_cycle | no_progress | error_loop
    state: str                       # primary state involved
    start_index: int                 # transition index where cycle begins
    length: int                      # number of transitions in the cycle
    description: str                 # human-readable description
    sample: list[dict] = field(default_factory=list)  # first few transitions


def detect_stuck_states(transitions: list[dict], threshold: int) -> list[CycleFinding]:
    """Detect same state repeating consecutively ≥ threshold times (as 'from' state)."""
    findings = []
    if not transitions:
        return findings

    i = 0
    while i < len(transitions):
        state = transitions[i]["from_state"]
        j = i
        while j < len(transitions) and transitions[j]["from_state"] == state:
            j += 1
        run_len = j - i
        if run_len >= threshold:
            findings.append(CycleFinding(
                kind="stuck_state",
                state=state,
                start_index=i,
                length=run_len,
                description=(
                    f"State '{state}' repeated {run_len} consecutive times "
                    f"(steps {transitions[i]['step']}–{transitions[j-1]['step']}) — "
                    f"FSM not advancing"
                ),
                sample=transitions[i:i + min(5, run_len)],
            ))
        i = j
    return findings


def detect_short_cycles(transitions: list[dict], threshold: int) -> list[CycleFinding]:
    """Detect short-period cycles (≤4 states) repeating ≥ threshold/2 times."""
    findings = []
    if len(transitions) < 4:
        return findings

    cycle_threshold = max(2, threshold // 2)

    for period in (2, 3, 4):
        i = 0
        while i <= len(transitions) - period * cycle_threshold:
            # Check if transitions[i:i+period] repeat
            pattern_states = tuple(t["from_state"] for t in transitions[i:i + period])
            # Count repetitions
            reps = 1
            j = i + period
            while j + period <= len(transitions):
                next_states = tuple(t["from_state"] for t in transitions[j:j + period])
                if next_states == pattern_states:
                    reps += 1
                    j += period
                else:
                    break
            if reps >= cycle_threshold:
                findings.append(CycleFinding(
                    kind="short_cycle",
                    state=" → ".join(pattern_states),
                    start_index=i,
                    length=reps * period,
                    description=(
                        f"Period-{period} cycle [{', '.join(pattern_states)}] "
                        f"repeated {reps} times (steps {transitions[i]['step']}–"
                        f"{transitions[j - period]['step']})"
                    ),
                    sample=transitions[i:i + period],
                ))
                i = j
            else:
                i += 1
    return findings


def detect_no_progress(transitions: list[dict], threshold: int) -> list[CycleFinding]:
    """Detect same state repeating without step increment."""
    findings = []
    if not transitions:
        return findings

    i = 0
    while i < len(transitions):
        state = transitions[i]["from_state"]
        step = transitions[i]["step"]
        j = i + 1
        while j < len(transitions) and transitions[j]["from_state"] == state \
                and transitions[j]["step"] == step:
            j += 1
        run_len = j - i
        if run_len >= threshold:
            findings.append(CycleFinding(
                kind="no_progress",
                state=state,
                start_index=i,
                length=run_len,
                description=(
                    f"State '{state}' at step {step} — {run_len} transitions "
                    f"with no step increment (stalled)"
                ),
                sample=transitions[i:i + min(5, run_len)],
            ))
        i = j
    return findings


def detect_error_loops(transitions: list[dict], threshold: int) -> list[CycleFinding]:
    """Detect consecutive ErrorHandling transitions ≥ threshold times.
    Corresponds to ErrorLoopAnalyzer in TraceTool (which uses ≥5 as threshold).
    """
    findings = []
    if not transitions:
        return findings

    i = 0
    while i < len(transitions):
        if transitions[i]["from_state"] == "ErrorHandling":
            j = i
            while j < len(transitions) and transitions[j]["from_state"] == "ErrorHandling":
                j += 1
            run_len = j - i
            if run_len >= threshold:
                findings.append(CycleFinding(
                    kind="error_loop",
                    state="ErrorHandling",
                    start_index=i,
                    length=run_len,
                    description=(
                        f"Error loop: ErrorHandling repeated {run_len} consecutive times "
                        f"(steps {transitions[i]['step']}–{transitions[j-1]['step']}) — "
                        f"matches ErrorLoopAnalyzer threshold ≥{threshold}"
                    ),
                    sample=transitions[i:i + min(5, run_len)],
                ))
            i = j
        else:
            i += 1
    return findings


# ---------------------------------------------------------------------------
# Output formatters
# ---------------------------------------------------------------------------

def format_findings(findings: list[CycleFinding], transitions: list[dict]) -> str:
    """Human-readable cycle report."""
    lines = []
    lines.append("FSM Cycle Detection Report")
    lines.append("=" * 80)
    lines.append(f"Total transitions analyzed: {len(transitions)}")
    lines.append(f"Findings: {len(findings)}")
    lines.append("")

    if not findings:
        lines.append("✅ No cycles or anomalies detected.")
        return "\n".join(lines)

    by_kind = defaultdict(list)
    for f in findings:
        by_kind[f.kind].append(f)

    kind_labels = {
        "stuck_state": "🔴 Stuck States (same state ≥ threshold consecutive)",
        "short_cycle": "🟡 Short Cycles (≤4-state pattern repeating)",
        "no_progress": "🟠 No Progress (same state, step not incrementing)",
        "error_loop": "🔴 Error Loops (consecutive ErrorHandling transitions)",
    }

    for kind, label in kind_labels.items():
        fs = by_kind.get(kind, [])
        if not fs:
            continue
        lines.append(label)
        lines.append("-" * 80)
        for f in fs:
            lines.append(f"  {f.description}")
            lines.append(f"    transitions [{f.start_index}:{f.start_index + f.length}]")
        lines.append("")

    return "\n".join(lines)


def format_json_output(findings: list[CycleFinding], transitions: list[dict]) -> str:
    return json.dumps({
        "total_transitions": len(transitions),
        "total_findings": len(findings),
        "findings": [
            {
                "kind": f.kind,
                "state": f.state,
                "start_index": f.start_index,
                "length": f.length,
                "description": f.description,
            }
            for f in findings
        ],
        "summary": {
            kind: len([f for f in findings if f.kind == kind])
            for kind in ["stuck_state", "short_cycle", "no_progress", "error_loop"]
        },
    }, indent=2)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Detect FSM cycles and anomalies in run.log or trace.jsonl",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s --run artifacts/runs/scenario/run-id
  %(prog)s --log trace/run-id/run.log --threshold 3
  %(prog)s --log trace/run-id/run.log --json
        """,
    )
    parser.add_argument("--run", help="Run directory (auto-locates run.log or trace.jsonl)")
    parser.add_argument("--log", help="Path to run.log file")
    parser.add_argument("--trace", help="Path to trace.jsonl file")
    parser.add_argument("--threshold", type=int, default=5,
                        help="Consecutive repetition threshold (default: 5, matches ErrorLoopAnalyzer)")
    parser.add_argument("--json", dest="json_out", action="store_true",
                        help="Output as JSON (machine-readable)")

    args = parser.parse_args()

    if not args.run and not args.log and not args.trace:
        parser.error("One of --run, --log, or --trace is required")

    transitions = resolve_input(args.run, args.log, args.trace)

    if not transitions:
        print("No FSM transitions found in input.", file=sys.stderr)
        sys.exit(1)

    findings = (
        detect_stuck_states(transitions, args.threshold)
        + detect_short_cycles(transitions, args.threshold)
        + detect_no_progress(transitions, args.threshold)
        + detect_error_loops(transitions, args.threshold)
    )

    if args.json_out:
        print(format_json_output(findings, transitions))
    else:
        print(format_findings(findings, transitions))

    sys.exit(0)


if __name__ == "__main__":
    main()
