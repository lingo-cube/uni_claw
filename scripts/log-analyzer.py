#!/usr/bin/env python3
"""run.log 分析工具：从 Host/FSM 运行日志生成表格、时间线和图表。

用法:
  python3 scripts/log-analyzer.py table   <run.log>     # step 执行摘要表
  python3 scripts/log-analyzer.py timeline <run.log>    # FSM 状态 ASCII 时间线
  python3 scripts/log-analyzer.py mermaid  <run.log>    # Mermaid 状态图
  python3 scripts/log-analyzer.py metrics  <run.log>    # 关键指标摘要
  python3 scripts/log-analyzer.py compare  <runA.log> <runB.log>  # 双 run 对比

日志格式:
  [HH:mm:ss.fff] [t=<runId>] [s=<spanId>] [LEVEL] Category: message
"""

import re
import sys
from dataclasses import dataclass
from typing import Optional

LOG_RE = re.compile(
    r"\[(?P<time>[^\]]+)\] \[t=(?P<trace>[^\]]*)\] \[s=(?P<span>[^\]]*)\] "
    r"\[(?P<level>\w+\s*)\] (?P<category>\S+): (?P<msg>.+)"
)

FSM_RE = re.compile(r"FSM (?P<from>\w+)→(?P<to>\w+) step=(?P<step>\d+)")
ACTION_RE = re.compile(r"action=(?P<action>\w+) result=(?P<result>\w+)")
DENY_RE = re.compile(r"action=(?P<action>\w+) → deny rule=(?P<rule>.+)")
PAGE_RE = re.compile(
    r"page=(?P<path>.+?) items=(?P<items>\d+) scroll=(?P<scroll>\S+) endOfList=(?P<eol>\S+)"
)
TERM_RE = re.compile(r"Engine terminated reason=(?P<reason>\S+) steps=(?P<steps>\d+)")
RUN_START_RE = re.compile(r"Run (?P<run>\S+) started mode=(?P<mode>\S+) provider=(?P<provider>\S+)")
RUN_END_RE = re.compile(r"Run (?P<run>\S+) ended status=(?P<status>\S+) duration=(?P<dur>\d+)ms")


@dataclass
class LogEntry:
    time: str
    trace_id: str
    span_id: str
    level: str
    category: str
    msg: str

    @classmethod
    def parse(cls, line: str) -> Optional["LogEntry"]:
        m = LOG_RE.match(line)
        if not m:
            return None
        d = m.groupdict()
        return cls(
            time=d["time"],
            trace_id=d["trace"],
            span_id=d["span"],
            level=d["level"].strip(),
            category=d["category"],
            msg=d["msg"],
        )


def parse_log(path: str) -> list[LogEntry]:
    entries = []
    with open(path) as f:
        for line in f:
            e = LogEntry.parse(line.strip())
            if e:
                entries.append(e)
    return entries


def make_sep(cols: list[int], left: str = "├", mid: str = "┼", right: str = "┤", fill: str = "─") -> str:
    parts = [left]
    for i, w in enumerate(cols):
        parts.append(fill * (w + 2))
        if i < len(cols) - 1:
            parts.append(mid)
    parts.append(right)
    return "".join(parts)


def make_row(vals: list[str], cols: list[int]) -> str:
    parts = ["│"]
    for v, w in zip(vals, cols):
        parts.append(f" {v:<{w}} │")
    return "".join(parts)


# ─── table ───────────────────────────────────────────────

def cmd_table(entries: list[LogEntry]) -> str:
    """Step 执行摘要表：step / FSM 转换 / 动作 / 页面分析."""
    steps: dict[int, dict] = {}

    for e in entries:
        fsm = FSM_RE.search(e.msg)
        if fsm:
            step = int(fsm["step"])
            steps.setdefault(step, {})
            steps[step].setdefault("fsm", [])
            steps[step]["fsm"].append(f"{fsm['from']}→{fsm['to']}")

        act = ACTION_RE.search(e.msg)
        if act:
            step = _find_step(entries, e)
            if step:
                steps.setdefault(step, {})
                steps[step].setdefault("actions", [])
                steps[step]["actions"].append(f"{act['action']}={act['result']}")

        deny = DENY_RE.search(e.msg)
        if deny:
            step = _find_step(entries, e)
            if step:
                steps.setdefault(step, {})
                steps[step].setdefault("actions", [])
                steps[step]["actions"].append(f"⛔ {deny['action']} deny:{deny['rule']}")

        pg = PAGE_RE.search(e.msg)
        if pg:
            step = _find_step(entries, e)
            if step:
                steps.setdefault(step, {})
                steps[step]["page"] = f"{pg['path']} ({pg['items']} items)"

    cols = [6, 32, 28, 36]
    header = make_row(["Step", "FSM Transitions", "Actions", "Page"], cols)
    sep_top = make_sep(cols, "┌", "┬", "┐")
    sep_mid = make_sep(cols)
    sep_bot = make_sep(cols, "└", "┴", "┘")

    lines = [sep_top, header, sep_mid]
    for step in sorted(steps):
        s = steps[step]
        fsm_str = " / ".join(s.get("fsm", []))
        act_str = " / ".join(s.get("actions", []))
        page_str = s.get("page", "-")
        lines.append(make_row([str(step), fsm_str, act_str, page_str], cols))
    lines.append(sep_bot)
    return "\n".join(lines)


