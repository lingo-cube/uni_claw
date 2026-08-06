## MODIFIED Requirements

### Requirement: PopupHandling handler delegates to PopupHandler pipeline

HandlePopupHandling SHALL delegate popup detection and dismissal to PopupHandler (existing 6-step pipeline + PopupActionExecutor dispatch-table). It SHALL NOT implement dismiss logic directly. It SHALL call PopupHandler.HandlePopup() which returns PopupHandlingResult (Success + Action + Description). On failed dismiss (Success=false), HandlePopupHandling SHALL set `TraversalRuntimeContext.LastError` to an `InvalidOperationException` describing the failure before routing to ErrorHandling. The exception message SHALL begin with `"Popup dismiss failed:"` and SHALL NOT contain PopupType or DismissStrategy enum names.

#### Scenario: Popup detected and dismissed successfully
- **WHEN** HandlePopupHandling is invoked and PopupHandler.HandlePopup returns Success=true
- **THEN** handler transitions FSM to ResultVerify (return to verification after popup dismissed)
- **THEN** LastError is NOT modified

#### Scenario: Popup detected but dismiss fails
- **WHEN** PopupHandler.HandlePopup returns Success=false (dispatch-table fallback: "back_fallback")
- **THEN** handler sets LastError to InvalidOperationException with message "Popup dismiss failed: dismiss_action=<action>" (using the Action field from PopupHandlingResult; fallback to "Popup dismiss failed: action=<action>" when Classification is null)
- **THEN** handler transitions FSM to ErrorHandling (dismiss failure needs error recovery)

#### Scenario: Popup dismiss failure message is safe for ErrorClassifier
- **WHEN** PopupHandler.HandlePopup returns Success=false
- **THEN** the LastError exception message does NOT contain the substrings "Permission", "Error", "Timeout", "Ad", "Dialog", or "Anr" (which would collide with ErrorClassifier's case-insensitive substring matching)
