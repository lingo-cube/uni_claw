## ADDED Requirements

### Requirement: PopupHandling handler delegates to PopupHandler pipeline
HandlePopupHandling SHALL delegate popup detection and dismissal to PopupHandler (existing 6-step pipeline + PopupActionExecutor dispatch-table). It SHALL NOT implement dismiss logic directly. It SHALL call PopupHandler.HandlePopup() which returns PopupHandlingResult (Success + Action + Description).

#### Scenario: Popup detected and dismissed successfully
- **WHEN** HandlePopupHandling is invoked and PopupHandler.HandlePopup returns Success=true
- **THEN** handler transitions FSM to ResultVerify (return to verification after popup dismissed)

#### Scenario: Popup detected but dismiss fails
- **WHEN** PopupHandler.HandlePopup returns Success=false (dispatch-table fallback: "back_fallback")
- **THEN** handler transitions FSM to ErrorHandling (dismiss failure needs error recovery)

### Requirement: PopupHandling handler uses PopupClassifier for detection
HandlePopupHandling SHALL use PopupDetector (regex-based) and PopupClassifier (PopupType + DismissStrategy + UrgencyLevel determination) for popup detection before delegation to PopupHandler pipeline. These components are already implemented.

#### Scenario: PopupClassifier identifies Permission popup
- **WHEN** PopupDetector detects popup and PopupClassifier classifies as PopupType.Permission
- **THEN** PopupHandler.HandlePopup uses Permission-specific dismiss strategy (→ PopupActionExecutor dispatch)

#### Scenario: PopupClassifier identifies Ad popup with dismiss target
- **WHEN** PopupClassifier classifies as PopupType.Ad with dismiss_target present
- **THEN** DismissStrategy = AutoClose (per D-10 conditional logic)

### Requirement: PopupHandling handler records trace on dismiss outcome
HandlePopupHandling SHALL call TraceCoordinator.RecordStateTransition with FsmType="TraversalFSM" and the FSM transition target (ResultVerify or ErrorHandling). It SHALL also call TraceCoordinator.RecordDecision with dismiss outcome details.

#### Scenario: Trace recording on successful dismiss
- **WHEN** PopupHandler.HandlePopup returns Success=true
- **THEN** TraceCoordinator.RecordStateTransition called with From=PopupHandling, To=ResultVerify

#### Scenario: Trace recording on failed dismiss
- **WHEN** PopupHandler.HandlePopup returns Success=false
- **THEN** TraceCoordinator.RecordStateTransition called with From=PopupHandling, To=ErrorHandling
