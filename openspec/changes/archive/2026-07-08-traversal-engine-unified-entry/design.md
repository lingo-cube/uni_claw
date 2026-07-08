## Context

Traversal-related components are split across 3 namespaces:
- **StateMachine**: TraversalFSM, TraversalRuntimeContext, TraversalState enums, empty `IGraphTraversalEngine` stub
- **Traversal**: StepOrchestrator, StepContext, DynamicChildManager, full `IGraphTraversalEngine` interface, TraversalEngine helpers
- **Simulation**: SimulationRunner, SimulationResult, SimulationConfig, StateFixture, StatefulMock*

Python provides a single `GraphTraversalEngine(plan, vision, action).run()` entry point. C# callers must manually construct a registry, register nodes, build root, and instantiate a SimulationRunner. The empty `IGraphTraversalEngine` stub in StateMachine namespace exists only to avoid circular dependency (D-14), causing `HasUnvisitedChildren` to always receive null and be dead code.

Constraints from constitution/constraints.md:
- C-4: Dual FSM independence — no shared state between TraversalFSM and GlobalFSM
- C-5: Dependency direction — Graph→StateMachine is the only allowed upward reference (but D-17 already acknowledges StateMachine→Observability as cross-cutting)
- C-7: GlobalState 8 values locked
- P-3: ITraversalContext readonly isolation
- P-5: sealed record class for immutable data types
- P-6: DomainValidationException for all validation

## Goals / Non-Goals

**Goals:**
- Single `TraversalEngine(plan, vision, action).Run()` / `RunAsync()` entry point aligning with Python `GraphTraversalEngine`
- Plan-driven initialization: constructor compiles TraversalPlan → node tree + registry + FSM + orchestrator
- Simulation/production unified: inject mock → simulation; inject real → production; engine unchanged
- Structured trace output via `TraceRecord[]` in `TraversalResult`
- Merge SimulationRunner logic into TraversalEngine; delete SimulationRunner, SimulationResult, SimulationConfig
- Resolve D-14: delete empty `IGraphTraversalEngine` stub; acknowledge StateMachine→Traversal upward reference
- Result/Config type unification: `TraversalResult` replaces old version + `SimulationResult`; `TraversalEngineConfig` replaces `SimulationConfig`

**Non-Goals:**
- No changes to TraversalFSM / StepOrchestrator / TraversalRuntimeContext internal implementations
- No changes to IVisionProvider / IActionExecutor interface definitions
- No BehaviorValidator / ProblemDetector implementation (Phase 2.4)
- No Scroll simulation
- No GlobalFSM concrete class (Phase 3)
- No PauseAsync/ResumeAsync complete logic (Phase 3 stubs only)

## Decisions

### D-1: Constructor calls Initialize() (fail-fast)
**Choice**: Constructor directly calls `Initialize()` which compiles Plan and sets up all internal components. Constructor can throw.
**Alternative**: Separate `InitializeAsync()` method requiring two-step init.
**Rationale**: C# constructors throwing is standard practice. Fail-fast at construction is preferred over deferred initialization that can silently produce broken objects. `InitializeAsync()` remains on `IGraphTraversalEngine` as a no-op validation check (contract alignment), not actual initialization.

### D-2: sealed class (not record) for TraversalEngine
**Choice**: `sealed class TraversalEngine` with 4 mutable internal fields (`_ctx`, `_fsm`, `_stepCtx`, `_orchestrator`).
**Rationale**: Records imply value semantics and structural equality. TraversalEngine has mutable state across its lifetime — same exception as TraversalRuntimeContext (P-5 carve-out). Equality semantics would be meaningless.

### D-3: SimulationRunner logic fully absorbed, not preserved as facade
**Choice**: Delete SimulationRunner.cs entirely. All logic (leaf-pop, child-push→NodeSelect, page-visit tracking, frame-completion, termination) lives in `TraversalEngine.RunAsync()`.
**Alternative**: Keep SimulationRunner as thin facade over TraversalEngine for backward compatibility.
**Rationale**: Backward compat not needed — this is Phase 2 internal API. Two entry points would confuse callers. SimulationRunner's dependencies (StateFixture, mock creation) are externalized to callers who inject IVisionProvider/IActionExecutor.

