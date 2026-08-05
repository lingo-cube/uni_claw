# vision-screen-state-with-uia-fallback delta Specification

## MODIFIED Requirements

### Requirement: VisionScreenStateProvider implements IObservableScreenStateProvider

VisionScreenStateProvider SHALL implement IObservableScreenStateProvider. The RefreshAsync method SHALL return a ScreenStateResult with HasScroll/IsEndOfList read from the current PageAnalysis accessor, always optimistic — HasScroll SHALL be true and IsEndOfList SHALL be false (a single vision frame cannot prove end-of-list; the ROI snapshot comparison in TryHandleScrollAsync is the sole end-of-list authority). RefreshAsync SHALL NOT accept previousHierarchyXml/afterScroll parameters, SHALL NOT consult any UIA provider, and SHALL NOT produce HierarchyXml/HierarchyFingerprint.

#### Scenario: RefreshAsync returns optimistic vision-derived scroll state

- **WHEN** VisionScreenStateProvider.RefreshAsync() is called
- **THEN** ScreenStateResult.Succeeded is true, HasScroll is true, and IsEndOfList is false (optimistic constants, not derived from PageAnalysis field values)

#### Scenario: RefreshAsync signature carries no UIA parameters

- **WHEN** the IObservableScreenStateProvider.RefreshAsync declaration is inspected
- **THEN** it accepts only a CancellationToken — the previousHierarchyXml and afterScroll parameters SHALL NOT exist

#### Scenario: RefreshAsync result carries no hierarchy fields

- **WHEN** RefreshAsync returns a result
- **THEN** the ScreenStateResult SHALL NOT contain HierarchyXml or HierarchyFingerprint (the fields are removed from the record)

#### Scenario: RefreshAsync never consults a UIA side channel

- **WHEN** RefreshAsync executes
- **THEN** no UIA provider is invoked — VisionScreenStateProvider SHALL NOT hold a `_uia` field and SHALL NOT call `IObservableScreenStateProvider.RefreshAsync` as a redundant side channel

## REMOVED Requirements

### Requirement: VisionScreenStateProvider UIA fallback side channel

VisionScreenStateProvider SHALL hold an optional UIA provider (`_uia` field supplied via constructor) and, within RefreshAsync, SHALL call `IObservableScreenStateProvider.RefreshAsync` on it as a redundant side channel, populating HierarchyXml and HierarchyFingerprint on the returned ScreenStateResult when the UIA refresh succeeds. UIA failures (exception or failed result) SHALL NOT affect the vision main path.

#### Scenario: RefreshAsync with UIA available includes hierarchy

- **WHEN** VisionScreenStateProvider has a non-null UIA provider and RefreshAsync succeeds
- **THEN** ScreenStateResult.HierarchyXml and HierarchyFingerprint are populated from UIA

#### Scenario: UIA failure does not affect main path

- **WHEN** VisionScreenStateProvider has a non-null UIA provider but it throws
- **THEN** ScreenStateResult.Succeeded is still true, HasScroll/IsEndOfList still from PageAnalysis, HierarchyXml is null

**Reason:** The UIA hierarchy side channel is a redundant second observation path alongside vision (UIAutomator XML dump vs YOLO + OCR). UIAutomator is Android-only and unavailable on WebView pages and car systems, and the hierarchy it supplies is consumed solely by the D5 fingerprint fast-path in `InterceptionHandler.TryHandleScrollAsync`, which is also being removed. Per the PRD (§4.2) the `_uia` field, the try/catch side-channel call, and hierarchy population are deleted — scroll-end detection moves entirely to vision-only ROI snapshot comparison, so the provider no longer needs to supply hierarchy XML to any consumer.

**Migration:** Delete the `_uia` field and its constructor parameter from `VisionScreenStateProvider`; `RefreshAsync` SHALL return vision-only state directly with no UIA consultation and no try/catch. Delete `HierarchyXml`/`HierarchyFingerprint` from `ScreenStateResult`. `IObservableScreenStateProvider.RefreshAsync` SHALL drop the `previousHierarchyXml`/`afterScroll` parameters; the D5 fast-path caller in `InterceptionHandler.TryHandleScrollAsync` is removed with the scroll judgment rewrite. Scroll-end detection is now performed by `InterceptionHandler.TryHandleScrollAsync` via ROI snapshot comparison (`RoiSelector`, `StableFrameCapturer`, `SnapshotComparer`).
