## Why

After V6.8 engine initialization, the engine can launch into target apps but cannot generate child nodes from page elements. When BRANCH executes at root container, `_get_next_unvisited_child` returns `None` for `DYNAMIC_MATCH`, immediately triggering `FRAME_COMPLETE`—the root node is popped and traversal ends. This is "can launch but cannot walk."

Dynamic matching and template registry are the core bridges connecting visual results to traversal tasks. Additionally, manually writing `TraversalPlan` JSON is too tedious—we need a compiler to map AI-extracted intent slots to executable plans.

## What Changes

- **Dynamic matching integration**: Engine BRANCH calls `DynamicMatcher.match_all()` to generate child nodes from `PageAnalysis.items`
- **Template registry actual loading**: `_load_template_registry()` truly initializes `TemplateRegistry` + `DynamicMatcher`
- **Child node path concatenation**: Auto-generate `precondition.path = parent_path + [name]` during instantiation
- **Page change awareness**: Detect `current_path` changes → invalidate child node cache
- **Plan compiler**: `PlanCompiler` maps `IntentSlots` deterministically to `TraversalPlan`
- **FRAME_COMPLETE interception**: Prevent premature container exit when unvisited dynamic children remain
- **Task parser skeleton**: `parse_task_to_slots()` with heuristic rules (AI integration deferred to V6.10)

## Capabilities

### New Capabilities

- `dynamic-matching`: Dynamic child node generation from page analysis items using `DynamicMatcher.match_all()`
- `template-registry-loading`: True initialization of `TemplateRegistry` and `DynamicMatcher` in engine
- `path-concatenation`: Automatic path concatenation for child node preconditions
- `cache-invalidation`: Page change detection and child cache invalidation
- `plan-compilation`: Deterministic mapping from `IntentSlots` to `TraversalPlan`
- `frame-complete-interception`: Prevent premature traversal termination
- `task-parsing`: Natural language to intent slots with heuristic rules

### Modified Capabilities

- `graph-traversal`: Extend `_get_next_unvisited_child` to support `DYNAMIC_MATCH` strategy
- `template-instantiation`: Add `parent_path` parameter for path concatenation

## Impact

- **Affected Code**:
  - `src/traversal/graph_engine.py` - Core traversal engine modifications
  - `src/graph/template.py` - Template instantiation with path concatenation
  - `src/graph/compiler.py` - **NEW** PlanCompiler class
  - `src/ai/task_parser.py` - **NEW** Task parsing with heuristics
- **API Changes**:
  - `TemplateInstantiator.instantiate()` - Add `parent_path` parameter
  - `TemplateRegistry.instantiate()` - Add `parent_path` parameter
- **Dependencies**: No new external dependencies
- **Systems**:
  - Traversal engine now supports dynamic child generation
  - Plan compilation phase added before execution
  - Cache management for dynamic children
