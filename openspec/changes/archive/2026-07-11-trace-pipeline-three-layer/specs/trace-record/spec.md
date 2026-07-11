## MODIFIED Requirements

### Requirement: TraceRecord is a sealed record class capturing per-step trace data
TraceRecord SHALL be a `sealed record class` with 14 fields: `int StepNumber`, `TraversalState FromState`, `TraversalState ToState`, `string? CurrentNodeId`, `string? CurrentPageId`, `string? ActionExecuted`, `bool ActionSuccess`, `bool ChildPushed`, `bool FrameCompleted`, `ImmutableArray<SpanType> SpanTypes = default`, `string? PageFrom = null`, `string? PageTo = null`, `string? PageTransitionType = null`, `double? StepDurationMs = null`. The 5 new fields (SpanTypes, PageFrom, PageTo, PageTransitionType, StepDurationMs) SHALL be optional (default/null) for backward compatibility. TraceRecord.SpanTypes replaces the single `SpanType?` field — one step can produce multiple semantic events. TraceRecord remains independent from ITraceRecorder — it records in-memory per-step data for TraversalResult, while ITraceRecorder/ITraceStorage/ITraceService handle canonical event-level data and queries.

#### Scenario: TraceRecord captures multiple SpanTypes per step
- **WHEN** a step produces both PageAnalysis and StateDecision events
- **THEN** TraceRecord.SpanTypes contains [SpanType.PageAnalysis, SpanType.StateDecision]; SpanTypes is ImmutableArray<SpanType> (not single SpanType?)

#### Scenario: TraceRecord captures page navigation
- **WHEN** a step navigates from "home" to "wifi" via "forward" transition
- **THEN** TraceRecord.PageFrom="home", PageTo="wifi", PageTransitionType="forward"

#### Scenario: TraceRecord captures step duration
- **WHEN** a step takes 250ms to execute
- **THEN** TraceRecord.StepDurationMs=250.0

#### Scenario: Backward compatibility — existing 9-field constructors work
- **WHEN** existing code constructs TraceRecord(StepNumber=1, FromState=NodeSelect, ToState=Execute, CurrentNodeId="n1", CurrentPageId="p1", ActionExecuted="click", ActionSuccess=true, ChildPushed=false, FrameCompleted=false)
- **THEN** SpanTypes=default (empty), PageFrom=null, PageTo=null, PageTransitionType=null, StepDurationMs=null; no existing test breaks
