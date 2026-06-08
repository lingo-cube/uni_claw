## ADDED Requirements

### Requirement: Dynamic child generation from page elements

The system SHALL generate child traversal nodes from the current page's element list using configured match rules and templates. Generation SHALL be deterministic — the same page analysis and rule set produce the same children.

#### Scenario: First-time generation creates children
- **WHEN** a CONTAINER node with DYNAMIC_MATCH strategy enters BRANCH state and has not yet generated children
- **THEN** the system calls vision service to obtain the current page analysis, matches elements against dynamic rules, and generates one child node per matched element

#### Scenario: Re-generation is prevented
- **WHEN** a node has already generated dynamic children once
- **THEN** the system SHALL NOT regenerate — it returns the cached children directly

### Requirement: Deduplication by page-element pair

The system SHALL track `(page_fingerprint, element_name)` pairs that have already been generated as container children. When generating dynamic children, the system MUST skip any element whose `(fingerprint, name)` pair has already been recorded.

#### Scenario: Same-page same-element is skipped
- **WHEN** generating dynamic children on a page and element "HomeNetwork" has already been generated as a container on this page
- **THEN** "HomeNetwork" SHALL NOT be generated again even if the generating container has a different node_id

#### Scenario: Different-page same-name is allowed
- **WHEN** generating dynamic children on page A (fingerprint = 100) for element "Settings" and the pair (100, "Settings") is not in the deduplication set
- **THEN** "Settings" SHALL be generated even if (200, "Settings") exists in the set from page B

### Requirement: Next unvisited child selection

The system SHALL return the next child from the generated children cache that has not been recorded in `context.visited_children[container_id]`. If all children have been visited, the system MUST return None.

#### Scenario: Returns unvisited child
- **WHEN** a container has 3 cached children and 1 has been visited
- **THEN** `get_next_unvisited_child` returns one of the 2 unvisited children

#### Scenario: All visited returns None
- **WHEN** all cached children have been visited
- **THEN** `get_next_unvisited_child` returns None

### Requirement: Cache invalidation

The system SHALL support invalidation of a container's cached children. After invalidation, the container's visited children set in the context MUST also be cleared.

#### Scenario: Cache invalidated on path change
- **WHEN** `invalidate_cache(container_id)` is called after a page transition
- **THEN** both the dynamic children cache entry and `context.visited_children[container_id]` are removed for that container
