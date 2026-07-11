## Context

The trace pipeline currently has a monolithic ITraceRecorder interface (13 methods mixing write + read + export), flat record types with correlation gaps (no NodeId/ChildNodeId/PageId/SpanId/StepSpanId), and a dead TraceNode hierarchy. The detailed design is in `docs/refactor/20-trace-pipeline-design.md` — this document captures the architectural decisions and rationale.

Current state: TraceCoordinator has 16 method stubs (15 empty lambdas + 1 real implementation). ExecutionRecord has `object? Target` (untyped). StateTransition/AICallRecord have no correlation to node/step/session. DFS tree reconstruction is impossible because DfsForward records don't capture which child was pushed. Correlation fields are duplicated across 5 record types (4×5=20 parameters), mixing domain and observability concerns.

## Goals / Non-Goals

**Goals:**
- Refactor into three-layer architecture: ITraceStorage (shared backend) → ITraceRecorder (write) → ITraceService (read+query)
- Encapsulate 4 common correlation fields into TraceContext sealed record class (4×5=20 → 1×5=5 parameters)
- Extend all 5 record types with TraceContext? Context instead of separate correlation parameters
- Add SpanId (unique per ExecutionRecord) and StepSpanId (per-engine-step grouping via TraceContext)
- Replace object? Target with typed TargetType+TargetValue on ExecutionRecord
- Make DFS tree reconstruction explicit via ChildNodeId
- Provide 6 basic query methods (ReconstructTree, GetNodeSpans, GetNodeVisitTimeline, GetStepTimeline, GetBySpanType, GetStepSpanGroup)
- Delete TraceNode hierarchy + UlidGenerator (dead code)
- Fill TraceCoordinator's 12 empty methods with real logic via BuildCorrelation() producing TraceContext
- Add TraceContext_Has4Fields guard test to prevent field boundary violations

**Non-Goals:**
- FsmAnalysis, ExecutionPlanDigest, PerformanceProfile queries (Phase 3)
- ReplayExecutor, ReplayScript, StateFixture auto-build (Phase 3)
- GlobalFSM callback writing to ITraceRecorder (Phase 3 — engine code change)
- VisitSpanId (per-node-visit, spanning multiple steps) — Phase 3 (will be added to TraceContext)
- ParentSpanId (span causality tree) — Phase 3 (will be added to TraceContext)
- AICallRecord.SpanId — Phase 3 (alongside ParentSpanId)
- Async ITraceStorage (current sync suffices for in-memory)

## Decisions

### D1: TraceContext encapsulates common correlation fields

**Choice**: Extract 4 common correlation fields (NodeId, StepSpanId, StepNumber, TraceId) from 5 record types into TraceContext sealed record class. Each record gets `TraceContext? Context = null` instead of 4 separate parameters.

**Alternatives considered**:
- A: Keep explicit fields on each record type (4×5=20 parameters) — mixed domain+trace, verbose, Phase 3 changes impact 5 types
- B: Put correlation in Metadata dictionary — loses type safety, untyped Dictionary<string, object>, violates project philosophy
- C: Base record inheritance — C# sealed records can't inherit; project philosophy requires sealed record class
- D: TraceContext nested record — ✅ chosen: core domain fields stay clean, trace correlation optional (Context=null), DRY (4×5→1×5), Phase 3 extension adds VisitSpanId/ParentSpanId to TraceContext only (5 record types unchanged)

**Rationale**: TraceContext answers "when/where/how was this event recorded" — observability correlation, not core domain data. StateTransition's core is From→To, not NodeId/StepSpanId. Separation of concerns. TraceCoordinator fills Context via `BuildCorrelation()` in one call instead of 4 separate fields. Phase 3 VisitSpanId+ParentSpanId go into TraceContext (general correlation — all types need them), no record type changes needed.

