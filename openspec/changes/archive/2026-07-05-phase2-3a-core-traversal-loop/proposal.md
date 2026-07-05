# Proposal: Phase 2.3a — Core Traversal Loop (HandleExecute + HandleBranch)

## Summary

Implement the two most fundamental TraversalFSM handler methods — HandleExecute and HandleBranch — forming the **minimum viable traversal loop**: select node → execute action → verify result → select next node.

This is the first step of Phase 2.3, filling the 70% placeholder gap in TraversalFSM.cs. The current 6 stub handlers return hardcoded default states without any real decision logic or external service calls. Without these handlers, the traversal engine can classify popups, detect completions, and match nodes — but it cannot actually run a traversal.

## Motivation

TraversalFSM.cs has 8 handler methods. Only 2 (HandleNodeSelect, HandleFrameComplete) have real logic. The remaining 6 are Phase 2.3 stubs that return hardcoded default states, making the FSM a skeleton that cannot drive real traversal behavior.

**Priority ordering** (per charter §AI Context Routing + FSM dependency chain):

| Priority | Handler | Python lines | Role | Dependencies |
|----------|---------|-------------|------|-------------|
| P1 | HandleExecute | ~80 | Execute action on current node + optional restore | IActionExecutor, TraversalNode.Operation |
| P1 | HandleBranch | ~60 | Select next subtree / check unvisited children / frame complete | IGraphTraversalEngine, TraversalNode.ChildrenStrategy |
| P2 | HandleResultVerify | ~50 | Vision verify page change | IVisionProvider |
| P2 | HandlePreconditionCheck | ~150 | 3-round retry with vision correction | IVisionProvider + IActionExecutor |
| P3 | HandleErrorHandling | ~100 | 3-layer recovery policy | ErrorHandler sub-components (already implemented) |
| P3 | HandlePopupHandling | ~100 | Safe button detection → click → verify | IActionExecutor + IVisionProvider |

P1 (Execute + Branch) forms the **minimum viable traversal loop**:
```
NodeSelect → PreconditionCheck (stub → Execute) → Execute → ResultVerify (stub → Branch) → Branch → NodeSelect
```

## Changes

### HandleExecute — Action Execution + Restore

**Python behavior** (`_handle_execute`, lines 1764-1859):
1. Build ExecutionContext from current_node.operation
2. Call `action.execute(exec_ctx)` → get result
3. If node has `needs_restore()` → execute restore operation (V6.15)
4. Record execution metrics
5. Return `RESULT_VERIFY` (success) or `ERROR_HANDLING` (exception)

**C# implementation plan**:
- Handler needs `IActionExecutor` and `StepContext` access
- TraversalNode already has `Operation` and `RestoreAction` fields
- ExecutionContext equivalent: Operation + Target + RestoreAction records
- Metrics recording: use TraceCoordinator or inline metrics dict
- Error path: catch → set Context.LastError → return ErrorHandling

**Key design decision**: How does TraversalFSM access IActionExecutor?
- Current TraversalFSM constructor only takes `ITraversalContext`
- Handlers need IVisionProvider + IActionExecutor for real logic
- Options: (A) Inject via constructor, (B) Pass via Step() method, (C) Use StepContext
- **Recommendation**: Option B — extend `Step(StepContext ctx)` so each step gets all dependencies
  - Matches Python pattern where `step()` receives vision + action as parameters
  - StepContext already exists and encapsulates all 13 dependencies
  - Non-breaking: existing `Step()` can call `Step(StepContext)` with null vision/action for stub handlers

### HandleBranch — Subtree Selection

**Python behavior** (`_handle_branch`, lines 1860-1915):
1. Get current node from stack
2. Check ChildrenStrategy type (STATIC / DYNAMIC_MATCH / NONE)
3. For STATIC: check if any static child not in visited_children
4. For DYNAMIC_MATCH: optimistic (engine gates actual availability)
5. If has_unvisited_children → NODE_SELECT
6. If leaf node → FRAME_COMPLETE (or NODE_SELECT if root)
7. If no unvisited children and not leaf → FRAME_COMPLETE

**C# implementation plan**:
- Uses ITraversalContext.VisitedChildren + TraversalNode.ChildrenStrategy
- NodeStack.Peek() gives current node
- ChildrenStrategyType.STATIC → check StaticChildren against VisitedChildren
- ChildrenStrategyType.DYNAMIC_MATCH → optimistic, return NODE_SELECT
- Leaf node → FRAME_COMPLETE (matches existing HandleFrameComplete logic)
- No external service dependency — pure state + data logic

### Mock Infrastructure

For testing, we need mock implementations:
- `MockActionExecutor` — configurable success/failure, records calls
- `MockVisionProvider` — returns predefined PageAnalysis
- These are test-only infrastructure, not production code

## Impact

| Module | Impact |
|--------|--------|
| `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | HandleExecute + HandleBranch real logic; Step(StepContext) overload |
| `src/UniClaw.Core/StateMachine/TraversalState.cs` | No change (enum values unchanged) |
| `src/UniClaw.Core/StateMachine/StepContext.cs` | No change (already has IVisionProvider + IActionExecutor) |
| `tests/.../StateMachine/` | MockActionExecutor, MockVisionProvider, HandleExecuteTests, HandleBranchTests |
| `docs/system/layers/state-machine.md` | Handler implementation status update |
| `docs/system/patterns/fsm-design.md` | Handler decision table update |

## Decisions Extract

| ID | Decision | Rationale | Status |
|----|----------|-----------|--------|
| D-18 | TraversalFSM.Step(StepContext ctx) overload — handlers access IVisionProvider + IActionExecutor via StepContext | Matches Python pattern (step receives vision+action); StepContext already has 13 deps; non-breaking for stub handlers | Proposed |
| D-19 | HandleExecute: execute operation → optional restore → ResultVerify / ErrorHandling | Aligned with Python _handle_execute; uses existing Operation/RestoreAction records | Proposed |
| D-20 | HandleBranch: ChildrenStrategy-based unvisited check → NodeSelect / FrameComplete | Aligned with Python _handle_branch; pure data logic, no external dependency | Proposed |
