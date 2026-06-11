## ADDED Requirements

### Requirement: Test ID generator shall provide unified ID creation
The test framework SHALL provide a `TestIdGenerator` class at `tests/config/test_ids.py` that generates consistent, semantically meaningful test IDs.

#### Scenario: TestIdGenerator module exists
- **WHEN** test code imports from `tests.config.test_ids`
- **THEN** the module SHALL be importable without errors
- **AND** the module SHALL provide a `TestIdGenerator` class

### Requirement: Node ID generation shall support names and indices
The `TestIdGenerator.node_id()` method SHALL generate node IDs from a name and optional index.

#### Scenario: Generate node ID without index
- **WHEN** test code calls `TestIdGenerator.node_id("TestNode")`
- **THEN** the result SHALL be `"testnode"`

#### Scenario: Generate node ID with index
- **WHEN** test code calls `TestIdGenerator.node_id("Child", 1)`
- **THEN** the result SHALL be `"child_1"`

#### Scenario: Node ID format is consistent
- **WHEN** multiple tests use the same name and index
- **THEN** all generated IDs SHALL have the same format

### Requirement: Span ID generation shall support prefixes and sequences
The `TestIdGenerator.span_id()` method SHALL generate span IDs from a prefix and sequence number.

#### Scenario: Generate span ID
- **WHEN** test code calls `TestIdGenerator.span_id("op", 1)`
- **THEN** the result SHALL be `"op_1"`

### Requirement: Trace ID generation shall produce unique IDs
The `TestIdGenerator.trace_id()` method SHALL generate unique trace IDs.

#### Scenario: Generate trace ID
- **WHEN** test code calls `TestIdGenerator.trace_id()`
- **THEN** the result SHALL be a string starting with `"trace_"`
- **AND** multiple calls SHALL produce different values

### Requirement: Element ID generation shall support type and text
The `TestIdGenerator.element_id()` method SHALL generate element IDs from type name and text.

#### Scenario: Generate element ID
- **WHEN** test code calls `TestIdGenerator.element_id("button", "Submit")`
- **THEN** the result SHALL be `"button_submit"`

### Requirement: ID reference points shall be completely replaced
When replacing hardcoded test IDs, ALL reference points SHALL be updated to prevent test failures.

#### Scenario: Scan all ID references before replacement
- **WHEN** replacing a hardcoded ID like `"node123"`
- **THEN** the developer SHALL run `grep -r "node123" tests/` to find all references
- **AND** SHALL update assignments, assertions, logging, and dictionary references

#### Scenario: Use consistent variable after replacement
- **WHEN** an ID is assigned to a variable
- **THEN** all subsequent references SHALL use that variable
- **AND** string concatenation SHALL be replaced with f-strings
