## 1. Interface + Result type

- [x] 1.1 Create `src/UniClaw.Core/Traversal/IInterceptionHandler.cs`: define `IInterceptionHandler` interface (3 methods: OnBranch, OnDynamicMatchNodeSelect, OnFrameComplete) + `InterceptionResult` record struct (NextState, ChildPushed, FrameCompleted, FrameOverrideTriggered); namespace `UniClaw.Core.Traversal`
- [x] 1.2 `dotnet build` — verify 0 errors

## 2. InterceptionHandler — scaffold + field

- [x] 2.1 Create `src/UniClaw.Core/Traversal/InterceptionHandler.cs`: `public sealed class InterceptionHandler : IInterceptionHandler` with empty method stubs returning `default`; move `_lastPushedChildNodeId` field from StepOrchestrator
- [x] 2.2 `dotnet build` — verify 0 errors

## 3. InterceptionHandler — move private helpers

- [x] 3.1 Move `FromFrame` (private static) from `StepOrchestrator.cs` → `InterceptionHandler.cs`; keep signature unchanged
- [x] 3.2 Move `GetElementIds` (private static) from `StepOrchestrator.cs` → `InterceptionHandler.cs`; keep signature unchanged
- [x] 3.3 Move `TryHandleScrollAsync` from `StepOrchestrator.cs` → `InterceptionHandler.cs`; keep `internal static` (10 direct test call sites in ScrollLoopTerminationTests — design §5 修正); keep tuple return and convert in caller; update test call sites to `InterceptionHandler.TryHandleScrollAsync`
- [x] 3.4 Move `TryHandleNavigation` from `StepOrchestrator.cs` → `InterceptionHandler.cs`; change signature: 3 `ref bool` + 1 `ref TraversalState` → 1 `ref InterceptionResult`; keep `private`
- [x] 3.5 `dotnet build` — verify 0 errors

## 4. InterceptionHandler — move step logic

- [x] 4.1 Implement `OnBranch`: move step 8 logic from StepOrchestrator (branch guard → GetNextUnvisitedChild → push / TryHandleNavigation / TryHandleScrollAsync / fallthrough); return `InterceptionResult`
- [x] 4.2 Implement `OnDynamicMatchNodeSelect`: move step 9 logic from StepOrchestrator (DynamicMatch guard → GetNextUnvisitedChild → push / TryHandleNavigation / TryHandleScrollAsync → PressBack+Pop or frameCompleted); return `InterceptionResult`
- [x] 4.3 Implement `OnFrameComplete`: move step 10 logic from StepOrchestrator (DynamicMatch guard → GetNextUnvisitedChild → override or let pass); return `InterceptionResult`
- [x] 4.4 `dotnet build` — verify 0 errors

## 5. StepOrchestrator — simplify

- [x] 5.1 Remove `_lastPushedChildNodeId` field, `TryHandleNavigation`, `TryHandleScrollAsync`, `FromFrame`, `GetElementIds` methods from StepOrchestrator.cs
- [x] 5.2 Remove steps 8-10 inline logic from `ExecuteStepAsync`; add `private readonly IInterceptionHandler _handler` field
- [x] 5.3 Add conditional delegation: `if (nextState == Branch && BranchAllowedSources...) → _handler.OnBranch(...)` (etc.) with `intercepted` flag guard
- [x] 5.4 Add constructor (or field initializer) `_handler = new InterceptionHandler()`
- [x] 5.5 `dotnet build` — verify 0 errors; StepOrchestrator should be ~120 lines

## 6. TraversalEngine — wire up

- [x] 6.1 Add `using UniClaw.Core.Traversal;` if needed (should already be present)
- [x] 6.2 Verify `TraversalEngine` constructs `StepOrchestrator` correctly with default `InterceptionHandler`; no API changes needed if StepOrchestrator uses field initializer
- [x] 6.3 `dotnet build` — verify 0 errors

## 7. Guard + validation

- [x] 7.1 Add `InterceptionHandler_ImplementsIInterceptionHandler` guard test to `ArchitectureGuardTests.cs`: verify `InterceptionHandler` implements `IInterceptionHandler`
- [x] 7.2 Update `docs/system/layers/traversal.md` §2: update StepOrchestrator description to reflect 2-component architecture; add InterceptionHandler to class inventory table
- [x] 7.3 Append D-N decision to `docs/system/decisions/log.md`: D-IV StepOrchestrator 分解 — 方案 A (2 组件)
- [x] 7.4 `dotnet build` clean (0 errors); `dotnet test` full suite 665+ tests green; `openspec validate steporchestrator-decomposition` (if available)
