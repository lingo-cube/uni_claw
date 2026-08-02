## MODIFIED Requirements

### Requirement: Page analysis uses unified observation pipeline
The `PageAnalyzer` SHALL delegate UIA-first thresholding, popup detection, and AI fallback to `ObservationPipeline` instead of implementing these decisions internally.

#### Scenario: UIA threshold check moved to pipeline
- **WHEN** `PageAnalyzer.AnalyzeCurrentPageAsync` is called
- **THEN** it SHALL invoke `ObservationPipeline.AnalyzeAsync(screenshot, xml, config)`
- **AND** SHALL NOT contain its own `Items.Length >= N` check

### Requirement: UiAutomatorAugmentingPageAnalyzer is removed
The `UiAutomatorAugmentingPageAnalyzer` class SHALL be removed. Its UIA-augmentation logic SHALL be subsumed by `ObservationPipeline`.

#### Scenario: Old augmenter no longer used
- **WHEN** `InvalidatingPageAnalysisCache` wraps the page analyzer
- **THEN** it SHALL wrap `PageAnalyzer` directly, not `UiAutomatorAugmentingPageAnalyzer`

## REMOVED Requirements

### Requirement: AdbScenarioObservationSource.useUiAutomatorAnalysis switch
**Reason**: The `useUiAutomatorAnalysis` boolean toggle on `AdbScenarioObservationSource` is replaced by the unified `ObservationPipeline` configuration.
**Migration**: Replace `useUiAutomatorAnalysis: true/false` with `ObservationConfig { UIA_MinItems: N }` (N=0 → always AI; N=large → mostly AI).
