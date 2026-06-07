# Graph Module

The graph module provides the core data structures and compilation infrastructure for V6 declarative traversal.

## Components

### Plan Compiler (V6.9)

The `PlanCompiler` class maps `IntentSlots` (extracted from natural language) to `TraversalPlan` using deterministic rules.

```python
from src.graph.compiler import PlanCompiler
from src.graph.node import IntentSlots

# Create slots from natural language (AI or heuristic)
slots = IntentSlots(
    target_app="settings",
    scope="target_only",
    target="WiFi",
    element_handling="full_interaction",
)

# Compile to traversal plan
compiler = PlanCompiler()
plan = compiler.compile(slots)
```

#### Scope Mapping

| `slots.scope` | `completion_policy.type` | Description |
|---------------|---------------------------|-------------|
| `"full"` | `NONE` | Traverse until complete |
| `"partial"` | `MAX_STEPS` | Limit to 50 steps |
| `"target_only"` | `TARGET_FOUND` | Stop when target found |
| `"target_path"` | `NONE` | Follow static path |

#### Element Handling

| `slots.element_handling` | Templates | Description |
|--------------------------|-----------|-------------|
| `"full_interaction"` | All 4 templates | Complete traversal |
| `"menu_only"` | `menu_container` | Recursive menus only |
| `"safe_mode"` | All 4 + meta flag | With safety annotations |
| `"read_only"` | `leaf_info` | Passive observation |

### Node Models

Core data classes for traversal plans:

- **`TraversalNode`**: Unified node abstraction
- **`IntentSlots`**: AI-extracted intent structure
- **`TraversalPlan`**: Top-level plan container
- **`CompletionPolicy`**: Global termination conditions
- **`ChildrenStrategy`**: Child generation (STATIC/DYNAMIC_MATCH/NONE)

### Template System

The template registry provides reusable node patterns:

```python
from src.graph.template import TemplateRegistry

# Create registry with built-in templates
registry = TemplateRegistry()

# Instantiate with path concatenation (V6.9)
node = registry.instantiate(
    "menu_container",
    {"item_text": "Settings", "name": "Settings"},
    parent_path=["Home"]  # V6.9: Path will be ["Home", "Settings"]
)
```

#### Built-in Templates

- `menu_container`: Recursive menu entry
- `switch_leaf`: Toggle control
- `slider_leaf`: Slider control
- `leaf_action`: One-time button

### Dynamic Matcher

The `DynamicMatcher` matches UI elements to templates:

```python
from src.graph.matcher import DynamicMatcher

matcher = DynamicMatcher(template_registry)
matcher.load_rules({
    "menu_rule": {
        "match_condition": {"type": "menu_item"},
        "child_template": "menu_container",
        "action": "generate_child"
    }
})

results = matcher.match_all(menu_items, parent_node)
```

## V6.9 Features

### Path Concatenation

Template instantiation now supports automatic path concatenation:

- With `parent_path`: `precondition.path = parent_path + [node.name]`
- Without `parent_path`: `precondition.path = [node.name]`

### Deterministic Compilation

No AI dependency in compilation - all mappings are rule-based for:
- Predictable behavior
- Fast execution
- Easy testing
- Clear semantics
