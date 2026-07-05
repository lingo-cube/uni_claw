# Capability Spec: TraversalFSM Handler Implementation (Phase 2.3a)

## Overview

TraversalFSM has 8 handler methods. Phase 2.3a implements HandleExecute and HandleBranch with real decision logic, replacing their current stub behavior (hardcoded return values).

## Scenario: Step accepts StepContext for handler dependencies

- **GIVEN** TraversalFSM is constructed with ITraversalContext
- **WHEN** `Step(StepContext? ctx)` is called with a non-null StepContext
- **THEN** handlers SHALL access `ctx.Vision` (IVisionProvider) and `ctx.Action` (IActionExecutor) for real logic
- **AND** `_currentStepContext` SHALL be set before DispatchHandler and cleared after

#### Scenario: Step(null) — stub fallback

- **WHEN** `Step()` is called (no StepContext)
- **THEN** `_currentStepContext` SHALL be null
- **AND** handlers SHALL return their current hardcoded stub values (non-breaking)
- **AND** existing 438 tests SHALL all pass unchanged

#### Scenario: Step(StepContext) — partial null dependencies

- **WHEN** `Step(StepContext ctx)` is called where `ctx.Action` is null
- **THEN** HandleExecute SHALL fall back to stub (return ResultVerify)
- **WHEN** `Step(StepContext ctx)` is called where `ctx.Vision` is null
- **THEN** Handlers that need Vision (PreconditionCheck, ResultVerify, PopupHandling — not in P1) SHALL fall back to stub
- **AND** Handlers that don't need Vision (HandleExecute, HandleBranch) SHALL use real logic

#### Scenario: Exception propagation in Step(StepContext)

- **WHEN** a handler throws an exception during real logic execution
- **THEN** the existing `Step()` try-catch SHALL catch it
- **AND** SHALL set `Context.LastError`
- **AND** SHALL route to `ErrorHandling` state
- **AND** this SHALL work identically whether Step() or Step(StepContext) was called

## Scenario: HandleExecute — action execution with optional restore

### Primary flow

- **GIVEN** TraversalFSM is in Execute state AND `_currentStepContext` is non-null AND `ctx.Action` is non-null
- **WHEN** `HandleExecute()` is called
- **THEN** it SHALL:
  1. Get current node from `Context.NodeStack.Peek()`
  2. If node is null → return `ResultVerify` (edge case, no action to execute)
  3. Dispatch operation via OperationType → IActionExecutor method mapping (see §OperationType Dispatch)
  4. On success → check RestoreAction → if present, execute restore → return `ResultVerify`
  5. On exception → set `Context.LastError` → return `ErrorHandling`

### NoAction handling

- **WHEN** `Operation.Action == OperationType.NoAction`
- **THEN** HandleExecute SHALL skip IActionExecutor call
- **AND** SHALL return `ResultVerify` directly (no-op execution step)

### Restore failure handling

- **WHEN** primary operation succeeds AND restore operation exists AND restore fails (returns false)
- **THEN** HandleExecute SHALL still return `ResultVerify`
- **AND** SHALL NOT route to ErrorHandling (restore failure is non-critical — matches Python behavior)
- **AND** SHALL record restore failure in metrics (Success=false)

### IActionExecutor returns false (no exception)

- **WHEN** IActionExecutor.TapAsync (or other method) returns `false` (not throws)
- **THEN** HandleExecute SHALL still return `ResultVerify`
- **AND** SHALL record execution failure in metrics (Success=false)
- **NOTE**: Python doesn't distinguish success flag vs exception; C# matches Python by only routing to ErrorHandling on exception, not on false return

### Target null handling

- **WHEN** `Operation.Target` is null AND OperationType requires a target (Click, Swipe, LongPress)
- **THEN** HandleExecute SHALL throw InvalidOperationException
- **AND** the Step() try-catch SHALL catch it → route to ErrorHandling
- **NOTE**: Click/Swipe without target is a malformed operation — fail-fast is correct

## Scenario: HandleExecute — OperationType dispatch

OperationType (5 values) → IActionExecutor method mapping:

| OperationType | IActionExecutor Method | Required Target | Target extraction |
|--------------|----------------------|----------------|-------------------|
| Click | TapAsync(x, y) | TargetType.Coordinate | `Target.Value` must be castable to Coordinate → use X, Y |
| Swipe | SwipeAsync(sx, sy, ex, ey, duration) | TargetType.Coordinate + Params["end"] | Start from Target.Value (Coordinate); End from Params["end_coordinate"] (Coordinate); Duration from Params["duration_ms"] (int, default 300) |
| Back | PressBackAsync() | None (Target ignored) | No target needed |
| InputText | InputTextAsync(text) | TargetType.Text | `Target.Value` → ToString() as text input |
| NoAction | (skip call) | None | Return ResultVerify immediately |

**Note**: OperationType does NOT include LongPress or Wait. IActionExecutor has these methods but Domain OperationType doesn't — they're infrastructure-level, not Domain-level. If a future OperationType value is added, this mapping table must be updated first (Hilly 级扩展规则).

**Target.Value type dispatch** (secondary):

| TargetType | Expected Value type | Extraction |
|-----------|--------------------|-----------|
| Coordinate | Coordinate record | `(Coordinate)Target.Value` → .X, .Y |
| Text | string | `Target.Value.ToString()` |
| UiIndex | int | `(int)Target.Value` |

- **WHEN** Target.Value type doesn't match TargetType expectation → HandleExecute SHALL throw InvalidOperationException → Step() try-catch → ErrorHandling

## Scenario: HandleBranch — subtree selection based on ChildrenStrategy

### Primary flow

- **GIVEN** TraversalFSM is in Branch state
- **WHEN** `HandleBranch()` is called
- **THEN** it SHALL determine next state via pure data logic (no external service dependency)

### Decision matrix

| Condition | Stack depth > 1 | Stack depth == 1 | Stack empty |
|-----------|----------------|-----------------|-------------|
| Node null | FrameComplete | NodeSelect | NodeSelect |
| ChildrenStrategy.STATIC + has unvisited | NodeSelect | NodeSelect | NodeSelect |
| ChildrenStrategy.STATIC + all visited | FrameComplete | FrameComplete | FrameComplete |
| ChildrenStrategy.DYNAMIC_MATCH | NodeSelect | NodeSelect | NodeSelect |
| ChildrenStrategy.NONE + IsLeaf | FrameComplete | NodeSelect | NodeSelect |
| ChildrenStrategy.NONE + not leaf (container) | FrameComplete | FrameComplete | FrameComplete |

### VisitedChildren edge case

- **WHEN** `Context.VisitedChildren` does not contain the current node's NodeId as a key
- **THEN** HandleBranch SHALL treat all children as unvisited (empty visited set)
- **AND** SHALL NOT throw or fail (matches Python: `context.visited_children.get(node_id, set())`)

### NodeStack.Peek() returns null

- **WHEN** `Context.NodeStack.Peek()` returns null (empty stack or invalid state)
- **AND** stack depth > 1
- **THEN** SHALL return `FrameComplete`
- **WHEN** stack depth <= 1
- **THEN** SHALL return `NodeSelect`

## Scenario: Mock infrastructure for handler testing

- **GIVEN** tests need to verify handler behavior without real ADB/Vision
- **THEN** test infrastructure SHALL provide:
  - `MockActionExecutor`: implements IActionExecutor; configurable `NextResult` (bool) per method; `CallLog` records all invocations with parameters; `ThrowsOnNext` property to simulate exceptions
  - `MockVisionProvider`: implements IVisionProvider; returns `NextResult` (PageAnalysis?); records `CallCount`
