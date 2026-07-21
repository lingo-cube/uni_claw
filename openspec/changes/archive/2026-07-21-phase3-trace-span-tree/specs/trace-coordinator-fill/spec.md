## ADDED Requirements

### Requirement: TraceCoordinator SHALL provide PushSpan/PopSpan/ClearVisitSpan for span tree
TraceCoordinator SHALL expose PushSpan() (generates SpanId, pushes to _spanStack, returns SpanId), PopSpan(string? spanId) (pops if stack top matches), and ClearVisitSpan() (nulls _currentVisitSpanId) on ITraceCoordinator.

#### Scenario: PushSpan generates and pushes
- **WHEN** PushSpan() is called on an active TraceCoordinator
- **THEN** a unique SpanId is generated, pushed onto the span stack, and returned

#### Scenario: PopSpan matches and pops
- **WHEN** PopSpan(spanId) is called with the current stack top
- **THEN** the stack top is popped (matching SpanId consumed)

#### Scenario: PopSpan mismatch no-ops
- **WHEN** PopSpan(spanId) is called with a SpanId that does NOT match the stack top
- **THEN** the stack is NOT modified

### Requirement: RecordAICallSpanAsync SHALL accept metadata parameter
RecordAICallSpanAsync SHALL accept an optional Dictionary\<string, object\>? metadata parameter and forward it to AICallRecord.Metadata.

#### Scenario: RecordAICallSpanAsync with metadata
- **WHEN** RecordAICallSpanAsync("vision", "provider", true, 150, metadata: dict) is called
- **THEN** AICallRecord.Metadata equals the passed dictionary

## MODIFIED Requirements

### Requirement: TraceCoordinator fills correlation fields via BuildCorrelation() producing TraceContext
TraceCoordinator SHALL accept ITraversalContext? ctx in its constructor and implement `private TraceContext? BuildCorrelation()` that constructs a TraceContext from engine context: NodeId from ctx.CurrentFrame?.NodeId, StepSpanId from _currentStepSpanId, StepNumber from ctx.StepCount, TraceId from _traceId, VisitSpanId from _currentVisitSpanId, ParentSpanId from _spanStack.Peek() (or null when stack empty). When _ctx is null, BuildCorrelation() returns null. All Record methods SHALL use BuildCorrelation() to fill the record's TraceContext? Context parameter in one call instead of 6 separate fields. This encapsulates observability correlation (when/where/how) separately from core domain fields (what the record IS).

#### Scenario: BuildCorrelation produces 6-field TraceContext from engine context
- **WHEN** ctx.CurrentFrame?.NodeId = "wifi_node", _currentStepSpanId = "abc-000005", ctx.StepCount = 5, _traceId = "abc", _currentVisitSpanId = "abc-000003", _spanStack top = "abc-000010"
- **THEN** BuildCorrelation() returns TraceContext(NodeId="wifi_node", StepSpanId="abc-000005", StepNumber=5, TraceId="abc", VisitSpanId="abc-000003", ParentSpanId="abc-000010")

#### Scenario: Null ctx produces null TraceContext
- **WHEN** TraceCoordinator is constructed with ctx=null
- **THEN** BuildCorrelation() returns null; all records have Context=null

#### Scenario: BuildCorrelation with StepSpanId override for RecordStepStart
- **WHEN** RecordStepStart calls BuildCorrelation() with StepSpanId override = spanId
- **THEN** Context = BuildCorrelation() with { StepSpanId = spanId } — the `with` expression overrides the default _currentStepSpanId (which may not yet be assigned)

### Requirement: TraceCoordinator step-by-step correlation via TraceContext
All TraceCoordinator Record methods that occur within the engine step loop SHALL use BuildCorrelation() to produce TraceContext for their respective record types. This ensures consistent correlation (NodeId, StepSpanId, StepNumber, TraceId, VisitSpanId, ParentSpanId) across all 5 record types via a single TraceContext object. The complete method mapping SHALL be:

| Method | → Record type | Context | SpanId | ChildNodeId | ParentNodeId | FsmType |
|--------|-------------|---------|--------|-------------|-------------|---------|
| RecordStepStart | ExecutionRecord | BuildCorrelation() with StepSpanId=spanId | ✅ (=StepSpanId) | null | null | — |
| RecordStepEnd | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordPageAnalysis | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordActionExecution | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordSkipSpan → DfsForward | ExecutionRecord | BuildCorrelation() | ✅ | matchResult.NodeId | null | — |
| RecordErrorSpan | ErrorRecord | BuildCorrelation() | — | — | — | — |
| RecordDecision | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordStateTransition | StateTransition | BuildCorrelation() | — | — | — | "TraversalFSM" |
| RecordRootNodePushed | StateTransition | **null** (before step loop) | — | — | — | "TraversalFSM" |
| RecordAICallSpan | AICallRecord | BuildCorrelation() | — | — | — | — |
| RecordPageTransition | PageTransition | BuildCorrelation() | — | — | — | — |
| RecordDynamicLifecycle → DfsForward | ExecutionRecord | BuildCorrelation() | ✅ | parentId param | ✅ | — |

#### Scenario: All in-step methods use BuildCorrelation for TraceContext
- **WHEN** any Record method is called during the engine step loop (not RecordRootNodePushed)
- **THEN** the record's Context = BuildCorrelation() — single TraceContext producing consistent 6-field correlation across all record types

#### Scenario: RecordRootNodePushed is the only exception with Context=null
- **WHEN** RecordRootNodePushed is called before the step loop
- **THEN** StateTransition.Context = null — no engine context available at that point
