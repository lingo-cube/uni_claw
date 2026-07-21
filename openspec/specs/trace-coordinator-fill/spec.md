## ADDED Requirements

### Requirement: TraceCoordinator generates SpanId with incremental counter
TraceCoordinator SHALL maintain an internal _spanCounter (int) starting at 0. Each call to NextSpanId() SHALL increment the counter and return a string in format "{traceId}-{counter:D6}" (e.g., "abc-000001", "abc-000002"). SpanId SHALL be unique within a trace session (same TraceId prefix). SpanId SHALL be assigned to every ExecutionRecord created by TraceCoordinator.

#### Scenario: SpanId format is traceId plus 6-digit counter
- **WHEN** TraceCoordinator has traceId="abc" and calls NextSpanId() three times
- **THEN** returned SpanIds are "abc-000001", "abc-000002", "abc-000003"

#### Scenario: SpanId is populated on all ExecutionRecords
- **WHEN** RecordPageAnalysis creates an ExecutionRecord
- **THEN** ExecutionRecord.SpanId is a non-null NextSpanId() value

### Requirement: TraceCoordinator maintains StepSpanId lifecycle
TraceCoordinator SHALL maintain _currentStepSpanId (string?). RecordStepStart SHALL assign _currentStepSpanId = the StepStart record's SpanId (so StepSpanId = StepStart.SpanId). RecordStepEnd SHALL use _currentStepSpanId for the StepEnd record's TraceContext, then set _currentStepSpanId = null. All other Record methods (RecordPageAnalysis, RecordActionExecution, RecordErrorSpan, etc.) SHALL have TraceContext with StepSpanId populated from _currentStepSpanId via BuildCorrelation().

#### Scenario: StepSpanId equals StepStart's SpanId
- **WHEN** RecordStepStart generates SpanId="abc-000005"
- **THEN** _currentStepSpanId = "abc-000005"; StepStart ExecutionRecord.SpanId = "abc-000005", Context.StepSpanId = "abc-000005"

#### Scenario: StepSpanId is released at StepEnd
- **WHEN** RecordStepEnd is called after RecordStepStart
- **THEN** StepEnd ExecutionRecord.Context.StepSpanId = _currentStepSpanId (the value from this step); then _currentStepSpanId becomes null

#### Scenario: RecordPageAnalysis uses current StepSpanId via BuildCorrelation
- **WHEN** RecordPageAnalysis is called during a step (between StepStart and StepEnd)
- **THEN** ExecutionRecord.Context.StepSpanId = _currentStepSpanId (same value as StepStart's SpanId, populated by BuildCorrelation())

#### Scenario: RecordRootNodePushed has null Context (no StepSpanId)
- **WHEN** RecordRootNodePushed is called before the engine step loop starts
- **THEN** StateTransition.Context = null (no BuildCorrelation — before step loop, no engine context available)

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

### Requirement: RecordActionExecution uses typed OperationType and Target parameters
RecordActionExecution SHALL accept (OperationType action, Target? target, bool success) instead of (string action, string target, bool success). TraceCoordinator SHALL extract TargetType from target?.By and TargetValue from SerializeTarget(target). SerializeTarget SHALL return (null, null) for null target (Back/NoAction operations), and (target.By, serialized value) for non-null targets where value serialization is: string->string, Coordinate->"{X},{Y}", int->ToString(), other->ToString(). ExecutionRecord SHALL have Context = BuildCorrelation() and type-specific fields (TargetType, TargetValue) separate from correlation.

#### Scenario: Click operation with Coordinate target
- **WHEN** RecordActionExecution(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(100, 200)), true)
- **THEN** ExecutionRecord.Action="click", Context=BuildCorrelation(), TargetType=TargetType.Coordinate, TargetValue="100,200"

#### Scenario: Back operation with no target
- **WHEN** RecordActionExecution(OperationType.Back, null, true)
- **THEN** ExecutionRecord.Action="back", Context=BuildCorrelation(), TargetType=null, TargetValue=null

#### Scenario: InputText operation with Text target
- **WHEN** RecordActionExecution(OperationType.InputText, new Target(TargetType.Text, "wifi_password"), true)
- **THEN** ExecutionRecord.Action="input_text", Context=BuildCorrelation(), TargetType=TargetType.Text, TargetValue="wifi_password"

