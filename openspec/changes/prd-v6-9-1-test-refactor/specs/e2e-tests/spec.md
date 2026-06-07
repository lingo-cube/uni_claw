# Spec: E2E Tests

## ADDED Requirements

### Requirement: E2E series tests SHALL verify full menu traversal
The system SHALL provide tests that verify complete menu traversal covers all nodes and builds correct visited_tree.

#### Scenario: Full menu traversal completes
- **WHEN** E2E1 test runs plan with scope="full" on 7-page settings app
- **THEN** result.visited_tree contains all pages and completion_reason is satisfied

### Requirement: E2E series tests SHALL verify target search
The system SHALL provide tests that verify TARGET_FOUND completion triggers correctly.

#### Scenario: Target search finds target
- **WHEN** E2E2 test runs plan with scope="target_only" and target="Brightness"
- **THEN** completion_reason equals "target_found" and target_name matches

### Requirement: E2E series tests SHALL verify static path traversal
The system SHALL provide tests that verify static path follows predefined path segments.

#### Scenario: Static path reaches destination
- **WHEN** E2E3 test runs plan with scope="target_path" and target="Settings/Display/Brightness"
- **THEN** traversal follows exact path and reaches final destination

### Requirement: E2E series tests SHALL verify nested popup handling
The system SHALL provide tests that verify multi-level popup chains are handled correctly.

#### Scenario: Nested popups all closed
- **WHEN** E2E4 test encounters 3-level popup chain during traversal
- **THEN** system detects and closes all popups in sequence then resumes traversal

### Requirement: E2E series tests SHALL verify dynamic matching and smart correction coordination
The system SHALL provide tests that verify dynamic matching and precondition correction work together.

#### Scenario: Dynamic matching with correction
- **WHEN** E2E5 test has DYNAMIC_MATCH node with precondition requiring correction
- **THEN** system performs correction then generates dynamic children from corrected page

### Requirement: E2E series tests SHALL verify depth limit and back strategy
The system SHALL provide tests that verify depth traversal respects limits and back strategy works.

#### Scenario: Deep traversal respects limit
- **WHEN** E2E6 test runs plan with depth=5 on 10-level deep menu
- **THEN** traversal stops at depth 5 and returns back to root

#### Scenario: Back strategy activates at depth limit
- **WHEN** E2E6 test reaches max_depth with unvisited siblings
- **THEN** back strategy navigates to parent and continues with next sibling

### Requirement: E2E series tests SHALL verify error recovery and retry
The system SHALL provide tests that verify error_policy and retry mechanism work end-to-end.

#### Scenario: Error recovery with retry
- **WHEN** E2E7 test encounters action failure with error_policy="retry" and max_retries=3
- **THEN** system retries up to 3 times before moving to next node
