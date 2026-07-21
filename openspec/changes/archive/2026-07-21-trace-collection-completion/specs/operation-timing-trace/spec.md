## ADDED Requirements

### Requirement: DurationMs from Stopwatch
TraceCoordinator SHALL maintain an internal Stopwatch that measures wall-clock time between RecordStepStartAsync and RecordStepEndAsync.

#### Scenario: DurationMs non-zero after step
- **WHEN** RecordStepStartAsync is called, then some time elapses, then RecordStepEndAsync is called
- **THEN** the resulting ExecutionRecord.DurationMs SHALL be a positive number (elapsed milliseconds)

#### Scenario: DurationMs reset between steps
- **WHEN** a second step starts after the first completes
- **THEN** the second step's DurationMs SHALL measure only the second step's elapsed time, not cumulative

### Requirement: DfsBacktrack trace — leaf_execution_complete
When a leaf node execution completes and triggers backtrack, the system SHALL record an ExecutionRecord (SpanType=DfsBacktrack, action="dfs_backtrack", backtrack_reason="leaf_execution_complete").

#### Scenario: DfsBacktrack leaf backtrack trace
- **WHEN** TraversalEngine.RunAsync processes a leaf step where stepResult.NextState==ResultVerify && Depth>1 && ChildrenStrategy=None
- **THEN** a RecordHandlerLifecycleAsync call is made with spanType=DfsBacktrack and metadata.backtrack_reason="leaf_execution_complete"

### Requirement: DfsBacktrack trace — pop_only
When a fingerprint match triggers pop-only backtrack, the system SHALL record an ExecutionRecord (SpanType=DfsBacktrack, action="dfs_backtrack", backtrack_reason="pop_only_parent_frame_matches").

#### Scenario: DfsBacktrack pop_only trace
- **WHEN** InterceptionHandler.OnDynamicMatchNodeSelect detects fingerprint match and chooses pop-only strategy
- **THEN** a RecordHandlerLifecycleAsync call is made with spanType=DfsBacktrack and metadata.backtrack_reason="pop_only_parent_frame_matches"

### Requirement: DfsBacktrack trace — press_back
When a fingerprint mismatch triggers press_back+pop backtrack, the system SHALL record an ExecutionRecord (SpanType=DfsBacktrack, action="dfs_backtrack", backtrack_reason="press_back_parent_frame_differs").

#### Scenario: DfsBacktrack press_back trace
- **WHEN** InterceptionHandler.OnDynamicMatchNodeSelect detects fingerprint mismatch and chooses press_back+pop strategy
- **THEN** a RecordHandlerLifecycleAsync call is made with spanType=DfsBacktrack and metadata.backtrack_reason="press_back_parent_frame_differs"

### Requirement: AICallRecord.Metadata
AICallRecord SHALL have an optional Dictionary<string, object>? Metadata field (default null) for ADB/vision operation context.

#### Scenario: AICallRecord backward compatibility
- **WHEN** constructing AICallRecord without Metadata
- **THEN** Metadata defaults to null, existing 5 constructor parameters are unchanged

### Requirement: RecordAICallSpanAsync metadata parameter
ITraceCoordinator.RecordAICallSpanAsync SHALL accept an optional Dictionary<string, object>? metadata parameter.

#### Scenario: RecordAICallSpanAsync forwards metadata
- **WHEN** RecordAICallSpanAsync is called with metadata={"adb_operation": "tap", "adb_latency_ms": 150}
- **THEN** the resulting AICallRecord.Metadata contains both key-value pairs

#### Scenario: RecordAICallSpanAsync null metadata
- **WHEN** RecordAICallSpanAsync is called without metadata
- **THEN** the resulting AICallRecord.Metadata is null (no behavioral change for existing callers)
