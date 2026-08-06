#!/usr/bin/env python3
"""
test_contract_extractor.py — 从测试代码提取 FSM 契约。

从 C# 测试文件中提取 FSM 转移矩阵、handler 行为模式和门限值。
**刻意不读 C# 生产代码**——仅从测试断言和测试方法名推断 FSM 契约。
这是 shadow-fsm-analyzer 的 matrix_from_source.py 等价物（但从测试推断，不是从源码提取）。

用途：
  - 从 TransitionMatrix_* 测试提取合法/非法转移
  - 从 Handle*Tests 提取 handler 输入→输出映射
  - 从回归测试提取熔断限值

输入：
  - 一个或多个 C# 测试文件或目录（默认: tests/UniClaw.Core.Tests/StateMachine/）

输出：
  - stdout: JSON（机器可读）或人类可读报告
  - 转移矩阵、handler 模式、门限值

示例：
  # 从 StateMachine 测试目录提取完整 FSM 契约
  python test_contract_extractor.py --test-dir tests/UniClaw.Core.Tests/StateMachine/ --json

  # 仅提取转移矩阵
  python test_contract_extractor.py --test-dir tests/UniClaw.Core.Tests/StateMachine/ --transitions-only

  # 仅提取门限值
  python test_contract_extractor.py --test-dir tests/UniClaw.Core.Tests/StateMachine/ --thresholds-only

  # 对照期望 JSON 自检（CI 可用）
  python test_contract_extractor.py --check expected_contract.json
"""

import argparse
import json
import os
import re
import sys
from pathlib import Path
from typing import Optional


# ---------------------------------------------------------------------------
# Patterns — regex for C# test code extraction
# ---------------------------------------------------------------------------

# TraversalState enum values (from constitution C-1: 8 locked values)
# Inferred from test code patterns and OpenSpec specs
TRAVERSAL_STATES = [
    "NodeSelect", "PreconditionCheck", "Execute", "ResultVerify",
    "Branch", "FrameComplete", "ErrorHandling", "PopupHandling",
]

# TransitionTo(TraversalState.Xxx) — extract target state
RE_TRANSITION_TO = re.compile(r'TransitionTo\(\s*TraversalState\.(\w+)\s*\)')
# TransitionTo(xxx) — without enum prefix (variable)
RE_TRANSITION_TO_VAR = re.compile(r'TransitionTo\(\s*(\w+)\s*\)')

# Assert.Throws<DomainValidationException>(...) — invalid transition
# Pattern: Assert.Throws<DomainValidationException>(() => fsm.TransitionTo(TraversalState.X))
RE_ASSERT_THROWS_DVE = re.compile(
    r'Assert\.Throws<DomainValidationException>\(\s*\(\)\s*=>\s*\w+\.TransitionTo\(\s*TraversalState\.(\w+)\s*\)',
    re.IGNORECASE
)

# TransitionTo call inside Assert.Throws — extract the forbidden target
RE_THROWS_TRANSITION_TO = re.compile(
    r'Assert\.Throws<DomainValidationException>\(\s*\(\)\s*=>\s*\w+\.TransitionTo\(\s*TraversalState\.(\w+)\s*\)',
)

# Step test patterns: Step_<FromState>_<Condition>_GoesTo<ToState>
RE_STEP_TEST = re.compile(r'Step_(\w+?)(?:With\w+)?_GoesTo(\w+)', re.IGNORECASE)
# e.g., Step_NodeSelectWithEmptyStack_GoesToBranch → (NodeSelect, Branch)

# Test method name patterns
RE_FACT_METHOD = re.compile(r'\[Fact\]\s*(?:\([^)]*\)\s*)?\n\s*public\s+\S+\s+(\w+)\s*\(')
RE_THEORY_METHOD = re.compile(r'\[Theory\]\s*(?:\([^)]*\)\s*)?\n\s*public\s+\S+\s+(\w+)\s*\(')

# TransitionMatrix test patterns (from method names)
RE_VALID_TRANSITION_TEST = re.compile(r'TransitionMatrix_(\w+)_To_(\w+)_(\w+)', re.IGNORECASE)
# e.g., TransitionMatrix_NodeSelect_To_Branch_Accepted → (NodeSelect, Branch, Accepted)
# e.g., TransitionMatrix_PreconditionCheck_To_Branch_Rejected → (PreconditionCheck, Branch, Rejected)

