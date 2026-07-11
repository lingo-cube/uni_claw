## Why

The current trace pipeline has a single ITraceRecorder interface mixing write (Record) and read (Get) responsibilities, flat record types with inadequate correlation keys (no NodeId, ChildNodeId, PageId, SpanId, StepSpanId), and a dead TraceNode hierarchy that was never populated. This blocks five high-value capabilities: node+operation tracking, FSM analysis, cacheable execution plan extraction, component performance profiling, and operation replay. The data model lacks explicit DFS tree edges (ChildNodeId), node-to-page mapping (PageId), per-step cross-type grouping (StepSpanId), and typed operation targets (TargetType+TargetValue replacing object? Target). Correlation fields are duplicated across 5 record types (4×5=20 parameters), mixing domain and observability concerns.

## What Changes

- **BREAKING**: ITraceRecorder slimmed from 13 to 7 methods — 5 Get methods, CurrentSession getter, and ExportTraceAsync moved to ITraceService/ITraceStorage
- **BREAKING**: Correlation fields (NodeId, StepSpanId, StepNumber, TraceId) removed from 5 record types and encapsulated into TraceContext sealed record class; each record gets TraceContext? Context = null instead of 4 separate parameters (4×5=20 → 1×5=5)
- **BREAKING**: ExecutionRecord.ParentNodeId semantics clarified as DFS tree parent for tree reconstruction (NOT "current node"); NodeId (event-at-node) moved into TraceContext
- **BREAKING**: ErrorRecord.ParentNodeId removed — replaced by TraceContext.NodeId with clarified semantics ("error at this node")
- **BREAKING**: ExecutionRecord.object? Target replaced by TargetType? + string? TargetValue (typed, queryable, cacheable)
- **BREAKING**: RecordActionExecution signature changed from (string, string, bool) to (OperationType, Target?, bool)
- Delete TraceNode hierarchy (SessionNode/StepNode/SpanNode) + UlidGenerator — dead code, 8 tests removed
- New TraceContext sealed record class (4 fields: NodeId, StepSpanId, StepNumber, TraceId) — observability correlation envelope shared by all 5 record types; field boundary rule prevents type-specific fields
- New ITraceStorage interface (13 methods) + InMemoryTraceStorage implementation (shared data backend with _byNodeId + _bySpanType indexes, using Context?.NodeId for index key)
- New ITraceService interface (1 property + 12 methods) + InMemoryTraceService implementation (pure read+query facade, queries use Context?.NodeId/StepNumber/StepSpanId)
- New InMemoryTraceRecorder (7 async wrapper methods over ITraceStorage — pure write)
- ExecutionRecord gains: SpanType? SpanType, TraceContext? Context, SpanId, ChildNodeId, ParentNodeId, PageId, TargetType, TargetValue, Depth
- StateTransition gains: TraceContext? Context, FsmType
- ErrorRecord gains: TraceContext? Context (replaces ParentNodeId + NodeId + StepSpanId + StepNumber + TraceId)
- PageTransition gains: TraceContext? Context, DurationMs
- AICallRecord gains: TraceContext? Context, Tokens
- TraceRecord gains: SpanTypes (ImmutableArray), PageFrom, PageTo, PageTransitionType, StepDurationMs
- TraceCoordinator gains: BuildCorrelation() helper (produces TraceContext from engine context), SpanId counter (_spanCounter), StepSpanId lifecycle (_currentStepSpanId), ITraversalContext reference, typed RecordActionExecution, StepTraceSnapshot
- 6 new query result types: TraversalTree, TreeEdge, NodeSpans, NodeVisitTimeline, StepTimeline, StepSpanGroup
- TraceContext_Has4Fields guard test prevents accidental field addition

## Capabilities

### New Capabilities
- `trace-storage`: ITraceStorage interface + InMemoryTraceStorage implementation with flat lists + indexes (index keys via Context?.NodeId)
- `trace-service`: ITraceService interface + InMemoryTraceService with 6 Node+Span query methods (ReconstructTree, GetNodeSpans, GetNodeVisitTimeline, GetStepTimeline, GetBySpanType, GetStepSpanGroup — all using Context access pattern)
- `trace-coordinator-fill`: TraceCoordinator real logic for all 12 previously-empty methods, BuildCorrelation() producing TraceContext, SpanId/StepSpanId generation, typed signatures, ITraversalContext reference

### Modified Capabilities
- `trace-foundation`: ITraceRecorder slimmed from 13→7 methods; TraceContext sealed record class created; ExecutionRecord, StateTransition, ErrorRecord, PageTransition, AICallRecord field extensions via TraceContext encapsulation; TraceNode+UlidGenerator deleted
- `trace-record`: TraceRecord gains 5 new fields (SpanTypes, PageFrom, PageTo, PageTransitionType, StepDurationMs)

## Impact

- **Observability/**: ITraceRecorder.cs restructured (record type definitions moved, interface slimmed); new TraceContext.cs, ITraceStorage.cs, ITraceService.cs, TraceQueryResults.cs files
- **Trace/**: TraceNode.cs + UlidGenerator.cs deleted
- **Traversal/**: TraversalEngine.cs (TraceCoordinator inline class refactored with BuildCorrelation + TraceContext, RunAsync storage creation changed); TraceRecord.cs extended
- **Tests**: 8 tests deleted (TraceNode+UlidGenerator); new tests for Storage indexes, Service queries (using Context access pattern), TraceCoordinator fill; Guard tests updated (TraceNode guard deleted, TraceContext_Has4Fields added, SpanType guard unchanged, possible new ITraceRecorder method count guard)
- **Dependency**: ExecutionRecord now references Domain.Common.TargetType (Observability→Domain downward reference, allowed per D-17)
- **API**: ITraceRecorder interface contract changed (13→7 methods) — any external consumer must migrate reads to ITraceService; all 5 record types changed from explicit correlation fields to TraceContext? Context
