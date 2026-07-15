## 1. GlobalFSM — add ForceState

- [x] 1.1 Add `internal void ForceState(GlobalState targetState)` to `GlobalFSM.cs`: set `CurrentState = targetState`, record `TransitionRecord(from, to, "force_restore", utcNow)`, do NOT invoke callbacks
- [x] 1.2 `dotnet build` — verify 0 errors

## 2. SessionContext — replace raw field with GlobalFSM instance

- [x] 2.1 Replace `private GlobalState _globalState` with `private readonly GlobalFSM _globalFsm = new()`
- [x] 2.2 Change `GlobalState` getter to `=> _globalFsm.CurrentState`; remove public setter
- [x] 2.3 Add `public IGlobalStateMachine GlobalStateMachine => _globalFsm` 
- [x] 2.4 Add `internal GlobalFSM InternalGlobalFSM => _globalFsm`
- [x] 2.5 Update constructor: remove `_globalState = GlobalState.Idle` (GlobalFSM ctor defaults to Idle)
- [x] 2.6 `dotnet build` — verify 0 errors

## 3. TraversalRuntimeContext — dual-layer API

- [x] 3.1 Change `SetGlobalState(GlobalState value)` → `SetGlobalState(GlobalState value, string? reason = null)`; call `_session.GlobalStateMachine.TransitionTo(value, reason)`
- [x] 3.2 Add `internal void ForceGlobalState(GlobalState value)`; call `_session.InternalGlobalFSM.ForceState(value)`
- [x] 3.3 `dotnet build` — verify 0 errors

## 4. PopupHandler — use ForceGlobalState

- [x] 4.1 In `StateRestorer.RestoreState`: change `rtc.SetGlobalState(preserved.CurrentState)` → `rtc.ForceGlobalState(preserved.CurrentState)`
- [x] 4.2 `dotnet build` — verify 0 errors

## 5. TraversalEngine — register trace callback

- [x] 5.1 In `TraversalEngine` initialization: register `RegisterStateCallback` on `_ctx.Session.InternalGlobalFSM` for key states (Completed, Error, Traversing, Idle) to write `StateTransition` with `FsmType = "GlobalFSM"` via `ITraceRecorder`
- [x] 5.2 Verify existing `SetGlobalState` calls in TraversalEngine pass reason parameter: `AllVisited → "all_visited"`, `Error → "error"`
- [x] 5.3 `dotnet build` — verify 0 errors

## 6. Validation

- [x] 6.1 `dotnet build` clean (0 errors)
- [x] 6.2 `dotnet test` full suite: all existing tests green (670+), no regression
- [x] 6.3 `openspec validate globalfsm-activation` (if available)