# TransitionMatrix test name variant: TransitionMatrix_ErrorNotToTraversing
RE_TRANSITION_NOT_TO = re.compile(r'TransitionMatrix_(\w+)NotTo(\w+)', re.IGNORECASE)
# e.g., TransitionMatrix_ErrorNotToTraversing → (Error, Traversing) invalid

# Handler test patterns
RE_HANDLER_TEST = re.compile(r'Handle(\w+?)(?:_|Tests)', re.IGNORECASE)
# e.g., HandleBranch_DynamicMatch_ReturnsNodeSelect → (Branch, DynamicMatch → NodeSelect)

# Threshold patterns — look for literal numbers in test assertions
RE_THRESHOLD_PATTERN = re.compile(
    r'(?:maxRetries|MaxRetries|consecutiveErrors|ConsecutiveErrors|'
    r'staleClick|StaleClick|pageItem|PageItem|'
    r'maxDepth|MaxDepth|maxSteps|MaxSteps|'
    r'limit|Limit|threshold|Threshold|gate|Gate)'
    r'\s*[=<>!]+\s*(\d+)',
    re.IGNORECASE
)

# ConsecutiveErrors increment patterns
RE_INCREMENT_CONSECUTIVE = re.compile(r'(?:IncrementConsecutiveErrors|ConsecutiveErrors\s*\+=\s*1)')

# LastError patterns
RE_SET_LAST_ERROR = re.compile(r'SetLastError')
RE_CLEAR_LAST_ERROR = re.compile(r'(?:ClearLastError|LastError\s*=\s*null|SetLastError\(\s*null\s*\))')

# handler return value patterns: "returns TraversalState.Xxx" or "Assert.Equal(TraversalState.Xxx, result)"
RE_RETURNS_STATE = re.compile(r'(?:returns?|==?|Equal\(TraversalState\.)\s*TraversalState\.(\w+)', re.IGNORECASE)
RE_ASSERT_EQUAL_STATE = re.compile(r'Assert\.Equal\(\s*TraversalState\.(\w+)\s*,', re.IGNORECASE)


def find_test_files(test_dir: str) -> list[Path]:
    """Find all .cs test files in directory."""
    root = Path(test_dir)
    if not root.exists():
        print(f"Error: directory not found: {test_dir}", file=sys.stderr)
        sys.exit(2)

    if root.is_file():
        return [root]

    return sorted(root.rglob("*.cs"))


