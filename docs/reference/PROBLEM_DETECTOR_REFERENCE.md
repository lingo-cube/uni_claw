# ProblemDetector Configuration Reference

Complete reference for the ProblemDetector and ProblemDetectorConfig classes introduced in V6.9.2 for automatic problem detection in simulation tests.

## Table of Contents

- [Overview](#overview)
- [ProblemDetectorConfig](#problemdetectorconfig)
- [Sensitivity Levels](#sensitivity-levels)
- [Feature Toggles](#feature-toggles)
- [Problem Types](#problem-types)
- [Severity Levels](#severity-levels)
- [Usage Examples](#usage-examples)
- [Detection Methods](#detection-methods)

## Overview

The ProblemDetector automatically identifies abnormal execution patterns in simulation traces, including:

- **Infinite Loops**: Repeated actions or state sequence loops
- **Repeated Actions**: Abnormal repetition on the same node
- **Unvisited Nodes**: Expected nodes that were never visited
- **State Machine Errors**: Invalid state transitions or error states
- **Page Mismatches**: Failed page transitions
- **Orphan Nodes**: Dynamic nodes created but never executed

## ProblemDetectorConfig

### Basic Configuration

```python
from src.simulation.problem_detector import ProblemDetector, ProblemDetectorConfig

# Default configuration
detector = ProblemDetector()

# Custom configuration
config = ProblemDetectorConfig(
    max_action_repeats=5,
    max_loop_depth=8,
)
detector = ProblemDetector(config)
```

### Configuration Fields

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| `max_action_repeats` | int | 3 | 1-10 | Maximum allowed consecutive repeats |
| `max_loop_depth` | int | 5 | 2-20 | Maximum depth for loop detection |
| `loop_detection_sensitivity` | str | "medium" | low/medium/high | Sensitivity for loop detection |
| `enable_infinite_loop_detection` | bool | true | - | Enable infinite loop detection |
| `enable_repeated_action_detection` | bool | true | - | Enable repeated action detection |
| `enable_unvisited_node_detection` | bool | true | - | Enable unvisited node detection |
| `enable_state_machine_error_detection` | bool | true | - | Enable state machine error detection |
| `enable_page_mismatch_detection` | bool | true | - | Enable page mismatch detection |
| `enable_orphan_node_detection` | bool | true | - | Enable orphan node detection |

## Sensitivity Levels

### Low Sensitivity

Less aggressive detection, higher thresholds:

```python
config = ProblemDetectorConfig(
    loop_detection_sensitivity="low"
)
```

**Effect:**
- `max_action_repeats` × 2
- `max_loop_depth` × 2

**Use when:** You want fewer false positives and can tolerate some repetition.

### Medium Sensitivity (Default)

Balanced detection:

```python
config = ProblemDetectorConfig(
    loop_detection_sensitivity="medium"
)
```

**Effect:**
- `max_action_repeats` (as configured)
- `max_loop_depth` (as configured)

**Use when:** Standard detection behavior.

### High Sensitivity

Aggressive detection, lower thresholds:

```python
config = ProblemDetectorConfig(
    loop_detection_sensitivity="high"
)
```

**Effect:**
- `max_action_repeats` ÷ 2 (minimum 1)
- `max_loop_depth` ÷ 2 (minimum 2)

**Use when:** You want to catch even subtle issues.

### Sensitivity Comparison

For default thresholds (max_action_repeats=3, max_loop_depth=5):

| Sensitivity | Effective max_repeats | Effective max_loop_depth |
|-------------|----------------------|--------------------------|
| Low | 6 | 10 |
| Medium | 3 | 5 |
| High | 1 | 2 |

## Feature Toggles

### Enable All Features (Default)

```python
config = ProblemDetectorConfig(
    enable_infinite_loop_detection=True,
    enable_repeated_action_detection=True,
    enable_unvisited_node_detection=True,
    enable_state_machine_error_detection=True,
    enable_page_mismatch_detection=True,
    enable_orphan_node_detection=True,
)
```

### Disable Specific Features

```python
config = ProblemDetectorConfig(
    enable_infinite_loop_detection=False,  # Disable infinite loop detection
    enable_repeated_action_detection=False,  # Disable repeated action detection
)
```

### Common Toggle Patterns

**Only critical issues:**
```python
config = ProblemDetectorConfig(
    enable_repeated_action_detection=False,
    enable_unvisited_node_detection=False,
    enable_page_mismatch_detection=False,
    enable_orphan_node_detection=False,
)
```

**All warnings:**
```python
config = ProblemDetectorConfig(
    loop_detection_sensitivity="low",
    enable_infinite_loop_detection=True,
    enable_state_machine_error_detection=True,
)
```

## Problem Types

### INFINITE_LOOP

**Severity:** Critical or Warning

Detected when:
- Same action repeated on same element beyond threshold
- State sequence shows repeating pattern

**Example:**
```python
Problem(
    type=ProblemType.INFINITE_LOOP,
    description="Action repeated 4 times: click on btn_submit",
    severity=ProblemSeverity.CRITICAL,
    location="btn_submit",
    evidence={"repeat_count": 4}
)
```

### REPEATED_ACTION

**Severity:** Warning

Detected when:
- Same action type executed consecutively on same node

**Example:**
```python
Problem(
    type=ProblemType.REPEATED_ACTION,
    description="Action 'click' repeated 3 times on btn_next",
    severity=ProblemSeverity.WARNING,
    location="btn_next",
    evidence={"repeat_count": 3}
)
```

### UNVISITED_NODE

**Severity:** Warning

Detected when:
- Expected node was never visited

**Example:**
```python
Problem(
    type=ProblemType.UNVISITED_NODE,
    description="Expected node not visited: settings_btn",
    severity=ProblemSeverity.WARNING,
    location="settings_btn",
    evidence={"expected_node": "settings_btn"}
)
```

### STATE_MACHINE_ERROR

**Severity:** Critical or Error

Detected when:
- Final state is ERROR
- Invalid state transition occurred

**Example:**
```python
Problem(
    type=ProblemType.STATE_MACHINE_ERROR,
    description="Invalid state transition: COMPLETED -> EXECUTING",
    severity=ProblemSeverity.ERROR,
    location="state_machine",
    evidence={"from_state": "COMPLETED", "to_state": "EXECUTING"}
)
```

### PAGE_MISMATCH

**Severity:** Warning

Detected when:
- Page transition stayed on same page (from == to)

**Example:**
```python
Problem(
    type=ProblemType.PAGE_MISMATCH,
    description="Page transition stayed on same page: login",
    severity=ProblemSeverity.WARNING,
    location="login",
    evidence={"transition": {...}}
)
```

### ORPHAN_NODE

**Severity:** Warning

Detected when:
- Dynamic node was created but never executed

**Example:**
```python
Problem(
    type=ProblemType.ORPHAN_NODE,
    description="Dynamic node created but never executed: dynamic_btn_123",
    severity=ProblemSeverity.WARNING,
    location="dynamic_btn_123",
    evidence={"lifecycle_events": ["created", "matched"]}
)
```

## Severity Levels

| Level | Description | Usage |
|-------|-------------|-------|
| `critical` | Critical issue | Test cannot proceed, infinite loop |
| `error` | Error condition | Invalid state, transition error |
| `warning` | Warning | Potential issue, worth reviewing |
| `info` | Informational | FYI only |

## Usage Examples

### Basic Usage

```python
from src.simulation.problem_detector import ProblemDetector

detector = ProblemDetector()
problems = detector.detect(trace_nodes)

# Check for critical issues
critical = [p for p in problems if p.severity == "critical"]
if critical:
    print(f"Found {len(critical)} critical issues")
    for problem in critical:
        print(f"  - {problem.description}")
```

### With Expected Nodes

```python
expected_nodes = {"root", "detail_btn", "back_btn"}
problems = detector.detect(trace_nodes, expected_nodes=expected_nodes)

# Check for unvisited nodes
unvisited = [p for p in problems if p.type == "unvisited_node"]
if unvisited:
    print(f"Missed {len(unvisited)} expected nodes")
```

### With Final Result

```python
actual_result = {
    "status": "ERROR",
    "error_type": "StateTransitionError",
    "error": "Invalid transition"
}
problems = detector.detect(trace_nodes, actual_result=actual_result)

# Check for state machine errors
sm_errors = [p for p in problems if p.type == "state_machine_error"]
if sm_errors:
    print("State machine errors detected")
```

### Custom Configuration

```python
from src.simulation.problem_detector import ProblemDetectorConfig

config = ProblemDetectorConfig(
    max_action_repeats=5,
    max_loop_depth=10,
    loop_detection_sensitivity="low",
)
detector = ProblemDetector(config)
problems = detector.detect(trace_nodes)
```

### Feature Toggle

```python
config = ProblemDetectorConfig(
    enable_infinite_loop_detection=False,
    enable_repeated_action_detection=False,
)
detector = ProblemDetector(config)
problems = detector.detect(trace_nodes)
```

## Detection Methods

### Main Detect Method

```python
def detect(
    trace: List[Dict[str, Any]],
    expected_nodes: Optional[Set[str]] = None,
    actual_result: Optional[Dict[str, Any]] = None,
) -> List[Problem]:
```

**Parameters:**
- `trace`: Execution trace nodes (list of dicts)
- `expected_nodes`: Optional set of expected node IDs
- `actual_result`: Optional final execution result

**Returns:**
- List of detected `Problem` objects

### Individual Detection Methods

```python
# Infinite loop detection
detector._detect_infinite_loop(actions, state_sequence)

# Repeated action detection
detector._detect_repeated_actions(actions)

# Unvisited node detection
detector._detect_unvisited_nodes(expected_nodes, visited_nodes)

# State machine error detection
detector._detect_state_machine_error(state_sequence, actual_result)

# Page mismatch detection
detector._detect_page_mismatch(page_transitions)

# Orphan node detection
detector._detect_orphan_nodes(lifecycle_events)
```

### Helper Methods

```python
# Extract actions from trace
actions = detector._extract_actions(trace)

# Extract state sequence
states = detector._extract_state_sequence(trace)

# Extract page transitions
transitions = detector._extract_page_transitions(trace)

# Extract visited nodes
visited = detector._extract_visited_nodes(trace)

# Extract dynamic lifecycle events
lifecycle = detector._extract_dynamic_lifecycle(trace)

# Check if transition is valid
is_valid = detector._is_valid_transition(from_state, to_state)

# Find repeating patterns
pattern = detector._find_repeating_patterns(sequence)
```

## Problem Object

### Fields

```python
@dataclass
class Problem:
    type: ProblemType
    description: str
    severity: ProblemSeverity
    location: str
    evidence: Dict[str, Any]
    hint: Optional[str]
```

### Methods

```python
# Convert to dictionary
problem_dict = problem.to_dict()

# Example output:
{
    "type": "infinite_loop",
    "description": "Action repeated 4 times: click on btn_submit",
    "severity": "critical",
    "location": "btn_submit",
    "evidence": {"repeat_count": 4},
    "hint": "Check if the target element is accessible and interactive"
}
```

## Valid State Transitions

The ProblemDetector validates state transitions against these rules:

| From State | Valid To States |
|------------|-----------------|
| IDLE | BINDING, EXECUTING |
| BINDING | IDLE, EXECUTING |
| EXECUTING | RESULT_VERIFY, AUTO_ESCAPE, BRANCH, FRAME_COMPLETE |
| RESULT_VERIFY | EXECUTING, BRANCH, FRAME_COMPLETE |
| AUTO_ESCAPE | EXECUTING |
| BRANCH | NODE_SELECT, FRAME_COMPLETE |
| NODE_SELECT | EXECUTING |
| FRAME_COMPLETE | BRANCH, NODE_SELECT, COMPLETED, ERROR |
| COMPLETED | (none - terminal) |
| ERROR | (none - terminal) |

Invalid transitions trigger STATE_MACHINE_ERROR problems.

## Best Practices

1. **Start with defaults**: Use default configuration initially
2. **Adjust sensitivity**: Use `loop_detection_sensitivity` before changing absolute thresholds
3. **Feature toggles**: Disable features you don't need for cleaner output
4. **Check severity**: Focus on critical and error problems first
5. **Review evidence**: Use the `evidence` field to understand why problems were detected
6. **Combine with validation**: Use BehaviorValidator alongside ProblemDetector

## Integration with Tests

```python
def test_simulation_no_critical_problems():
    """Test that simulation runs without critical issues."""
    # Run simulation
    result = run_simulation(plan, fixture)
    trace_nodes = storage.read(result.trace_id)

    # Detect problems
    detector = ProblemDetector()
    problems = detector.detect(trace_nodes)

    # Assert no critical problems
    critical = [p for p in problems if p.severity == "critical"]
    assert len(critical) == 0, f"Found critical problems: {[p.description for p in critical]}"

def test_simulation_all_expected_visited():
    """Test that all expected nodes were visited."""
    expected_nodes = {"root", "btn1", "btn2"}

    result = run_simulation(plan, fixture)
    trace_nodes = storage.read(result.trace_id)

    detector = ProblemDetector()
    problems = detector.detect(trace_nodes, expected_nodes=expected_nodes)

    unvisited = [p for p in problems if p.type == "unvisited_node"]
    assert len(unvisited) == 0
```

---

**Last Updated:** 2026-06-07
**Version:** V6.9.2
