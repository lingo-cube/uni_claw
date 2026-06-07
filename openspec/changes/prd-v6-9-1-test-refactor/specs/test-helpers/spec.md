# Spec: Test Helpers

## ADDED Requirements

### Requirement: Test helpers module SHALL provide factory functions for creating test artifacts
The system SHALL provide factory functions in `tests/helpers/factories.py` for creating minimal TraversalPlan, TraversalNode, and MockVisionService instances.

#### Scenario: Create minimal plan
- **WHEN** developer calls `create_minimal_plan()`
- **THEN** system returns a valid TraversalPlan with root_node CONTAINER type

#### Scenario: Create test node
- **WHEN** developer calls `create_test_node()`
- **THEN** system returns a valid TraversalNode with specified parameters

#### Scenario: Create mock vision service
- **WHEN** developer calls `create_mock_vision()`
- **THEN** system returns a MockVisionService configured with virtual pages

### Requirement: State inspector SHALL provide internal state verification
The system SHALL provide `StateInspector` class with methods to verify stack consistency, cache coherency, Span relationships, and metrics completeness.

#### Scenario: Verify stack consistency
- **WHEN** StateInspector.verify_stack_consistency() is called with valid stack and context
- **THEN** system returns True if stack path matches context.current_path

#### Scenario: Verify cache coherency
- **WHEN** StateInspector.verify_cache_coherency() is called on engine with cached children
- **THEN** system returns True if cached children match current page elements

#### Scenario: Verify no orphan spans
- **WHEN** StateInspector.verify_no_orphan_spans() is called with trace
- **THEN** system returns True if all Spans have valid parent_span_id or are root

#### Scenario: Verify metrics completeness
- **WHEN** StateInspector.verify_metrics_completeness() is called with trace
- **THEN** system returns True if all operations have corresponding metrics

#### Scenario: Verify state machine invariants
- **WHEN** StateInspector.verify_state_machine_invariants() is called with FSM
- **THEN** system returns True if all state transitions are valid per VALID_TRANSITIONS

### Requirement: Trace analyzer SHALL provide trace analysis capabilities
The system SHALL provide `TraceAnalyzer` class with methods to build trace trees, extract operations, and count span types.

#### Scenario: Build tree
- **WHEN** TraceAnalyzer.build_tree() is called with spans
- **THEN** system returns hierarchical tree structure with parent-child relationships

#### Scenario: Extract operations
- **WHEN** TraceAnalyzer.extract_operations() is called with trace
- **THEN** system returns ordered list of operations (click/back/swipe)

#### Scenario: Count span types
- **WHEN** TraceAnalyzer.count_span_types() is called with trace
- **THEN** system returns dictionary mapping span_type to count

### Requirement: Chaos engine SHALL provide randomization and fault injection
The system SHALL provide `ChaosEngine` class with methods to randomize page order, inject delays, corrupt page data, and duplicate elements.

#### Scenario: Randomize page order
- **WHEN** ChaosEngine.randomize_page_order() is called with elements list
- **THEN** system returns elements in random order

#### Scenario: Inject delay
- **WHEN** ChaosEngine.inject_delay() is called with delay_ms
- **THEN** system adds random delay within variance range

#### Scenario: Corrupt page data
- **WHEN** ChaosEngine.corrupt_page_data() is called with corruption_type
- **THEN** system returns page data with missing/null/wrong_type fields per type

#### Scenario: Duplicate elements
- **WHEN** ChaosEngine.duplicate_elements() is called with page
- **THEN** system returns page with some elements duplicated

### Requirement: Boundary tester SHALL provide boundary condition testing
The system SHALL provide `BoundaryTester` class with methods to test empty elements, excessive depth, massive elements, unicode edge cases, and extreme coordinates.

#### Scenario: Test empty elements
- **WHEN** BoundaryTester.test_empty_elements() is called with empty page
- **THEN** system SHALL NOT crash and handle gracefully

#### Scenario: Test excessive depth
- **WHEN** BoundaryTester.test_excessive_depth() is called with depth=100
- **THEN** system SHALL detect stack overflow or handle gracefully

#### Scenario: Test massive elements
- **WHEN** BoundaryTester.test_massive_elements() is called with count=1000
- **THEN** system SHALL complete without OOM or timeout

#### Scenario: Test unicode edge cases
- **WHEN** BoundaryTester.test_unicode_edge_cases() is called with unicode text
- **THEN** system SHALL handle emoji, surrogate pairs, zero-width chars correctly

#### Scenario: Test extreme coordinates
- **WHEN** BoundaryTester.test_extreme_coordinates() is called with boundary values
- **THEN** system SHALL handle 0, 1, negative, and >1 coordinates correctly

### Requirement: Fault injector SHALL provide fault injection capabilities
The system SHALL provide `FaultInjector` class with methods to inject vision failures, action failures, state corruption, and page mismatches.

#### Scenario: Inject vision timeout
- **WHEN** FaultInjector.inject_vision_failure("timeout") is called
- **THEN** subsequent vision calls raise timeout exception

#### Scenario: Inject vision null result
- **WHEN** FaultInjector.inject_vision_failure("null_result") is called
- **THEN** subsequent vision calls return None

#### Scenario: Inject action failure
- **WHEN** FaultInjector.inject_action_failure("timeout") is called
- **THEN** subsequent action calls raise timeout exception

#### Scenario: Inject state corruption
- **WHEN** FaultInjector.inject_state_corruption() is called
- **THEN** stack.path and context.current_path become inconsistent

#### Scenario: Inject page mismatch
- **WHEN** FaultInjector.inject_mismatched_page(expected, actual) is called
- **THEN** vision returns page with actual path while precondition expects expected