def extract_transitions_from_file(filepath: Path) -> dict:
    """
    Extract transition information from a test file.
    Returns dict with 'valid', 'invalid', 'unclear' transitions, and 'handler_returns'.
    """
    content = filepath.read_text(encoding='utf-8')
    result = {
        'file': str(filepath),
        'valid_transitions': set(),
        'invalid_transitions': set(),
        'handler_returns': {},  # handler_name → [states]
        'thresholds': {},
    }

    # --- Extract from test method names ---
    fact_methods = RE_FACT_METHOD.findall(content)
    theory_methods = RE_THEORY_METHOD.findall(content)
    all_methods = fact_methods + theory_methods

    for method_name in all_methods:
        # TransitionMatrix_Xxx_To_Yyy_Accepted/Rejected
        tm_match = RE_VALID_TRANSITION_TEST.match(method_name)
        if tm_match:
            from_state = tm_match.group(1)
            to_state = tm_match.group(2)
            verdict = tm_match.group(3).lower()
            if from_state in TRAVERSAL_STATES and to_state in TRAVERSAL_STATES:
                if 'accept' in verdict or 'valid' in verdict:
                    result['valid_transitions'].add((from_state, to_state))
                elif 'reject' in verdict or 'invalid' in verdict:
                    result['invalid_transitions'].add((from_state, to_state))

        # TransitionMatrix_XxxNotToYyy → (Xxx, Yyy) invalid
        not_match = RE_TRANSITION_NOT_TO.match(method_name)
        if not_match:
            from_state = not_match.group(1)
            to_state = not_match.group(2)
            if from_state in TRAVERSAL_STATES and to_state in TRAVERSAL_STATES:
                result['invalid_transitions'].add((from_state, to_state))

        # Step_<FromState>_<Condition>_GoesTo<ToState>
        step_match = RE_STEP_TEST.search(method_name)
        if step_match:
            from_hint = step_match.group(1)
            to_state = step_match.group(2)
            # Map "NodeSelectWithEmptyStack" → "NodeSelect"
            for state in TRAVERSAL_STATES:
                if from_hint.lower().startswith(state.lower()):
                    if to_state in TRAVERSAL_STATES:
                        result['valid_transitions'].add((state, to_state))
                    break

    # --- Extract from test bodies: transitions via TransitionTo calls ---

    # Track the "current state" by following TransitionTo chains in test bodies
    # Find all TransitionTo(TraversalState.X) calls
    all_transition_to = list(RE_TRANSITION_TO.finditer(content))
    # Find all Assert.Throws<DVE>(...TransitionTo(TraversalState.X)...) calls
    throws_transitions = set()
    for match in RE_THROWS_TRANSITION_TO.finditer(content):
        target = match.group(1)
        if target in TRAVERSAL_STATES:
            throws_transitions.add((match.start(), target))

    # For each bare TransitionTo call, infer the "from" state by tracking the chain
    # Strategy: scan TransitionTo calls sequentially. The first one transitions from
    # the initial state (NodeSelect, since FSM is new TraversalFSM(ctx) followed by TransitionTo).
    # Each subsequent TransitionTo transitions from the previous target.
    prev_target = "NodeSelect"  # default initial state in tests
    seen_new_fsm = False

    for match in all_transition_to:
        target = match.group(1)
        if target not in TRAVERSAL_STATES:
            continue

        # Check if this is inside Assert.Throws
        is_throws = any(abs(match.start() - t_start) < 50 for t_start, _ in throws_transitions)
        if is_throws:
            continue

        # Check if a new FSM was created before this call (resets to NodeSelect)
        before = content[max(0, match.start() - 200):match.start()]
        if 'new TraversalFSM' in before:
            prev_target = "NodeSelect"
            seen_new_fsm = True

        # Record the transition: prev_target → target
        if prev_target in TRAVERSAL_STATES and prev_target != target:
            result['valid_transitions'].add((prev_target, target))

        prev_target = target

    # Extract invalid transitions from Assert.Throws-wrapped calls
    for t_start, target in throws_transitions:
        # Find the "from" state by looking at the last TransitionTo before this
        last_target = "NodeSelect"
        for match in all_transition_to:
            if match.start() >= t_start:
                break
            last_target = match.group(1)
        if last_target in TRAVERSAL_STATES and target in TRAVERSAL_STATES:
            result['invalid_transitions'].add((last_target, target))

    # --- Extract handler return values from StepAsync assertions ---

    # Pattern: var next = await fsm.StepAsync(); Assert.Equal(TraversalState.X, next)
    # Find the state before StepAsync by tracking the TransitionTo chain
    for match in RE_ASSERT_EQUAL_STATE.finditer(content):
        target_state = match.group(1)
        if target_state not in TRAVERSAL_STATES:
            continue
        # Look back for "StepAsync" to distinguish handler output from other Assert.Equal
        before = content[max(0, match.start() - 400):match.start()]
        if 'StepAsync' not in before:
            # This Assert.Equal may not be about handler output — skip noise
            continue
        # Try to determine which handler produced this output
        # Look for "fromState" context in the surrounding test
        before_context = content[max(0, match.start() - 800):match.start()]
        for handler in TRAVERSAL_STATES:
            if handler.lower() in before_context.lower():
                key = f"Handle{handler}"
                if key not in result['handler_returns']:
                    result['handler_returns'][key] = set()
                result['handler_returns'][key].add(target_state)
                break

    # Extract thresholds
    for match in RE_THRESHOLD_PATTERN.finditer(content):
        pattern_name = match.group(0).split('=')[0].strip().split()[-1] if '=' in match.group(0) else match.group(1)
        value = int(match.group(1))
        if value > 0 and value < 1000:  # reasonable threshold range
            result['thresholds'][pattern_name] = value

    # Detect LastError lifecycle patterns
    if RE_SET_LAST_ERROR.search(content):
        if RE_CLEAR_LAST_ERROR.search(content):
            result['last_error_lifecycle'] = 'set_and_clear'
        else:
            result['last_error_lifecycle'] = 'set_only'

    # Detect ConsecutiveErrors patterns
    inc_matches = RE_INCREMENT_CONSECUTIVE.findall(content)
    if inc_matches:
        result['consecutive_error_increment_sites'] = len(inc_matches)

    return result


