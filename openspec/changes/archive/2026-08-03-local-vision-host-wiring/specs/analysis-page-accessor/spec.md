# analysis-page-accessor Specification

## ADDED Requirements

### Requirement: CurrentPageAnalysisAccessor provides shared state between writer and reader

`CurrentPageAnalysisAccessor` SHALL be a sealed class in `UniClaw.Host` namespace with a single `PageAnalysis? Current` property. It SHALL be created once at assembly time and injected into both the AnalysisWritingDecorator (writer) and VisionScreenStateProvider (reader).

#### Scenario: Writer updates after analysis

- **WHEN** AnalysisWritingDecorator.AnalyzeCurrentPageAsync completes
- **THEN** accessor.Current is set to the returned PageAnalysis

#### Scenario: Reader returns latest value

- **WHEN** VisionScreenStateProvider.HasScroll() is called after an analysis completed
- **THEN** the returned value matches accessor.Current?.HasScroll

#### Scenario: Null before first analysis

- **WHEN** VisionScreenStateProvider.HasScroll() is called before any analysis has run
- **THEN** returns false (accessor.Current is null → safe default)
