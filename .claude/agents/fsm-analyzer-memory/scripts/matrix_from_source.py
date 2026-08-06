#!/usr/bin/env python3
"""从 C# 源码和设计文档中提取并比对转移矩阵。

源码提取 = ground truth（代码实际行为）。
文档提取 = design intent（fsm-design.md 中的表格）。
--diff-docs 比对两者——差异本身就是 FSM 分析的最高价值信号：
  源码有·文档无 → 可能是 bug 或未记录的决策
  文档有·源码无 → 可能是代码未实现或文档过时

用法:
  python3 matrix_from_source.py                                 # 打印两个矩阵（ASCII）
  python3 matrix_from_source.py --json                          # JSON 输出
  python3 matrix_from_source.py --python                        # Python dict 输出
  python3 matrix_from_source.py --diff-docs                     # 源码 ↔ 文档交叉比对
  python3 matrix_from_source.py --diff-docs --json              # 比对结果 JSON
  python3 matrix_from_source.py --check <expected.json>         # 与预期 JSON 比对（CI / 自检用）
  python3 matrix_from_source.py --fsm traversal                 # 只输出 TraversalFSM 矩阵
  python3 matrix_from_source.py --fsm global                    # 只输出 GlobalFSM 矩阵

源码位置（自动定位，也可用 --source 指定）:
  src/UniClaw.Core/StateMachine/TraversalFSM.cs
  src/UniClaw.Core/StateMachine/GlobalFSM.cs

文档位置:
  docs/system/patterns/fsm-design.md  (§2 TraversalFSM, §3 GlobalFSM)

退出码: 0=成功, 1=解析失败, 2=用法错误, 3=--check 不匹配
"""

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Optional

# ---------------------------------------------------------------------------
# Default source locations (relative to repo root)
# ---------------------------------------------------------------------------
DEFAULT_TRAVERSAL_FSM = "src/UniClaw.Core/StateMachine/TraversalFSM.cs"
DEFAULT_GLOBAL_FSM = "src/UniClaw.Core/StateMachine/GlobalFSM.cs"


def find_repo_root() -> Path:
    """Locate repo root by walking up from this script's location."""
    d = Path(__file__).resolve().parent
    for _ in range(10):
        if (d / ".git").exists() or (d / "CLAUDE.md").exists() or (d / "AGENTS.md").exists():
            return d
        d = d.parent
    return Path.cwd()


# ---------------------------------------------------------------------------
# Matrix extraction from C# source
# ---------------------------------------------------------------------------

# Matches: [TraversalState.XXX] = ImmutableArray.Create(
#              TraversalState.YYY, TraversalState.ZZZ),
# Also handles multi-line entries
TRAVERSAL_ENTRY_RE = re.compile(
    r"\[TraversalState\.(\w+)\]\s*=\s*ImmutableArray\.Create\("
    r"((?:\s*TraversalState\.\w+,?)*)\s*\)[\s,]*",
    re.MULTILINE,
)

GLOBAL_ENTRY_RE = re.compile(
    r"\[GlobalState\.(\w+)\]\s*=\s*ImmutableArray\.Create\("
    r"((?:\s*GlobalState\.\w+,?)*)\s*\)[\s,]*",
    re.MULTILINE,
)

GLOBAL_EMPTY_RE = re.compile(
    r"\[GlobalState\.(\w+)\]\s*=\s*ImmutableArray<GlobalState>\.Empty[\s,]*",
    re.MULTILINE,
)

TARGET_RE = re.compile(r"(?:TraversalState|GlobalState)\.(\w+)")


def extract_traversal_matrix(source_path: str) -> dict[str, list[str]]:
    """Parse TraversalFSM.cs and return {state: [valid_targets]}."""
    text = Path(source_path).read_text()

    # Find the TransitionMatrix block
    start = text.find("TransitionMatrix =")
    if start < 0:
        raise ValueError(f"TransitionMatrix not found in {source_path}")

    # Find the closing of the dictionary (matching braces)
    depth = 0
    started = False
    end = start
    for i in range(start, len(text)):
        if text[i] == "{":
            depth += 1
            started = True
        elif text[i] == "}":
            depth -= 1
            if started and depth == 0:
                end = i + 1
                break

    block = text[start:end]

    matrix: dict[str, list[str]] = {}
    for m in TRAVERSAL_ENTRY_RE.finditer(block):
        from_state = m.group(1)
        targets_str = m.group(2)
        targets = TARGET_RE.findall(targets_str)
        matrix[from_state] = targets

    return matrix


