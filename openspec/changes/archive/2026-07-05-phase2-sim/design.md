# Design: Phase 2.3-sim — Simulation Infrastructure

## Context

Python `src/simulation/` is a proven simulation platform that runs real `GraphTraversalEngine` with mock `VisionService` + `OperationExecutor` injected. C# has the same architecture (real `TraversalFSM` + `StepOrchestrator`, injectable `IVisionProvider` + `IActionExecutor` via `StepContext`) but lacks stateful mock implementations. Phase 2.3a created simple `MockActionExecutor`/`MockVisionProvider` for unit tests, but these return hardcoded values and can't simulate page transitions or multi-step traversal.

Migration scope: core 3 components from Python (~855 lines Python → ~800 lines C#). Excludes `BehaviorValidator`, `ProblemDetector`, `ExpectedBehavior`, `SimulationRunner`, and scroll extensions — deferred to subsequent phases.

## Goals / Non-Goals

**Goals:**
- Enable end-to-end traversal testing without real ADB/AI/device
- Provide state-aware mock services that simulate page transitions
- Keep FSM, Context, and StepOrchestrator as real production code — only mock the I/O layer
- Zero new NuGet dependencies
- JSON fixture format for file-driven test scenarios, Fluent Builder for inline tests

**Non-Goals:**
- Full `SimulationRunner` orchestrator (deferred)
- `BehaviorValidator` / `ProblemDetector` (deferred)
- Scroll simulation / `ScrollableMockVisionService` (deferred)
- Real ADB/AI implementations of `IVisionProvider` / `IActionExecutor` (separate Phase)
- Modifying `TraversalFSM` handler logic beyond what 2.3a already delivered

## Decisions

### D-21: IVisionProvider — 2 methods, not 1

**Decision**: Expand from 1 placeholder method to 2 methods: `AnalyzeCurrentPageAsync` + `FindAppEntryAsync`.

**Rationale**: Python `VisionService` ABC has 3 methods. `get_current_page` is redundant with `analyze_screenshot` (PageAnalysis already contains path). `find_app_entry` is essential for HandlePreconditionCheck (locate target app in launcher). 2 methods is the minimal viable set.

**Alternatives**: (A) Keep 1 method, mock internally extends → violates interface segregation, handlers can't use FindAppEntry. (B) Copy Python's full 3-method ABC → adds unused abstraction.

### D-22: Simulation in Core library, not test project

**Decision**: Place `StateFixture`, `StatefulMockVisionService`, `StatefulMockActionExecutor` in `src/UniClaw.Core/Simulation/`.

**Rationale**: Aligns with Python (`src/simulation/` is production source). `StateFixture` is a data model, not a test double. Future `SimulationRunner` (in core) needs these types. The existing lightweight `MockActionExecutor`/`MockVisionProvider` remain in test project for simple unit tests.

### D-23: JSON + Fluent Builder dual-format

**Decision**: Support both JSON file deserialization and C# Fluent Builder for StateFixture construction.

**Rationale**: JSON for file-driven test scenarios (version-controlled, human-readable). Fluent Builder for inline test setup (strongly typed, IDE support). No YAML to avoid YamlDotNet dependency. Internal DTO pattern for `ImmutableDictionary`/`ImmutableArray` deserialization.

### D-24: ActionTarget on PageElement — JSON optional, runtime ignores

**Decision**: `PageElement.ActionTarget` is an optional field in JSON for human readability. Runtime only queries the `StateFixture._transitionIndex` (built from `Transitions`). Transition table is the single source of truth.

**Rationale**: Python `StateFixture` uses the same pattern — `action_target` on element is documentation, `_transition_index` is authoritative. Prevents data inconsistency between element-level and transition-level target references.

### D-25: StepOrchestrator.Step(ctx) — pass StepContext to FSM

**Decision**: Change StepOrchestrator line 41 from `ctx.StateMachine.Step()` to `ctx.StateMachine.Step(ctx)`.

**Rationale**: Without this, FSM handlers never receive `_currentStepContext`, so `HandleExecute` (2.3a) and future handlers always run in stub mode when called through the orchestrator. `Step(null)` is equivalent to `Step()` — non-breaking for existing callers.

### D-26: FindElementAt tolerance 0.05

**Decision**: Coordinate matching uses ±0.05 (5% of screen) tolerance.

**Rationale**: Coordinates from AI vision analysis have inherent imprecision. Fixture authors specify approximate coordinates. 0.05 tolerance catches intended targets without false matches. Python uses the same tolerance.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `BuildPageAnalysis` element type mapping may diverge from Python | Reference Python `_build_page_analysis` source; test coverage for all 8 element types |
| `ImmutableDictionary` JSON deserialization requires DTO layer | Internal DTO not exposed; maintenance burden is ~30 lines |
| `StepOrchestrator.Step(ctx)` change could affect 438 existing tests | `Step(null)` ≡ `Step()`; handlers have null-check stub fallback |
| `DynamicChildManager` may have uninitialized state in simulation | First pass only tests STATIC children; DYNAMIC_MATCH deferred |
| JSON fixture hand-authoring is error-prone | Provide `StateFixtureBuilder` as primary DX; JSON for persistence |
