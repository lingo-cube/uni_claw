## 1. Mock Service Extensions

- [x] 1.1 Add `GetScrollUpCount()` method to `ScrollableMockActionExecutor`
  - Implementation: `return ScrollHistory.Count(s => s.IsScrollUp);`
  - File: `src/UniClaw.Core/Simulation/Scroll/ScrollableMockActionExecutor.cs`
- [x] 1.2 Add `GetScrollDistance()` method to `ScrollableMockVisionService`
  - Implementation: `return GetScrollProgress(CurrentPageId);`
  - File: `src/UniClaw.Core/Simulation/Scroll/ScrollableMockVisionService.cs`

## 2. Report Infrastructure

- [x] 2.1 Create `BaselineReport` record class
  - Location: `tests/UniClaw.Core.Tests/Baseline/BaselineReport.cs`
  - Fields: Scenario, Timestamp, AllPassed, Details, ExpectedNumeric, ActualNumeric
  - Add `[JsonSerializable]` attribute for System.Text.Json
- [x] 2.2 Create `BaselineReportCollector` class
  - Location: `tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs`
  - Implement `ICollectionFixture` pattern
  - Add `Add()` method with executor/vision optional parameters
  - Add `BuildActualNumeric()` private method
  - Implement `Dispose()` → `WriteAll()` flow
- [x] 2.3 Create `BaselineReportWriter` class
  - Location: `tests/UniClaw.Core.Tests/Baseline/BaselineReportWriter.cs`
  - Add static `WriteJson()` method with error handling
  - Add static `WriteIndex()` method with Markdown template
  - Use `DomainJsonOptions.Default` for JSON serialization
- [x] 2.4 Create reports directory and gitkeep
  - Location: `tests/UniClaw.Core.Tests/Baseline/reports/.gitkeep`
- [x] 2.5 Update `.gitignore`
  - Add: `tests/UniClaw.Core.Tests/Baseline/reports/`

## 3. Test Integration - Non-Scroll Tests

- [x] 3.1 Update `SimulationBaselineTests.cs` - FullTraversal test
  - Add after `Assert.True`: `Collector.Add("settings-full-traversal", expected, result, report);`
- [x] 3.2 Update `SimulationBaselineTests.cs` - TargetSearch test
  - Add after `Assert.True`: `Collector.Add("settings-target-search", expected, result, report);`

## 4. Test Integration - Scroll Tests

- [x] 4.1 Update `ScrollableBaselineTests.cs` - WiFiList_ScrollThroughAllScreens
  - Add `Collector.Add()` with executor/vision casting
- [x] 4.2 Update `ScrollableBaselineTests.cs` - WiFiList_ScrollBackToTop
  - Add `Collector.Add()` with executor/vision casting
- [x] 4.3 Update `ScrollableBaselineTests.cs` - WiFiList_ElementDeduplication
  - Add `Collector.Add()` with executor/vision casting
- [x] 4.4 Update `ScrollableBaselineTests.cs` - WiFiList_BoundaryConditions
  - Add `Collector.Add()` with executor/vision casting
- [x] 4.5 Update `ScrollableBaselineTests.cs` - SparseList_JumpRecovery
  - Add `Collector.Add()` with executor/vision casting
- [x] 4.6 Update `ScrollableBaselineTests.cs` - OverlappingList_AdaptiveStep
  - Add `Collector.Add()` with executor/vision casting

## 5. Verification

- [ ] 5.1 Run all tests: `dotnet test tests/UniClaw.Core.Tests.sln`
- [ ] 5.2 Verify reports directory created with 8 JSON files + index.md
- [ ] 5.3 Verify JSON format uses camelCase and matches schema
- [ ] 5.4 Verify index.md contains pass rate and all scenarios
- [ ] 5.5 Verify scroll metrics (ScrollCount, ScrollDistance) appear in scroll test reports
- [ ] 5.6 Verify non-scroll tests show scroll metrics as 0

## 6. Documentation Updates (Phase 4)

- [x] 6.1 Update `docs/system/layers/simulation-baseline.md` §3 gap table
  - Mark baseline reporting system as implemented
- [x] 6.2 Add decision record to `docs/system/decisions/log.md`
  - Add entry for D-N: Baseline Reporting architecture
