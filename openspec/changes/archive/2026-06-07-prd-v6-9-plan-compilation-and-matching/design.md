## Context

After V6.8 engine initialization, the GraphTraversalEngine can successfully launch into target apps but cannot generate child nodes from page elements. When the state machine reaches BRANCH state at the root container, `_get_next_unvisited_child` returns `None` for `DYNAMIC_MATCH` strategy (which is currently unimplemented), immediately triggering `FRAME_COMPLETE`. This causes premature termination of traversal.

**Current State:**
- `TemplateRegistry` and `DynamicMatcher` are mocked/placeholder implementations
- `_get_next_unvisited_child` only handles `STATIC` children strategy
- No path concatenation for dynamic child nodes
- No plan compiler—manual `TraversalPlan` JSON is required

**Constraints:**
- Must maintain backward compatibility with existing V6.8 initialization
- Dynamic matching must integrate seamlessly with the state machine
- Path concatenation must work for both template-instantiated and static nodes
- Compiler must be deterministic (no AI dependency in compilation phase)

**Stakeholders:**
- GraphTraversalEngine consumers (simulation and real device traversal)
- Template system users (custom template authors)
- Plan authors (currently writing raw JSON)

## Goals / Non-Goals

**Goals:**
1. Enable dynamic child node generation from `PageAnalysis.items` using `DynamicMatcher.match_all()`
2. Truly initialize `TemplateRegistry` and `DynamicMatcher` with built-in templates
3. Automatically concatenate `precondition.path` for child nodes during instantiation
4. Detect page changes and invalidate cached dynamic children
5. Provide deterministic `PlanCompiler` mapping from `IntentSlots` to `TraversalPlan`
6. Intercept `FRAME_COMPLETE` to prevent premature exit when unvisited dynamic children remain
7. Provide heuristic-based `parse_task_to_slots()` as AI integration placeholder

**Non-Goals:**
- AI-powered natural language parsing (deferred to V6.10)
- Scrolling/long-list handling (deferred to V6.10+)
- Fuzzy matching for static paths (deferred to V6.10+)
- Safe mode actual filtering logic (deferred to V6.10+)

## Decisions

### 1. Dynamic Child Generation Strategy

**Decision:** Generate dynamic children once per container node, cache results, and invalidate on path change.

**Rationale:**
- Page analysis is expensive—avoid repeated matching for same container
- Path changes indicate page navigation—cache would be stale
- Simpler than incremental generation for V6.9

**Alternatives Considered:**
- **Generate on every BRANCH**: Rejected—too expensive, redundant page analysis
- **Incremental generation as items appear**: Rejected—complexity not justified for V6.9
- **Never cache, always regenerate**: Rejected—performance cost too high

### 2. Path Concatenation Approach

**Decision:** Pass `parent_path` parameter through `TemplateRegistry.instantiate()` → `TemplateInstantiator.instantiate()`.

**Rationale:**
- Maintains backward compatibility (parameter is optional)
- Centralizes path logic in instantiation layer
- Works for both template-based and static node creation

**Alternatives Considered:**
- **Post-processing after instantiation**: Rejected—more scattered logic
- **Path as separate field in Template**: Rejected—duplication, harder to maintain

### 3. Compiler Architecture

**Decision:** Deterministic mapping from `IntentSlots` to `TraversalPlan` with no AI dependency.

**Rationale:**
- Compilation is fast and deterministic
- Separates concerns: AI (parsing) from compilation (mapping)
- Easier to test and verify
- AI dependency only in `parse_task_to_slots()` placeholder

**Alternatives Considered:**
- **AI-powered compiler**: Rejected—overkill, non-deterministic, harder to test
- **No compiler, keep manual JSON**: Rejected—too tedious for users

### 4. FRAME_COMPLETE Interception

**Decision:** Intercept in `_step_once()` after state transition, check for remaining unvisited dynamic children, and push next child if available.

**Rationale:**
- Minimally invasive to state machine logic
- Only affects containers with `DYNAMIC_MATCH` strategy
- Clear semantic: "don't exit yet if more children to visit"

**Alternatives Considered:**
- **Modify FSM transitions**: Rejected—more invasive, harder to verify
- **Add new state**: Rejected—unnecessary complexity for this use case

### 5. Page Change Detection

**Decision:** Track `current_path` in context, detect changes at end of `_step_once()`, invalidate cache of current container.

**Rationale:**
- Simple and reliable indicator of page navigation
- Low overhead—single list comparison per step
- Cache invalidation is safe (regeneration handles stale data)

**Alternatives Considered:**
- **Activity/package name tracking**: Rejected—less granular, may miss in-app navigation
- **Page hash/signature**: Rejected—overkill for V6.9, adds complexity

### 6. Element Handling → Template Mapping

**Decision:** Four predefined template sets corresponding to `element_handling` values.

