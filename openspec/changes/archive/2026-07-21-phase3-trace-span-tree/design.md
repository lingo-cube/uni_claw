## Context

C-9/C-10/C-8 (trace-collection-completion, 42/42 tasks, 833 tests) completed the trace collection pipeline. Three gaps remain:

1. **No span-tree correlation**: TraceContext has 4 fields, cannot express parent-child span relationships across the 5 record types
2. **HandlerTraceWriter Context=null**: Handler lifecycle ExecutionRecords have no TraceContext — NodeId/StepSpanId/StepNumber/TraceId are all null
3. **6 manual trace injection points**: Repeated RecordHandlerLifecycleAsync boilerplate in orchestration layer

Full design: `docs/refactor/2026-07-21-phase3-trace-span-tree-design.md`

## Goals / Non-Goals

**Goals:**
- Phase 3-A: Extend TraceContext from 4 to 6 fields (+VisitSpanId, +ParentSpanId); add SpanStack push/pop mechanism; fix HandlerTraceWriter Context gap
- Phase 3-B: Deploy Roslyn incremental source generator that scans [TraceHandler], generates async wrappers with auto-extracted metadata + extraMetadata merge; replace 3 handler manual injection points

**Non-Goals:**
- ❌ AsyncLocal-based ParentSpanId propagation (Stack-based confirmed in brainstorming)
- ❌ ITraceHandlerMetadata interface on result types (方案 D auto-extract confirmed — keeps handlers pure)
- ❌ DfsBacktrack 3 insertion points (if-block conditional — unsuitable for source generator)
- ❌ IVisionProvider/RecordAICallSpanAsync automation (async already, unused in production)
- ❌ GetByVisitSpanId/GetSpanChildren query methods (Phase 3-C)
- ❌ Span-tree visualization (Phase 3-C)

## Decisions

| # | Decision | Alternatives Rejected | Rationale |
|---|----------|----------------------|-----------|
| D-3A-1 | Stack-based ParentSpanId: TraceCoordinator `_spanStack`, PushSpan/PopSpan, BuildCorrelation reads stack top | AsyncLocal environment propagation | Consistent with existing explicit mutable state pattern (_currentStepSpanId). Inspectable in debugger. Immune to Task.Run/ConfigureAwait boundary issues. Manual push/pop eliminated in Phase 3-B |
| D-3A-2 | HandlerTraceWriter explicit TraceContext parameter | Constructor injection of ITraversalContext | Stateless — keeps HandlerTraceWriter testable with null context. Mirrors existing ITraceRecorder.RecordExecutionAsync pattern |
| D-3B-1 | 方案 D: Source generator auto-extracts return type properties → metadata + extraMetadata dictionary | A: ITraceHandlerMetadata interface, B: Attribute field mapping, C: Callback lambda | Keeps handlers pure (result types don't depend on Observability). Compile-time property extraction (zero runtime reflection). Cross-source fields merged via 1-line dictionary |
| D-3B-2 | Source generator emits async wrapper (original stays sync) | Generate full method body replacement | Thin wrapper delegates to original — safer, easier to reason about. Rollback = remove [TraceHandler] |
| D-3B-3 | TraceIgnoreAttribute for property exclusion | No exclusion mechanism | Conservatively include all properties by default, explicit opt-out. Phase 3 future: [TraceName("key")] for key customization |
| D-3B-4 | 3 handler pipeline methods only | All 6 manual injection points | DfsBacktrack 3 points are if-block conditional — unsuitable for method-level source generation |

## Risks / Trade-offs

- [Risk] SpanStack push/pop mismatch → stack corruption, wrong ParentSpanId on subsequent spans → Phase 3-B generated try/finally guarantees pop; manual calls validate pop matches push via spanId equality check
- [Risk] Source generator compile-time dependency adds build complexity → Separate netstandard2.0 project, analyzer reference only (not library reference), no runtime dependency
- [Risk] Auto-extracted metadata may include fields caller didn't want → [TraceIgnore] opt-out; extraMetadata overrides auto-extracted keys in merge
- [Risk] Existing 4-field JSONL traces break on deserialization → New fields default to null; STJ positional record deserialization with defaults handles missing keys gracefully

## Migration Plan

```
Phase 3-A:
  1. TraceContext: 4→6 fields (new fields default null)
  2. TraceCoordinator: +SpanStack + PushSpan/PopSpan/ClearVisitSpan
  3. HandlerTraceWriter: +TraceContext parameter
  4. Guard: TraceContext_Has6Fields
  5. 833 tests green (no behavioral change)

Phase 3-B:
  1. Deploy SourceGen project (zero output — no [TraceHandler] yet)
  2. Decorate ErrorHandler + mark partial → generator emits wrapper
  3. TraversalFSM switches to HandleErrorTracedAsync
  4. Repeat for PopupHandler, ContainerHandler
  5. Clean up: remove manual RecordHandlerLifecycleAsync from orchestration
  6. 833+ tests green throughout (manual + generated coexist)
```

**Rollback**: Remove [TraceHandler], revert orchestration call site to manual pattern. Generated code disappears at next compilation.
