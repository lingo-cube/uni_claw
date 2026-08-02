## ADDED Requirements

### Requirement: Observation pipeline provides UIA-first analysis with AI fallback
The system SHALL provide a unified observation pipeline that accepts a screenshot and UIA XML, attempts UIA-based analysis first, and falls back to AI vision when UIA is insufficient.

#### Scenario: UIA produces sufficient items
- **WHEN** UIA dump succeeds AND UIA parsing yields ≥N items AND no popup items are detected
- **THEN** the pipeline SHALL return the UIA-only PageAnalysis without calling AI
- **AND** SHALL record a trace decision with path "UIA"

#### Scenario: UIA produces too few items
- **WHEN** UIA dump succeeds AND UIA parsing yields <N items
- **THEN** the pipeline SHALL call AI vision and return the AI-produced PageAnalysis
- **AND** SHALL record a trace decision with path "AI"

#### Scenario: UIA detects popup-like items
- **WHEN** UIA parsing produces items matching popup button labels (e.g. "close app", "dismiss", "allow", "deny")
- **THEN** the pipeline SHALL fall through to AI vision
- **AND** SHALL NOT return the UIA-only analysis

#### Scenario: UIA dump fails
- **WHEN** UIA dump returns `Succeeded=false` or empty `HierarchyXml`
- **THEN** the pipeline SHALL call AI vision directly without attempting UIA parsing

### Requirement: UIA can be disabled dynamically
The system SHALL automatically disable UIA-first analysis when the device's UIAutomator is unavailable, falling back to AI-only for the remainder of the session.

#### Scenario: First UIA dump fails
- **WHEN** the first `AdbScreenStateProvider.RefreshAsync` call fails
- **THEN** UIA SHALL be marked as unavailable for the session
- **AND** subsequent observations SHALL skip UIA parsing and call AI directly

#### Scenario: Back navigation skips UIA dump
- **WHEN** `SkipUIAOnBackNavigation` is enabled AND the current action is "back"
- **THEN** the pipeline SHALL reuse the cached PageAnalysis from before the back action
- **AND** SHALL NOT perform an ADB UIA dump

### Requirement: Pipeline is configurable
The system SHALL expose configuration for the observation pipeline's behavior through `ObservationConfig`.

#### Scenario: Configuration applied
- **WHEN** `ObservationConfig` is provided with `UIA_MinItems=5`
- **THEN** UIA SHALL be skipped unless ≥5 items are detected
- **WHEN** `ObservationConfig.EnablePopupDetection=false`
- **THEN** popup heuristics SHALL NOT trigger AI fallback
