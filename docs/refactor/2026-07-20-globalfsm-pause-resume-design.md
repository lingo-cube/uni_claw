# P4-B2: GlobalFSM Completion — PauseAsync/ResumeAsync Implementation

> Date: 2026-07-20
> Priority: P4 (engine lifecycle completion, depends on B1)
> Branch: feature/refactor

## 1. Summary

Implement actual pause/resume mechanism in TraversalEngine. Current stubs only change GlobalState — the step loop continues running regardless. This change adds: (1) TaskCompletionSource-based loop suspension, (2) precondition checks on PauseAsync/ResumeAsync, (3) B1 hook call points.

## 2. Current State

- GlobalFSM activated (D-81): SessionContext holds GlobalFSM instance, two-step termination via Paused
- PauseAsync()/ResumeAsync() are stubs — only call `SetGlobalState(Paused/Traversing)`, don't actually suspend the step loop
- RunAsync step loop has no pause check — continues running even when GlobalState=Paused
- No precondition validation — can call PauseAsync when not Traversing, ResumeAsync when not Paused
- D-82: Two-step termination: Traversing→Paused→Terminated (no direct edge in matrix)

## 3. Implementation: TaskCompletionSource Gate Pattern

```csharp
// TraversalEngine internals
private volatile TaskCompletionSource _resumeSignal = CreateCompletedTCS();

private static TaskCompletionSource CreateCompletedTCS()
{
    var tcs = new TaskCompletionSource();
    tcs.SetResult();  // initially completed — await returns immediately
    return tcs;
}
```

### PauseAsync

```csharp
public async Task PauseAsync(string? reason = null)
{
    if (_ctx.GlobalState != GlobalState.Traversing)
        throw new DomainValidationException("GlobalState", "Cannot pause when not Traversing");

    _resumeSignal = new TaskCompletionSource();  // close gate (new uncompleted TCS)
    await _ctx.SetGlobalState(GlobalState.Paused, reason ?? "user_pause");
    await FireAsync(h => h.OnPauseAsync(_ctx));  // B1 hook call point
}
```

### ResumeAsync

```csharp
public async Task ResumeAsync(string? reason = null)
{
    if (_ctx.GlobalState != GlobalState.Paused)
        throw new DomainValidationException("GlobalState", "Cannot resume when not Paused");

    await _ctx.SetGlobalState(GlobalState.Traversing, reason ?? "user_resume");
    // FireAsync BEFORE TrySetResult — hooks must complete before the step loop resumes.
    // If TrySetResult fires first, the step loop starts executing the next step
    // concurrently with OnResumeAsync, creating a race condition.
    await FireAsync(h => h.OnResumeAsync(_ctx));  // B1 hook call point
    _resumeSignal.TrySetResult();  // open gate (complete TCS, release awaiter) — after hooks
}
```

### RunAsync Step Loop — Pause Check

```csharp
// Each step entry (after OnBeforeStep, before orchestrator.ExecuteStepAsync)
await _resumeSignal.Task;  // blocks when Paused (TCS uncompleted), returns immediately when Traversing
ct.ThrowIfCancellationRequested();  // check cancellation after resume
```

### Thread Safety

- TaskCompletionSource is thread-safe — PauseAsync/ResumeAsync callable from any thread
- TrySetResult (not SetResult) — doesn't throw if TCS already completed (handles duplicate Resume calls)
- SetGlobalState goes through GlobalFSM matrix validation — Paused→Traversing is a legal transition
- `_resumeSignal` MUST be `volatile` — the field is written by the external caller (PauseAsync) and read by the step loop (RunAsync). Without `volatile`, JIT inlining or register caching could cause the step loop to read a stale reference to the old (completed) TCS even after PauseAsync replaces it with a new (uncompleted) one, making the pause ineffective.
- Declaration: `private volatile TaskCompletionSource _resumeSignal = CreateCompletedTCS();`

### Why TaskCompletionSource Over SemaphoreSlim

| Aspect | TaskCompletionSource | SemaphoreSlim |
|--------|---------------------|---------------|
| Semantics | One-shot gate (close=open, open=release) — exact pause/resume semantics | Counting semaphore — throttle/access control, not pause/resume |
| Polling overhead | Zero — await-level blocking, immediate release on Resume | Zero — but wrong semantic model |
| Multiple pause/resume | Create new TCS on each Pause — natural reset | Need careful acquire/release coordination |
| Cancellation | Can link TCS.Task with CancellationToken | More complex |

## 4. Testing Strategy

### Test Matrix

| Test Group | Coverage | Est. Count |
|------------|----------|------------|
| Pause during Traversing | RunAsync running → PauseAsync → loop blocks | 1 |
| Resume restores loop | Pause → ResumeAsync → loop continues next step | 1 |
| Pause precondition | GlobalState≠Traversing → PauseAsync → DomainValidationException | 1 |
| Resume precondition | GlobalState≠Paused → ResumeAsync → DomainValidationException | 1 |
| Multiple pause/resume | Pause→Resume→Pause→Resume alternating → each correctly blocks/restores | 1 |
| Pause step count verification | Pause after 3 steps → Resume → total steps = 3 + post-resume steps | 1 |
| Cancel during pause | CancellationToken fires during pause → loop exits | 1 |
| OnPause/OnResume hook | B1 hooks fire correctly on pause/resume | 1 |

**Total: ~8 tests**.

### Test Method

Use Simulation baseline test framework (StateFixtureBuilder) to construct scenarios. PauseAsync/ResumeAsync called from external thread/task, verify engine loop block/restore behavior.

## 5. Out of Scope

- ❌ UI/external pause button (upper layer host implements)
- ❌ Pause state persistence (pause only blocks loop, doesn't save progress)
- ❌ Pause timeout auto-resume (YAGNI)
- ❌ SemaphoreSlim alternative (TaskCompletionSource chosen)
- ❌ Trace writing during pause (pause = loop doesn't advance, no new trace produced)
