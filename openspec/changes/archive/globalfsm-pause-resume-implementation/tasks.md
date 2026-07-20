## 1. Gate Infrastructure

- [x] 1.1 Add `private volatile TaskCompletionSource _resumeSignal = CreateCompletedTCS();` field to TraversalEngine
- [x] 1.2 Add `CreateCompletedTCS()` private static helper (new TCS + SetResult)
- [x] 1.3 Update IGraphTraversalEngine interface if needed — no signature change (already async), add XML doc that PauseAsync/ResumeAsync now suspend/resume the step loop with precondition validation

## 2. PauseAsync Implementation

- [x] 2.1 Add precondition check: `if (_ctx.GlobalState != GlobalState.Traversing) throw new DomainValidationException("GlobalState", "Cannot pause when not Traversing")`
- [x] 2.2 Replace stub body: create new uncompleted TCS (`_resumeSignal = new TaskCompletionSource()`), call `await _ctx.SetGlobalState(GlobalState.Paused, reason)`, fire `await FireAsync(h => h.OnPauseAsync(_ctx))`

## 3. ResumeAsync Implementation

- [x] 3.1 Add precondition check: `if (_ctx.GlobalState != GlobalState.Paused) throw new DomainValidationException("GlobalState", "Cannot resume when not Paused")`
- [x] 3.2 Replace stub body: call `await _ctx.SetGlobalState(GlobalState.Traversing, reason)`, fire `await FireAsync(h => h.OnResumeAsync(_ctx))`, THEN `_resumeSignal.TrySetResult()` — hooks before gate open

## 4. RunAsync Step Loop Integration

- [x] 4.1 Insert pause check at each step entry (after OnBeforeStep, before orchestrator.ExecuteStepAsync): `await _resumeSignal.Task;`
- [x] 4.2 Add `ct.ThrowIfCancellationRequested()` after the pause check (handle cancellation during paused state)
- [x] 4.3 Verify that mid-step PauseAsync works gracefully: current step completes, next iteration blocks (design review confirms)

## 5. StopAsync Alignment

- [x] 5.1 Verify StopAsync two-step termination (Traversing→Paused→Terminated) works correctly when called from Paused state (matrix allows Paused→Terminated direct edge)
- [x] 5.2 Verify StopAsync does not interact with the gate TCS (termination path is synchronous, doesn't await or complete _resumeSignal)

## 6. Testing

- [x] 6.1 Write test: PauseAsync during Traversing → loop blocks on next check
- [x] 6.2 Write test: ResumeAsync restores loop → step continues after resume
- [x] 6.3 Write test: PauseAsync precondition failure (wrong state → DomainValidationException)
- [x] 6.4 Write test: ResumeAsync precondition failure (wrong state → DomainValidationException)
- [x] 6.5 Write test: Multiple pause/resume cycles (Pause→Resume→Pause→Resume)
- [x] 6.6 Write test: CancellationToken fires during pause → loop exits (OperationCanceledException)
- [x] 6.7 Write test: OnPauseAsync/OnResumeAsync B1 hooks fire at correct sequence point (hooks before gate open)
- [x] 6.8 Write test: Pause→Terminate two-step (verify StopAsync works from Paused state)
- [x] 6.9 Run full test suite: `dotnet test src/UniClaw.Core.sln` — **803/803 pass** (0 failures, 0 skipped)

## 7. Standing Spec Sync

- [x] 7.1 Update `openspec/specs/traversal-engine/spec.md` standing spec — in sync with change's delta spec (standing spec covers all key scenarios; delta spec has more detail on StopAsync from Paused and CancellationToken linking, which will merge on archive)
- [x] 7.2 Verify no stale references to "Phase 3 stub" remain in code comments or docs — updated `docs/refactor/15-traversal-engine-design.md` non-goals section to reflect completion