**Field boundary rule**: TraceContext contains ONLY fields shared by ALL 5 types. Type-specific fields (FsmType, SpanId, ChildNodeId, ParentNodeId, PageId, TargetType/TargetValue, Depth, DurationMs, Tokens) stay on their respective record types.

### D2: Three-layer architecture (ITraceStorage + ITraceRecorder + ITraceService)

**Choice**: Separate storage, write, and read into three independent interfaces.

**Alternatives considered**:
- A: Single ITraceRecorder with all methods (current 13-method monolith) — mixed write+read contract, no CQRS separation
- B: Split into ITraceRecorderWriter + ITraceRecorderReader (two sub-interfaces) — Recorder implementation still stores data and provides reads; no clean separation
- C: Three-layer (Storage + Recorder + Service) — ✅ chosen: Storage is shared backend, Recorder is pure async-over-sync write wrapper, Service is pure read+query. No component needs both write and read.

**Rationale**: CQRS at the interface level. TraceCoordinator only writes (injects ITraceRecorder). Analysis only reads (injects ITraceService). The shared ITraceStorage backend decouples them — swap storage without changing either consumer.

### D2b: InMemoryTraceService injects InMemoryTraceStorage (concrete), not ITraceStorage (interface)

**Choice**: Asymmetric injection — Recorder injects interface, Service injects concrete class.

**Alternatives considered**:
- A: Both inject ITraceStorage — Service can't access index methods (GetByNodeId, GetBySpanType) because they're not on the interface
- B: Put index methods on ITraceStorage interface — forces all future implementations (DatabaseTraceStorage, FileTraceStorage) to provide index queries, violating ISP
- C: Service injects InMemoryTraceStorage concrete — ✅ chosen: Service gets index access. Different storage backends have different query strategies (SQL for DB, scan for file). Index methods belong on the concrete class, not the interface.

**Rationale**: ISP principle — not all ITraceStorage implementations need memory indexes. Each ITraceService implementation pairs with its specific storage (InMemoryTraceService↔InMemoryTraceStorage, future DatabaseTraceService↔DatabaseTraceService with SQL queries).

### D3: StepSpanId (per-step) not VisitSpanId (per-node-visit)

**Choice**: StepSpanId assigned at RecordStepStart, released at RecordStepEnd. Semantics = per-engine-step grouping. StepSpanId lives in TraceContext (shared by all 5 record types).

**Alternatives considered**:
- A: VisitSpanId per-node-visit — requires TraceCoordinator to detect NodeId changes across steps (push child → new VisitSpanId, backtrack → restore old VisitSpanId). Complex node lifecycle tracking in TraceCoordinator.
- B: StepSpanId per-engine-step — ✅ chosen: simple lifecycle (assign at StepStart, release at StepEnd). TraceCoordinator only needs _currentStepSpanId state. Node visit grouping computed at query time from NodeId + StepNumber range.

