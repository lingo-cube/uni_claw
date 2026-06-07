# ExpectedBehavior YAML Format Reference

Complete reference for the ExpectedBehavior YAML format used in V6.9.2 simulation testing for behavior validation.

## Table of Contents

- [Overview](#overview)
- [Structure](#structure)
- [Action Definition](#action-definition)
- [Page Transition Definition](#page-transition-definition)
- [Completion Modes](#completion-modes)
- [Examples](#examples)
- [Validation Behavior](#validation-behavior)

## Overview

An ExpectedBehavior YAML file defines the expected behavior of a simulation test, including:
- Expected action sequence
- Expected page transitions
- Expected visited nodes
- Expected final state
- Expected completion mode

This enables automated validation that actual execution matches expected behavior.

## Structure

```yaml
scenario: str
description: str

actions:
  - action: str
    node: str
    target: str
    order: int

page_transitions:
  - from: str
    to: str
    trigger: str
    order: int

visited_nodes:
  - str

final_state: str
completion_mode: str
```

## Action Definition

### Basic Structure

```yaml
actions:
  - action: "click"
    node: "submit_btn"
    target: "submit"
    order: 1
```

### Action Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `action` | string | Yes | Action type (click, back, swipe, no_action) |
| `node` | string | Yes | Node ID that performs the action |
| `target` | string | No | Target element ID |
| `order` | int | Yes | Expected order in sequence (0-based) |

### Action Types

| Type | Description |
|------|-------------|
| `no_action` | No action (container nodes) |
| `click` | Click/tap action |
| `back` | Back navigation |
| `swipe` | Swipe gesture |
| `type` | Text input |
| `scroll` | Scroll action |

### Order Field

The `order` field defines the expected position in the action sequence (0-based):

```yaml
actions:
  - action: "no_action"    # order: 0 (first)
    node: "root"
    order: 0
  - action: "click"        # order: 1 (second)
    node: "detail_btn"
    target: "btn_detail"
    order: 1
  - action: "click"        # order: 2 (third)
    node: "back_btn"
    target: "btn_back"
    order: 2
```

## Page Transition Definition

### Basic Structure

```yaml
page_transitions:
  - from: "home"
    to: "detail"
    trigger: "btn_detail"
    order: 0
```

### Page Transition Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `from` | string | Yes | Source page ID |
| `to` | string | Yes | Target page ID |
| `trigger` | string | Yes | Element ID that triggered transition |
| `order` | int | Yes | Expected order in sequence (0-based) |

## Completion Modes

### Available Modes

| Mode | Description | Usage |
|------|-------------|-------|
| `normal` | Normal completion | Test completed as expected |
| `exception` | Exception occurred | Test encountered an error |
| `cancelled` | Test was cancelled | Test was interrupted |
| `timeout` | Test timed out | Test exceeded time limit |

### Example

```yaml
completion_mode: "normal"  # Most common
```

## Examples

### Example 1: Simple Navigation

```yaml
scenario: "Simple Navigation"
description: "User navigates from home to detail and back"

actions:
  - action: "no_action"
    node: "root"
    order: 0
  - action: "click"
    node: "detail_btn"
    target: "btn_detail"
    order: 1
  - action: "click"
    node: "back_btn"
    target: "btn_back"
    order: 2

page_transitions:
  - from: "home"
    to: "detail"
    trigger: "btn_detail"
    order: 0
  - from: "detail"
    to: "home"
    trigger: "btn_back"
    order: 1

visited_nodes:
  - "root"
  - "detail_btn"
  - "back_btn"

final_state: "COMPLETED"
completion_mode: "normal"
```

### Example 2: Multi-Path Navigation

```yaml
scenario: "Settings Navigation"
description: "User navigates to settings from home"

actions:
  - action: "no_action"
    node: "root"
    order: 0
  - action: "click"
    node: "settings_btn"
    target: "btn_settings"
    order: 1

page_transitions:
  - from: "home"
    to: "settings"
    trigger: "btn_settings"
    order: 0

visited_nodes:
  - "root"
  - "settings_btn"

final_state: "COMPLETED"
completion_mode: "normal"
```

### Example 3: Error Scenario

```yaml
scenario: "Login Failure"
description: "User fails login and stays on login page"

actions:
  - action: "type"
    node: "username_field"
    target: "username"
    order: 0
  - action: "type"
    node: "password_field"
    target: "password"
    order: 1
  - action: "click"
    node: "submit_btn"
    target: "btn_submit"
    order: 2

page_transitions: []  # No transition expected

visited_nodes:
  - "username_field"
  - "password_field"
  - "submit_btn"

final_state: "ERROR"
completion_mode: "exception"
```

### Example 4: Complex Flow

```yaml
scenario: "Checkout Flow"
description: "Complete checkout process from cart to confirmation"

actions:
  - action: "no_action"
    node: "cart_root"
    order: 0
  - action: "click"
    node: "checkout_btn"
    target: "btn_checkout"
    order: 1
  - action: "type"
    node: "shipping_field"
    target: "shipping_address"
    order: 2
  - action: "click"
    node: "continue_btn"
    target: "btn_continue"
    order: 3
  - action: "click"
    node: "confirm_btn"
    target: "btn_confirm"
    order: 4

page_transitions:
  - from: "cart"
    to: "checkout"
    trigger: "btn_checkout"
    order: 0
  - from: "checkout"
    to: "confirmation"
    trigger: "btn_continue"
    order: 1
  - from: "confirmation"
    to: "success"
    trigger: "btn_confirm"
    order: 2

visited_nodes:
  - "cart_root"
  - "checkout_btn"
  - "shipping_field"
  - "continue_btn"
  - "confirm_btn"

final_state: "COMPLETED"
completion_mode: "normal"
```

## Validation Behavior

### Node Matching

The validator uses multi-level matching:

1. **Exact Match**: Node ID exactly matches (confidence 1.0)
2. **Fuzzy ID Substring**: One ID contains the other (confidence 0.9)
3. **Fuzzy Target Text**: Target text matches (confidence 0.7)
4. **No Match**: No match found (confidence 0.0)

### Validation Rules

1. **Action Sequence**: Actions should occur in expected order
2. **Page Transitions**: Transitions should match expected pages
3. **Node Visitation**: Expected nodes should be visited
4. **Final State**: Final state should match expected
5. **Completion Mode**: Completion should match expected mode

### Matching Example

Given expected action:
```yaml
- action: "click"
  node: "detail_btn"
  target: "btn_detail"
  order: 1
```

The validator will match against actual actions with:
- Exact match on `"detail_btn"` → confidence 1.0
- Substring match on `"detail_btn_child"` → confidence 0.9
- Text match on target `"btn_detail"` → confidence 0.7

## Loading in Code

```python
from src.simulation.expected_behavior import ExpectedBehavior
from src.simulation.behavior_validator import BehaviorValidator

# Load from YAML file
expected = ExpectedBehavior.from_yaml("path/to/expected.yaml")

# Validate against actual trace
validator = BehaviorValidator()
result = validator.validate(
    expected=expected,
    actual_trace=trace_nodes,
    actual_result={"status": "COMPLETED"}
)

# Check results
if result.is_valid():
    print("Behavior matches expected")
else:
    print(f"Found {len(result.issues)} issues")
    for issue in result.issues:
        print(f"  - {issue.message}")
```

## Validation Result

### ValidationResult Fields

| Field | Type | Description |
|-------|------|-------------|
| `is_valid` | bool | Whether validation passed |
| `issues` | list | List of ValidationIssue objects |
| `exact_match_count` | int | Number of exact matches |
| `fuzzy_match_count` | int | Number of fuzzy matches |

### Issue Severity

| Severity | Description |
|----------|-------------|
| `error` | Critical mismatch |
| `warning` | Potential issue |
| `info` | Informational |

### ValidationIssue Fields

| Field | Type | Description |
|-------|------|-------------|
| `category` | string | Issue category (action_sequence, page_transition, etc.) |
| `message` | string | Human-readable description |
| `severity` | string | Issue severity (error, warning, info) |
| `expected` | any | Expected value |
| `actual` | any | Actual value |

## Best Practices

1. **Be Specific**: Use specific node IDs and targets for better matching
2. **Order Matters**: Ensure order field reflects actual execution order
3. **Page Transitions**: Define transitions for all expected page changes
4. **Visited Nodes**: Include all nodes that should be visited
5. **Final State**: Set appropriate final state and completion mode

## Common Patterns

### Pattern 1: No-Op Test

```yaml
scenario: "No Navigation"
description: "Load page and verify"

actions:
  - action: "no_action"
    node: "root"
    order: 0

page_transitions: []
visited_nodes:
  - "root"
final_state: "COMPLETED"
completion_mode: "normal"
```

### Pattern 2: Single Action

```yaml
scenario: "Single Click"
description: "Click one button"

actions:
  - action: "no_action"
    node: "root"
    order: 0
  - action: "click"
    node: "submit_btn"
    target: "btn_submit"
    order: 1

page_transitions:
  - from: "form"
    to: "success"
    trigger: "btn_submit"
    order: 0

visited_nodes:
  - "root"
  - "submit_btn"

final_state: "COMPLETED"
completion_mode: "normal"
```

### Pattern 3: Back Navigation

```yaml
scenario: "Back Navigation"
description: "Navigate and go back"

actions:
  - action: "click"
    node: "forward_btn"
    target: "btn_forward"
    order: 0
  - action: "back"
    node: "back_btn"
    order: 1

page_transitions:
  - from: "page_a"
    to: "page_b"
    trigger: "btn_forward"
    order: 0
  - from: "page_b"
    to: "page_a"
    trigger: "back"
    order: 1

visited_nodes:
  - "forward_btn"
  - "back_btn"

final_state: "COMPLETED"
completion_mode: "normal"
```

---

**Last Updated:** 2026-06-07
**Version:** V6.9.2
