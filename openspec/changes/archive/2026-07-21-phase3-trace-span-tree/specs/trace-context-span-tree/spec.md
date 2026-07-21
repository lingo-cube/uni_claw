## ADDED Requirements

### Requirement: TraceContext SHALL have 6 fields
TraceContext SHALL extend from 4 to 6 fields by adding VisitSpanId (string?) and ParentSpanId (string?), both defaulting to null.

#### Scenario: 6-field TraceContext construction
- **WHEN** constructing TraceContext with all 6 fields
- **THEN** NodeId, StepSpanId, StepNumber, TraceId, VisitSpanId, ParentSpanId are all accessible

#### Scenario: 4-field backward compatibility
- **WHEN** deserializing old JSONL with only 4 context fields (NodeId, StepSpanId, StepNumber, TraceId)
- **THEN** VisitSpanId and ParentSpanId default to null

#### Scenario: null fields omitted from JSON
- **WHEN** TraceContext with VisitSpanId=null is serialized
- **THEN** visitSpanId key is absent from JSON output

### Requirement: TraceCoordinator SHALL maintain a SpanStack
TraceCoordinator SHALL maintain an internal Stack\<string?\> _spanStack and provide PushSpan()/PopSpan(spanId)/ClearVisitSpan() methods for span lifecycle control.

#### Scenario: PushSpan generates SpanId and pushes to stack
- **WHEN** PushSpan() is called
- **THEN** a unique SpanId is generated, pushed onto _spanStack, and returned

#### Scenario: PopSpan pops when SpanId matches
- **WHEN** PopSpan(spanId) is called with the current stack top
- **THEN** the stack top is popped

#### Scenario: PopSpan no-ops when SpanId mismatches
- **WHEN** PopSpan(spanId) is called with a SpanId that does not match the stack top
- **THEN** the stack is NOT modified (mismatch guard)

### Requirement: BuildCorrelation SHALL include VisitSpanId and ParentSpanId
BuildCorrelation() SHALL populate VisitSpanId from _currentVisitSpanId and ParentSpanId from _spanStack.Peek() (or null when stack is empty).

#### Scenario: ParentSpanId from stack top
- **WHEN** PushSpan() has been called and BuildCorrelation() is invoked
- **THEN** Context.ParentSpanId equals the stack top SpanId

#### Scenario: ParentSpanId null when stack empty
- **WHEN** BuildCorrelation() is invoked with an empty span stack
- **THEN** Context.ParentSpanId is null

### Requirement: VisitSpanId SHALL be set on node entry
VisitSpanId SHALL be set in RecordSkipSpanAsync and RecordDynamicLifecycleAsync when entering a node.

#### Scenario: VisitSpanId set on DFS forward
- **WHEN** RecordSkipSpanAsync is called
- **THEN** _currentVisitSpanId is set and flows to Context.VisitSpanId

#### Scenario: VisitSpanId cleared on exit
- **WHEN** ClearVisitSpan() is called
- **THEN** _currentVisitSpanId is null, subsequent BuildCorrelation produces VisitSpanId=null

### Requirement: HandlerTraceWriter SHALL accept TraceContext
IHandlerTraceWriter.RecordHandlerLifecycleAsync SHALL accept an optional TraceContext? context parameter and set it on the resulting ExecutionRecord.

#### Scenario: HandlerTraceWriter sets Context on ExecutionRecord
- **WHEN** RecordHandlerLifecycleAsync is called with TraceContext
- **THEN** the ExecutionRecord's Context field is populated (NodeId/StepSpanId/StepNumber/TraceId non-null)

#### Scenario: HandlerTraceWriter null context backward compatible
- **WHEN** RecordHandlerLifecycleAsync is called without TraceContext
- **THEN** the ExecutionRecord's Context field is null (existing behavior unchanged)