def merge_results(results: list[dict]) -> dict:
    """Merge results from multiple test files into one contract."""
    merged = {
        'valid_transitions': set(),
        'invalid_transitions': set(),
        'handler_returns': {},
        'thresholds': {},
        'source_files': [],
    }

    for r in results:
        merged['source_files'].append(r['file'])
        merged['valid_transitions'].update(r.get('valid_transitions', set()))
        merged['invalid_transitions'].update(r.get('invalid_transitions', set()))

        for handler, states in r.get('handler_returns', {}).items():
            if handler not in merged['handler_returns']:
                merged['handler_returns'][handler] = set()
            merged['handler_returns'][handler].update(states)

        merged['thresholds'].update(r.get('thresholds', {}))

        # Preserve lifecycle / increment metadata from files that have it
        if 'last_error_lifecycle' in r:
            merged['last_error_lifecycle'] = r['last_error_lifecycle']
        if 'consecutive_error_increment_sites' in r:
            merged['consecutive_error_increment_sites'] = r['consecutive_error_increment_sites']

    return merged


def build_matrix(merged: dict) -> dict:
    """Build a transition matrix from merged test data."""
    matrix = {s: {'valid': [], 'invalid': [], 'unknown': []} for s in TRAVERSAL_STATES}

    for from_s in TRAVERSAL_STATES:
        valid_targets = set()
        invalid_targets = set()

        for (f, t) in merged['valid_transitions']:
            if f == from_s:
                valid_targets.add(t)

        for (f, t) in merged['invalid_transitions']:
            if f == from_s:
                invalid_targets.add(t)

        matrix[from_s]['valid'] = sorted(valid_targets)
        matrix[from_s]['invalid'] = sorted(invalid_targets)

        # Unknown = all states - valid - invalid - self (self-loop always invalid)
        all_others = set(TRAVERSAL_STATES) - {from_s}
        unknown = all_others - valid_targets - invalid_targets
        matrix[from_s]['unknown'] = sorted(unknown)

    return matrix


