## 1. Delete Dead Code

- [x] 1.1 Delete `src/UniClaw.Core/Trace/TraceNode.cs` (TraceNode + SessionNode + StepNode + SpanNode)
- [x] 1.2 Delete `src/UniClaw.Core/Common/UlidGenerator.cs` (only consumer was TraceNode.SpanId)
- [x] 1.3 Delete TraceNodeTests (3 tests) and UlidGeneratorTests (5 tests)
- [x] 1.4 Delete `TraceNodeHierarchy_ExactlyThreeSubtypes` guard test from ArchitectureGuardTests.cs
- [x] 1.5 Verify all tests still pass after deletions (575 tests green)

## 2. Data Model — TraceContext + Record Field Changes

- [x] 2.1 Create TraceContext sealed record class in Observability/TraceContext.cs with 4 fields: `string? NodeId = null`, `string? StepSpanId = null`, `int? StepNumber = null`, `string? TraceId = null`. TraceContext encapsulates observability correlation shared by ALL 5 record types. NOT type-specific fields.
- [x] 2.2 Add TraceContext_Has4Fields guard test to ArchitectureGuardTests.cs — verifies TraceContext has exactly 4 properties, preventing accidental addition of type-specific fields
- [x] 2.3 Extend ExecutionRecord: add `SpanType? SpanType = null` (before Context), `TraceContext? Context = null`, `string? SpanId = null`, `string? ChildNodeId = null`, `string? ParentNodeId = null`, `string? PageId = null`, `TargetType? TargetType = null`, `string? TargetValue = null`, `int? Depth = null`; remove `object? Target`. ParentNodeId semantics clarified: DFS tree parent for tree reconstruction (NOT "current node"). NodeId (event-at-node) is in TraceContext.
- [x] 2.4 Extend StateTransition: add `TraceContext? Context = null` (encapsulates NodeId, StepSpanId, StepNumber, TraceId), `string? FsmType = null`. Remove separate NodeId, StepSpanId, StepNumber, TraceId parameters.
- [x] 2.5 Extend ErrorRecord: add `TraceContext? Context = null` (encapsulates NodeId, StepSpanId, StepNumber, TraceId). Remove old `ParentNodeId` field (replaced by Context.NodeId with clarified semantics: "error at this node", not DFS parent). Remove separate correlation parameters.
- [x] 2.6 Extend PageTransition: add `TraceContext? Context = null` (encapsulates NodeId, StepSpanId, StepNumber, TraceId), `double? DurationMs = null` (PageTransition-specific). Remove separate StepSpanId, StepNumber, TraceId parameters.
- [x] 2.7 Extend AICallRecord: add `TraceContext? Context = null` (encapsulates NodeId, StepSpanId, StepNumber, TraceId), `int? Tokens = null` (AICallRecord-specific). Remove separate NodeId, StepSpanId, StepNumber, TraceId parameters.
- [x] 2.8 Extend TraceRecord: add `ImmutableArray<SpanType> SpanTypes = default`, `string? PageFrom = null`, `string? PageTo = null`, `string? PageTransitionType = null`, `double? StepDurationMs = null` (5 new optional fields)
- [x] 2.9 Update all existing test code that constructs these records (add defaults for new optional fields, update ErrorRecord to use Context instead of ParentNodeId)
- [x] 2.10 Verify all tests pass after record changes (575 tests green)

## 3. Storage Architecture — Three-Layer Separation

- [x] 3.1 Create ITraceStorage interface (13 methods: session, write, read, export) in Observability/ITraceStorage.cs
- [x] 3.2 Create InMemoryTraceStorage implementation with 5 flat lists + _byNodeId + _bySpanType indexes + index methods (GetByNodeId, GetBySpanType). Index keys use `r.Context?.NodeId` and `r.SpanType` (TraceContext encapsulation).
- [x] 3.3 Slim ITraceRecorder from 13→7 methods: remove 5 Get methods, CurrentSession getter, ExportTraceAsync
- [x] 3.4 Rewrite InMemoryTraceRecorder: inject ITraceStorage (interface), 7 async wrapper methods (AddXxx + Task.CompletedTask)
- [x] 3.5 Create ITraceService interface (1 property + 12 methods) in Observability/ITraceService.cs
- [x] 3.6 Create InMemoryTraceService: inject InMemoryTraceStorage (concrete), implement all 13 members. Query methods access correlation via `record.Context?.NodeId`, `record.Context?.StepNumber`, `record.Context?.StepSpanId`.
- [x] 3.7 Create 6 query result types (TraversalTree, TreeEdge, NodeSpans, NodeVisitTimeline, StepTimeline, StepSpanGroup) in Observability/TraceQueryResults.cs