def extract_global_matrix(source_path: str) -> dict[str, list[str]]:
    """Parse GlobalFSM.cs and return {state: [valid_targets]}."""
    text = Path(source_path).read_text()

    # Find the TransitionMatrix block
    start = text.find("TransitionMatrix =")
    if start < 0:
        raise ValueError(f"TransitionMatrix not found in {source_path}")

    depth = 0
    started = False
    end = start
    for i in range(start, len(text)):
        if text[i] == "{":
            depth += 1
            started = True
        elif text[i] == "}":
            depth -= 1
            if started and depth == 0:
                end = i + 1
                break

    block = text[start:end]

    matrix: dict[str, list[str]] = {}
    # First: entries with ImmutableArray.Create(...)
    for m in GLOBAL_ENTRY_RE.finditer(block):
        from_state = m.group(1)
        targets_str = m.group(2)
        targets = TARGET_RE.findall(targets_str)
        matrix[from_state] = targets

    # Then: entries with ImmutableArray<GlobalState>.Empty
    for m in GLOBAL_EMPTY_RE.finditer(block):
        from_state = m.group(1)
        if from_state not in matrix:
            matrix[from_state] = []

    return matrix


# ---------------------------------------------------------------------------
# Validation helpers (run against extracted matrix)
# ---------------------------------------------------------------------------

def validate_matrix(matrix: dict[str, list[str]], label: str) -> list[str]:
    """Run structural checks on a matrix. Returns list of issues (empty = valid)."""
    issues: list[str] = []

    # All targets must be valid state names (no typos)
    all_states = set(matrix.keys())
    for src, targets in matrix.items():
        for tgt in targets:
            if tgt not in all_states:
                issues.append(f"{label}: {src}→{tgt}: '{tgt}' is not a declared state")

    # No self-loops
    for src, targets in matrix.items():
        if src in targets:
            issues.append(f"{label}: {src} has a self-loop ({src}→{src})")

    # No duplicate targets
    for src, targets in matrix.items():
        if len(targets) != len(set(targets)):
            issues.append(f"{label}: {src} has duplicate targets: {targets}")

    # Reachability (BFS from first state)
    first = next(iter(matrix.keys()))
    reachable: set[str] = set()
    queue = [first]
    while queue:
        s = queue.pop(0)
        if s in reachable:
            continue
        reachable.add(s)
        for tgt in matrix.get(s, []):
            if tgt not in reachable:
                queue.append(tgt)

    unreachable = all_states - reachable
    if unreachable:
        issues.append(f"{label}: unreachable states: {sorted(unreachable)}")

    # Dead states: states with no outgoing transitions
    dead = {s for s, t in matrix.items() if len(t) == 0}
    # Dead states that are not intended terminals — for TraversalFSM no state should be dead
    if label == "TraversalFSM" and dead:
        issues.append(f"{label}: dead states (no outgoing transitions): {sorted(dead)}")

    return issues


# ---------------------------------------------------------------------------
# Doc extraction — parse fsm-design.md markdown tables
# ---------------------------------------------------------------------------

# Matches a markdown table row like: | **StateName** | Target1, Target2 | ... |
DOC_TABLE_ROW_RE = re.compile(
    r"\|\s*\*\*(\w+)\*\*\s*\|"
    r"\s*([^|]+?)\s*\|"
)

# Patterns that indicate "empty / terminal — no outgoing transitions"
EMPTY_TARGET_PATTERNS = [
    re.compile(r"\*\(空.*\)\*"),   # *(空 — 无出迁)*
    re.compile(r"\*\(empty.*\)\*", re.IGNORECASE),
    re.compile(r"\*\(terminal.*\)\*", re.IGNORECASE),
]

DEFAULT_FSM_DESIGN_DOC = "docs/system/patterns/fsm-design.md"


