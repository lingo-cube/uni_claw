## ADDED Requirements

### Requirement: Dynamic child generation from page analysis
The GraphTraversalEngine SHALL generate child nodes from `PageAnalysis.items` using `DynamicMatcher.match_all()` when a container node has `DYNAMIC_MATCH` children strategy.

#### Scenario: First-time dynamic child generation
- **WHEN** engine enters BRANCH state for a container with DYNAMIC_MATCH strategy
- **WHEN** no cached children exist for this node
- **THEN** engine calls `_generate_dynamic_children()`
- **THEN** `_generate_dynamic_children()` loads dynamic rules from node's children strategy
- **THEN** `_generate_dynamic_children()` calls `DynamicMatcher.match_all()` with page analysis items
- **THEN** each matched item instantiates a child node via `DynamicMatcher.instantiate_match()`
- **THEN** generated children are cached in `_dynamic_children[node_id]`

#### Scenario: MenuItem to dict field mapping
- **WHEN** converting PageAnalysis.items to matcher format
- **THEN** system maps `item.type` to `"type"` field
- **THEN** system maps `item.name` to `"text"` field (matcher expects "text", not "name")
- **THEN** system maps item index to `"index"` field
- **THEN** system maps `item.coordinate.x` to `"coordinate_x"` field
- **THEN** system maps `item.coordinate.y` to `"coordinate_y"` field

#### Scenario: Dynamic child node registration
- **WHEN** a child node is instantiated from a match result
- **THEN** child is registered in `_node_registry[child.node_id]`
- **THEN** child is added to `_dynamic_children[parent.node_id]` list

#### Scenario: Unvisited child retrieval
- **WHEN** engine calls `_get_next_unvisited_child()` for DYNAMIC_MATCH node
- **WHEN** cached children exist
- **THEN** system returns first child with `node_id` not in `visited_children`
- **THEN** system marks returned child as visited
- **THEN** subsequent calls return next unvisited child

#### Scenario: All children visited returns None
- **WHEN** all dynamic children are in `visited_children` set
- **THEN** `_get_next_unvisited_child()` returns `None`
- **THEN** this signals FRAME_COMPLETE for the container

#### Scenario: DynamicRule to dict conversion
- **WHEN** loading rules for DynamicMatcher
- **THEN** system converts each `DynamicRule` object to dict with keys: match_condition, child_template, action
- **THEN** system passes dict format to `DynamicMatcher.load_rules()`

#### Scenario: Skip span recording
- **WHEN** a match result has `matched=False` or action is not GENERATE_CHILD
- **THEN** engine calls `_record_skip_span(result)` for debugging
- **THEN** no child node is created for this result