## 4. TraceCoordinator Refactor

- [x] 4.1 Add ITraversalContext? ctx to TraceCoordinator constructor + _spanCounter + _currentStepSpanId fields
- [x] 4.2 Implement NextSpanId() counter format "{traceId}-{counter:D6}"
- [x] 4.3 Implement BuildCorrelation() — private helper returning TraceContext? from ctx: NodeId from ctx.CurrentFrame?.NodeId, StepSpanId from _currentStepSpanId, StepNumber from ctx.StepCount, TraceId from _traceId. Returns null when ctx=null.
- [x] 4.4 Implement RecordStepStart: generate SpanId, assign _currentStepSpanId=SpanId, create ExecutionRecord with Context = BuildCorrelation() with StepSpanId override (=spanId)
- [x] 4.5 Implement RecordStepEnd: create ExecutionRecord with Context = BuildCorrelation(), DurationMs from stopwatch; release _currentStepSpanId=null
- [x] 4.6 Implement RecordPageAnalysis: create ExecutionRecord with Context = BuildCorrelation(), SpanId, SpanType=PageAnalysis, Depth from ctx
- [x] 4.7 Implement RecordActionExecution with typed (OperationType, Target?, bool) signature + SerializeTarget helper; ExecutionRecord with Context = BuildCorrelation(), TargetType, TargetValue
- [x] 4.8 Implement RecordAICallSpan typed: create AICallRecord with Context = BuildCorrelation()
- [x] 4.9 Implement RecordErrorSpan: create ErrorRecord with Context = BuildCorrelation()
- [x] 4.10 Implement RecordStateTransition: create StateTransition with Context = BuildCorrelation(), FsmType="TraversalFSM"
- [x] 4.11 Implement RecordRootNodePushed: create StateTransition with Context=null (before step loop), FsmType="TraversalFSM"
- [x] 4.12 Implement RecordSkipSpan → DfsForward: create ExecutionRecord with Context = BuildCorrelation(), ChildNodeId from matchResult
- [x] 4.13 Implement RecordPageTransition: create PageTransition with Context = BuildCorrelation()
- [x] 4.14 Implement RecordDynamicLifecycle → DfsForward: create ExecutionRecord with Context = BuildCorrelation(), ChildNodeId, ParentNodeId
- [x] 4.15 Implement RecordDecision and RecordStateDecision: create ExecutionRecord with Context = BuildCorrelation()
- [x] 4.16 Implement StepTraceSnapshot: _stepSpanTypes accumulation, GetStepSnapshot(), reset on read

## 5. TraversalEngine Integration

- [x] 5.1 Update TraversalEngine.RunAsync: create InMemoryTraceStorage + InMemoryTraceRecorder instead of direct ITraceRecorder
- [x] 5.2 Update TraversalEngine.Initialize(): pass ITraversalContext (ctx) to TraceCoordinator constructor
- [x] 5.3 Update StepOrchestrator call points for typed RecordActionExecution (OperationType, Target?, bool)
- [x] 5.4 Verify TraversalResult.Trace (TraceRecord) still populated correctly for ExpectedBehavior

## 6. Verification — Tests + Guards

- [x] 6.1 Write InMemoryTraceStorage tests: Add+Get, index correctness (byNodeId via Context?.NodeId, bySpanType), null Context not indexed, session lifecycle
- [x] 6.2 Write InMemoryTraceRecorder tests: async wrapper delegates to storage, StartSessionAsync creates session
- [x] 6.3 Write InMemoryTraceService tests: ReconstructTree from DfsForward edges (accessing Context?.NodeId), GetNodeSpans aggregates 5 types (filtering by Context?.NodeId), GetStepTimeline (filtering by Context?.StepNumber), GetStepSpanGroup (filtering by Context?.StepSpanId), GetNodeVisitTimeline, GetBySpanType
- [x] 6.4 Write TraceCoordinator fill tests: SpanId generation, StepSpanId lifecycle, BuildCorrelation() produces TraceContext, typed RecordActionExecution, RecordAICallSpan typed
- [x] 6.5 Update SpanType guard test (SpanType_Has11Values) — unchanged, verify still passes
- [x] 6.6 Add optional ITraceRecorder method count guard (7 methods only)
- [x] 6.7 Add TraceContext_Has4Fields guard test (verify exactly 4 properties)
- [x] 6.8 Run full test suite: 575 tests all green (229 original + 346 new)