**Rationale:**
- Covers common traversal patterns
- Simple mapping without runtime template construction
- Extensible in future versions

**Template Sets:**
- `full_interaction`: All templates (menu_container, switch_leaf, slider_leaf, leaf_action)
- `menu_only`: Recursive menu traversal only (menu_container)
- `safe_mode`: Full interaction + safety metadata
- `read_only`: Passive observation (leaf_info)

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| Cache invalidation timing issues | Stale children used after navigation | Detect path changes, invalidate liberally |
| Template loading failures | Engine cannot initialize | Provide built-in templates as fallback |
| Compiler validation gaps | Invalid plans generated | Comprehensive `_validate_slots()` with clear errors |
| MenuItem → dict field mapping errors | Matching fails silently | Add test coverage for all field transformations |
| FRAME_COMPLETE interception conflicts | Premature exit still occurs | Add comprehensive simulation tests |

**Trade-offs:**
- **Performance vs. Correctness**: Caching improves performance but adds invalidation complexity
- **Simplicity vs. Flexibility**: Predefined template sets are simple but limit customization
- **Heuristic vs. AI parsing**: Heuristics are fragile but V6.9 cannot wait for full AI integration

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Compilation Phase (NEW)                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  NL Task                                                                     │
│    │                                                                         │
│    ▼                                                                         │
│  src/ai/task_parser.py  ── AI/Heuristics ──→ IntentSlots                     │
│    │                                                                         │
│    ▼                                                                         │
│  src/graph/compiler.py  ── PlanCompiler.compile() ──→ TraversalPlan          │
│    ├── scope → completion_policy                                             │
│    ├── element_handling → dynamic_rules                                      │
│    ├── navigation → exit_condition.fallback                                 │
│    └── _validate_slots()                                                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Execution Phase (MODIFIED)                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  GraphTraversalEngine.run()                                                  │
│    │                                                                         │
│    ├── initialize()    [V6.8]                                                │
│    │   └── _load_template_registry()  ← Now truly loads                     │
│    │                                                                         │
│    └── _step_once()    [V6.9 modifications]                                  │
│        ├── state_machine.step()                                              │
│        ├── BRANCH → _get_next_unvisited_child()                              │
│        │   ├── STATIC: Existing behavior                                     │
│        │   └── DYNAMIC_MATCH: [NEW]                                          │
│        │       ├── First time → _generate_dynamic_children()                │
│        │       │   ├── load_rules()                                          │
│        │       │   ├── match_all(page_analysis.items)                       │
│        │       │   ├── instantiate_match() → path concatenation             │
│        │       │   └── cache in _dynamic_children                            │
│        │       └── Return next unvisited child                               │
│        │                                                                     │
│        ├── FRAME_COMPLETE → [NEW] Intercept premature exit                 │
│        │   └── If unvisited dynamic children remain → push next             │
│        │                                                                     │
│        └── Path change detection → [NEW] invalidate cache                    │
│            └── if current_path != last_known_path → invalidate()            │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## File Modifications

### New Files

1. **`src/graph/compiler.py`**
   - `CompilerError` exception
   - `PlanCompiler` class with `compile()` method
   - Mapping rules for all `IntentSlots` fields
   - `_validate_slots()` validation

2. **`src/ai/task_parser.py`**
   - `parse_task_to_slots()` function
   - Heuristic rules for common Chinese/English inputs
   - Returns `IntentSlots` object

### Modified Files

1. **`src/traversal/graph_engine.py`**
   - Add fields: `template_registry`, `dynamic_matcher`, `_dynamic_children`, `_last_known_path`
   - Implement `_load_template_registry()` fully
   - Extend `_get_next_unvisited_child()` for `DYNAMIC_MATCH`
   - Add `_generate_dynamic_children()` method
   - Add `invalidate_children_cache()` method
   - Modify `_step_once()` for FRAME_COMPLETE interception
   - Modify `_step_once()` for path change detection

2. **`src/graph/template.py`**
   - `TemplateInstantiator.instantiate()` add `parent_path` parameter
   - `TemplateRegistry.instantiate()` add `parent_path` parameter
   - Path concatenation logic: `precondition.path = parent_path + [name]`

## Open Questions

1. **Should cache invalidation be granular (per node) or global?**
   - Proposal: Per-container invalidation via `invalidate_children_cache(node_id)`
   - Rationale: More precise, less regeneration

2. **Should `parse_task_to_slots` fail or use defaults when parsing fails?**
   - Proposal: Return `IntentSlots` with minimal required fields, raise warning
   - Rationale: Graceful degradation for V6.9, full AI integration in V6.10

3. **Should `PlanCompiler` be extensible for custom mappings?**
   - Proposal: No—keep simple for V6.9, extensibility can be added later
   - Rationale: YAGNI principle, avoid premature complexity
