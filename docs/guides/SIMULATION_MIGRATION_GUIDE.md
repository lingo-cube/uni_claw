# Migration Guide: MockVisionService to StatefulMockVisionService

This guide helps you migrate existing simulation tests from the static `MockVisionService` to the stateful `StatefulMockVisionService` introduced in V6.9.2.

## Overview

The V6.9.2 enhancement introduces stateful mock services that can simulate page transitions, enabling more realistic simulation tests that can catch bugs like infinite loops and incorrect navigation patterns.

## Why Migrate?

**Before (MockVisionService):**
- Returns the same page data regardless of actions
- Cannot simulate page transitions
- Tests show COMPLETED status even with incorrect behavior
- Cannot detect the AUTO_ESCAPE infinite loop bug

**After (StatefulMockVisionService):**
- Tracks page state and simulates transitions
- Supports realistic multi-page scenarios
- Behavior validation catches mismatches
- Problem detection identifies abnormal patterns

## Quick Comparison

### Old Approach (MockVisionService)

```python
from src.simulation.mock_vision import MockVisionService
from src.simulation.mock_action import MockActionExecutor

# Static virtual pages JSON
vision = MockVisionService(virtual_pages_json)
action = MockActionExecutor()

# Always returns same page, no transitions
```

### New Approach (StatefulMockVisionService)

```python
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor
from src.simulation.state_fixture import StateFixture

# YAML-based fixture with transition rules
fixture = StateFixture.from_yaml("tests/v6/fixtures/simple_two_page.yaml")
vision = StatefulMockVisionService(fixture)
action = StatefulMockActionExecutor(vision)

# Simulates page transitions based on fixture rules
```

## Step-by-Step Migration

### Step 1: Convert virtual_pages JSON to StateFixture YAML

**Before (virtual_pages.json):**
```json
{
  "login_page": {
    "name": "LoginScreen",
    "items": [
      {"id": "username", "text": "Username", "type": "text_input"},
      {"id": "submit", "text": "Login", "type": "button"}
    ]
  },
  "home_page": {
    "name": "HomeScreen",
    "items": [
      {"id": "logout", "text": "Logout", "type": "button"}
    ]
  }
}
```

**After (fixture.yaml):**
```yaml
pages:
  login:
    page_name: "LoginScreen"
    elements:
      - id: "username"
        type: "text_input"
        text: "Username"
        coordinate: {x: 0.5, y: 0.3}
      - id: "submit"
        type: "button"
        text: "Login"
        coordinate: {x: 0.5, y: 0.5}
        action_target: "home"
    is_complete: false

  home:
    page_name: "HomeScreen"
    elements:
      - id: "logout"
        type: "button"
        text: "Logout"
        coordinate: {x: 0.9, y: 0.9}
    is_complete: true

transitions:
  login_to_home:
    trigger: "submit"
    from_page: "login"
    to_page: "home"
    action: "click"

initial_page: "login"
history_depth: 10
```

### Step 2: Update Test Setup

**Before:**
```python
def test_login_flow():
    vision = MockVisionService(load_json("virtual_pages.json"))
    action = MockActionExecutor()
    # ... test code
```

**After:**
```python
def test_login_flow():
    fixture = StateFixture.from_yaml("tests/v6/fixtures/login.yaml")
    vision = StatefulMockVisionService(fixture)
    action = StatefulMockActionExecutor(vision)
    # ... test code
```

### Step 3: Update Page Element References

**Field Mapping Changes:**

| Old Field (virtual_pages) | New Field (StateFixture) | PageAnalysis Field |
|---------------------------|--------------------------|---------------------|
| `text` | `text` | `MenuItem.name` |
| `type` | `type` | `MenuItem.type` |
| N/A | `coordinate` | `Coordinate` |
| N/A | `action_target` | Used for transitions |

**Important:** The PageAnalysis model uses:
- `items` (not `menu_items`) for the list of elements
- `name` (not `text`) for the element's display text

### Step 4: Add Expected Behavior (Optional)

For enhanced validation, define expected behavior:

```yaml
# tests/v6/fixtures/expected/login_expected.yaml
scenario: "Login Flow"
description: "User logs in and reaches home screen"

actions:
  - action: "click"
    node: "submit_btn"
    target: "submit"
    order: 0

page_transitions:
  - from: "login"
    to: "home"
    trigger: "submit"
    order: 0

visited_nodes:
  - "root"
  - "submit_btn"

final_state: "COMPLETED"
completion_mode: "normal"
```

