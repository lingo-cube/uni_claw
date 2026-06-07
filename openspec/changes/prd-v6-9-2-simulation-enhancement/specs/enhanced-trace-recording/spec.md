# Spec: Enhanced Trace Recording

## ADDED Requirements

### Requirement: Page Transition Span Recording
The system SHALL record page transition events in execution traces.

#### Scenario: Record successful page transition
- **WHEN** an action causes a page transition from "home" to "detail"
- **THEN** a PageTransitionSpan SHALL be recorded
- **AND** from_page SHALL be "home"
- **AND** to_page SHALL be "detail"
- **AND** trigger_element SHALL identify the element that caused the transition
- **AND** action SHALL be "click"

#### Scenario: Record no transition when page unchanged
- **WHEN** an action executes but the page does not change
- **THEN** no PageTransitionSpan SHALL be recorded

### Requirement: Dynamic Node Lifecycle Recording
The system SHALL record lifecycle events for dynamically generated nodes.

#### Scenario: Record dynamic node creation
- **WHEN** a dynamic child node is generated via DYNAMIC_MATCH
- **THEN** a DynamicNodeLifecycleSpan SHALL be recorded with event="created"
- **AND** node_id SHALL identify the new node
- **AND** parent_id SHALL identify the parent node
- **AND** match_rule_id SHALL identify the rule that created it

#### Scenario: Record dynamic node push
- **WHEN** a dynamic node is pushed onto the execution stack
- **THEN** a DynamicNodeLifecycleSpan SHALL be recorded with event="pushed"
- **AND** node_id SHALL identify the pushed node

#### Scenario: Record dynamic node execution
- **WHEN** a dynamic node begins execution
- **THEN** a DynamicNodeLifecycleSpan SHALL be recorded with event="executed"
- **AND** node_id SHALL identify the executing node

#### Scenario: Record dynamic node pop
- **WHEN** a dynamic node is popped from the execution stack
- **THEN** a DynamicNodeLifecycleSpan SHALL be recorded with event="popped"
- **AND** node_id SHALL identify the popped node

### Requirement: State Decision Recording
The system SHALL record state machine decision points in execution traces.

#### Scenario: Record AUTO_ESCAPE decision
- **WHEN** the state machine transitions to AUTO_ESCAPE state
- **THEN** a StateDecisionSpan SHALL be recorded
- **AND** current_state SHALL be "EXECUTING" (before transition)
- **AND** decision SHALL be "AUTO_ESCAPE"
- **AND** reason SHALL explain why AUTO_ESCAPE was chosen

#### Scenario: Record completion decision
- **WHEN** the state machine determines traversal is complete
- **THEN** a StateDecisionSpan SHALL be recorded
- **AND** decision SHALL be "complete"
- **AND** reason SHALL explain the completion condition

### Requirement: Span Type Validation
The system SHALL validate span types during creation.

#### Scenario: Validate PageTransitionSpan type
- **WHEN** a PageTransitionSpan is created with span_type="page_transition"
- **THEN** validation SHALL succeed
- **AND** when span_type is not "page_transition", validation SHALL fail

#### Scenario: Validate DynamicNodeLifecycleSpan type
- **WHEN** a DynamicNodeLifecycleSpan is created with span_type="dynamic_lifecycle"
- **THEN** validation SHALL succeed

#### Scenario: Validate StateDecisionSpan type
- **WHEN** a StateDecisionSpan is created with span_type="state_decision"
- **THEN** validation SHALL succeed

### Requirement: Trace Integration
The system SHALL integrate enhanced span recording with existing trace infrastructure.

#### Scenario: Spans inherit from SpanNode
- **WHEN** PageTransitionSpan, DynamicNodeLifecycleSpan, or StateDecisionSpan is created
- **THEN** all SHALL inherit from SpanNode base class
- **AND** all SHALL be compatible with TraceRecorder
- **AND** all SHALL be serializable to trace storage

### Requirement: Backward Compatibility
The system SHALL maintain backward compatibility with existing trace readers.

#### Scenario: Existing trace readers unchanged
- **WHEN** existing trace readers process traces with new span types
- **THEN** readers SHALL skip unknown span types without error
- **AND** readers SHALL continue to process existing span types normally

### Requirement: Element ID Tracking
The system SHALL track element IDs in lifecycle spans.

#### Scenario: Element ID in creation span
- **WHEN** a dynamic node is created from element "btn1"
- **THEN** the DynamicNodeLifecycleSpan event="created" SHALL have element_id="btn1"

### Requirement: Context Information
The system SHALL record context information in state decision spans.

#### Scenario: Context in state decision
- **WHEN** a StateDecisionSpan is recorded
- **THEN** context SHALL contain relevant state information
- **AND** context MAY include retry_count, error_message, or frame_status

### Requirement: Timestamp Recording
The system SHALL record timestamps for all enhanced span types.

#### Scenario: Timestamp in page transition
- **WHEN** a PageTransitionSpan is recorded
- **THEN** timestamp SHALL be the Unix timestamp of the transition

#### Scenario: Timestamp in lifecycle event
- **WHEN** a DynamicNodeLifecycleSpan is recorded
- **THEN** timestamp SHALL be the Unix timestamp of the lifecycle event

### Requirement: Trace Analysis Support
The system SHALL support analysis of enhanced trace data.

#### Scenario: Extract page transitions from trace
- **WHEN** analyzing a trace for page transitions
- **THEN** all PageTransitionSpan nodes SHALL be extractable
- **AND** transitions SHALL be ordered by timestamp

#### Scenario: Extract dynamic node lifecycle from trace
- **WHEN** analyzing a trace for dynamic node lifecycle
- **THEN** all DynamicNodeLifecycleSpan nodes SHALL be extractable
- **AND** lifecycle events SHALL be grouped by node_id

#### Scenario: Extract state decisions from trace
- **WHEN** analyzing a trace for state decisions
- **THEN** all StateDecisionSpan nodes SHALL be extractable
- **AND** decisions SHALL be ordered by timestamp
