## Context

TraversalEngine currently has PauseAsync/ResumeAsync as synchronous stubs that only change GlobalState via GlobalFSM. The RunAsync step loop has no pause check — it continues executing steps regardless of state. GlobalFSM activation (completed change) provides the transition matrix (Traversing↔Paused) and two-step termination (Traversing→Paused→Terminated), but no actual loop suspension mechanism exists.

This design completes the engine lifecycle by adding a TaskCompletionSource-based gate. It depends on B1 (ITraversalHook) for the OnPauseAsync/OnResumeAsync lifecycle hook points — B1 defines the interface stubs, this change makes them functional.

## Goals / Non-Goals

**Goals:**
- Suspend the RunAsync step loop on PauseAsync, resume on ResumeAsync
- Precondition validation: PauseAsync requires Traversing, ResumeAsync requires Paused
- Fire B1 lifecycle hooks (OnPauseAsync/OnResumeAsync) at the correct points in the sequence
- Thread-safe cross-thread communication (pause/resume called from external thread, step loop on engine thread)
- Graceful mid-step pause: current step completes, pause takes effect at next iteration
- CancellationToken support: cancellation during paused state exits the loop

**Non-Goals:**
- UI/external pause button (upper layer responsibility)
- Pause state persistence (pause only blocks loop, doesn't save progress)
- Pause timeout auto-resume (YAGNI)
- SemaphoreSlim alternative (TCS chosen — see Decisions)
- Trace writing during pause (no step advancement, no new traces)

## Decisions

### TaskCompletionSource over SemaphoreSlim
TaskCompletionSource provides one-shot gate semantics (close=open, open=release) that exactly match pause/resume. SemaphoreSlim is a counting semaphore for throttle/access control — wrong semantic model. TCS is naturally reset by creating a new instance on each PauseAsync call; Thread-safe; can be linked with CancellationToken.

### TrySetResult after FireAsync (not before)
**Critical ordering constraint.** ResumeAsync calls `TrySetResult()` AFTER `FireAsync(OnResumeAsync)`, not before. If TrySetResult fires first, the step loop (awaiting `_resumeSignal.Task`) resumes immediately and starts the next step concurrently with the OnResumeAsync hook — a race condition. The correct sequence: set state → fire hooks (gate still closed) → open gate → step loop resumes.

### volatile on _resumeSignal field
The field is written by the external caller (PauseAsync thread) and read by the step loop (engine thread). Without `volatile`, JIT register caching could cause the step loop to read a stale reference to the old (completed) TCS, making the pause ineffective. `volatile` ensures fresh reads at the cost of a memory barrier per read (negligible for a reference field).

### B1 hook after state change
PauseAsync: close gate → set Paused → fire OnPauseAsync. Hooks observe the already-paused state. ResumeAsync: set Traversing → fire OnResumeAsync (gate still closed) → open gate. Hooks observe the traversing state before the step loop resumes.

## Risks / Trade-offs

- **Uncompleted TCS on termination**: If engine transitions to Terminated/Error while Paused, `_resumeSignal` remains uncompleted. Not a leak (TCS is lightweight, engine not reused), but code should not await `_resumeSignal.Task` after the loop exits. → Mitigation: CancellationToken already provides loop exit; no additional TCS cleanup needed.
- **Re-entrancy risk**: If a B1 hook calls PauseAsync/ResumeAsync internally, the gate state machine could see unexpected transitions. → Mitigation: Hooks are observers, not controllers; documented in B1 design that hooks should not call lifecycle methods.
- **Pause delay up to one step**: If PauseAsync is called during an active step, the pause takes effect only at the next iteration. → Acceptable: graceful pause is a feature, not a bug.
