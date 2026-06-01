# Test Assets

This directory contains reusable test assets including fixtures and utility functions for testing the Uni-Claw Android UI traversal system.

## Directory Structure

```
tests/assets/
├── fixtures/           # Test data fixtures (JSON)
│   ├── page_analysis.json
│   ├── graph_nodes.json
│   ├── state_machines.json
│   ├── trace_data.json
│   └── ai_data.json
└── utils/              # Test utility modules
    ├── model_helpers.py
    └── assertions.py
```

## Fixtures

### `page_analysis.json`

Sample page analysis data for testing page content models.

**Format:**
```json
{
  "level1_dir": "left|right|top|bottom",
  "level1_menus": [...],
  "level2_dir": "left|right|top|bottom",
  "level2_menus": [...],
  "current_path": [...],
  "items": [...],
  "is_popup": false,
  "popup_info": null
}
```

### `graph_nodes.json`

Sample graph node configurations for testing node models.

**Format:**
```json
{
  "nodes": [
    {
      "node_id": "...",
      "name": "...",
      "node_type": "container|leaf_action|...",
      "operation": {...},
      "precondition": {...},
      "children_strategy": {...},
      "error_policy": {...}
    }
  ]
}
```

### `state_machines.json`

Sample state machine configurations and transitions.

**Format:**
```json
{
  "global_states": [...],
  "traversal_states": [...],
  "transitions": [...],
  "stack_frames": [...]
}
```

### `trace_data.json`

Sample trace data for testing trace recording and serialization.

**Format:**
```json
{
  "session_info": {...},
  "steps": [...],
  "snapshots": [...],
  "summary": {...}
}
```

### `ai_data.json`

Sample AI capability data including inferences, plans, and safety evaluations.

**Format:**
```json
{
  "inferences": [...],
  "plans": [...],
  "operations": [...],
  "strategies": [...],
  "safety_evaluations": [...]
}
```

## Utilities

### `model_helpers.py`

Helper functions for creating test model instances.

**Available Functions:**

- `create_test_coordinate(x, y)`: Create a Coordinate instance
- `create_test_menu_info(name, x, y)`: Create a MenuInfo instance
- `create_test_menu_item(name, type, x, y)`: Create a MenuItem instance
- `create_test_page_analysis(...)`: Create a PageAnalysis instance
- `create_test_traversal_node(...)`: Create a TraversalNode instance
- `create_test_operation(action)`: Create an Operation instance

### `assertions.py`

Custom assertion helpers for model validation.

**Available Assertions:**

- `assert_valid_coordinate(coord)`: Assert coordinate has valid x, y values (0-1 range)
- `assert_enum_values(enum_class, expected_values)`: Assert enum has expected values
- `assert_enum_helper_methods(enum_class)`: Assert enum has values(), from_value(), is_valid() methods
- `assert_model_field_validation(model, field, value, should_pass)`: Test field validation

## Usage Examples

### Using Fixtures in Tests

```python
import json
from tests.assets.fixtures.page_analysis import PAGE_ANALYSIS_FIXTURE

def test_page_analysis():
    # Load fixture data
    with open('tests/assets/fixtures/page_analysis.json') as f:
        fixture_data = json.load(f)
    
    # Use in test
    analysis = PageAnalysis(**fixture_data)
    assert analysis.level1_dir == Direction.LEFT
```

### Using Model Helpers

```python
from tests.assets.utils.model_helpers import (
    create_test_coordinate,
    create_test_menu_info,
    create_test_page_analysis
)

def test_page_analysis_creation():
    analysis = create_test_page_analysis(
        level1_dir=Direction.LEFT,
        current_path=["Settings"]
    )
    assert analysis.level1_dir == Direction.LEFT
```

### Using Custom Assertions

```python
from tests.assets.utils.assertions import (
    assert_valid_coordinate,
    assert_enum_helper_methods
)

def test_coordinate_validation():
    coord = Coordinate(x=0.5, y=0.5)
    assert_valid_coordinate(coord)

def test_enum_methods():
    assert_enum_helper_methods(MenuItemType)
```

## Adding New Fixtures

When adding new fixture files:

1. Create the JSON file in `tests/assets/fixtures/`
2. Follow the naming convention `<domain>_data.json`
3. Include a format comment at the top describing the structure
4. Add example data that covers common test scenarios
5. Update this README with the fixture description

## Adding New Utilities

When adding new utility functions:

1. Add the function to the appropriate module in `tests/assets/utils/`
2. Include a docstring explaining the function's purpose and parameters
3. Add type hints for parameters and return values
4. Include usage examples in the docstring
5. Update this README with the utility description

## Test Asset Guidelines

- **Reusability**: Assets should be reusable across multiple test files
- **Simplicity**: Keep fixtures simple and focused on common scenarios
- **Documentation**: Document all fixtures and utilities clearly
- **Maintenance**: Keep assets in sync with model changes
- **Validation**: Ensure fixture data is valid according to model schemas

## Notes

- Fixture data should represent realistic but simplified test scenarios
- Utilities should avoid complex logic to keep tests maintainable
- When models change, update affected fixtures and utilities