def format_report(merged: dict, matrix: dict) -> str:
    """Format human-readable report."""
    lines = []
    lines.append("=" * 60)
    lines.append("FSM Contract Extracted from Tests")
    lines.append("=" * 60)
    lines.append(f"Source files: {len(merged['source_files'])}")
    lines.append(f"Valid transitions found: {len(merged['valid_transitions'])}")
    lines.append(f"Invalid transitions confirmed: {len(merged['invalid_transitions'])}")
    lines.append()

    lines.append("--- Transition Matrix (from test assertions) ---")
    for state in TRAVERSAL_STATES:
        info = matrix[state]
        valid_str = ", ".join(info['valid']) if info['valid'] else "(none found)"
        invalid_str = ", ".join(info['invalid']) if info['invalid'] else "(none found)"
        lines.append(f"  {state}:")
        lines.append(f"    → valid:   {valid_str}")
        lines.append(f"    → invalid: {invalid_str}")
        if info['unknown']:
            lines.append(f"    → untested: {', '.join(info['unknown'])}")
    lines.append()

    lines.append("--- Handler Return Values (from test assertions) ---")
    for handler, states in sorted(merged.get('handler_returns', {}).items()):
        lines.append(f"  {handler}: → {{{', '.join(sorted(states))}}}")
    if not merged.get('handler_returns'):
        lines.append("  (none extracted — may need deeper test content analysis)")
    lines.append()

    lines.append("--- Thresholds (from test literals) ---")
    for name, value in sorted(merged.get('thresholds', {}).items()):
        lines.append(f"  {name}: {value}")
    if not merged.get('thresholds'):
        lines.append("  (none extracted)")
    lines.append()

    if 'last_error_lifecycle' in merged:
        lines.append(f"--- LastError Lifecycle: {merged['last_error_lifecycle']} ---")
    if 'consecutive_error_increment_sites' in merged:
        lines.append(f"--- ConsecutiveErrors increment sites in tests: {merged['consecutive_error_increment_sites']} ---")

    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(
        description="test_contract_extractor — 从测试代码提取 FSM 契约",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  %(prog)s --test-dir tests/UniClaw.Core.Tests/StateMachine/ --json
  %(prog)s --test-dir tests/UniClaw.Core.Tests/StateMachine/ --transitions-only
  %(prog)s --check expected_contract.json
        """,
    )
    parser.add_argument(
        '--test-dir', default='tests/UniClaw.Core.Tests/StateMachine/',
        help='测试文件目录（默认: tests/UniClaw.Core.Tests/StateMachine/）'
    )
    parser.add_argument('--json', action='store_true', help='以 JSON 格式输出')
    parser.add_argument('--transitions-only', action='store_true', help='仅输出转移矩阵')
    parser.add_argument('--thresholds-only', action='store_true', help='仅输出门限值')
    parser.add_argument('--check', metavar='EXPECTED.json', help='对照期望 JSON 自检（CI 可用）')

    args = parser.parse_args()

    # Resolve test_dir relative to repo root
    if not os.path.isabs(args.test_dir):
        # Try common repo root locations
        for prefix in ['', '../..', '../../..']:
            candidate = os.path.join(prefix, args.test_dir)
            if os.path.exists(candidate):
                args.test_dir = os.path.abspath(candidate)
                break

    files = find_test_files(args.test_dir)
    if not files:
        print(f"Error: no .cs files found in {args.test_dir}", file=sys.stderr)
        sys.exit(1)

    results = [extract_transitions_from_file(f) for f in files]
    merged = merge_results(results)
    matrix = build_matrix(merged)

    # Convert sets to lists for JSON serialization
    json_ready = {
        'valid_transitions': sorted(list(t) for t in merged['valid_transitions']),
        'invalid_transitions': sorted(list(t) for t in merged['invalid_transitions']),
        'handler_returns': {k: sorted(v) for k, v in merged.get('handler_returns', {}).items()},
        'thresholds': merged.get('thresholds', {}),
        'source_files': merged['source_files'],
    }
    if 'last_error_lifecycle' in merged:
        json_ready['last_error_lifecycle'] = merged['last_error_lifecycle']
    if 'consecutive_error_increment_sites' in merged:
        json_ready['consecutive_error_increment_sites'] = merged['consecutive_error_increment_sites']

    if args.check:
        with open(args.check, 'r') as f:
            expected = json.load(f)
        # Simple diff: check valid_transitions match
        expected_set = set(tuple(t) for t in expected.get('valid_transitions', []))
        actual_set = set(tuple(t) for t in json_ready['valid_transitions'])

        missing = expected_set - actual_set
        extra = actual_set - expected_set

        if missing or extra:
            if missing:
                print(f"Expected but not found in tests: {sorted(missing)}", file=sys.stderr)
            if extra:
                print(f"Found in tests but not expected: {sorted(extra)}", file=sys.stderr)
            sys.exit(3)  # exit code 3 = contract mismatch
        else:
            print("✅ Test contract matches expected.", file=sys.stderr)
            sys.exit(0)

    if args.transitions_only:
        print(json.dumps({
            'valid_transitions': json_ready['valid_transitions'],
            'invalid_transitions': json_ready['invalid_transitions'],
        }, indent=2))
    elif args.thresholds_only:
        print(json.dumps({'thresholds': json_ready['thresholds']}, indent=2))
    elif args.json:
        print(json.dumps(json_ready, indent=2, default=list))
    else:
        print(format_report(merged, matrix))

    # Exit code: 1 if no valid transitions found (incomplete extraction)
    if not merged['valid_transitions']:
        sys.exit(1)


if __name__ == '__main__':
    main()