def _is_empty_targets_cell(cell: str) -> bool:
    """Check if the targets cell indicates an empty/terminal state."""
    for pat in EMPTY_TARGET_PATTERNS:
        if pat.search(cell):
            return True
    stripped = cell.strip()
    return stripped == "" or stripped == "—" or stripped == "-"


def _extract_matrix_from_markdown_section(text: str, section_header: str) -> dict[str, list[str]]:
    """Extract a transition matrix from a markdown section's first table.

    Finds the section by header text (e.g., "## 2. TraversalFSM"),
    then parses the first table following that header.
    """
    # Find section
    idx = text.find(section_header)
    if idx < 0:
        raise ValueError(f"Section '{section_header}' not found in doc")

    # Scan forward for the first table row (starts with "| **")
    table_start = idx
    found_table = False
    for line in text[idx:].split("\n"):
        if DOC_TABLE_ROW_RE.match(line):
            table_start = text.index(line, idx)
            found_table = True
            break

    if not found_table:
        raise ValueError(f"No table found in section '{section_header}'")

    # Parse table rows until we hit a non-table line
    matrix: dict[str, list[str]] = {}
    for line in text[table_start:].split("\n"):
        m = DOC_TABLE_ROW_RE.match(line)
        if not m:
            # Stop at end of table (blank line or non-table line)
            if matrix:
                break
            continue

        state_name = m.group(1)
        targets_cell = m.group(2).strip()

        if _is_empty_targets_cell(targets_cell):
            matrix[state_name] = []
        else:
            # Split by comma, strip whitespace
            targets = [t.strip() for t in targets_cell.split(",") if t.strip()]
            matrix[state_name] = targets

    if not matrix:
        raise ValueError(f"Could not parse any rows from table in '{section_header}'")

    return matrix


def extract_traversal_from_docs(doc_path: str | None = None) -> dict[str, list[str]]:
    """Parse TraversalFSM matrix from fsm-design.md §2 table."""
    path = doc_path or str(find_repo_root() / DEFAULT_FSM_DESIGN_DOC)
    text = Path(path).read_text()
    return _extract_matrix_from_markdown_section(text, "## 2. TraversalFSM")


def extract_global_from_docs(doc_path: str | None = None) -> dict[str, list[str]]:
    """Parse GlobalFSM matrix from fsm-design.md §3 table."""
    path = doc_path or str(find_repo_root() / DEFAULT_FSM_DESIGN_DOC)
    text = Path(path).read_text()
    return _extract_matrix_from_markdown_section(text, "## 3. GlobalFSM")


# ---------------------------------------------------------------------------
# Source ↔ Doc cross-reference diff
# ---------------------------------------------------------------------------

DiffEntry = dict  # {state, target, kind: "CODE_ONLY"|"DOC_ONLY", implication: str}


def diff_matrices(
    source: dict[str, list[str]],
    doc: dict[str, list[str]],
    label: str,
) -> list[DiffEntry]:
    """Compare source (C# code) vs doc (fsm-design.md table). Returns list of discrepancies.

    Each entry has:
      - state: the source state
      - target: the target state (or None for state-level mismatches)
      - kind: "CODE_ONLY" (in source, not in doc) or "DOC_ONLY" (in doc, not in source)
      - implication: human-readable interpretation
    """
    diffs: list[DiffEntry] = []

    source_edges: set[tuple[str, str]] = set()
    for src, targets in source.items():
        for tgt in targets:
            source_edges.add((src, tgt))

    doc_edges: set[tuple[str, str]] = set()
    for src, targets in doc.items():
        for tgt in targets:
            doc_edges.add((src, tgt))

    source_states = set(source.keys())
    doc_states = set(doc.keys())

    # States only in source (missing from docs)
    for s in sorted(source_states - doc_states):
        diffs.append({
            "state": s,
            "target": None,
            "kind": "CODE_ONLY",
            "implication": f"State '{s}' exists in {label} C# code but is missing from fsm-design.md table — doc needs updating",
        })

    # States only in doc (missing from source)
    for s in sorted(doc_states - source_states):
        diffs.append({
            "state": s,
            "target": None,
            "kind": "DOC_ONLY",
            "implication": f"State '{s}' is documented in fsm-design.md but does NOT exist in {label} C# code — code may have removed/renamed this state",
        })

    # Edges in source but not doc → CODE_ONLY
    for (src, tgt) in sorted(source_edges - doc_edges):
        if tgt not in source_states:
            continue  # Invalid target — caught by validate_matrix
        if src not in doc_states:
            continue  # State-level mismatch already reported
        diffs.append({
            "state": src,
            "target": tgt,
            "kind": "CODE_ONLY",
            "implication": (
                f"{src}→{tgt} exists in C# TransitionMatrix but NOT in fsm-design.md table. "
                f"Either: (a) code has an undocumented edge (bug?), "
                f"(b) doc was not updated after adding this transition, or "
                f"(c) this is an intentional runtime-only edge (e.g., exception routing)"
            ),
        })

    # Edges in doc but not source → DOC_ONLY
    for (src, tgt) in sorted(doc_edges - source_edges):
        if src not in source_states:
            continue  # State-level mismatch already reported
        diffs.append({
            "state": src,
            "target": tgt,
            "kind": "DOC_ONLY",
            "implication": (
                f"{src}→{tgt} is documented in fsm-design.md but NOT in C# TransitionMatrix. "
                f"Either: (a) code removed this transition but doc wasn't updated (doc drift), "
                f"(b) this was the intended design but implementation never completed, or "
                f"(c) this transition is handled outside the matrix (e.g., interception layer)"
            ),
        })

    return diffs


