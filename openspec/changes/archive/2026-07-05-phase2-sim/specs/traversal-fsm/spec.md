## ADDED Requirements

### Requirement: IVisionProvider defines AnalyzeCurrentPageAsync and FindAppEntryAsync

The `IVisionProvider` interface SHALL define exactly two methods:

1. `AnalyzeCurrentPageAsync(CancellationToken ct)` — returns `Task<PageAnalysis?>`, analyzing the current screen and returning structured page data including elements, menus, popups, and navigation controls.
2. `FindAppEntryAsync(string targetApp, CancellationToken ct)` — returns `Task<AppEntryPoint?>`, locating the target app's icon coordinates in the launcher/desktop.

The existing `GetCurrentPageAnalysisAsync` method SHALL be replaced by `AnalyzeCurrentPageAsync`.

A new `AppEntryPoint` record class SHALL be introduced with `double X` and `double Y` properties representing normalized coordinates (0-1).

#### Scenario: IVisionProvider has exactly 2 methods

- **WHEN** the `IVisionProvider` interface is inspected
- **THEN** it SHALL declare `AnalyzeCurrentPageAsync` and `FindAppEntryAsync`
- **AND** `GetCurrentPageAnalysisAsync` SHALL NOT exist

#### Scenario: AppEntryPoint carries normalized coordinates

- **WHEN** an `AppEntryPoint` is constructed with (0.5, 0.5)
- **THEN** `X` SHALL be 0.5 and `Y` SHALL be 0.5

#### Scenario: StatefulMockVisionService implements both methods

- **WHEN** `StatefulMockVisionService` is instantiated
- **THEN** it SHALL implement `AnalyzeCurrentPageAsync` (returns fixture-backed `PageAnalysis` or null)
- **AND** it SHALL implement `FindAppEntryAsync` (returns `AppEntryPoint(0.5, 0.5)`)
