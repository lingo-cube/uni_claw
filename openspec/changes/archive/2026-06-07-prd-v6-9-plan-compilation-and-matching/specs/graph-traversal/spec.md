## MODIFIED Requirements

### Requirement: Graph traversal engine supports dynamic children
The GraphTraversalEngine SHALL support both STATIC and DYNAMIC_MATCH children strategies for child node generation.

#### Scenario: DYNAMIC_MATCH strategy child generation
- **WHEN** `_get_next_unvisited_child()` is called for node with DYNAMIC_MATCH strategy
- **WHEN** no cached children exist for this node
- **THEN** system calls `_generate_dynamic_children()` to create children
- **THEN** system returns first unvisited child node ID
- **THEN** system marks child as visited in context

#### Scenario: DYNAMIC_MATCH strategy with cached children
- **WHEN** `_get_next_unvisited_child()` is called for node with DYNAMIC_MATCH strategy
- **WHEN** cached children already exist in `_dynamic_children[node_id]`
- **THEN** system does NOT regenerate children
- **THEN** system returns first unvisited child from cache
- **THEN** system marks child as visited

#### Scenario: STATIC strategy unchanged
- **WHEN** `_get_next_unvisited_child()` is called for node with STATIC strategy
- **THEN** system iterates through `static_children` list
- **THEN** system returns first child_id not in visited set
- **THEN** behavior is unchanged from V6.8

#### Scenario: NONE strategy unchanged
- **WHEN** `_get_next_unvisited_child()` is called for node with NONE strategy
- **THEN** system returns None immediately
- **THEN** behavior is unchanged from V6.8

### Requirement: FRAME_COMPLETE handling with interception
The engine SHALL intercept FRAME_COMPLETE transitions when unvisited dynamic children remain.

#### Scenario: FRAME_COMPLETE interception
- **WHEN** state machine transitions to FRAME_COMPLETE
- **WHEN** current node has DYNAMIC_MATCH strategy
- **WHEN** unvisited dynamic children remain
- **THEN** engine pushes next unvisited child onto stack
- **THEN** engine overrides next_state to NODE_SELECT
- **THEN** container remains on stack

#### Scenario: FRAME_COMPLETE proceeds normally
- **WHEN** state machine transitions to FRAME_COMPLETE
- **WHEN** current node has no unvisited dynamic children
- **THEN** FRAME_COMPLETE proceeds without interception
- **THEN** container is popped from stack

### Requirement: Path change detection during traversal
The engine SHALL detect page navigation changes and invalidate cached children.

#### Scenario: Path change detection
- **WHEN** `_step_once()` completes
- **THEN** system compares `current_path` with `last_known_path`
- **WHEN** paths differ
- **THEN** system invalidates cache for current container
- **THEN** system updates `last_known_path`

#### Scenario: No path change
- **WHEN** `_step_once()` completes
- **WHEN** current_path equals last_known_path
- **THEN** no cache invalidation occurs
