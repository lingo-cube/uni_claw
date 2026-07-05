# Tasks: Phase 2.3a — Core Traversal Loop

## Implementation Tasks

- [x] T1: Add `Step(StepContext? ctx)` overload to TraversalFSM — store ctx in `_currentStepContext` field before DispatchHandler; clear after; existing `Step()` delegates to `Step(null)`; both paths use same try-catch for exception routing
- [x] T2: Create `MockActionExecutor` in tests — implements IActionExecutor; NextResult (bool), ThrowsOnNext (Exception?), CallLog (List<ActionRecord>); per-method parameter recording
- [x] T3: Create `MockVisionProvider` in tests — implements IVisionProvider; NextResult (PageAnalysis?), CallCount (int)
- [x] T4: Create `OperationDispatcher` internal helper — OperationType (5 values) + TargetType → IActionExecutor method dispatch; Target.Value type extraction (Coordinate/Text/UiIndex); NoAction → skip; Target null + requires-target → throw InvalidOperationException
- [x] T5: Implement `HandleExecute()` real logic — full decision flow: null node → ResultVerify; NoAction → ResultVerify; normal execution → OperationDispatcher → success/restore → ResultVerify; IActionExecutor returns false → ResultVerify (non-critical); exception → ErrorHandling; restore failure → ResultVerify (non-critical)
- [x] T6: Implement `HandleBranch()` real logic — full decision matrix: null node → NodeSelect/FrameComplete by depth; ChildrenStrategy.STATIC unvisited check with empty-VisitedChildren fallback; DYNAMIC_MATCH → NodeSelect; NONE → leaf/container logic
- [x] T7: Write HandleExecute tests (8 scenarios):
  - Execute_Click_Success (Operation.Click + Coordinate target → TapAsync called → ResultVerify)
  - Execute_Back_Success (Operation.Back → PressBackAsync called → ResultVerify)
  - Execute_NoAction (Operation.NoAction → skip IActionExecutor → ResultVerify)
  - Execute_WithRestore_Success (Operation + RestoreAction → two calls → ResultVerify)
  - Execute_WithRestore_Failure (primary success, restore returns false → ResultVerify, not ErrorHandling)
  - Execute_ActionReturnsFalse (IActionExecutor returns false, no exception → ResultVerify)
  - Execute_Exception (IActionExecutor throws → ErrorHandling)
  - Execute_NullStepContext (Step() with no ctx → stub ResultVerify)
- [x] T8: Write HandleBranch tests (6 scenarios):
  - Branch_StaticUnvisited (STATIC + unvisited child → NodeSelect)
  - Branch_StaticAllVisited (STATIC + all visited → FrameComplete)
  - Branch_DynamicMatch (DYNAMIC_MATCH → NodeSelect optimistic)
  - Branch_LeafNode_DepthMoreThan1 (IsLeaf + depth > 1 → FrameComplete)
  - Branch_LeafNode_Depth1 (IsLeaf + depth == 1 → NodeSelect)
  - Branch_EmptyVisitedChildren (NodeId not in VisitedChildren dict → treat as all unvisited)
- [x] T9: Write OperationDispatcher tests (5 scenarios):
  - Dispatch_Click_Coordinate (Operation.Click + Target.Coordinate → TapAsync(x, y))
  - Dispatch_Swipe (Operation.Swipe + start/end coordinates → SwipeAsync)
  - Dispatch_Back (Operation.Back → PressBackAsync, no target needed)
  - Dispatch_InputText (Operation.InputText + Target.Text → InputTextAsync)
  - Dispatch_NullTarget_Throws (Operation.Click + null Target → InvalidOperationException)
- [x] T10: Write Step(StepContext) tests (3 scenarios):
  - Step_WithStepContext (Step(StepContext) → handlers use real logic)
  - Step_NullStepContext (Step() → handlers use stub fallback, non-breaking)
  - Step_ExceptionRouting (handler exception → ErrorHandling, same for both Step() and Step(ctx))
- [x] T11: Verify all existing 438 tests still pass (non-breaking change)
- [x] T12: Update `docs/system/layers/state-machine.md` — TraversalFSM handler status: HandleExecute/HandleBranch from stub to P1 implemented
- [x] T13: Update `docs/system/patterns/fsm-design.md` — handler decision table with HandleExecute/HandleBranch decision flows

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Core/StateMachine/` | `docs/system/layers/state-machine.md` + `docs/system/patterns/fsm-design.md` |
