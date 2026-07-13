# Capability: Long List Baseline

长列表滚动基线测试，验证 20-30 项列表的完整遍历、跳跃恢复、自适应步长行为。

## ADDED Requirements

### Requirement: System shall provide 30-item long list baseline test

The system SHALL provide a baseline test that validates complete traversal of a 30-item scrollable list distributed across 8 segments.

#### Scenario: 30-item list complete traversal
- **WHEN** the long list baseline test runs with 30 items
- **THEN** the system SHALL visit all 30 items
- **AND** the system SHALL complete with `success: true` and `reason: all_visited`
- **AND** the system SHALL execute at least 7 scroll operations
- **AND** the finalProgress SHALL be 1.0 (end of list reached)

### Requirement: System shall handle sparse long lists with jump recovery

The system SHALL provide a baseline test that validates traversal of a 25-item sparse list with large gaps (> 40%) that trigger jump detection and recovery.

#### Scenario: Sparse list jump detection and recovery
- **WHEN** the long list baseline test runs with 25 items in sparse segments (40%+ gaps)
- **THEN** the system SHALL detect at least 2 jumps
- **AND** the system SHALL successfully recover from all detected jumps
- **AND** the system SHALL visit all 25 items
- **AND** the system SHALL complete with `success: true`

### Requirement: System shall handle dense long lists with adaptive step

The system SHALL provide a baseline test that validates traversal of a 20-item dense list with high overlap (> 80%) that triggers adaptive step growth.

#### Scenario: Dense list adaptive step growth
- **WHEN** the long list baseline test runs with 20 items in dense segments (80%+ overlap)
- **THEN** the system SHALL increase adaptive step size at least 3 times
- **AND** the system SHALL visit all 20 items
- **AND** the system SHALL complete with `success: true`
- **AND** the scroll count SHALL be optimized compared to uniform step size

### Requirement: System shall support element deduplication across scroll segments

The system SHALL ensure that overlapping items appearing in multiple scroll segments are only visited once during traversal.

#### Scenario: Overlapping items visited once
- **WHEN** the long list test scrolls through segments with overlapping items
- **THEN** each overlapping item SHALL appear only once in the visited elements list
- **AND** the ElementCoverage SHALL reflect unique items only

### Requirement: System shall validate ElementCoverage with manual element listing

The system SHALL support ElementCoverage validation where all scrollable list elements are manually listed in the ExpectedBehavior JSON.

#### Scenario: ElementCoverage validates all manually listed items
- **WHEN** the long list test validates ElementCoverage
- **THEN** all 20-30 items SHALL be listed in ElementCoverage.Required
- **AND** the system SHALL validate that all required items were visited
- **AND** the coverage ratio SHALL be 1.0 (100%)

### Requirement: System shall support long list boundary conditions

The system SHALL correctly handle top and bottom boundary conditions for long lists.

#### Scenario: Long list boundary conditions
- **WHEN** the long list test starts
- **THEN** the initial progress SHALL be 0.0
- **AND** the IsEndOfList SHALL be false
- **WHEN** the long list test completes full traversal
- **THEN** the final progress SHALL be 1.0
- **AND** the IsEndOfList SHALL be true
