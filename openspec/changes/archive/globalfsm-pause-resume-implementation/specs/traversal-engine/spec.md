## MODIFIED Requirements

### Requirement: TraversalEngine implements IGraphTraversalEngine lifecycle methods
TraversalEngine SHALL implement InitializeAsync() as Task.CompletedTask (constructor already initialized), PauseAsync() using TaskCompletionSource gate suspension with precondition validation, ResumeAsync() releasing the gate with precondition validation, StopAsync() using two-step termination (Traversing→Paused→Terminated), and GetStateAsync() returning ctx.GlobalState.

#### Scenario: InitializeAsync called after construction
- **WHEN** InitializeAsync() is called on a fully constructed TraversalEngine
- **THEN** it returns Task.CompletedTask immediately (no-op, constructor already initialized)

#### Scenario: StopAsync two-step termination from Traversing
- **WHEN** StopAsync() is called while engine is Traversing
- **THEN** ctx.GlobalState first transitions to Paused("stopping") via GlobalFSM
- **AND** then to Terminated("user_stop") — the locked matrix has no Traversing→Terminated direct edge
- **AND** the Task SHALL complete synchronously (no gate interaction on termination path)

#### Scenario: StopAsync from Paused
- **WHEN** StopAsync() is called while engine is Paused
- **THEN** ctx.GlobalState transitions directly from Paused to Terminated (legal edge in matrix)
- **AND** the Task SHALL complete synchronously

### Requirement: PauseAsync precondition validation
PauseAsync SHALL validate that GlobalState == Traversing before taking any action. If the precondition fails, it SHALL throw DomainValidationException.

#### Scenario: PauseAsync succeeds from Traversing
- **WHEN** PauseAsync() is called while GlobalState is Traversing
- **THEN** it SHALL create a new uncompleted TaskCompletionSource (close gate — step loop blocks on next check)
- **AND** set GlobalState to Paused via GlobalFSM.TransitionTo
- **AND** fire OnPauseAsync B1 lifecycle hook (after state change)

#### Scenario: PauseAsync precondition failure
- **WHEN** PauseAsync() is called while GlobalState is not Traversing (e.g., Idle, Paused, Error)
- **THEN** it SHALL throw DomainValidationException with reason "Cannot pause when not Traversing"
- **AND** no state change or gate mutation SHALL occur

### Requirement: ResumeAsync precondition validation
ResumeAsync SHALL validate that GlobalState == Paused before taking any action. If the precondition fails, it SHALL throw DomainValidationException.

#### Scenario: ResumeAsync succeeds from Paused
- **WHEN** ResumeAsync() is called while GlobalState is Paused
- **THEN** it SHALL set GlobalState to Traversing via GlobalFSM.TransitionTo
- **AND** fire OnResumeAsync B1 lifecycle hook (while gate is still closed — step loop still blocked)
- **AND** call TrySetResult on the current TaskCompletionSource AFTER all hooks complete (open gate)
- **AND** the step loop SHALL unblock and continue with the next step

#### Scenario: ResumeAsync precondition failure
- **WHEN** ResumeAsync() is called while GlobalState is not Paused (e.g., Traversing, Completed, Terminated)
- **THEN** it SHALL throw DomainValidationException with reason "Cannot resume when not Paused"
- **AND** no state change or gate mutation SHALL occur

### Requirement: TaskCompletionSource gate field is volatile
The `_resumeSignal` field holding the gate TaskCompletionSource SHALL be declared `volatile` to prevent JIT register caching across threads. The field is written by PauseAsync (external caller thread) and read by the RunAsync step loop (engine thread).

#### Scenario: volatile prevents stale read
- **WHEN** PauseAsync writes `_resumeSignal = new TaskCompletionSource()` from an external thread
- **THEN** the RunAsync step loop's subsequent read of `_resumeSignal.Task` SHALL observe the new uncompleted TCS, not a cached reference to the previous (completed) TCS

### Requirement: RunAsync step loop pause check
Each step iteration in RunAsync SHALL check the pause gate before executing the step. The check SHALL occur after OnBeforeStep hook and before orchestrator.ExecuteStepAsync.

#### Scenario: Step loop blocks when paused
- **WHEN** GlobalState is Paused (gate TCS is uncompleted)
- **THEN** `await _resumeSignal.Task` SHALL block until ResumeAsync completes the TCS
- **AND** after resume, `ct.ThrowIfCancellationRequested()` SHALL be evaluated (handle cancellation during pause)

#### Scenario: Step loop passes through when traversing
- **WHEN** GlobalState is Traversing (gate TCS is completed — initial state or after resume)
- **THEN** `await _resumeSignal.Task` SHALL return immediately without blocking

#### Scenario: Mid-step pause is graceful
- **WHEN** PauseAsync is called while a step is executing (the step has already passed the pause check)
- **THEN** the current step SHALL complete normally (vision analysis, orchestration, trace recording, termination checks)
- **AND** the pause check SHALL block at the START of the next iteration
