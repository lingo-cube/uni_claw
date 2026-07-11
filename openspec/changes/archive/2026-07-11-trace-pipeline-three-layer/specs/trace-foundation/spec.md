## REMOVED Requirements

### Requirement: TraceNode hierarchy defines 4 sealed record types
**Reason**: TraceNode hierarchy (SessionNode/StepNode/SpanNode) is dead code — never populated by engine or any production code. 3-type model too coarse for user vision. SpanNode.SpanType is string? (semantic mismatch with SpanType enum). ITraceStorage + InMemoryTraceStorage replace storage role; ITraceService replaces query role.
**Migration**: All TraceNode references removed. Tree reconstruction via ITraceService.ReconstructTree() using ExecutionRecord DfsForward edges with ChildNodeId. SpanId generation moves from UlidGenerator to TraceCoordinator._spanCounter.

### Requirement: ULID generation produces 26-char Crockford Base32 identifiers
**Reason**: UlidGenerator's only consumer was TraceNode.SpanId. With TraceNode deleted, UlidGenerator has zero production references. SpanId generation now uses TraceCoordinator._spanCounter format "{traceId}-{counter:D6}" which is simpler, readable, sortable, and sufficient for trace session scope.
**Migration**: No ULID generation needed. SpanId uses incremental counter format. All UlidGenerator references removed.

## MODIFIED Requirements

### Requirement: ITraceRecorder defines session lifecycle and span recording methods only
The `ITraceRecorder` interface SHALL define exactly 7 methods organized into two categories: session lifecycle (2 methods: StartSessionAsync, EndSessionAsync) and span recording (5 methods: RecordExecutionAsync, RecordTransitionAsync, RecordErrorAsync, RecordPageTransitionAsync, RecordAICallAsync). All methods SHALL be async (return `Task` or `Task<T>`). ITraceRecorder SHALL NOT include query methods (GetXxxAsync), CurrentSession getter, or ExportTraceAsync — these belong on ITraceService and ITraceStorage respectively. ITraceRecorder is a pure write contract.

#### Scenario: ITraceRecorder has exactly 7 members
- **WHEN** ITraceRecorder is inspected for method declarations
- **THEN** it declares exactly: StartSessionAsync, EndSessionAsync, RecordExecutionAsync, RecordTransitionAsync, RecordErrorAsync, RecordPageTransitionAsync, RecordAICallAsync

#### Scenario: ITraceRecorder has no query methods
- **WHEN** ITraceRecorder is inspected for GetXxxAsync, CurrentSession, or ExportTraceAsync
- **THEN** these are NOT declared on the interface

### Requirement: ITraceRecorder records PageTransition via storage delegation
ITraceRecorder SHALL include RecordPageTransitionAsync(PageTransition transition, CancellationToken ct) as one of its 5 Record methods. The method SHALL delegate to ITraceStorage.AddPageTransition + Task.CompletedTask. Retrieval SHALL be via ITraceService.GetPageTransitions() (synchronous IReadOnlyList return).

#### Scenario: RecordPageTransitionAsync stores PageTransition via ITraceStorage
- **WHEN** RecordPageTransitionAsync is called with PageTransition(FromPage="home", ToPage="wifi", TransitionType="forward", Context=new TraceContext(NodeId="home_node", StepSpanId="abc-000005", StepNumber=5, TraceId="abc"))
- **THEN** the PageTransition MUST be stored via ITraceStorage.AddPageTransition and retrievable via ITraceService.GetPageTransitions()