def _find_step(entries: list[LogEntry], current: LogEntry) -> Optional[int]:
    """从 action/page 日志的 spanId 反查最近的 FSM step."""
    # 先看同 span 有没有 FSM 记录
    for e in entries:
        if e.span_id == current.span_id:
            fsm = FSM_RE.search(e.msg)
            if fsm:
                return int(fsm["step"])
    # 回退：找最近的前一条 FSM 记录
    for e in reversed(entries[: entries.index(current)]):
        fsm = FSM_RE.search(e.msg)
        if fsm:
            return int(fsm["step"])
    return None


# ─── timeline ────────────────────────────────────────────

def cmd_timeline(entries: list[LogEntry]) -> str:
    """FSM 状态 ASCII 时间线."""
    fsm_states = [
        e for e in entries
        if e.category in ("TraversalFSM", "TraversalEngine")
        and (FSM_RE.search(e.msg) or "Engine terminated" in e.msg or "Run" in e.msg)
    ]

    if not fsm_states:
        return "(no FSM events)"

    lines = []
    prev_time = None
    for e in fsm_states:
        t = e.time[:12]  # HH:mm:ss.fff
        gap = ""
        if prev_time:
            delta = _time_diff(prev_time, t)
            gap = f"  +{delta}s"
        prev_time = t

        if "Engine terminated" in e.msg:
            m = TERM_RE.search(e.msg)
            reason = m["reason"] if m else "?"
            steps = m["steps"] if m else "?"
            lines.append(f"{t} {'─' * 40} ◼ {reason} ({steps} steps)")
        elif "Run" in e.msg and "started" in e.msg:
            lines.append(f"{t} ▶ RUN START")
        else:
            fsm = FSM_RE.search(e.msg)
            if fsm:
                bar = "─" * max(1, min(40, int(fsm["step"]) * 2))
                lines.append(f"{t} {gap:>8s}  step={fsm['step']:>3}  {bar} {fsm['from']}→{fsm['to']}")

    return "\n".join(lines)


def _time_diff(a: str, b: str) -> str:
    """返回两个 HH:mm:ss.fff 的差值（秒），保留 1 位小数."""
    try:
        def to_s(t: str) -> float:
            parts = t.split(":")
            h, m, s = int(parts[0]), int(parts[1]), float(parts[2])
            return h * 3600 + m * 60 + s
        return f"{to_s(b) - to_s(a):.1f}"
    except ValueError:
        return "?"


# ─── mermaid ─────────────────────────────────────────────

def cmd_mermaid(entries: list[LogEntry]) -> str:
    """生成 Mermaid stateDiagram."""
    transitions: list[tuple[str, str, str]] = []
    for e in entries:
        fsm = FSM_RE.search(e.msg)
        if fsm:
            transitions.append((fsm["from"], fsm["to"], e.time[:12]))

    if not transitions:
        return "(no FSM transitions)"

    lines = ["```mermaid", "stateDiagram-v2"]
    seen = set()
    for from_s, to_s, t in transitions:
        key = (from_s, to_s)
        if key not in seen:
            seen.add(key)
            lines.append(f"    {from_s} --> {to_s}")
    lines.append("```")
    return "\n".join(lines)


# ─── metrics ─────────────────────────────────────────────

