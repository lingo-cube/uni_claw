# Spec: Dynamic Matching Tests

## ADDED Requirements

### Requirement: D series tests SHALL verify dynamic child generation
The system SHALL provide tests that verify `_generate_dynamic_children()` creates correct number of children from PageAnalysis.items.

#### Scenario: First-time generation creates correct count
- **WHEN** D1 test generates dynamic children from page with 3 menu_items
- **THEN** `_dynamic_children[root]` length equals 3

#### Scenario: MenuItem to dict field mapping
- **WHEN** D2 test calls matcher.match_all() with items
- **THEN** matcher correctly consumes `text`, `type`, `index`, `coordinate_x/y` fields

#### Scenario: Get next child without duplicates
- **WHEN** D3 test calls `_get_next_unvisited_child()` multiple times
- **THEN** each call returns different child_id until exhausted

#### Scenario: All visited returns None
- **WHEN** D4 test calls `_get_next_unvisited_child()` after all children visited
- **THEN** system returns None

#### Scenario: Page analysis None is handled
- **WHEN** D9 test generates children with page_analysis=None
- **THEN** system SHALL NOT crash and returns empty list

### Requirement: D series tests SHALL verify cache mechanism
The system SHALL provide tests that verify dynamic children caching, invalidation, and regeneration.

#### Scenario: Cache invalidation works
- **WHEN** D6 test calls `invalidate_children_cache(node_id)` after generation
- **THEN** cache is cleared and next BRANCH regenerates children

#### Scenario: Path concatenation is correct
- **WHEN** D7 test instantiates child with parent_path=["Settings"]
- **THEN** child.precondition.path equals ["Settings", "Child"]

### Requirement: D series tests SHALL verify FRAME_COMPLETE interception
The system SHALL provide tests that verify FRAME_COMPLETE state interception when unvisited children remain.

#### Scenario: FRAME_COMPLETE interception with remaining children
- **WHEN** D5 test has unvisited dynamic children when FRAME_COMPLETE state reached
- **THEN** system pushes child onto stack and continues NODE_SELECT

### Requirement: D series tests SHALL verify skip element recording
The system SHALL provide tests that verify `_record_skip_span()` is called for unmatched elements.

#### Scenario: Skip element records Span
- **WHEN** D8 test has element that matches no dynamic rule
- **THEN** `_record_skip_span()` is called with match_result

### Requirement: D series tests SHALL verify DynamicRule conversion
The system SHALL provide tests that verify DynamicRule objects convert to dict format for matcher.load_rules().

#### Scenario: DynamicRule to dict conversion
- **WHEN** D10 test loads rules with DynamicRule objects
- **THEN** matcher.load_rules() correctly consumes match_condition, child_template, action

### Requirement: D series extended tests SHALL provide chaos and boundary testing
The system SHALL provide tests with randomization, boundary conditions, and fault injection.

#### Scenario: Random element order matching
- **WHEN** D11 test randomizes page element order before matching
- **THEN** matcher still finds correct matches regardless of order

#### Scenario: Empty elements boundary
- **WHEN** D12 test generates children from page with 0 elements
- **THEN** system returns empty list without crashing

#### Scenario: Massive elements boundary
- **WHEN** D12 test generates children from page with 1000 elements
- **THEN** system completes within acceptable time

#### Scenario: Vision failure tolerance
- **WHEN** D13 test injects vision failure during child generation
- **THEN** system handles gracefully without crashing
