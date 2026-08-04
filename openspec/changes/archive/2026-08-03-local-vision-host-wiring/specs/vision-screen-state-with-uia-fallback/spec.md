# vision-screen-state-with-uia-fallback Specification

## ADDED Requirements

### Requirement: VisionScreenStateProvider implements IObservableScreenStateProvider

VisionScreenStateProvider SHALL implement IObservableScreenStateProvider. The RefreshAsync method SHALL return a ScreenStateResult with HasScroll/IsEndOfList from the injected PageAnalysis accessor. HierarchyXml and HierarchyFingerprint SHALL be populated from the optional UIA provider when available, null otherwise.

#### Scenario: RefreshAsync returns Vision-derived scroll state

- **WHEN** VisionScreenStateProvider.RefreshAsync() is called and PageAnalysis has HasScroll=true
- **THEN** ScreenStateResult.HasScroll is true, IsEndOfList is from PageAnalysis

#### Scenario: RefreshAsync with UIA available includes hierarchy

- **WHEN** VisionScreenStateProvider has a non-null UIA provider and RefreshAsync succeeds
- **THEN** ScreenStateResult.HierarchyXml and HierarchyFingerprint are populated from UIA

#### Scenario: RefreshAsync with UIA unavailable still succeeds

- **WHEN** VisionScreenStateProvider has a null UIA provider
- **THEN** ScreenStateResult.Succeeded is true, HierarchyXml is null, HasScroll/IsEndOfList are from PageAnalysis

#### Scenario: UIA failure does not affect main path

- **WHEN** VisionScreenStateProvider has a non-null UIA provider but it throws
- **THEN** ScreenStateResult.Succeeded is still true, HasScroll/IsEndOfList still from PageAnalysis, HierarchyXml is null
