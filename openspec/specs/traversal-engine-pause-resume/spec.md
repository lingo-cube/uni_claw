## ADDED Requirements

### Requirement: GlobalFSM PauseAsync suspends step loop with precondition validation
TraversalEngine.PauseAsync SHALL precondition-check `GlobalState == Traversing` (throw DomainValidationException otherwise). On success: create new uncompleted TaskCompletionSource `_resumeSignal`, transition GlobalState to Paused via `SetGlobalState(GlobalState.Paused, reason)`, fire `OnPauseAsync` hooks. The step loop SHALL block on `await _resumeSignal.Task` at each step entry (after OnBeforeStep, before orchestrator.ExecuteStepAsync). Current step completes; next iteration blocks.

#### Scenario: PauseAsync blocks step loop at next step entry
- **WHEN** PauseAsync is called while Traversing
- **THEN** GlobalState transitions to Paused; step loop blocks on next iteration; current step completes first

#### Scenario: PauseAsync precondition failure throws DomainValidationException
- **WHEN** PauseAsync is called with GlobalState != Traversing
- **THEN** DomainValidationException("GlobalState", "Cannot pause when not Traversing") is thrown

#### Scenario: CancellationToken fires during pause exits loop
- **WHEN** CancellationToken is signaled while step loop is paused
- **THEN** OperationCanceledException is thrown after the pause check

### Requirement: GlobalFSM ResumeAsync resumes step loop with hooks before gate open
TraversalEngine.ResumeAsync SHALL precondition-check `GlobalState == Paused` (throw DomainValidationException otherwise). On success: transition GlobalState to Traversing via `SetGlobalState(GlobalState.Traversing, reason)`, fire `OnResumeAsync` hooks, THEN `_resumeSignal.TrySetResult()` — hooks fire before gate opens. Resumed loop continues from blocked point.

#### Scenario: ResumeAsync restores step loop
- **WHEN** ResumeAsync is called while Paused
- **THEN** GlobalState transitions to Traversing; OnResumeAsync hooks fire; step loop continues

#### Scenario: ResumeAsync precondition failure throws DomainValidationException
- **WHEN** ResumeAsync is called with GlobalState != Paused
- **THEN** DomainValidationException("GlobalState", "Cannot resume when not Paused") is thrown

#### Scenario: Hooks fire before gate opens
- **WHEN** ResumeAsync sequence is: SetGlobalState → OnResumeAsync → TrySetResult
- **THEN** hooks observe GlobalState=Traversing before step loop resumes

### Requirement: Multiple pause/resume cycles supported
The pause/resume mechanism SHALL support repeated PauseAsync→ResumeAsync cycles. Each PauseAsync creates a fresh uncompleted TCS; each ResumeAsync completes it. No stale signal accumulation.

#### Scenario: Two pause/resume cycles work correctly
- **WHEN** PauseAsync → ResumeAsync → PauseAsync → ResumeAsync cycle is executed
- **THEN** each cycle correctly blocks and unblocks the step loop

### Requirement: StopAsync works from Paused state
TraversalEngine.StopAsync SHALL work from Paused state (FSM matrix allows Paused→Terminated direct edge). StopAsync does NOT interact with _resumeSignal TCS — termination path is synchronous, doesn't await or complete it. The blocked step loop exits via CancellationToken when StopAsync is called.

#### Scenario: StopAsync from Paused state transitions to Terminated
- **WHEN** StopAsync is called while Paused
- **THEN** GlobalState transitions to Terminated; step loop exits via CancellationToken

#### Scenario: StopAsync does not complete _resumeSignal
- **WHEN** StopAsync is called while Paused
- **THEN** _resumeSignal remains uncompleted; step loop exits via ct.ThrowIfCancellationRequested(), not via _resumeSignal
