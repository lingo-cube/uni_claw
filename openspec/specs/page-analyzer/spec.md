## ADDED Requirements

### Requirement: IPageAnalyzer defines 3 methods for page perception and verification

IPageAnalyzer SHALL define exactly 3 async methods:
- `Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)`
- `Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)`
- `Task<PageTypeVerification> VerifyPageTypeAsync(PageAnalysis pageAnalysis, string expectedType, string? expectedPageName = null, CancellationToken ct = default)`

IPageAnalyzer SHALL NOT include scroll-related methods (HasScroll, GetScrollProgress, IsEndOfList, GetScrollSwipeConfig). IPageAnalyzer SHALL NOT include VerifyPageWithVisionAsync (Host layer convenience method, YAGNI).

#### Scenario: AnalyzeCurrentPageAsync returns PageAnalysis from screenshot analysis
- **WHEN** IPageAnalyzer.AnalyzeCurrentPageAsync is called
- **THEN** implementation captures screenshot, invokes AI model, returns PageAnalysis or null on failure

#### Scenario: FindAppEntryAsync returns target app icon coordinates
- **WHEN** IPageAnalyzer.FindAppEntryAsync("Settings") is called
- **THEN** returns AppEntryPoint with icon coordinates, or null if not found

#### Scenario: VerifyPageTypeAsync validates page type from metadata
- **WHEN** IPageAnalyzer.VerifyPageTypeAsync(pageAnalysis, "settings_list") is called
- **THEN** returns PageTypeVerification with IsMatch, Confidence, ActualType, Reasoning

#### Scenario: IPageAnalyzer has zero scroll methods
- **WHEN** IPageAnalyzer interface is inspected
- **THEN** it does not contain HasScroll, GetScrollProgress, IsEndOfList, or GetScrollSwipeConfig methods

### Requirement: AppEntryPoint is sealed record class with coordinate fields

AppEntryPoint SHALL be a sealed record class with:
- `string AppName`
- `double X` (normalized 0-1)
- `double Y` (normalized 0-1)
- `double Confidence`

AppEntryPoint SHALL use DomainValidationException for X/Y range validation (0-1) and Confidence range validation (0-1).

#### Scenario: AppEntryPoint validates coordinate ranges
- **WHEN** AppEntryPoint is constructed with X=1.5
- **THEN** DomainValidationException is thrown with FieldName="X" and IllegalValue=1.5

#### Scenario: AppEntryPoint validates confidence range
- **WHEN** AppEntryPoint is constructed with Confidence=-0.1
- **THEN** DomainValidationException is thrown with FieldName="Confidence" and IllegalValue=-0.1
