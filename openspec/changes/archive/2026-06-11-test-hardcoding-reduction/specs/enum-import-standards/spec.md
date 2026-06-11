## ADDED Requirements

### Requirement: Enum values shall be imported from source code
Test code SHALL import enum types from source code modules rather than using hardcoded string values.

#### Scenario: Import traversal state enum
- **WHEN** test code needs to use `TraversalState`
- **THEN** the test SHALL import from `src.state_machine.traversal_fsm`
- **AND** the test SHALL NOT use hardcoded strings like `"NODE_SELECT"`

#### Scenario: Import global state enum
- **WHEN** test code needs to use `GlobalState`
- **THEN** the test SHALL import from `src.state_machine.global_fsm`

#### Scenario: Import fallback action enum
- **WHEN** test code needs to use `FallbackAction`
- **THEN** the test SHALL import from `src.state_machine.container_handler`

### Requirement: Enum imports shall replace hardcoded strings
Tests SHALL replace hardcoded enum strings with imported enum types.

#### Scenario: Hardcoded string is replaced
- **WHEN** a test currently uses `assert state == "NODE_SELECT"`
- **THEN** the test SHALL be updated to `assert state == TraversalState.NODE_SELECT`
- **AND** the test SHALL import `TraversalState` from source code

### Requirement: String fields may use constants when enums unavailable
When a field is a string type (not enum), tests MAY use string values but SHOULD import them as constants from the defining module if available.

#### Scenario: String field uses value
- **WHEN** a completion_reason field is a string
- **THEN** the test MAY use `assert result.completion_reason == "ALL_VISITED"`
- **AND** the test SHOULD NOT define these strings locally

### Requirement: Source code changes require test synchronization
When source code enum values are modified, ALL test references SHALL be scanned and updated.

#### Scenario: Enum value is renamed in source
- **WHEN** a source code enum value is renamed (e.g., `NODE_SELECT` → `NODE_SELECTION`)
- **THEN** the developer SHALL run `grep -r "NODE_SELECT" tests/` to find all references
- **AND** SHALL update all test files that reference the old value
- **AND** SHALL verify no references remain after update

#### Scenario: Scan before source code modification
- **WHEN** planning to modify an enum value in source code
- **THEN** the developer SHALL first scan tests for current references
- **AND** SHALL record the list of affected test files
- **AND** SHALL update those tests as part of the same change

### Requirement: Enum import paths shall be stable
Source code enum locations SHALL be considered stable interfaces. Tests MAY import from these locations.

#### Scenario: Expected import paths
- **WHEN** test code imports `TraversalState`
- **THEN** the import SHALL be `from src.state_machine.traversal_fsm import TraversalState`
- **AND** this path SHALL be treated as a stable interface

### Requirement: Test assertions shall use enum values for type safety
When comparing values that are enum types, tests SHALL use the enum value for type safety and IDE support.

#### Scenario: Enum comparison in test
- **WHEN** a test asserts `next_state == TraversalState.NODE_SELECT`
- **THEN** the comparison SHALL benefit from type checking
- **AND** misspellings SHALL be caught by IDE or type checker