### Requirement: ExecutionRecord includes TraceContext, SpanType, and typed target fields
ExecutionRecord SHALL be extended with the following fields: `SpanType? SpanType = null` (semantic classification, placed immediately after Action+Status), `TraceContext? Context = null` (encapsulates 4 common correlation fields: NodeId, StepSpanId, StepNumber, TraceId), `string? SpanId = null`, `string? ChildNodeId = null`, `string? ParentNodeId = null`, `string? PageId = null`, `TargetType? TargetType = null`, `string? TargetValue = null`, `int? Depth = null`. `object? Target` SHALL be REMOVED — replaced by `TargetType?` and `string? TargetValue`. TraceContext? Context SHALL encapsulate the 4 common correlation fields that were previously separate parameters on each record type. `NodeId` in TraceContext represents "the node the event occurred at" (NOT the DFS parent). `ParentNodeId` on ExecutionRecord represents "DFS tree parent for tree reconstruction" (NOT the current node). `ChildNodeId` represents "pushed child ID for DfsForward events". `SpanId` is unique per ExecutionRecord (TraceCoordinator counter format). `StepSpanId` in TraceContext is per-engine-step grouping (assigned at StepStart = StepStart's SpanId). All new fields SHALL be optional (default null) for backward compatibility.

#### Scenario: ExecutionRecord with TraceContext
- **WHEN** an ExecutionRecord is constructed with Action="click", Status="success", SpanType=SpanType.ActionExecution, Context=new TraceContext(NodeId="wifi_node", StepSpanId="abc-000005", StepNumber=5, TraceId="abc"), SpanId="abc-000006", ChildNodeId=null, ParentNodeId="home_node", PageId="wifi_settings", TargetType=TargetType.Coordinate, TargetValue="100,200", Depth=2
- **THEN** Context.NodeId="wifi_node", Context.StepSpanId="abc-000005", Context.StepNumber=5, Context.TraceId="abc"; SpanType=SpanType.ActionExecution; all type-specific fields populated correctly

#### Scenario: Backward compatibility — existing constructors unaffected
- **WHEN** existing code constructs ExecutionRecord(Action="step_start", Status="ok") without any new parameters
- **THEN** Context=null, SpanId=null, SpanType=null, all type-specific fields default to null; no existing test or production code MUST break

#### Scenario: object? Target is removed
- **WHEN** ExecutionRecord is inspected for a `Target` field of type `object?`
- **THEN** no such field exists — replaced by `TargetType?` (nullable enum) and `TargetValue?` (nullable string)

#### Scenario: NodeId in TraceContext vs ParentNodeId on ExecutionRecord
- **WHEN** RecordPageAnalysis creates ExecutionRecord with Context.NodeId = ctx.CurrentFrame?.NodeId and ParentNodeId = ctx.CurrentFrame?.Parent?.NodeId
- **THEN** Context.NodeId = "the node where this page analysis occurred"; ParentNodeId = "the DFS parent node in the traversal tree" — distinct semantics

#### Scenario: ChildNodeId semantics for DfsForward
- **WHEN** RecordSkipSpan creates ExecutionRecord with SpanType=DfsForward and ChildNodeId=matchResult.NodeId
- **THEN** ChildNodeId explicitly records "NodeA pushed NodeB as child" — enables direct DFS tree reconstruction

#### Scenario: SpanType field placement
- **WHEN** ExecutionRecord field order is inspected
- **THEN** SpanType appears immediately after Status (before Context), reflecting its domain classification role

### Requirement: Trace writes use Log-and-Continue pattern
All trace write operations SHALL use a try-catch wrapper that catches exceptions and logs them without interrupting the traversal. Trace write failures SHALL NOT propagate to the engine loop. When `ITraceRecorder` is null or `active=False`, all trace methods SHALL be no-ops.

#### Scenario: Trace write failure does not interrupt traversal
- **WHEN** a trace write method (e.g., RecordExecutionAsync) throws an exception via LogAndContinue
- **THEN** the exception is caught, a warning is logged, and the traversal step continues without interruption

#### Scenario: Null recorder is a no-op
- **WHEN** the `ITraceRecorder` reference is null
- **THEN** all trace write methods return immediately without executing any logic

#### Scenario: Inactive recorder is a no-op
- **WHEN** the `ITraceRecorder` is set but `active=False` (no `trace_id`)
- **THEN** all trace write methods return immediately without executing any logic

## ADDED Requirements

### Requirement: TraceContext encapsulates common observability correlation fields
TraceContext SHALL be a `sealed record class` with exactly 4 fields: `string? NodeId = null`, `string? StepSpanId = null`, `int? StepNumber = null`, `string? TraceId = null`. TraceContext encapsulates "when/where/how" observability correlation that is shared by ALL 5 ITraceRecorder record types. TraceContext SHALL NOT contain type-specific fields (FsmType, SpanId, ChildNodeId, ParentNodeId, PageId, TargetType, TargetValue, Depth, DurationMs, Tokens). Each record type SHALL have `TraceContext? Context = null` as an optional parameter. When Context is null, the record has no observability correlation (e.g., created outside trace infrastructure). TraceContext field boundary rule: ONLY fields shared by ALL 5 types belong in TraceContext. Type-specific fields stay on their respective record types.

#### Scenario: TraceContext has exactly 4 fields
- **WHEN** TraceContext is inspected for field declarations
- **THEN** it contains exactly: NodeId, StepSpanId, StepNumber, TraceId — no other fields

#### Scenario: TraceContext is sealed record class
- **WHEN** the type declaration of TraceContext is inspected
- **THEN** it is `sealed record class`

#### Scenario: All 5 record types have TraceContext? Context parameter
- **WHEN** ExecutionRecord, StateTransition, ErrorRecord, PageTransition, AICallRecord are inspected
- **THEN** each declares `TraceContext? Context = null` as an optional parameter

#### Scenario: TraceContext default null means no correlation
- **WHEN** a record is constructed without Context parameter
- **THEN** Context is null; accessing Context?.NodeId returns null; queries filtering by Context?.NodeId exclude this record

#### Scenario: Phase 3 extension adds VisitSpanId+ParentSpanId to TraceContext only
- **WHEN** VisitSpanId and ParentSpanId are added as general correlation fields in Phase 3
- **THEN** they are added to TraceContext (4→6 fields); the 5 record types get the new fields automatically via Context — no record type parameter changes needed

### Requirement: TraceContext_Has4Fields guard test prevents accidental field addition
ArchitectureGuardTests SHALL include a test verifying TraceContext has exactly 4 fields. This prevents accidental addition of type-specific fields (e.g., SpanId, FsmType) to TraceContext, which would violate the "only shared fields" boundary rule.

#### Scenario: Guard test verifies TraceContext field count
- **WHEN** TraceContext is inspected via reflection for declared properties
- **THEN** exactly 4 properties exist: NodeId, StepSpanId, StepNumber, TraceId

### Requirement: StateTransition includes TraceContext + FsmType
StateTransition SHALL be extended with `TraceContext? Context = null` (encapsulating NodeId, StepSpanId, StepNumber, TraceId) and `string? FsmType = null` (identifying which FSM produced this transition: "TraversalFSM" this iteration, "GlobalFSM" Phase 3). The 4 separate correlation fields (NodeId, StepSpanId, StepNumber, TraceId) SHALL NOT appear as separate parameters — they are encapsulated in Context. FsmType stays on StateTransition because only FSM transitions have an FSM type (not in TraceContext — it's type-specific).

#### Scenario: StateTransition with TraceContext and TraversalFSM tag
- **WHEN** RecordStateTransition creates a StateTransition
- **THEN** Context = BuildCorrelation() (NodeId, StepSpanId, StepNumber, TraceId populated); FsmType = "TraversalFSM"

#### Scenario: FsmType is not in TraceContext
- **WHEN** TraceContext is inspected for FsmType field
- **THEN** FsmType is NOT in TraceContext — it exists only on StateTransition (type-specific field)

### Requirement: ErrorRecord includes TraceContext — replaces ParentNodeId + separate correlation fields
ErrorRecord SHALL be extended with `TraceContext? Context = null` (encapsulating NodeId, StepSpanId, StepNumber, TraceId). The old `string? ParentNodeId` field SHALL be REMOVED from ErrorRecord — its semantics ("error at this node") are now expressed as TraceContext.NodeId with clarified meaning (event-at-node, not DFS parent). The 4 separate correlation fields SHALL NOT appear as separate parameters — encapsulated in Context.

#### Scenario: ErrorRecord correlation via TraceContext
- **WHEN** RecordErrorSpan creates an ErrorRecord
- **THEN** Context = BuildCorrelation() (NodeId, StepSpanId, StepNumber, TraceId populated); no separate NodeId/StepSpanId/StepNumber/TraceId parameters exist; no ParentNodeId field exists

#### Scenario: ErrorRecord.ParentNodeId field removed
- **WHEN** ErrorRecord is inspected for ParentNodeId field
- **THEN** no ParentNodeId field exists — replaced by Context.NodeId (clarified semantics: "error occurred at this node", NOT "DFS parent")

### Requirement: PageTransition includes TraceContext + DurationMs
PageTransition SHALL be extended with `TraceContext? Context = null` (encapsulating NodeId, StepSpanId, StepNumber, TraceId) and `double? DurationMs = null` (PageTransition-specific: navigation duration). The 4 separate correlation fields SHALL NOT appear as separate parameters — encapsulated in Context. DurationMs stays on PageTransition because only page transitions have navigation duration (not in TraceContext — type-specific field).

#### Scenario: PageTransition correlation via TraceContext
- **WHEN** RecordPageTransition creates a PageTransition
- **THEN** Context = BuildCorrelation(); DurationMs populated if available

### Requirement: AICallRecord includes TraceContext + Tokens
AICallRecord SHALL be extended with `TraceContext? Context = null` (encapsulating NodeId, StepSpanId, StepNumber, TraceId) and `int? Tokens = null` (AICallRecord-specific: token consumption). The 4 separate correlation fields SHALL NOT appear as separate parameters — encapsulated in Context. Tokens stays on AICallRecord because only AI calls track token consumption (not in TraceContext — type-specific field).

#### Scenario: AICallRecord correlation via TraceContext
- **WHEN** RecordAICallSpan creates an AICallRecord
- **THEN** Context = BuildCorrelation(); Tokens populated if available

### Requirement: InMemoryTraceRecorder is a minimal async wrapper over ITraceStorage
InMemoryTraceRecorder SHALL implement ITraceRecorder (7 methods) by injecting ITraceStorage and delegating: StartSessionAsync → _storage.SetSession + Task.FromResult, EndSessionAsync → _storage.EndSession + Task.CompletedTask, RecordXxxAsync → _storage.AddXxx + Task.CompletedTask. Zero business logic — pure async-over-sync wrapper.

#### Scenario: RecordExecutionAsync delegates to storage
- **WHEN** InMemoryTraceRecorder.RecordExecutionAsync(record) is called
- **THEN** _storage.AddExecution(record) is called synchronously; Task.CompletedTask is returned

#### Scenario: InMemoryTraceRecorder injects ITraceStorage interface
- **WHEN** InMemoryTraceRecorder is constructed
- **THEN** its constructor parameter type is ITraceStorage (interface), not InMemoryTraceStorage (concrete)

### Requirement: TraceContext encapsulation reduces parameter duplication and separates domain from correlation
TraceContext encapsulation replaces 4 separate correlation parameters on each of 5 record types (4×5=20 total) with a single TraceContext? Context parameter (1×5=5 total). This separates concerns: core domain fields describe what the record IS (From→To for StateTransition, ErrorType+ErrorMessage for ErrorRecord), TraceContext describes how it relates to engine context. TraceCoordinator fills Context via BuildCorrelation() in one call instead of 4 separate fields. Phase 3 extension (VisitSpanId, ParentSpanId) adds 2 fields to TraceContext only — no record type changes needed.

#### Scenario: Parameter count reduction for StateTransition
- **WHEN** StateTransition's positional parameters are counted
- **THEN** StateTransition has 6 positional parameters (FromState, ToState, Context, FsmType, Reason, Timestamp) instead of 9 (with separate NodeId, StepSpanId, StepNumber, TraceId)

#### Scenario: Parameter count reduction for ErrorRecord
- **WHEN** ErrorRecord's positional parameters are counted
- **THEN** ErrorRecord has 5 positional parameters (ErrorType, ErrorMessage, Severity, Context, Timestamp) instead of 8 (with separate NodeId, StepSpanId, StepNumber, TraceId, ParentNodeId)
