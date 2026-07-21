## 1. Hook Registration Migration (Config Field)

- [ ] 1.1 Add `Hooks: ImmutableArray<ITraversalHook> { get; init; } = ImmutableArray<ITraversalHook>.Empty` to `TraversalEngineConfig.cs`
- [ ] 1.2 Delete `private readonly List<ITraversalHook> _hooks = new()` from TraversalEngine.cs (L38)
- [ ] 1.3 Delete `public void RegisterHook(ITraversalHook hook)` method from TraversalEngine.cs (L522-525)
- [ ] 1.4 Add `private readonly ImmutableArray<ITraversalHook> _hooks = config.Hooks` (assigned from config in constructor/Initialize)
- [ ] 1.5 Migrate `TraversalEnginePauseResumeTests.cs`: replace `engine.RegisterHook(hook)` with config field `new TraversalEngineConfig { Hooks = ImmutableArray.Create(hook) }` passed to engine constructor
- [ ] 1.6 Verify build succeeds — `dotnet build src/UniClaw.Core.sln` (0 errors)

## 2. FireAsync Catch Block Improvement

- [ ] 2.1 Update `FireAsync` catch block: change `catch { }` to `catch (Exception ex) { Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}"); }`
- [ ] 2.2 Add `_hooks.Length == 0` early return (zero-overhead shortcut): `if (_hooks.Length == 0) return;`
- [ ] 2.3 Verify build succeeds

## 3. OnBeforeRun Call Point

- [ ] 3.1 Insert `await FireAsync(h => h.OnBeforeRunAsync(_plan, _ctx))` in RunAsync — before the `try` block (outside try, so hook exceptions aren't caught by engine catch handler)
- [ ] 3.2 Verify: hook fires before first step iteration

## 4. OnBeforeStep Call Point

- [ ] 4.1 Insert `await FireAsync(h => h.OnBeforeStepAsync(_ctx))` in RunAsync step loop — after pause-gate (`ct.ThrowIfCancellationRequested()` L246) and before vision analysis (`_vision.AnalyzeCurrentPageAsync` L250)
- [ ] 4.2 Verify: hook fires for each step, before expensive vision call

## 5. OnAfterStep Call Point

- [ ] 5.1 Insert `await FireAsync(h => h.OnAfterStepAsync(_ctx))` in RunAsync step loop — after page-visit recording (L290) and before termination checks (L293 `if stepResult.FrameCompleted...`)
- [ ] 5.2 Verify: OnAfterStep fires for every step including terminating step (termination check happens AFTER OnAfterStep)

## 6. OnAfterRun Call Points (at Done() call sites)

- [ ] 6.1 At each `return Done(...)` call site in RunAsync, refactor to: `var result = Done(...); await FireAsync(h => h.OnAfterRunAsync(result)); return result;`
- [ ] 6.2 Done() call sites to refactor (~5): AllVisited return (L294-295), AntiLoop return (L299-300), TargetFound return, Timeout return, MaxSteps return, Error return (L366-367), Cancelled return
- [ ] 6.3 Verify: OnAfterRun fires for every exit path with the constructed TraversalResult

## 7. OnError (fatal) Call Point

- [ ] 7.1 Insert `await FireAsync(h => h.OnErrorAsync(new TraversalErrorContext(ex.GetType().Name, ex.Message, _ctx.CurrentFrame?.NodeId, IsRecoverable: false), _ctx))` in `catch(Exception)` block (L362-366) — before `return Done(Reasons.Error, ...)`
- [ ] 7.2 Verify: fatal error hook fires with IsRecoverable=false

## 8. OnError (recoverable) Call Point (engine-level intercept)

- [ ] 8.1 After `stepResult = await _orchestrator.ExecuteStepAsync(_stepCtx)`, add recoverable error intercept: if `stepResult.NextState == TraversalState.ErrorHandling && _ctx.LastError != null`, fire `await FireAsync(h => h.OnErrorAsync(new TraversalErrorContext(_ctx.LastError.GetType().Name, _ctx.LastError.Message, _ctx.CurrentFrame?.NodeId, IsRecoverable: true), _ctx))`
- [ ] 8.2 Verify: recoverable error hook fires with IsRecoverable=true; FSM does not access hooks

## 9. Tests

- [ ] 9.1 Create `TraversalHookTests.cs` in `tests/UniClaw.Core.Tests/Traversal/`
- [ ] 9.2 Test: Empty Hooks list — engine runs normally, zero overhead
- [ ] 9.3 Test: Single Hook counting — CountingHook records each OnXxx call count
- [ ] 9.4 Test: Multiple Hook order — HookA fires before HookB (registration order)
- [ ] 9.5 Test: Hook throws exception — engine continues + Console.WriteLine warning
- [ ] 9.6 Test: OnBeforeRun/OnAfterRun timing — OnBeforeRun before step loop, OnAfterRun at each exit path
- [ ] 9.7 Test: OnBeforeStep/OnAfterStep timing — OnBeforeStep before vision, OnAfterStep before termination checks (including terminating step)
- [ ] 9.8 Test: OnError recoverable — FSM ErrorHandling → IsRecoverable=true
- [ ] 9.9 Test: OnError fatal — Engine-level exception → IsRecoverable=false
- [ ] 9.10 Test: TraversalHookBase no-op — inherit without override → all Task.CompletedTask
- [ ] 9.11 Test: Config field registration — TraversalEngineConfig.Hooks works; RegisterHook() no longer exists

## 10. Verification + Decision Recording

- [ ] 10.1 Run `dotnet test src/UniClaw.Core.sln` — all tests green (803 existing + ~10 new = ~813 total)
- [ ] 10.2 Run ArchitectureGuardTests — all guard tests pass
- [ ] 10.3 Record decisions D-A through D-E in `docs/system/decisions/log.md`
- [ ] 10.4 Update `docs/system/patterns/dispatch-table.md` — add TraversalEngine.FireAsync as 6th instance in the five-instance table
- [ ] 10.5 Update `docs/system/layers/traversal.md` — mention ITraversalHook + TraversalHookBase + TraversalErrorContext + config field + call point mapping
- [ ] 10.6 Update CLAUDE.md test count if total changed
