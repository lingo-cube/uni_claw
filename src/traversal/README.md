# Traversal Module

The traversal module provides the graph-based traversal engine for V6 declarative plans.

## GraphTraversalEngine

The `GraphTraversalEngine` executes `TraversalPlan` instances using state machine-driven control flow.

### V6.9 Dynamic Matching Features

#### Template Registry Loading

The engine now truly initializes the template registry:

```python
# In GraphTraversalEngine.__init__
self.template_registry = TemplateRegistry()  # Built-in templates
if self.plan.template_registry:
    self.template_registry.load_from_file(path)  # Custom templates
self.dynamic_matcher = DynamicMatcher(self.template_registry)
```

#### Dynamic Child Generation

When a container has `DYNAMIC_MATCH` children strategy, the engine:

1. Generates children from `PageAnalysis.items` on first access
2. Caches generated children for subsequent access
3. Invalidates cache when page navigation changes

```python
# In _get_next_unvisited_child()
if strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
    if node.node_id not in self._dynamic_children:
        self._generate_dynamic_children(node)
    # Return next unvisited child from cache
```

#### Field Mapping

MenuItem fields are mapped for the matcher:

| MenuItem Field | Matcher Field |
|---------------|---------------|
| `item.type` | `type` |
| `item.name` | `text` (matcher expects "text") |
| Item index | `index` |
| `coordinate.x` | `coordinate_x` |
| `coordinate.y` | `coordinate_y` |

#### FRAME_COMPLETE Interception

The engine intercepts `FRAME_COMPLETE` transitions when unvisited dynamic children remain:

```python
# In _step_once()
if transition.to_state == TraversalState.FRAME_COMPLETE:
    current = stack.peek()
    if current and current.children_strategy.type == DYNAMIC_MATCH:
        remaining_child = self._get_next_unvisited_child(current)
        if remaining_child:
            self._push_node(remaining_child)
            next_state = NODE_SELECT  # Override
```

#### Cache Invalidation

The engine detects page navigation changes and invalidates cached children:

```python
# In _step_once() end
path_now = list(self.context.current_path)
if path_now != self._last_known_path:
    current = stack.peek()
    if current:
        self.invalidate_children_cache(current.node_id)
    self._last_known_path = path_now
```

### Engine Fields

V6.9 adds the following fields to `GraphTraversalEngine`:

| Field | Type | Description |
|-------|------|-------------|
| `template_registry` | `TemplateRegistry` | Template registry instance |
| `dynamic_matcher` | `DynamicMatcher` | Dynamic matcher instance |
| `_dynamic_children` | `Dict[str, List[Node]]` | Cached children per node |
| `_last_known_path` | `List[str]` | Previous page path for change detection |

### Engine Lifecycle

```
1. __init__(): Initialize fields, load template registry
2. initialize(): Validate plan, execute entry policy, push root node
3. run(): Main loop - _step_once() until completion
4. _step_once(): State machine step + dynamic child handling
```

### Trace Integration

V6.9 integrates with the distributed tracing system:

- State transitions recorded as spans
- Dynamic matching skips recorded as spans
- Path changes detected and cached
