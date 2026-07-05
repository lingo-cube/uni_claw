# Design: Phase 2.3-sim-runner

## Context

Phase 2.3-sim delivered StateFixture + StatefulMockVisionService + StatefulMockActionExecutor + SimpleNodeRegistry. E2E tests manually drive FSM transitions and node lifecycle. `StepOrchestrator.ExecuteStep(ctx)` already handles all inter-step logic (BRANCH interception, child push, visited marking, anti-loop, frame-override). A runner is a thin loop around `ExecuteStep`.

## Goals / Non-Goals

**Goals:**
- Eliminate manual FSM state management in E2E tests (~80 lines → 0)
- Provide `SimulationRunner` that drives `StepOrchestrator.ExecuteStep(ctx)` in a loop
- Terminate on `FrameCompleted + stack depth ≤ 1` (all_visited) or `AntiLoopTriggered` or `MaxSteps`
- Collect `SimulationResult` (steps, actions, pages, timing, error)

**Non-Goals:**
- Plan-level integration (Runner takes `TraversalNode`, not `TraversalPlan`)
- Scroll simulation (SimulateDelayMs is timing only)
- Concurrent simulation runs
- Path injection (fixture always starts from `InitialPage`)

## Decisions

### D-27: Runner loops StepOrchestrator.ExecuteStep, not raw FSM.Step

**Decision**: Runner calls `_orchestrator.ExecuteStep(_stepCtx)` per iteration.

**Rationale**: StepOrchestrator handles BRANCH interception, child node discovery, visited marking, anti-loop detection, and frame-override. If Runner called `fsm.Step()` directly, all of this would need to be reimplemented. StepOrchestrator already has the correct logic.

### D-28: Termination = FrameCompleted + stack depth ≤ 1

**Decision**: Runner stops when `stepResult.FrameCompleted && ctx.NodeStack.Depth <= 1`.

**Rationale**: FrameCompleted means the current subtree is done. Depth ≤ 1 means only the root node remains. Both together mean the entire traversal tree is complete. Python uses a similar check (`engine._is_complete`).

### D-29: Runner constructor takes rootNode, not TraversalPlan

**Decision**: Constructor parameter `TraversalNode rootNode`, not `TraversalPlan`.

**Rationale**: `PlanCompiler` (already exists in Graph layer) handles Plan → Node tree conversion. Runner focuses on execution. Caller is responsible for compiling the plan first and registering all nodes.

### D-30: Default ThrowOnError = false

**Decision**: Exceptions during handler execution do NOT terminate the runner by default.

**Rationale**: HandleErrorHandling is a legitimate FSM state. Throwing from a handler routes to ErrorHandling via the existing Step() try-catch. Runner should allow the FSM to recover, not abort. Set `ThrowOnError = true` for debugging.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Infinite loop without MaxSteps | Default MaxSteps=1000, configurable |
| Runner state leak between runs | Runner is single-use; create new instance per run |
| DYNAMIC_MATCH nodes may cause unexpected anti-loop termination | AntiLoop is a valid terminal state; documented in result |
| ActionHistory may be incomplete if handlers skip IActionExecutor calls | Runner captures from `_action.GetHistory()` — only records actual mock calls |