def format_diff(
    source: dict[str, list[str]],
    doc: dict[str, list[str]],
    diffs: list[DiffEntry],
    label: str,
) -> str:
    """ASCII diff report between source and doc matrices."""
    lines = [
        f"{label} — Source ↔ Doc Cross-Reference",
        "=" * 80,
        f"  Source (C#):  {len(source)} states, {sum(len(v) for v in source.values())} edges",
        f"  Doc (md):     {len(doc)} states, {sum(len(v) for v in doc.values())} edges",
        f"  Discrepancies: {len(diffs)}",
        "",
    ]

    if not diffs:
        lines.append("  ✅ Source and doc are identical — no drift detected.")
        return "\n".join(lines)

    # Group by kind
    code_only = [d for d in diffs if d["kind"] == "CODE_ONLY"]
    doc_only = [d for d in diffs if d["kind"] == "DOC_ONLY"]

    if code_only:
        lines.append(f"  ⚠ CODE_ONLY ({len(code_only)} items) — in C# source but NOT in doc")
        lines.append(f"  {'─' * 70}")
        for d in code_only:
            if d["target"]:
                lines.append(f"    {d['state']} → {d['target']}")
            else:
                lines.append(f"    State: {d['state']} (entire state missing from doc)")
        lines.append("")

    if doc_only:
        lines.append(f"  ⚠ DOC_ONLY ({len(doc_only)} items) — in doc but NOT in C# source")
        lines.append(f"  {'─' * 70}")
        for d in doc_only:
            if d["target"]:
                lines.append(f"    {d['state']} → {d['target']}")
            else:
                lines.append(f"    State: {d['state']} (doc-only state, not in code)")
        lines.append("")

    # Implications summary
    lines.append("  Implications:")
    lines.append(f"  {'─' * 70}")
    seen = set()
    for d in diffs:
        imp = d["implication"]
        if imp not in seen:
            seen.add(imp)
            lines.append(f"    • {imp}")

    return "\n".join(lines)


def format_diff_json(
    source: dict[str, list[str]],
    doc: dict[str, list[str]],
    diffs: list[DiffEntry],
    label: str,
) -> dict:
    """Structured diff output."""
    return {
        "label": label,
        "source": {
            "origin": "C# TransitionMatrix field",
            "states": len(source),
            "edges": sum(len(v) for v in source.values()),
        },
        "doc": {
            "origin": "fsm-design.md markdown table",
            "states": len(doc),
            "edges": sum(len(v) for v in doc.values()),
        },
        "discrepancies": diffs,
        "verdict": "identical" if not diffs else "drift_detected",
    }

