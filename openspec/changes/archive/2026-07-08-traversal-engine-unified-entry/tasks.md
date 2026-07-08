## 1. Type Definitions (New Files)

- [x] 1.1 Create `TraversalResult.cs` in Traversal namespace — sealed record class with Success, CompletionReason (Reasons constants), TotalSteps, ElapsedSeconds, ActionHistory, VisitedPages, Trace, TraceId, FinalState, Error fields; nested static Reasons class with 5 const strings
- [x] 1.2 Create `TraceRecord.cs` in Traversal namespace — sealed record class with StepNumber, FromState, ToState, CurrentNodeId, CurrentPageId, ActionExecuted, ActionSuccess, ChildPushed, FrameCompleted fields
- [x] 1.3 Create `TraversalEngineConfig.cs` in Traversal namespace — sealed record class with init-only properties: MaxSteps=1000, MaxDepth=10, ThrowOnError=false, TraceEnabled=true, DelayPerStepMs=0
- [x] 1.4 Move and rename `SimpleNodeRegistry.cs` from Simulation namespace to Traversal namespace as `DictionaryNodeRegistry.cs` — rename class, update namespace, keep INodeRegistry implementation unchanged

## 2. D-14 Resolution (Stub Cleanup + Dependency Acknowledgment)

- [x] 2.1 Delete empty `IGraphTraversalEngine` stub from `TraversalState.cs` lines 152-155 (the `public interface IGraphTraversalEngine {}` in StateMachine namespace)
- [x] 2.2 Update `TraversalFSM.cs` — change `HasUnvisitedChildren` parameter type from StateMachine stub to `UniClaw.Core.Traversal.IGraphTraversalEngine`; add `using UniClaw.Core.Traversal;`
- [x] 2.3 Update `ITraversalStateMachine` interface in `TraversalState.cs` — change `HasUnvisitedChildren` parameter type to `UniClaw.Core.Traversal.IGraphTraversalEngine`; add `using UniClaw.Core.Traversal;`
- [x] 2.4 Update `ArchitectureGuardTests.cs` — whitelist StateMachine→Traversal and StateMachine→Observability as acknowledged upward references (consistent with D-17)

## 3. IGraphTraversalEngine Interface Update

- [x] 3.1 Update `IGraphTraversalEngine.cs` in Traversal namespace — remove old `TraversalResult` record (GlobalState Status, HashSet violations); update `RunAsync()` return type to new TraversalResult; keep 8-member interface unchanged otherwise

## 4. TraversalEngine Implementation

- [x] 4.1 Create `TraversalEngine.cs` in Traversal namespace (replacing existing helper-only file) — sealed class implementing IGraphTraversalEngine with constructor(plan, vision, action, config?, traceRecorder?) calling Initialize()
- [x] 4.2 Implement `Initialize()` — 7-step setup: create TraversalRuntimeContext, CompilePlan(), push root, create TraversalFSM, assemble StepContext, create StepOrchestrator, set GlobalState=Traversing
- [x] 4.3 Implement `CompilePlan()` — create DictionaryNodeRegistry, register StaticNodes, determine root (plan.RootNode ?? BuildDefaultRoot), ensure root registered
- [x] 4.4 Implement `BuildDefaultRoot(string entryApp)` — minimal Container root from EntryApp + StaticNodes.Keys
- [x] 4.5 Implement `RunAsync(CancellationToken ct)` — core loop with step iteration, DelayPerStepMs, ExecuteStep(), leaf-pop, child-push→NodeSelect, trace recording, page-visit tracking, termination checks, exception handling (Log-and-Continue: never throw)
- [x] 4.6 Implement `Run()` — sync convenience wrapper via `.GetAwaiter().GetResult()` with documented ⚠️ deadlock warning
- [x] 4.7 Implement IGraphTraversalEngine stubs — InitializeAsync() → CompletedTask, PauseAsync() → Paused, ResumeAsync() → Traversing, StopAsync() → Terminated, GetStateAsync() → ctx.GlobalState, Plan/Context/CurrentState properties
- [x] 4.8 Implement `Done()` helper — map CompletionReason→GlobalState, construct TraversalResult with all fields

## 5. Simulation Cleanup (Deletions)

- [x] 5.1 Delete `SimulationRunner.cs` — all logic migrated into TraversalEngine.RunAsync()
- [x] 5.2 Delete `SimulationResult.cs` — merged into TraversalResult
- [x] 5.3 Delete `SimulationConfig.cs` — merged into TraversalEngineConfig
- [x] 5.4 Verify StateFixture.cs, StatefulMockVisionService.cs, StatefulMockActionExecutor.cs remain in Simulation namespace (still needed for test construction)

## 6. Test Migration

- [x] 6.1 Migrate `SimulationE2ETests.cs` — replace SimulationRunner construction with TraversalEngine(plan, vision, action) construction; build TraversalPlan from nodes instead of manually creating SimpleNodeRegistry
- [x] 6.2 Add TraversalEngine unit tests — constructor validation, Initialize(), CompilePlan(), RunAsync() termination scenarios (AllVisited, AntiLoop, MaxSteps, Error, Cancelled), Run() sync wrapper, Done() helper
- [x] 6.3 Add TraceRecord unit tests — construction, field verification, serialization
- [x] 6.4 Add TraversalEngineConfig unit tests — defaults, custom values, DelayPerStepMs behavior
- [x] 6.5 Add TraversalResult unit tests — construction, Reasons constants, Success mapping, Error field
- [x] 6.6 Verify all 229+ tests pass after migration (dotnet test)

## 7. Documentation Updates

- [x] 7.1 Update `docs/system/layers/traversal.md` — add TraversalEngine, TraversalResult, TraceRecord, TraversalEngineConfig, DictionaryNodeRegistry to type inventory
- [x] 7.2 Update `docs/system/layers/simulation.md` — mark SimulationRunner/SimulationResult/SimulationConfig as deleted; note remaining StateFixture/mock services
- [x] 7.3 Update `docs/system/constitution/constraints.md` — C-5 update: explicitly acknowledge StateMachine→Traversal and StateMachine→Observability as upward references (not design defects, consistent with D-17)
- [x] 7.4 Update `docs/system/decisions/log.md` — D-14 marked as resolved (empty stub deleted, upward reference acknowledged)
