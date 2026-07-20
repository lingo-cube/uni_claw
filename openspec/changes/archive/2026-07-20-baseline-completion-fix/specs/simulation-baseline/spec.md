## MODIFIED Requirements

### Requirement: Baseline JSON numericAnchor values SHALL reflect actual engine behavior after DFS fix

The `numericAnchor` values in `settings-full-traversal.json` and `settings-target-search.json` SHALL be recalibrated to reflect the actual engine behavior after fixing DynamicChildManager dedup scope and InterceptionHandler PressBack logic. Values SHALL be obtained by running the baseline tests with `--logger "console;verbosity=detailed"` and recording the actual TotalSteps, VisitedPagesCount, ActionHistoryCount, and ElapsedSeconds. Per §4.1.2, `elapsedSecondsMax` SHALL use a generous value (5-10× actual) to avoid CI flakiness.

#### Scenario: Full traversal numericAnchor recalibrated
- **WHEN** `SettingsApp_FullTraversal_AllVisited` runs after the DFS fix
- **THEN** `totalSteps`, `visitedPagesCount`, `actionHistoryCount` SHALL be updated to match actual engine output (±5% tolerance in Verify)
- **AND** `elapsedSecondsMax` SHALL be ≥ 5s (generous CI tolerance)

#### Scenario: Target search numericAnchor recalibrated
- **WHEN** `SettingsApp_TargetSearch_StopsAtDarkMode` runs after the DFS fix
- **THEN** `totalSteps`, `visitedPagesCount`, `actionHistoryCount` SHALL reflect the shorter traversal (TargetFound terminates early)
- **AND** values SHALL be significantly fewer than full traversal values

### Requirement: ElementCoverage SHALL include all 18 required elements in full traversal

After the DFS fix, `elementCoverage.required` for `settings-full-traversal.json` SHALL include all 18 non-readonly/non-back_button elements from the 7-page fixture. The engine SHALL visit all 18 elements (exact mode: `missed ⊆ AllowedMisses.Ids` and `extra = ∅`). Previously missed elements [device_2, dark_mode, network_2, network_3] SHALL now be visited.

#### Scenario: All 18 elements visited in full traversal
- **WHEN** `SettingsApp_FullTraversal_AllVisited` runs after the DFS fix
- **THEN** `element_coverage:completeness` SHALL PASS with matched = 18/18, missed = [], extra = []

#### Scenario: No AllowedMisses needed for previously missed elements
- **WHEN** the full traversal element coverage is verified
- **THEN** `allowedMisses` SHALL remain empty (no legitimate engine bug to exempt — all elements must be visited)

### Requirement: TargetFound SHALL terminate traversal at Dark mode in target search scenario

After the DFS fix, `SettingsApp_TargetSearch_StopsAtDarkMode` SHALL complete with `CompletionReason = target_found`. The engine SHALL visit "Dark mode" (as a tap action), detect the TargetFound match, and terminate immediately. Pages after Display in DFS order (Storage, Internal Storage, SD Card) SHALL NOT be visited.

#### Scenario: TargetFound triggers at Dark mode
- **WHEN** `SettingsApp_TargetSearch_StopsAtDarkMode` runs with CompletionPolicy TargetFound("Dark mode", Exact, MarkAndStop)
- **THEN** `result.CompletionReason` SHALL be `target_found`
- **AND** `result.Success` SHALL be `true`

#### Scenario: Early termination prevents visiting Storage subtree
- **WHEN** TargetFound terminates at Dark mode (in Display subtree)
- **THEN** `result.VisitedPages` SHALL NOT contain "Storage", "Internal Storage", or "SD Card"
