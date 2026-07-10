## ADDED Requirements

### Requirement: SimulationBaselineTests provides 7-page Settings App fixture for baseline scenarios

SimulationBaselineTests.cs SHALL contain a `private static StateFixture SettingsAppFixture7Pages()` method that constructs a 7+2 page Settings App fixture via StateFixtureBuilder Fluent API. The fixture SHALL contain pages: home(6 elements), wifi(3 elements+back), bluetooth(3 elements+back), display(3 elements+back), storage(2 elements+back), storage_internal(3 readonly+back), storage_external(2 readonly+back) with 11 transitions (6 forward, 4 back, 2 sub-page). This fixture SHALL be shared between both baseline test scenarios.

#### Scenario: Fixture contains all 7 pages
- **WHEN** SettingsAppFixture7Pages() is called
- **THEN** the resulting StateFixture contains exactly 7 pages: home, wifi, bluetooth, display, storage, storage_internal, storage_external

#### Scenario: Fixture contains all 11 transitions
- **WHEN** SettingsAppFixture7Pages() is called
- **THEN** the resulting StateFixture contains transitions for 6 forward (home→wifi, home→bluetooth, home→display, home→storage, storage→internal, storage→external), 4 back (wifi→home, bluetooth→home, display→home, storage→home), and 2 sub-page back (internal→storage, external→storage)

### Requirement: SettingsApp_FullTraversal_AllVisited tests complete DFS traversal without CompletionPolicy

SimulationBaselineTests.cs SHALL contain a `[Fact]` test method `SettingsApp_FullTraversal_AllVisited` that uses the 7-page fixture with DynamicMatch root (menu_rule + switch_rule) and CompletionPolicy=null. The test SHALL verify: (1) `result.Success == true`, (2) `result.CompletionReason == TraversalResult.Reasons.AllVisited`, (3) `result.VisitedPages.Count >= 7`, (4) `Assert.Contains("home", result.VisitedPages)`, (5) `Assert.Contains("wifi", result.VisitedPages)`, (6) `result.TotalSteps > 0`, (7) `result.ActionHistory.Length > 0`.

#### Scenario: Full traversal completes all pages naturally
- **WHEN** SettingsApp_FullTraversal_AllVisited runs with 7-page fixture and CompletionPolicy=null
- **THEN** result.Success is true, CompletionReason is "all_visited", VisitedPages contains at least 7 pages including "home" and "wifi"

#### Scenario: Full traversal has actual execution
- **WHEN** SettingsApp_FullTraversal_AllVisited runs
- **THEN** TotalSteps > 0 and ActionHistory has entries

### Requirement: SettingsApp_TargetSearch_StopsAtDarkMode tests TargetFound early termination

SimulationBaselineTests.cs SHALL contain a `[Fact]` test method `SettingsApp_TargetSearch_StopsAtDarkMode` that uses the same 7-page fixture and DynamicMatch root as scenario 1, with CompletionPolicy = TargetFound(TargetName="Dark mode", MatchMode=Exact, ActionOnFound=MarkAndStop). The test SHALL verify: (1) `result.Success == true`, (2) `result.CompletionReason == TraversalResult.Reasons.TargetFound`, (3) `Assert.Contains("Display", result.VisitedPages)` — target is in Display subtree, (4) `Assert.DoesNotContain("Storage", result.VisitedPages)` — early termination proof, (5) `result.TotalSteps > 0 && result.TotalSteps < fullTraversalTotalSteps` — fewer steps than full traversal.

#### Scenario: Target search stops at Dark mode node
- **WHEN** SettingsApp_TargetSearch_StopsAtDarkMode runs with CompletionPolicy TargetFound "Dark mode" Exact
- **THEN** result.Success is true, CompletionReason is "target_found"

#### Scenario: Display page visited (DFS reached target subtree)
- **WHEN** SettingsApp_TargetSearch_StopsAtDarkMode runs
- **THEN** result.VisitedPages contains "Display" (DFS traversed to Display subtree where target resides)

#### Scenario: Storage page NOT visited (early termination proof)
- **WHEN** SettingsApp_TargetSearch_StopsAtDarkMode runs
- **THEN** result.VisitedPages does NOT contain "Storage" (DFS stopped after finding target, did not continue to Storage)

#### Scenario: Target search has fewer steps than full traversal
- **WHEN** SettingsApp_TargetSearch_StopsAtDarkMode runs
- **THEN** TotalSteps > 0 and TotalSteps is less than the full traversal scenario's TotalSteps

### Requirement: Baseline test assertions use range-based strategy in Phase B

Phase B baseline tests SHALL use range-based assertions (>=, Contains, DoesNotContain, >0) instead of exact numeric values. This tolerates C# vs Python DFS/step differences. Phase C SHALL upgrade to exact values (Count==, TotalSteps==) after C# actual runtime baseline values are confirmed.

#### Scenario: VisitedPages count uses >= assertion
- **WHEN** Phase B assertions are written for VisitedPages
- **THEN** `Assert.True(result.VisitedPages.Count >= 7)` is used, not `Assert.Equal(19, result.VisitedPages.Count)`

#### Scenario: TotalSteps uses > 0 assertion
- **WHEN** Phase B assertions are written for TotalSteps
- **THEN** `Assert.True(result.TotalSteps > 0)` is used, not `Assert.Equal(118, result.TotalSteps)`
