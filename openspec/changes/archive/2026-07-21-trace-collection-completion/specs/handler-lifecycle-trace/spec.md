## ADDED Requirements

### Requirement: IHandlerTraceWriter interface
The system SHALL provide an IHandlerTraceWriter interface in the UniClaw.Core.Observability namespace with a single async method for recording handler lifecycle events.

#### Scenario: RecordHandlerLifecycleAsync writes to ITraceRecorder
- **WHEN** RecordHandlerLifecycleAsync is called with action, spanType, and metadata
- **THEN** the underlying implementation delegates to ITraceRecorder.RecordExecutionAsync with the correct ExecutionRecord

### Requirement: TraceHandlerAttribute definition
The system SHALL provide a TraceHandlerAttribute (AttributeUsage.Method) with SpanType and Action constructor parameters for documenting handler entry points.

#### Scenario: TraceHandlerAttribute stores properties
- **WHEN** a method is decorated with [TraceHandler(SpanType.PopupHandling, "handle_popup")]
- **THEN** SpanType returns PopupHandling and Action returns "handle_popup"

### Requirement: TraceMetadata builder
The system SHALL provide a TraceMetadata static class with a chainable Builder for constructing handler metadata dictionaries.

#### Scenario: TraceMetadata.Build chain
- **WHEN** TraceMetadata.Build().Add("key1", "value1").Add("key2", 42).Add<SpanType>("key3", SpanType.PopupHandling).ToDict() is called
- **THEN** the resulting dictionary contains "key1"="value1", "key2"=42, "key3"="PopupHandling"

#### Scenario: TraceMetadata.Build skips nulls
- **WHEN** TraceMetadata.Build().Add("key", (string?)null).ToDict() is called
- **THEN** the resulting dictionary does NOT contain "key"

### Requirement: Handler lifecycle trace — PopupHandler
The system SHALL record a handler lifecycle ExecutionRecord (SpanType=PopupHandling) when PopupHandler completes handling.

#### Scenario: PopupHandler lifecycle trace metadata
- **WHEN** PopupHandler.HandlePopup returns with a PopupHandlingResult
- **THEN** the orchestration layer calls RecordHandlerLifecycleAsync with metadata containing popup_type, dismiss_strategy, dismiss_target, urgency, blocking_type, handling_success, handling_action

### Requirement: Handler lifecycle trace — ContainerHandler
The system SHALL record a handler lifecycle ExecutionRecord (SpanType=ContainerHandling) when ContainerHandler completes handling.

#### Scenario: ContainerHandler lifecycle trace metadata
- **WHEN** ContainerHandler.HandleContainer returns with a ContainerActionResult
- **THEN** the orchestration layer calls RecordHandlerLifecycleAsync with metadata containing completion_reason, fallback_action, container_success, elapsed_ms, depth, total_children, visited_child_count

### Requirement: Handler lifecycle trace — ErrorHandler
The system SHALL record a handler lifecycle ExecutionRecord (SpanType=ErrorHandling) when ErrorHandler completes handling.

#### Scenario: ErrorHandler lifecycle trace metadata
- **WHEN** ErrorHandler.HandleError returns with an ErrorRecoveryResult
- **THEN** the orchestration layer calls RecordHandlerLifecycleAsync with metadata containing classified_error_type, strategy, outcome, backoff_delay_seconds, consecutive_errors, can_backtrack, can_skip, stack_depth, error_policy

#### Scenario: ErrorHandler dual trace output
- **WHEN** ErrorHandler completes
- **THEN** BOTH RecordHandlerLifecycleAsync (ExecutionRecord) AND RecordErrorSpanAsync (ErrorRecord) are called — the records are orthogonal

### Requirement: PopupHandlingResult extension
PopupHandlingResult SHALL have an optional PopupClassification? Classification field (default null) for trace metadata extraction.

#### Scenario: PopupHandlingResult backward compatibility
- **WHEN** constructing PopupHandlingResult without Classification
- **THEN** Classification defaults to null, all existing fields are unchanged

### Requirement: ContainerActionResult extension
ContainerActionResult SHALL have optional CompletionReason?, TotalChildren?, VisitedChildCount?, Depth? fields (all default null) for trace metadata extraction.

#### Scenario: ContainerActionResult backward compatibility
- **WHEN** constructing ContainerActionResult without completion fields
- **THEN** all optional fields default to null, existing fields are unchanged

### Requirement: DecideFrameCompletion async
DecideFrameCompletion SHALL be renamed to DecideFrameCompletionAsync and return Task.

#### Scenario: DecideFrameCompletionAsync behavior preserved
- **WHEN** an existing sync caller path is migrated to async
- **THEN** the return value (frameCompleted, childPushed, nextState) SHALL be identical to the previous sync version

### Requirement: OnFrameComplete async
OnFrameComplete SHALL be async to support the DecideFrameCompletionAsync call chain.

#### Scenario: OnFrameComplete await chain
- **WHEN** OnFrameComplete calls DecideFrameCompletionAsync
- **THEN** it awaits the result before continuing execution
