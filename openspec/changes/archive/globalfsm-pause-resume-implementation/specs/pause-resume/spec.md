## ADDED Requirements

### Requirement: Pause/resume gate mechanism
The engine SHALL implement a TaskCompletionSource-based gate that suspends the RunAsync step loop when paused and releases it when resumed. The gate SHALL be initialized as a completed TCS (no blocking on first check).

#### Scenario: Initial state is open
- **WHEN** TraversalEngine is constructed and RunAsync starts
- **THEN** `_resumeSignal` SHALL be a completed TaskCompletionSource (await returns immediately)
- **AND** the step loop SHALL execute normally without blocking

#### Scenario: Pause closes gate
- **WHEN** PauseAsync() is called during Traversing
- **THEN** `_resumeSignal` SHALL be replaced with a new uncompleted TaskCompletionSource
- **AND** the next step loop pause check SHALL block on this new TCS

#### Scenario: Resume opens gate
- **WHEN** ResumeAsync() is called during Paused
- **THEN** TrySetResult SHALL be called on the current uncompleted TaskCompletionSource
- **AND** the step loop's pending await SHALL complete, allowing the step loop to continue

#### Scenario: Multiple pause/resume cycles
- **WHEN** Pause→Resume→Pause→Resume is called in sequence
- **THEN** each pause SHALL create a fresh uncompleted TCS (natural reset)
- **AND** each resume SHALL complete the current TCS
- **AND** the step loop SHALL correctly block/release each cycle

### Requirement: Duplicate resume is safe
If ResumeAsync is called when the gate is already open (e.g., race condition from two callers), TrySetResult SHALL be used instead of SetResult to avoid throwing on already-completed TCS.

#### Scenario: Extra ResumeAsync is no-op
- **WHEN** ResumeAsync is called while already Traversing (gate is already completed)
- **THEN** DomainValidationException SHALL be thrown by precondition check before any gate mutation
- **AND** if the precondition somehow passes (impossible with current matrix), TrySetResult SHALL return false without throwing
