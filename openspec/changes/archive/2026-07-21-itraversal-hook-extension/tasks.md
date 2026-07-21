## 1. Hook Registration Migration (Config Field)

- [x] 1.1 Add `Hooks: ImmutableArray<ITraversalHook> { get; init; } = ImmutableArray<ITraversalHook>.Empty` to `TraversalEngineConfig.cs`
- [x] 1.2 Delete `private readonly List<ITraversalHook> _hooks = new()` from TraversalEngine.cs (L38)
- [x] 1.3 Delete `public void RegisterHook(ITraversalHook hook)` method from TraversalEngine.cs (L522-525)
- [x] 1.4 Add `private readonly ImmutableArray<ITraversalHook> _hooks = config.Hooks` (assigned from config in constructor)
- [x] 1.5 Migrate `TraversalEnginePauseResumeTests.cs`: replace `engine.RegisterHook(hook)` with config field `new TraversalEngineConfig { Hooks = ImmutableArray.Create(hook) }` passed to engine constructor
- [x] 1.6 Verify build succeeds — `dotnet build src/UniClaw.Core.sln` (0 errors)

## 2. FireAsync Catch Block Improvement

- [x] 2.1 Update `FireAsync` catch block: change `catch { }` to `catch (Exception ex) { Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}"); }`
- [x] 2.2 Add `_hooks.Length == 0` early return (zero-overhead shortcut): `if (_hooks.Length == 0) return;`
- [x] 2.3 Verify build succeeds

## 3. OnBeforeRun Call Point

- [x] 3.1 Insert `await FireAsync(h => h.OnBeforeRunAsync(_plan, _ctx))` in RunAsync — before the `try` block (outside try, so hook exceptions aren't caught by engine catch handler)
- [x] 3.2 Verify: hook fires before first step iteration (verified by TraversalHookTests.BeforeRunAfterRun_TimingCorrect)

## 4. OnBeforeStep Call Point

- [x] 4.1 Insert `await FireAsync(h => h.OnBeforeStepAsync(_ctx))` in RunAsync step loop — after pause-gate (`ct.ThrowIfCancellationRequested()` L246) and before vision analysis (`_vision.AnalyzeCurrentPageAsync` L250)
- [x] 4.2 Verify: hook fires for each step, before expensive vision call (verified by TraversalHookTests.BeforeStepAfterStep_TimingCorrect)

## 5. OnAfterStep Call Point

- [x] 5.1 Insert `await FireAsync(h => h.OnAfterStepAsync(_ctx))` in RunAsync step loop — after page-visit recording (L290) and before termination checks (L293 `if stepResult.FrameCompleted...`)
- [x] 5.2 Verify: OnAfterStep fires for every step including terminating step (termination check happens AFTER OnAfterStep) (verified by TraversalHookTests.BeforeStepAfterStep_TimingCorrect)

## 6. OnAfterRun Call Points (at Done() call sites)

- [x] 6.1 At each `return Done(...)` call site in RunAsync, refactor to: `var result = Done(...); await FireAsync(h => h.OnAfterRunAsync(result)); return result;`
- [x] 6.2 Done() call sites refactored (7): AllVisited, AntiLoop, TargetFound, Timeout, MaxSteps(policy), MaxSteps(exhausted), Cancelled, Error
- [x] 6.3 Verify: OnAfterRun fires for every exit path with the constructed TraversalResult (verified by TraversalHookTests.BeforeRunAfterRun_TimingCorrect + AfterRun_FiresAtCancelledExit)

## 7. OnError (fatal) Call Point

- [x] 7.1 Insert `await FireAsync(h => h.OnErrorAsync(new TraversalErrorContext(ex.GetType().Name, ex.Message, _ctx.CurrentFrame?.NodeId, IsRecoverable: false), _ctx))` in `catch(Exception)` block — before `return Done(Reasons.Error, ...)`
- [x] 7.2 Verify: fatal error hook fires with IsRecoverable=false (verified by TraversalHookTests.OnError_Fatal_IsRecoverableFalse)

## 8. OnError (recoverable) Call Point (engine-level intercept)

- [x] 8.1 After `stepResult = await _orchestrator.ExecuteStepAsync(_stepCtx)`, add recoverable error intercept: if `stepResult.NextState == TraversalState.ErrorHandling && _ctx.LastError != null`, fire `await FireAsync(h => h.OnErrorAsync(new TraversalErrorContext(_ctx.LastError.GetType().Name, _ctx.LastError.Message, _ctx.CurrentFrame?.NodeId, IsRecoverable: true), _ctx))`
- [x] 8.2 Verify: recoverable error hook fires with IsRecoverable=true; FSM does not access hooks (verified by TraversalHookTests.OnError_Recoverable_IsRecoverableTrue)

## 9. Tests

- [x] 9.1 Create `TraversalHookTests.cs` in `tests/UniClaw.Core.Tests/Traversal/`
- [x] 9.2 Test: Empty Hooks list — engine runs normally, zero overhead
- [x] 9.3 Test: Single Hook counting — CountingHook records each OnXxx call count
- [x] 9.4 Test: Multiple Hook order — HookA fires before HookB (registration order)
- [x] 9.5 Test: Hook throws exception — engine continues + Console.WriteLine warning
- [x] 9.6 Test: OnBeforeRun/OnAfterRun timing — OnBeforeRun before step loop, OnAfterRun at each exit path
- [x] 9.7 Test: OnBeforeStep/OnAfterStep timing — OnBeforeStep before vision, OnAfterStep before termination checks (including terminating step)
- [x] 9.8 Test: OnError recoverable — FSM ErrorHandling → IsRecoverable=true
- [x] 9.9 Test: OnError fatal — Engine-level exception → IsRecoverable=false
- [x] 9.10 Test: TraversalHookBase no-op — inherit without override → all Task.CompletedTask
- [x] 9.11 Test: Config field registration — TraversalEngineConfig.Hooks works; RegisterHook() no longer exists

## 10. Verification + Decision Recording

- [x] 10.1 Run `dotnet test src/UniClaw.Core.sln` — all tests green (814 total: 803 existing + 11 new)
- [x] 10.2 Run ArchitectureGuardTests — all guard tests pass (included in 814 total)
- [x] 10.3 Record decisions D-100 through D-104 in `docs/system/decisions/log.md` (D-A→D-100, D-B→D-101, D-C→D-102, D-D→D-103, D-E→D-104)
- [x] 10.4 Update `docs/system/patterns/dispatch-table.md` — add TraversalEngine.FireAsync as 6th instance (five→six instances table, Log-and-Continue sub-pattern table, instance detail paragraph)
- [x] 10.5 Update `docs/system/layers/traversal.md` — add ITraversalHook + TraversalHookBase + TraversalErrorContext + Hooks config field + call point mapping table (§5 Lifecycle Hook Infrastructure)
- [x] 10.6 Update CLAUDE.md test count: 803→814