def format_ascii_matrix(matrix: dict[str, list[str]], label: str) -> str:
    """ASCII representation of a transition matrix."""
    lines = [f"{label} Transition Matrix ({len(matrix)} states, extracted from source)", "=" * 80]
    for src in sorted(matrix.keys()):
        targets = matrix[src]
        if targets:
            t_str = ", ".join(targets)
        else:
            t_str = "(terminal — no outgoing transitions)"
        lines.append(f"  {src:<20s} →  {t_str}")

    lines.append("")

    # Validation
    issues = validate_matrix(matrix, label)
    if issues:
        lines.append("⚠ Matrix Issues:")
        for iss in issues:
            lines.append(f"  {iss}")
    else:
        lines.append("✅ Matrix is structurally valid")
    lines.append("")

    # Stats
    total_edges = sum(len(v) for v in matrix.values())
    terminal = sum(1 for v in matrix.values() if len(v) == 0)
    lines.append(f"  States: {len(matrix)}  Edges: {total_edges}  Terminal: {terminal}")
    return "\n".join(lines)


def format_matrix_json(traversal: dict, global_: dict) -> str:
    """JSON output with both matrices + validation."""
    return json.dumps({
        "source": "C# TransitionMatrix fields (extracted at runtime)",
        "traversal_fsm": {
            "matrix": traversal,
            "states": len(traversal),
            "edges": sum(len(v) for v in traversal.values()),
            "issues": validate_matrix(traversal, "TraversalFSM"),
        },
        "global_fsm": {
            "matrix": global_,
            "states": len(global_),
            "edges": sum(len(v) for v in global_.values()),
            "issues": validate_matrix(global_, "GlobalFSM"),
        },
    }, indent=2)