**Rationale**: Per-step is what TraceCoordinator actually implements (StepStart→StepEnd lifecycle). Naming matches implementation. Phase 3 will add VisitSpanId as a separate field in TraceContext when TraceCoordinator is upgraded with node lifecycle tracking. StepSpanId stays (it's a useful independent concept).

### D4: StepSpanId = StepStart's SpanId

**Choice**: When RecordStepStart generates a SpanId, that SpanId also becomes _currentStepSpanId. "The step's grouping key = the step's first record's unique key." Both StepSpanId and StepNumber are in TraceContext.

**Rationale**: Enables direct lookup: find the StepStart record by matching SpanId == StepSpanId. Avoids two separate counter values for the same conceptual event (step start).

### D5: TargetType + TargetValue replacing object? Target

**Choice**: ExecutionRecord gets `TargetType?` (Domain.Common enum) + `string? TargetValue` instead of `object? Target`. TargetType and TargetValue are type-specific fields on ExecutionRecord (NOT in TraceContext — only action executions have targets).

**Alternatives considered**:
- A: Keep object? Target — untyped, can't query/filter, can't cache, serialization messy
- B: TargetType enum + TargetValue string — ✅ chosen: type-safe classification, human-readable value, queryable (filter all Coordinate clicks), cacheable (page+action+target→result page)
- C: Typed Target record (same as Domain.Common.Target) — creates Domain dependency on every ExecutionRecord construction; Target.Value is object? which has same problems

**Rationale**: TargetType enum gives compile-time safety for classification. TargetValue string gives queryability + cacheability. Back/NoAction have TargetType=null (nullable handles "no target" naturally). Serializes cleanly to JSON. Allows ExecutionPlanDigest (Phase 3) to extract "wifi_settings + click(100,200) → connected_page". TargetType is ExecutionRecord-specific, not shared by all 5 types — stays on ExecutionRecord, not in TraceContext.

### D6: ITraceStorage write methods are synchronous

**Choice**: AddExecution/AddTransition/etc. are synchronous void methods. ITraceRecorder wraps with Task.CompletedTask for async contract.

**Rationale**: In-memory operations are always synchronous. The async layer is on ITraceRecorder (consumer contract), not ITraceStorage (internal mechanism). Future async storage implementations will use a separate IAsyncTraceStorage interface (YAGNI now).

### D7: ErrorRecord.ParentNodeId removed — replaced by TraceContext.NodeId

**Choice**: ErrorRecord previously had a `ParentNodeId` field with ambiguous semantics ("error at this node" vs "DFS parent"). With TraceContext encapsulation, this field is removed. Context.NodeId provides "error occurred at this node" semantics with clear documentation.

**Rationale**: ErrorRecord.ParentNodeId was a naming confusion (same issue as ExecutionRecord.ParentNodeId audit item #11). For ErrorRecord, the field actually meant "the node where this error happened" — which is exactly what TraceContext.NodeId represents. Removing the field and using Context.NodeId eliminates ambiguity. ExecutionRecord.ParentNodeId stays because it genuinely means "DFS tree parent for tree reconstruction" — a different concept from "event-at-node".

## Risks / Trade-offs

- **[ITraceRecorder breaking change]** 13→7 methods removes Get + CurrentSession + Export. No current consumer uses these methods (ExpectedBehavior uses TraversalResult.Trace, not ITraceRecorder). → Mitigation: verified zero production callers of removed methods.

- **[TraceContext encapsulation breaking change]** All 5 record types change from 4 separate correlation parameters to TraceContext? Context. Existing code constructing records with NodeId=..., StepSpanId=... etc. must be updated to use Context=new TraceContext(...). → Mitigation: all new fields are optional (default null); TraceCoordinator is the sole record constructor via LogAndContinue; existing tests only need to add Context=null defaults.

- **[ErrorRecord.ParentNodeId removal]** Removing ParentNodeId from ErrorRecord changes the record's field set. Any code that set ErrorRecord.ParentNodeId must now use Context.NodeId. → Mitigation: TraceCoordinator is the sole ErrorRecord constructor. The field semantics are clarified: "error at this node" (Context.NodeId), not "DFS parent".

- **[StepSpanId ≈ StepNumber redundancy]** For this iteration, StepSpanId and StepNumber provide equivalent per-step grouping. → Acceptable: StepSpanId is an independent concept that will diverge from StepNumber in Phase 3 (VisitSpanId spans multiple steps). The redundancy is temporary and intentional.

- **[RecordRootNodePushed Context=null]** Called before engine step loop, no BuildCorrelation() available. → Acceptable: root push is a special case. Documented in mapping table.

- **[ExecutionRecord→Domain.Common.TargetType dependency]** New cross-layer reference from Observability to Domain. → Allowed per D-17 (Observability is cross-cutting). Downward reference direction is correct. No Guard test needed.

- **[TraceContext field boundary enforcement]** Without a guard test, type-specific fields could accidentally be added to TraceContext. → Mitigation: TraceContext_Has4Fields guard test in ArchitectureGuardTests.cs.