### Step 5: Add Problem Detection (Optional)

Add problem detection to catch abnormal patterns:

```python
from src.simulation.problem_detector import ProblemDetector

# Run simulation
result = engine.run()
trace_nodes = storage.read(result.trace_id)

# Detect problems
detector = ProblemDetector()
problems = detector.detect(trace_nodes)

# Assert no critical problems
critical = [p for p in problems if p.severity == "critical"]
assert len(critical) == 0
```

## Common Migration Patterns

### Pattern 1: Simple Static Page

If your test only uses one static page:

```yaml
pages:
  main:
    page_name: "MainScreen"
    elements:
      - id: "button1"
        type: "button"
        text: "Button 1"
        coordinate: {x: 0.5, y: 0.5}
    is_complete: true

transitions: {}  # No transitions

initial_page: "main"
```

### Pattern 2: Linear Navigation

For simple A → B → C navigation:

```yaml
pages:
  page_a:
    page_name: "PageA"
    elements:
      - id: "next"
        type: "button"
        text: "Next"
        action_target: "page_b"
    is_complete: false

  page_b:
    page_name: "PageB"
    elements:
      - id: "next"
        type: "button"
        text: "Next"
        action_target: "page_c"
    is_complete: false

  page_c:
    page_name: "PageC"
    elements: []
    is_complete: true

transitions:
  a_to_b:
    trigger: "next"
    from_page: "page_a"
    to_page: "page_b"
    action: "click"
  b_to_c:
    trigger: "next"
    from_page: "page_b"
    to_page: "page_c"
    action: "click"

initial_page: "page_a"
```

### Pattern 3: Branching Navigation

For scenarios with multiple paths:

```yaml
pages:
  home:
    page_name: "HomeScreen"
    elements:
      - id: "settings_btn"
        type: "button"
        text: "Settings"
        action_target: "settings"
      - id: "profile_btn"
        type: "button"
        text: "Profile"
        action_target: "profile"
    is_complete: false

  settings:
    page_name: "SettingsScreen"
    elements:
      - id: "back"
        type: "back_button"
        text: "Back"
    is_complete: false

  profile:
    page_name: "ProfileScreen"
    elements:
      - id: "back"
        type: "back_button"
        text: "Back"
    is_complete: false

transitions:
  home_to_settings:
    trigger: "settings_btn"
    from_page: "home"
    to_page: "settings"
    action: "click"
  home_to_profile:
    trigger: "profile_btn"
    from_page: "home"
    to_page: "profile"
    action: "click"
  settings_back:
    trigger: "back"
    from_page: "settings"
    to_page: "home"
    action: "click"
  profile_back:
    trigger: "back"
    from_page: "profile"
    to_page: "home"
    action: "click"

initial_page: "home"
history_depth: 10
```

## Verification Checklist

After migration, verify:

- [ ] Test runs without errors
- [ ] Page transitions occur as expected
- [ ] Trace recording includes page transitions
- [ ] Problem detection runs without errors
- [ ] Expected behavior validation (if added) passes
- [ ] No critical problems in clean scenarios

## Troubleshooting

### Issue: "Page doesn't change after action"

**Cause:** Missing transition rule in fixture.

**Solution:** Add the transition:
```yaml
transitions:
  my_transition:
    trigger: "button_id"
    from_page: "current_page"
    to_page: "target_page"
    action: "click"
```

### Issue: "MenuItem not compatible with DynamicMatcher"

**Cause:** Incorrect field mapping.

**Solution:** Ensure StateFixture correctly maps:
- `text` → `MenuItem.name`
- `items` (not `menu_items`) in PageAnalysis

### Issue: "Tests pass but behavior is wrong"

**Cause:** Using old MockVisionService without state.

**Solution:** Add ExpectedBehavior validation:
```python
expected = ExpectedBehavior.from_yaml("expected.yaml")
validator = BehaviorValidator()
result = validator.validate(expected, actual_trace)
assert result.is_valid()
```

## Backward Compatibility

The old `MockVisionService` remains available for simple static scenarios. You don't have to migrate all tests at once - migrate incrementally based on your needs.

## Next Steps

1. Read [StateFixture YAML Reference](#) for detailed format documentation
2. Review [Example Fixtures](../tests/v6/fixtures/) for common patterns
3. Add [Expected Behavior Validation](#) for enhanced verification
4. Configure [Problem Detection](#) for automatic issue detection

---

**Last Updated:** 2026-06-07
**Version:** V6.9.2