def cmd_metrics(entries: list[LogEntry]) -> str:
    """关键指标摘要."""
    info = {
        "run": "-", "mode": "-", "provider": "-",
        "termination": "-", "total_steps": "-",
        "actions_total": 0, "actions_ok": 0, "actions_failed": 0,
        "denies": 0,
        "page_analyses": 0,
        "errors": 0, "warnings": 0,
        "duration_ms": "-",
    }

    for e in entries:
        m = RUN_START_RE.search(e.msg)
        if m:
            info["run"] = m["run"][:20]
            info["mode"] = m["mode"]
            info["provider"] = m["provider"]

        m = RUN_END_RE.search(e.msg)
        if m:
            info["duration_ms"] = m["dur"]

        m = TERM_RE.search(e.msg)
        if m:
            info["termination"] = m["reason"]
            info["total_steps"] = m["steps"]

        m = ACTION_RE.search(e.msg)
        if m:
            info["actions_total"] += 1
            if m["result"] == "ok":
                info["actions_ok"] += 1
            else:
                info["actions_failed"] += 1

        if DENY_RE.search(e.msg):
            info["denies"] += 1

        if PAGE_RE.search(e.msg):
            info["page_analyses"] += 1

        if e.level == "ERROR":
            info["errors"] += 1
        if e.level == "WARN":
            info["warnings"] += 1

    dur_str = str(info['duration_ms'])
    a_str = f"{info['actions_total']} total ({info['actions_ok']} ok, {info['actions_failed']} failed)"

    return f"""┌─────────────────────────────────────────┐
│ Run:      {info['run']:<30} │
│ Mode:     {info['mode']:<30} │
│ Provider: {info['provider']:<30} │
├─────────────────────────────────────────┤
│ Termination:  {info['termination']:<25} │
│ Total Steps:  {info['total_steps']:<25} │
│ Duration:     {dur_str}ms{'':>{35 - len(dur_str)}} │
├─────────────────────────────────────────┤
│ Actions:      {a_str:<38} │
│ Denies:       {info['denies']:<25} │
│ Page Analyses:{info['page_analyses']:<25} │
├─────────────────────────────────────────┤
│ Errors:   {info['errors']:<28} │
│ Warnings: {info['warnings']:<28} │
└─────────────────────────────────────────┘"""


# ─── compare ─────────────────────────────────────────────

def cmd_compare(a_entries: list[LogEntry], b_entries: list[LogEntry]) -> str:
    """双 run 对比表."""
    def extract(e: list[LogEntry]) -> dict:
        m = {}
        for entry in e:
            r = RUN_START_RE.search(entry.msg)
            if r: m["mode"] = r["mode"]; m["provider"] = r["provider"]
            r = RUN_END_RE.search(entry.msg)
            if r: m["status"] = r["status"]; m["duration_ms"] = int(r["dur"])
            r = TERM_RE.search(entry.msg)
            if r: m["reason"] = r["reason"]; m["steps"] = int(r["steps"])
            if ACTION_RE.search(entry.msg): m["actions"] = m.get("actions", 0) + 1
            if PAGE_RE.search(entry.msg): m["analyses"] = m.get("analyses", 0) + 1
            if entry.level == "ERROR": m["errors"] = m.get("errors", 0) + 1
            if entry.level == "WARN": m["warnings"] = m.get("warnings", 0) + 1
        return m

    a = extract(a_entries)
    b = extract(b_entries)

    cols = [16, 20, 20]
    sep_top = make_sep(cols, "┌", "┬", "┐")
    sep_mid = make_sep(cols)
    sep_bot = make_sep(cols, "└", "┴", "┘")

    rows = [
        sep_top,
        make_row(["Metric", "Run A", "Run B"], cols),
        sep_mid,
        make_row(["Mode", a.get("mode", "-"), b.get("mode", "-")], cols),
        make_row(["Provider", a.get("provider", "-"), b.get("provider", "-")], cols),
        make_row(["Status", a.get("status", "-"), b.get("status", "-")], cols),
        make_row(["Termination", a.get("reason", "-"), b.get("reason", "-")], cols),
        make_row(["Steps", str(a.get("steps", "-")), str(b.get("steps", "-"))], cols),
        make_row(["Duration", f"{a.get('duration_ms','-')}ms", f"{b.get('duration_ms','-')}ms"], cols),
        make_row(["Actions", str(a.get("actions", "-")), str(b.get("actions", "-"))], cols),
        make_row(["Page Analyses", str(a.get("analyses", "-")), str(b.get("analyses", "-"))], cols),
        make_row(["Errors", str(a.get("errors", "-")), str(b.get("errors", "-"))], cols),
        make_row(["Warnings", str(a.get("warnings", "-")), str(b.get("warnings", "-"))], cols),
        sep_bot,
    ]
    return "\n".join(rows)


# ─── main ────────────────────────────────────────────────

def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    cmd = sys.argv[1]

    if cmd == "compare":
        if len(sys.argv) < 4:
            print("usage: log-analyzer.py compare <runA.log> <runB.log>")
            sys.exit(1)
        a = parse_log(sys.argv[2])
        b = parse_log(sys.argv[3])
        print(cmd_compare(a, b))
    else:
        path = sys.argv[2]
        entries = parse_log(path)
        if not entries:
            print(f"no log entries found in {path}")
            sys.exit(1)

        if cmd == "table":
            print(cmd_table(entries))
        elif cmd == "timeline":
            print(cmd_timeline(entries))
        elif cmd == "mermaid":
            print(cmd_mermaid(entries))
        elif cmd == "metrics":
            print(cmd_metrics(entries))
        else:
            print(f"unknown command: {cmd}")
            print(__doc__)
            sys.exit(1)


if __name__ == "__main__":
    main()
