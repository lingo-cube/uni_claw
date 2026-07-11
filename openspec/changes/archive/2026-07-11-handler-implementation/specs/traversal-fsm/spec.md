## MODIFIED Requirements

### Requirement: HandlePreconditionCheck determines next state based on precondition
HandlePreconditionCheck SHALL transition FSM to Execute when precondition passes, or ErrorHandling when precondition fails. Current implementation always returns Execute (assume pass). Real precondition checking requires ITraversalNode.Precondition (Phase 3 extension). Until then, handler SHALL return Execute with explicit TraceCoordinator.RecordDecision logging the "assume pass" decision.

#### Scenario: Precondition assumed pass
- **WHEN** HandlePreconditionCheck is invoked
- **THEN** handler transitions FSM to Execute
- **THEN** TraceCoordinator.RecordDecision called with "precondition_assume_pass"

#### Scenario: Precondition fails (Phase 3 future)
- **WHEN** ITraversalNode.Precondition returns false (future capability)
- **THEN** handler transitions FSM to ErrorHandling
- **THEN** TraceCoordinator.RecordDecision called with "precondition_failed"

### Requirement: HandleResultVerify verifies action result and routes to next state
HandleResultVerify SHALL check page stability after action execution. It SHALL retry up to 3 rounds with vision re-call. If popup detected during retry, it SHALL route to PopupHandling. If all retries fail, it SHALL route to Branch. Current stub always returns Branch without verification.

#### Scenario: Verification passes — page changed
- **WHEN** HandleResultVerify checks PageSnapshotManager.HasChanged and it returns true
- **THEN** handler transitions FSM to Branch

#### Scenario: Popup detected during verification retry
- **WHEN** HandleResultVerify detects popup during retry round
- **THEN** handler transitions FSM to PopupHandling

### Requirement: HandleErrorHandling selects recovery strategy and transitions FSM
HandleErrorHandling SHALL delegate to RecoveryExecutor for 5-strategy recovery (Retry→Execute, Backtrack→NodeSelect, Skip→Branch, Continue→NodeSelect, Abort→FrameComplete). It SHALL track consecutive errors via TraversalRuntimeContext._consecutiveErrors. Current stub always returns NodeSelect without recovery logic.

#### Scenario: Error recovery with Retry strategy
- **WHEN** ErrorClassifier determines Retry strategy
- **THEN** RecoveryExecutor.Execute returns Retry result
- **THEN** handler transitions FSM to Execute
- **THEN** ConsecutiveErrors incremented

#### Scenario: Error recovery with Abort strategy
- **WHEN** ErrorClassifier determines Abort strategy
- **THEN** RecoveryExecutor.Execute returns Abort + Failure outcome
- **THEN** handler transitions FSM to FrameComplete
- **THEN** ConsecutiveErrors reset to 0

### Requirement: HandlePopupHandling dismisses popup and routes to next state
HandlePopupHandling SHALL delegate to PopupHandler 6-step pipeline for popup detection and dismissal. On successful dismiss, it SHALL route to ResultVerify. On failed dismiss, it SHALL route to ErrorHandling. Current stub always returns ResultVerify without popup handling logic.

#### Scenario: Popup dismissed successfully
- **WHEN** PopupHandler.HandlePopup returns Success=true
- **THEN** handler transitions FSM to ResultVerify

#### Scenario: Popup dismiss failed
- **WHEN** PopupHandler.HandlePopup returns Success=false (fallback)
- **THEN** handler transitions FSM to ErrorHandling
