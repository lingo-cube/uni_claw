## ADDED Requirements

### Requirement: Page change detection and cache invalidation
The GraphTraversalEngine SHALL detect page navigation changes and invalidate cached dynamic children.

#### Scenario: Path change detection
- **WHEN** `_step_once()` completes a state transition
- **THEN** system compares `context.current_path` with `self._last_known_path`
- **WHEN** paths differ
- **THEN** system detects page change
- **THEN** system updates `self._last_known_path = current_path`

#### Scenario: Cache invalidation on path change
- **WHEN** a path change is detected
- **WHEN** there is a current container node on stack
- **THEN** system calls `invalidate_children_cache(current.node_id)`
- **THEN** cached children for that node are removed
- **THEN** next BRANCH for this node triggers regeneration

#### Scenario: Explicit cache invalidation method
- **WHEN** `invalidate_children_cache(node_id)` is called
- **THEN** system removes entry from `_dynamic_children` dict
- **THEN** subsequent BRANCH for this node regenerates children

#### Scenario: Initial path tracking
- **WHEN** GraphTraversalEngine is initialized
- **THEN** `self._last_known_path` is initialized to empty list
- **THEN** first step compares empty list with actual path
- **THEN** no invalidation occurs (no prior cache)

#### Scenario: No invalidation on same page
- **WHEN** engine remains on same page across multiple steps
- **THEN** current_path equals last_known_path
- **THEN** no cache invalidation occurs
- **THEN** cached children are reused

#### Scenario: Cache invalidation after auto_escape
- **WHEN** engine performs AUTO_ESCAPE fallback navigation
- **WHEN** this changes the current_path
- **THEN** next `_step_once()` detects path change
- **THEN** previous container's cached children are invalidated
- **THEN** new page children are generated on next BRANCH

#### Scenario: Missing node_id handling
- **WHEN** `invalidate_children_cache()` is called with non-existent node_id
- **THEN** system uses `.pop(node_id, None)` to avoid KeyError
- **THEN** no error is raised
