## Context

Phase 2.2 accumulated 3 categories of gaps:
- 4 constitution constraints (C-3, C-4, C-9, C-10) have no CI-enforced Guard tests — architecture rules are documentation-only conventions, not machine-verified
- Container/Error handlers are 3 independent sub-components without unified pipeline entry (D-16 deferred). PopupHandler has `HandlePopup()` but Container/Error require manual 3-step orchestration with no pipeline-level fallback
- Trace infrastructure lacks SpanType/PageTransition fields, blocking 2 ExpectedBehavior verification dimensions (D-E4: operation_rules, trace_integrity)

Full design details: `docs/refactor/19-phase22-refactoring-design.md`

## Goals / Non-Goals

**Goals:**
- CI-enforce 4 constitution constraints via Guard tests
- Add ContainerHandler.HandleContainer() and ErrorHandler.HandleError() as unified pipeline entry points with pipeline-level try/catch fallback
- Add SpanType enum, PageTransition record, ExecutionRecord.SpanType field, ITraceRecorder 2 new methods
- Extend ErrorRecoveryResult with Description field for pipeline fallback diagnostic info
- Update all documentation (constitution, handler-pipeline, decisions/log, locked-enums)

**Non-Goals:**
- TraceCoordinator pipeline refactoring (14 empty-shell methods stay unchanged)
- ExpectedBehavior operation_rules / trace_integrity verification logic implementation (fields added, verification logic is future change)
- TraversalFSM wiring to ContainerHandler/ErrorHandler (Phase 2.3 full implementation)
- P3 Domain items (ContentNode.ToMarkdown, TypeHint JsonPropertyName, etc.)

## Decisions

### D-G1: Guard test implementation — FindSourceRoot() + file scan pattern

Decision: All 4 Guard tests follow existing `FindSourceRoot()` + file scanning pattern in ArchitectureGuardTests.cs. New tests in 2 inner classes (NamespaceIsolationGuardTests, CodingConventionGuardTests).
Rationale: Consistent with existing EnumValueGuardTests and DependencyDirectionGuardTests. No new infrastructure needed.
Alternatives: (A) ArchUnitNET dependency analysis — requires new NuGet dependency, Phase 2.2 scope too small for framework adoption. (B) Roslyn Analyzer — Phase 3 target, too heavyweight for current phase. File scan is sufficient and matches existing pattern.

### D-G2: Handler wrapper — Option B (wrapper method + Engine wiring)

Decision: ContainerHandler and ErrorHandler as `sealed class` with HandleContainer()/HandleError() methods, optional constructor injection of sub-components, pipeline-level try/catch fallback. No IHandler<T> generic base.
Rationale: Correctness (3-step sequence encapsulated, no skip/reorder risk), design consistency (each handler has one entry like PopupHandler.HandlePopup()), simplicity (2 straightforward wrapper classes, no inheritance hierarchy). PopupHandler's StateRestorer lifecycle makes generic base low-payoff — Container/Error don't need preserve/restore, forcing them through abstract hooks creates dead paths.
Alternatives: (A) Wrapper methods only, no Engine wiring — dual calling paths (wrapper + manual 3-step), design debt. (C) Full pipeline pattern with IHandler<T> + PipelineOrchestrator base — 80 lines of new abstraction for 2 simple wrapper methods, PopupHandler overrides 60% of base due to StateRestorer lifecycle, payoff minimal.

### D-G3: ErrorRecoveryResult.Description field extension

Decision: Add `string? Description = null` to ErrorRecoveryResult. Pipeline fallback fills exception diagnostic info.
Rationale: ContainerActionResult and PopupHandlingResult both have Description. Without it, ErrorRecoveryResult pipeline fallback cannot preserve exception info. `string? = null` is backward compatible.
Alternatives: (B) Don't add Description, rely on ITraceRecorder for diagnostics — but trace is optional (not always active), and callers need inline failure reason without querying a separate system.

### D-G4: Container pipeline fallback Success=false vs executor fallback Success=true

Decision: Pipeline fallback returns `ContainerActionResult(Back, false, ...)`, executor fallback returns `ContainerActionResult(Back, true, ...)`. The Success difference is intentional.
Rationale: Executor fallback = "I executed BACK, known to work" (true). Pipeline fallback = "Pipeline crashed, BACK is safest guess" (false). PopupHandler has both layers returning false because Popup executor fallback also uses `false` ("back_fallback" is a fallback action, not a confirmed success). Container's executor DefaultBack is a *normal* action that works, so Success=true is correct.

### D-G5: ErrorRecoveryContext.RetryCount takes StrategySelectionContext.RetryCount (not ErrorClassificationContext)

Decision: HandleError constructs ErrorRecoveryContext using strategyCtx.RetryCount.
Rationale: StrategySelectionContext.RetryCount is authoritative — it directly participates in strategy decision (IsApplicable: RetryCount < MaxRetries) and backoff calculation (min(2^retryCount, 10)). ErrorClassificationContext.RetryCount is a noise field (ErrorClassifier never reads it).

### D-G6: SpanType 11 values (火山级 lock)

Decision: SpanType enum with 11 values: DfsForward, DfsBacktrack, RestoreOp, SkipDangerous, PopupHandling, ContainerHandling, ErrorHandling, PageAnalysis, CacheOp, AICall, StateDecision. Locked via constitution + Guard test.
Rationale: Covers operation_rules and trace_integrity classification dimensions from Python expected_behavior.yaml. Each value maps to a traceable semantic event type in the traversal lifecycle.

### D-G7: TraceCoordinator unchanged (14 empty shells stay)

Decision: TraceCoordinator's 14 empty-shell methods are NOT refactored in this change. SpanType/PageTransition fields are added to records only; TraceCoordinator methods remain stubs.
Rationale: TraceCoordinator refactoring is cross-cutting (touches StateMachine + Traversal + Simulation) and would significantly expand scope. Field additions unlock future verification without requiring pipeline changes now.

## Risks / Trade-offs

- [SpanType enum value lock may need adjustment before Phase 3] → Mitigation: constitution change flow (C-11 style) for any value additions. 11 values cover current needs; add via formal change if new span types emerge.
- [ContainerHandler.HandleContainer() has 4 parameters — more than PopupHandler's 3] → Mitigation: CompletionContext is a self-contained record (7 fields), not arbitrary parameters. nodeId + ITraversalContext are necessary because CompletionContext lacks these fields. Acceptable trade-off for correctness.
- [Pipeline fallback Success=false for Container differs from Popup's pattern] → Mitigation: Documented in D-G4 as intentional. Caller code should check Success field, not assume it.
- [No production code calls Container/Error sub-components currently — wrappers are "orphan" entry points until Phase 2.3 FSM wiring] → Mitigation: Wrapper tests validate pipeline behavior independently. FSM wiring is Phase 2.3 scope (explicitly excluded from this change).
