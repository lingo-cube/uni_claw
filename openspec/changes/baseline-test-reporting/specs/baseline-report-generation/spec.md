## ADDED Requirements

### Requirement: BaselineReportCollector collects test results across all baseline tests

`BaselineReportCollector` SHALL be an xUnit `ICollectionFixture` that collects `BaselineReport` data from all baseline tests before writing reports. The collector SHALL provide an `Add()` method accepting scenario name, expected behavior, traversal result, verification report, and optional mock services. The collector SHALL trigger report writing in `Dispose()` after all tests complete.

#### Scenario: Collector gathers reports from multiple tests
- **WHEN** multiple baseline tests call `Collector.Add()` with their results
- **THEN** the collector accumulates all reports before writing
- **AND** `Dispose()` writes all reports together

#### Scenario: Disposal triggers report generation
- **WHEN** xUnit calls `Collector.Dispose()` after all tests complete
- **THEN** the collector invokes `BaselineReportWriter.WriteAll()`
- **AND** reports are written to the `reports/` directory

### Requirement: BaselineReportWriter generates JSON and Markdown reports

`BaselineReportWriter` SHALL provide static methods to write baseline test reports in dual format: JSON per-scenario reports and an aggregated index.md. The JSON format SHALL use camelCase naming consistent with `DomainJsonOptions`. The writer SHALL create the `reports/` directory if it doesn't exist.

#### Scenario: WriteJson creates per-scenario JSON report
- **WHEN** `WriteJson(report)` is called with a `BaselineReport`
- **THEN** a `{scenario}.json` file is created in the reports directory
- **AND** JSON uses camelCase property names
- **AND** all fields (Scenario, Timestamp, AllPassed, Details, ExpectedNumeric, ActualNumeric) are serialized

#### Scenario: WriteIndex creates aggregate Markdown summary
- **WHEN** `WriteIndex(allReports)` is called with multiple reports
- **THEN** an `index.md` file is created in the reports directory
- **AND** index includes run timestamp, pass rate (X/Y format), and table of all scenarios
- **AND** table columns include: Scenario, Status, Steps, Pages, Actions, Scrolls, Details

#### Scenario: Directory creation on demand
- **WHEN** report writer is invoked and reports/ directory doesn't exist
- **THEN** the directory is created automatically

### Requirement: BaselineReport data model contains verification and numeric results

`BaselineReport` SHALL be a sealed record class containing: `Scenario` (string), `Timestamp` (DateTime), `AllPassed` (bool), `Details` (ImmutableArray<RuleResult>), `ExpectedNumeric` (NumericAnchor), and `ActualNumeric` (NumericAnchor). The record SHALL be serializable to JSON using `DomainJsonOptions`.

#### Scenario: BaselineReport captures all verification results
- **WHEN** a `BaselineReport` is created from test data
- **THEN** it contains the scenario name, timestamp, verification outcome, and numeric comparisons
- **AND** both expected and actual `NumericAnchor` values are preserved

#### Scenario: BaselineReport excludes aggregate statistics
- **WHEN** a `BaselineReport` is constructed
- **THEN** it does NOT include TotalScenarios or PassedScenarios fields
- **AND** aggregate stats are computed during index generation, not stored per-report

### Requirement: Report generation failures do not break tests

The report writing infrastructure SHALL catch all I/O and serialization exceptions, log errors to `Console.WriteLine`, and never cause test failures. Individual file write failures SHALL NOT prevent other files from being written.

#### Scenario: JSON write failure is logged and ignored
- **WHEN** writing a JSON file throws `IOException` or `UnauthorizedAccessException`
- **THEN** the exception is caught and logged via `Console.WriteLine`
- **AND** other reports continue to be written
- **AND** the test result is not affected

#### Scenario: Index write failure is logged and ignored
- **WHEN** writing index.md throws any exception
- **THEN** the exception is caught and logged via `Console.WriteLine`
- **AND** the test result is not affected

### Requirement: Non-scroll tests integrate with minimal code changes

Non-scroll baseline tests SHALL integrate with report collection by adding a single line calling `Collector.Add(scenario, expected, result, report)` after the existing verification assert.

#### Scenario: SimulationBaselineTests integration
- **WHEN** a non-scroll test (e.g., `SettingsApp_FullTraversal_AllVisited`) runs
- **THEN** it calls `Collector.Add("settings-full-traversal", expected, result, report)` after `Assert.True`
- **AND** no other test code changes are required

### Requirement: Scroll tests pass mock services for metric extraction

Scroll-enabled baseline tests SHALL integrate with report collection by calling `Collector.Add(scenario, expected, result, report, executor, vision)` with cast mock service references. This enables extraction of scroll-specific metrics.

#### Scenario: ScrollableBaselineTests integration
- **WHEN** a scroll test (e.g., `WiFiList_ScrollThroughAllScreens`) runs
- **THEN** it calls `Collector.Add()` with `executor: (ScrollableMockActionExecutor)engine.ActionExecutor`
- **AND** it calls `Collector.Add()` with `vision: (ScrollableMockVisionService)engine.VisionProvider`
- **AND** report includes scroll metrics extracted from these services

### Requirement: Reports directory is gitignored

The `tests/UniClaw.Core.Tests/Baseline/reports/` directory SHALL be added to `.gitignore` to prevent committing test run artifacts.

#### Scenario: Reports not tracked in git
- **WHEN** reports are generated in the reports/ directory
- **THEN** these files are not committed to version control
- **AND** `.gitignore` contains the appropriate ignore pattern