### D-4: StateMachine→Traversal upward reference acknowledged (D-14 resolution)
**Choice**: Delete empty `IGraphTraversalEngine` stub in StateMachine namespace. `HasUnvisitedChildren` parameter uses `UniClaw.Core.Traversal.IGraphTraversalEngine`. Architecture guard whitelists this upward reference.
**Alternative**: Keep stub, extract interface to shared namespace, or use adapter pattern.
**Rationale**: The stub is dead code — `HasUnvisitedChildren` always receives null. D-17 already acknowledges upward references to cross-cutting utilities. Traversal is consumed by StateMachine (FSM needs engine for visited-children queries); hiding this via stub doesn't eliminate the dependency, just obscures it. Explicit whitelist > hidden stub.

### D-5: SimpleNodeRegistry → DictionaryNodeRegistry, moves to Traversal namespace
**Choice**: Rename `SimpleNodeRegistry` to `DictionaryNodeRegistry`, move from Simulation namespace to Traversal namespace.
**Rationale**: TraversalEngine.CompilePlan() needs an INodeRegistry implementation. Traversal→Simulation dependency is wrong direction. DictionaryNodeRegistry is a generic dictionary-backed registry, not simulation-specific.

### D-6: TraversalResult replaces both old TraversalResult and SimulationResult
**Choice**: New sealed record class `TraversalResult` with structured fields (CompletionReason, Trace, FinalState, etc.). Old `IGraphTraversalEngine.cs` TraversalResult (using HashSet + Dictionary) deleted. SimulationResult.cs deleted.
**Rationale**: Two result types for the same conceptual output (simulation vs real) is unnecessary. The engine is unified; the result type should be unified. Old TraversalResult violated P-4/P-5 (HashSet exposed, mutable collections).

### D-7: Run() sync convenience via GetAwaiter().GetResult()
**Choice**: `Run()` wraps `RunAsync()` via `.GetAwaiter().GetResult()`.
**Rationale**: Matches Python's synchronous `engine.run()` for CLI/test environments. Deadlock risk exists only in ASP.NET/WinForms/WPF SynchronizationContext environments — documented with ⚠️ warning. CLI and xUnit test runners have no SynchronizationContext, so safe.

### D-8: TraceRecord independent from ITraceRecorder
**Choice**: `TraceRecord[]` in TraversalResult is in-memory per-step trace. `ITraceRecorder` via TraceCoordinator handles external persistence. Two systems are independent.
**Rationale**: In-memory trace serves immediate debugging and test assertions. External trace serves long-term storage and dashboard visualization. Coupling them would force TraceRecord to carry ITraceRecorder semantics (session IDs, span nesting) that don't belong in a result object.

## Risks / Trade-offs

- **[Run() deadlock risk]** → Mitigation: documented ⚠️ warning; only used in CLI/test contexts (no SynchronizationContext). Phase 3 may add `Task.Run()` wrapper for UI contexts.
- **[Breaking API change: SimulationRunner deleted]** → Mitigation: Phase 2 internal API, not public release. Migration path documented (test code changes SimulationRunner→TraversalEngine construction). Tests are updated in same commit.
- **[C-5 upward dependency now visible]** → Mitigation: Guard test whitelists explicitly. D-17 precedent established. This is not loosening a constraint — it's acknowledging an already-existing dependency that was hidden via stub.
- **[CompilePlan() assumes StaticNodes.Keys are root children]** → Mitigation: BuildDefaultRoot uses `StaticNodes.Keys` as child IDs, which is correct for flat plans but may be wrong for nested ones. PlanCompiler.BuildRootNode() handles nesting correctly. RootNode from plan (preferred path) is always correct. BuildDefaultRoot is fallback for minimal plans only.
- **[StepOrchestrator.ExecuteStep() is synchronous but RunAsync() is async]** → Mitigation: StepOrchestrator doesn't call async services directly — it delegates to handlers. The async boundary is at RunAsync() level (DelayPerStepMs, trace recording). This is consistent with existing StepOrchestrator design.
