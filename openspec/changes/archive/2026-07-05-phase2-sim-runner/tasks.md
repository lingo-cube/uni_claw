# Tasks: Phase 2.3-sim-runner

## 1. SimulationConfig + SimulationResult

- [x] 1.1 Create `SimulationConfig` record class (MaxSteps=1000, MaxDepth=10, ThrowOnError=false, SimulateDelayMs=0)
- [x] 1.2 Create `SimulationResult` record class (Success, CompletionReason, TotalSteps, ElapsedSeconds, ActionHistory, VisitedPages, FinalState, Error) with `Reasons` constants

## 2. SimulationRunner

- [x] 2.1 Create `SimulationRunner` class skeleton with private fields
- [x] 2.2 Implement constructor — create mock services, real Context/FSM, assemble StepContext, store StepOrchestrator
- [x] 2.3 Implement `Run()` — while loop calling `_orchestrator.ExecuteStep(_stepCtx)`, page tracking, delay simulation
- [x] 2.4 Implement termination logic — FrameCompleted+depth≤1, AntiLoopTriggered, MaxSteps, exception
- [x] 2.5 Implement `Done()` helper — build SimulationResult

## 3. Test Refactor

- [x] 3.1 Refactor existing E2E tests to use SimulationRunner — replace manual FSM driving (~80 lines → 0)
- [x] 3.2 Write `Runner_2PageCompletes` — verify 2-page fixture via Runner
- [x] 3.3 Write `Runner_MaxStepsExceeded` — verify max step limit
- [x] 3.4 Write `Runner_EmptyNodeTree` — verify immediate completion for root-only traversal
- [x] 3.5 Verify all 489 existing tests still pass

## 4. Documentation

- [x] 4.1 Update `docs/system/layers/simulation.md` — add SimulationRunner / SimulationConfig / SimulationResult to type inventory
