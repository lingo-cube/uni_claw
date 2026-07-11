## ADDED Requirements

### Requirement: ResultVerify handler checks page stability after action execution
HandleResultVerify SHALL verify that the page changed after the action was executed. It SHALL call PageSnapshotManager.HasChanged(current, previous) to detect changes. If no change detected, it SHALL retry up to 3 rounds by re-calling IVisionProvider.GetPageAnalysis() and re-checking HasChanged.

#### Scenario: Page changed on first check
- **WHEN** HandleResultVerify is invoked after Execute
- **THEN** PageSnapshotManager.HasChanged returns true
- **THEN** handler transitions FSM to Branch

#### Scenario: No change, retry succeeds after 2 rounds
- **WHEN** HandleResultVerify is invoked and HasChanged returns false on first check
- **THEN** handler retries IVisionProvider.GetPageAnalysis() up to 3 rounds
- **WHEN** HasChanged returns true on round 2
- **THEN** handler transitions FSM to Branch

#### Scenario: No change after 3 rounds — continue traversal
- **WHEN** HandleResultVerify retries 3 rounds and HasChanged still returns false
- **THEN** handler transitions FSM to Branch (continue traversal, do not block)

### Requirement: ResultVerify handler detects popup during retry
HandleResultVerify SHALL check for popup presence during each retry round. The primary detection mechanism SHALL be `PageAnalysis.IsPopup` (the authoritative flag from the vision/AI layer). If `PageAnalysis.IsPopup == true`, handler SHALL transition FSM to PopupHandling instead of continuing retry. PopupDetector regex-based detection SHALL NOT be used as an initial scan of normal page elements due to substring false positives (e.g., "ad" in "Headphones Pro"). PopupDetector is reserved for popup classification once IsPopup=true is confirmed.

#### Scenario: Popup detected during retry round 1
- **WHEN** HasChanged returns false and PopupDetector detects popup elements
- **THEN** handler transitions FSM to PopupHandling (popup handling takes priority over retry)

#### Scenario: Popup detected during retry round 2
- **WHEN** HasChanged returns false on round 1, retry continues, popup appears on round 2
- **THEN** handler transitions FSM to PopupHandling

### Requirement: ResultVerify handler records trace decisions
HandleResultVerify SHALL call TraceCoordinator.RecordDecision with SpanType=StateDecision for each retry round and final transition decision. This provides observability into retry count and final outcome.

#### Scenario: Trace recording on successful verification
- **WHEN** HasChanged returns true on first check
- **THEN** TraceCoordinator.RecordDecision called with "verification_passed_first_check"

#### Scenario: Trace recording on 3-round retry
- **WHEN** HasChanged returns true after multiple rounds
- **THEN** TraceCoordinator.RecordDecision called for each retry round + final success