### Requirement: RecordAICallSpan creates AICallRecord with TraceContext via BuildCorrelation
RecordAICallSpan SHALL accept typed parameters (string capability, string providerId, bool success, double latencyMs, int? tokens, Dictionary\<string, object\>? metadata = null) and create an AICallRecord with Context = BuildCorrelation() (encapsulating NodeId, StepSpanId, StepNumber, TraceId). Tokens and Metadata are AICallRecord-specific (type-specific fields on AICallRecord, not in TraceContext).

#### Scenario: AICallRecord correlation via TraceContext
- **WHEN** RecordAICallSpan("vision", "provider", true, 230.5) is called during step 7
- **THEN** AICallRecord.Context = BuildCorrelation() (NodeId, StepSpanId, StepNumber, TraceId populated); Tokens=null by default; Metadata=null by default

#### Scenario: AICallRecord with tokens
- **WHEN** RecordAICallSpan("vision", "provider", true, 230.5, tokens=1500) is called
- **THEN** AICallRecord.Tokens = 1500; Context = BuildCorrelation()

#### Scenario: AICallRecord with metadata
- **WHEN** RecordAICallSpan("vision", "provider", true, 150, metadata: dict) is called
- **THEN** AICallRecord.Metadata equals the passed dictionary

### Requirement: RecordStateTransition creates StateTransition with FsmType and TraceContext via BuildCorrelation
RecordStateTransition SHALL create StateTransition with FsmType="TraversalFSM" and Context = BuildCorrelation(). This iteration only fills TraversalFSM; Phase 3 will add GlobalFSM support. FsmType is StateTransition-specific (not in TraceContext — only FSM transitions have an FSM type).

#### Scenario: StateTransition has TraversalFSM tag and TraceContext
- **WHEN** RecordStateTransition("NodeSelect", "Execute") is called
- **THEN** StateTransition.FsmType = "TraversalFSM", Context = BuildCorrelation()

### Requirement: RecordErrorSpan creates ErrorRecord with TraceContext via BuildCorrelation
RecordErrorSpan SHALL create ErrorRecord with Context = BuildCorrelation(). ErrorRecord has no ParentNodeId field — Context.NodeId provides "error occurred at this node" semantics (replacing old ParentNodeId).

#### Scenario: ErrorRecord correlation via TraceContext
- **WHEN** RecordErrorSpan is called during a step
- **THEN** ErrorRecord.Context = BuildCorrelation(); no ParentNodeId field exists on ErrorRecord

### Requirement: RecordPageTransition creates PageTransition with TraceContext via BuildCorrelation
RecordPageTransition SHALL create PageTransition with Context = BuildCorrelation(). DurationMs is PageTransition-specific (not in TraceContext).

#### Scenario: PageTransition correlation via TraceContext
- **WHEN** RecordPageTransition("home", "wifi", "forward") is called
- **THEN** PageTransition.Context = BuildCorrelation(); DurationMs may be populated

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

| Method | -> Record type | Context | SpanId | ChildNodeId | ParentNodeId | FsmType |
|--------|-------------|---------|--------|-------------|-------------|---------|
| RecordStepStart | ExecutionRecord | BuildCorrelation() with StepSpanId=spanId | ✅ (=StepSpanId) | null | null | — |
| RecordStepEnd | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordPageAnalysis | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordActionExecution | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordSkipSpan -> DfsForward | ExecutionRecord | BuildCorrelation() | ✅ | matchResult.NodeId | null | — |
| RecordErrorSpan | ErrorRecord | BuildCorrelation() | — | — | — | — |
| RecordDecision | ExecutionRecord | BuildCorrelation() | ✅ | null | null | — |
| RecordStateTransition | StateTransition | BuildCorrelation() | — | — | — | "TraversalFSM" |
| RecordRootNodePushed | StateTransition | **null** (before step loop) | — | — | — | "TraversalFSM" |
| RecordAICallSpan | AICallRecord | BuildCorrelation() | — | — | — | — |
| RecordPageTransition | PageTransition | BuildCorrelation() | — | — | — | — |
| RecordDynamicLifecycle -> DfsForward | ExecutionRecord | BuildCorrelation() | ✅ | parentId param | ✅ | — |

#### Scenario: All in-step methods use BuildCorrelation for TraceContext
- **WHEN** any Record method is called during the engine step loop (not RecordRootNodePushed)
- **THEN** the record's Context = BuildCorrelation() — single TraceContext producing consistent 6-field correlation across all record types

#### Scenario: RecordRootNodePushed is the only exception with Context=null
- **WHEN** RecordRootNodePushed is called before the step loop
- **THEN** StateTransition.Context = null — no engine context available at that point