def format_matrix_python(traversal: dict, global_: dict) -> str:
    """Python dict literal that can be exec'd or copy-pasted into other scripts."""
    import pprint
    lines = [
        "# Auto-generated by matrix_from_source.py — reflects current C# source",
        f"# TraversalFSM: {len(traversal)} states, {sum(len(v) for v in traversal.values())} edges",
        f"# GlobalFSM:   {len(global_)} states, {sum(len(v) for v in global_.values())} edges",
        "",
        "TRAVERSAL_MATRIX = " + pprint.pformat(traversal, width=100),
        "",
        "GLOBAL_MATRIX = " + pprint.pformat(global_, width=100),
    ]
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Extract FSM transition matrices from C# source and/or design docs, with cross-reference diff",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s                                   # ASCII both matrices (source)
  %(prog)s --json                            # JSON output
  %(prog)s --diff-docs                       # Source ↔ Doc cross-reference diff
  %(prog)s --diff-docs --json                # Diff as JSON
  %(prog)s --fsm traversal                   # TraversalFSM only
  %(prog)s --check expected.json             # Compare against expected
        """,
    )
    parser.add_argument("--source-traversal",
                        help=f"Path to TraversalFSM.cs (default: {DEFAULT_TRAVERSAL_FSM})")
    parser.add_argument("--source-global",
                        help=f"Path to GlobalFSM.cs (default: {DEFAULT_GLOBAL_FSM})")
    parser.add_argument("--doc-path",
                        help=f"Path to fsm-design.md (default: {DEFAULT_FSM_DESIGN_DOC})")
    parser.add_argument("--fsm", choices=["traversal", "global"],
                        help="Output only one FSM matrix")
    parser.add_argument("--diff-docs", dest="diff_docs", action="store_true",
                        help="Cross-reference C# source matrix ↔ fsm-design.md table. "
                             "Flags every discrepancy — source≠doc is the highest-value FSM signal.")
    parser.add_argument("--json", dest="json_out", action="store_true",
                        help="JSON output (machine-readable, includes validation)")
    parser.add_argument("--python", dest="python_out", action="store_true",
                        help="Python dict output (for import by other scripts)")
    parser.add_argument("--check", metavar="EXPECTED_JSON",
                        help="Path to expected JSON — exit 3 on mismatch (CI / self-check)")
    args = parser.parse_args()

    repo_root = find_repo_root()

    traversal_path = args.source_traversal or str(repo_root / DEFAULT_TRAVERSAL_FSM)
    global_path = args.source_global or str(repo_root / DEFAULT_GLOBAL_FSM)
    doc_path = args.doc_path or str(repo_root / DEFAULT_FSM_DESIGN_DOC)

    # Extract from source
    try:
        traversal = extract_traversal_matrix(traversal_path) if args.fsm in (None, "traversal") else {}
        global_ = extract_global_matrix(global_path) if args.fsm in (None, "global") else {}
    except (FileNotFoundError, ValueError) as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    target = traversal if args.fsm == "traversal" else global_ if args.fsm == "global" else None

    # --diff-docs mode: cross-reference source ↔ doc
    if args.diff_docs:
        try:
            doc_traversal = extract_traversal_from_docs(doc_path) if args.fsm in (None, "traversal") else {}
            doc_global = extract_global_from_docs(doc_path) if args.fsm in (None, "global") else {}
        except (FileNotFoundError, ValueError) as e:
            print(f"Error reading docs: {e}", file=sys.stderr)
            sys.exit(1)

        if args.json_out:
            result = {}
            if args.fsm != "global":
                t_diffs = diff_matrices(traversal, doc_traversal, "TraversalFSM")
                result["traversal_fsm"] = format_diff_json(traversal, doc_traversal, t_diffs, "TraversalFSM")
            if args.fsm != "traversal":
                g_diffs = diff_matrices(global_, doc_global, "GlobalFSM")
                result["global_fsm"] = format_diff_json(global_, doc_global, g_diffs, "GlobalFSM")
            print(json.dumps(result, indent=2))
        else:
            if args.fsm != "global":
                t_diffs = diff_matrices(traversal, doc_traversal, "TraversalFSM")
                print(format_diff(traversal, doc_traversal, t_diffs, "TraversalFSM"))
                print()
            if args.fsm != "traversal":
                g_diffs = diff_matrices(global_, doc_global, "GlobalFSM")
                print(format_diff(global_, doc_global, g_diffs, "GlobalFSM"))

        # Exit with 1 if discrepancies found (useful for CI)
        all_diffs = (
            diff_matrices(traversal, doc_traversal, "TraversalFSM") if args.fsm != "global" else []
        ) + (
            diff_matrices(global_, doc_global, "GlobalFSM") if args.fsm != "traversal" else []
        )
        if all_diffs:
            sys.exit(1)  # Non-zero = drift detected
        return

    # --check mode
    if args.check:
        expected = json.loads(Path(args.check).read_text())
        current = traversal if args.fsm == "traversal" else global_ if args.fsm == "global" else None
        if current is None:
            current = {"traversal_fsm": {"matrix": traversal}, "global_fsm": {"matrix": global_}}
        # Normalize expected to same structure
        expected_matrix = expected.get("traversal_fsm", {}).get("matrix", expected) if "traversal_fsm" in expected else expected
        current_matrix = traversal if "traversal_fsm" not in str(type(current)) else current.get("traversal_fsm", {}).get("matrix", current)

        if args.fsm == "traversal":
            expected_matrix = expected.get("traversal_fsm", {}).get("matrix", expected)
            match = traversal == expected_matrix
        elif args.fsm == "global":
            expected_matrix = expected.get("global_fsm", {}).get("matrix", expected)
            match = global_ == expected_matrix
        else:
            match = (
                expected.get("traversal_fsm", {}).get("matrix", {}) == traversal
                and expected.get("global_fsm", {}).get("matrix", {}) == global_
            )

        if match:
            print("✅ Matrix matches expected.", file=sys.stderr)
        else:
            print("❌ Matrix MISMATCH — source has changed since expected.json was written.", file=sys.stderr)
            print("Current matrix (--json):", file=sys.stderr)
            print(format_matrix_json(traversal, global_), file=sys.stderr)
            sys.exit(3)
        return

    # Output
    if args.json_out:
        if target is not None:
            print(json.dumps(target, indent=2))
        else:
            print(format_matrix_json(traversal, global_))
    elif args.python_out:
        if target is not None:
            import pprint
            label = "TRAVERSAL_MATRIX" if args.fsm == "traversal" else "GLOBAL_MATRIX"
            print(f"# Extracted from {traversal_path if args.fsm == 'traversal' else global_path}")
            print(f"{label} = " + pprint.pformat(target, width=100))
        else:
            print(format_matrix_python(traversal, global_))
    else:
        if args.fsm != "global":
            print(format_ascii_matrix(traversal, "TraversalFSM"))
        if args.fsm != "traversal":
            print(format_ascii_matrix(global_, "GlobalFSM"))


if __name__ == "__main__":
    main()
