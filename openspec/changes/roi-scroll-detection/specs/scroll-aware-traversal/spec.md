# scroll-aware-traversal delta Specification

## MODIFIED Requirements

### Requirement: TryHandleScroll executes scroll as ROI aggregation comparison

`TryHandleScrollAsync` SHALL be an `internal` async method (no longer `static`) operating on the "scroll = action + judgment" model. It SHALL use instance fields `_roiRect`, `_currentBaseline`, `_consecutiveUnchanged`, `_consecutiveUnknown`, and a lazily-initialized `StableFrameCapturer`. It SHALL:

1. Ensure ROI via `GetOrSelectRoi` — reuse the full-screen raw screenshot associated with the current PageAnalysis when available; otherwise capture one via `IScreenCapture` and run `RoiSelector.Select`
2. Capture stable pre-scroll frame S0 via `StableFrameCapturer.CaptureBeforeScrollAsync` (one consecutive similar pair)
3. Execute swipe asynchronously via `await ctx.Action.SwipeAsync(...)` with coordinates from the resolved `ScrollSwipeConfig` (page-level config ?? engine default)
4. Capture stable post-scroll frame S1 via `StableFrameCapturer.CaptureAfterScrollAsync` (two consecutive similar pairs)
5. Compare S0/S1 via `SnapshotComparer.Compare` (dHash Hamming distance + mean absolute difference + changed pixel ratio)
6. Different(S0, S1) → Scrolled: update `_currentBaseline = S1`, reset `_consecutiveUnchanged` and `_consecutiveUnknown`, `ctx.ChildMgr.Invalidate(currentFrame.NodeId)`
7. Same(S0, S1) → second swipe with distance scaled by `SecondSwipeDistanceRatio` (0.5), same direction → capture S2 → compare three pairs (S0, S1), (S1, S2), (S0, S2):
   - all three Same → EndReached (release snapshots, complete the frame)
   - any pair Different → Scrolled (`_currentBaseline = S2`, invalidate children)
   - conflicting evidence → Unknown

A null S0/S1/S2 capture (retry or absolute-timeout exhaustion) SHALL yield Unknown and increment `_consecutiveUnknown`. When `_consecutiveUnknown` reaches `MaxConsecutiveUnknown` (3), `_roiRect` and `_currentBaseline` SHALL be cleared so the next scroll re-runs `RoiSelector.Select`.

#### Scenario: Scroll operation is awaited not blocked

- **WHEN** `TryHandleScrollAsync` executes
- **THEN** all `IActionExecutor` and `IScreenCapture` calls use `await`
- **AND** no `.GetAwaiter().GetResult()` is present in the method body

#### Scenario: Stable frame capture failure returns Unknown

- **WHEN** `CaptureBeforeScrollAsync` or `CaptureAfterScrollAsync` returns null (retry or absolute-timeout exhaustion)
- **THEN** `TryHandleScrollAsync` returns Unknown and `_consecutiveUnknown` is incremented

#### Scenario: Different(S0, S1) confirms scrolled

- **WHEN** `SnapshotComparer.Compare(S0, S1)` returns `IsSame = false`
- **THEN** `TryHandleScrollAsync` returns Scrolled
- **AND** `_currentBaseline` is updated to S1, counters are reset, and the current frame's children are invalidated

#### Scenario: Same(S0, S1) triggers reduced-distance second swipe

- **WHEN** `SnapshotComparer.Compare(S0, S1)` returns `IsSame = true`
- **THEN** a second swipe executes with distance = original distance × `SecondSwipeDistanceRatio` (0.5), same direction, followed by S2 capture

#### Scenario: All three pairs same confirms end of list

- **WHEN** (S0, S1), (S1, S2), and (S0, S2) are all `IsSame = true`
- **THEN** `TryHandleScrollAsync` returns EndReached, triggering frame completion (root → FrameComplete; non-root → PressBack + Pop)

#### Scenario: Any later pair different confirms scrolled

- **WHEN** (S1, S2) or (S0, S2) is `IsSame = false`
- **THEN** `TryHandleScrollAsync` returns Scrolled with `_currentBaseline` updated to S2

#### Scenario: Conflicting evidence returns Unknown

- **WHEN** evidence conflicts (e.g., hash similar but mean absolute difference significantly above threshold)
- **THEN** `TryHandleScrollAsync` returns Unknown without concluding Scrolled or EndReached

#### Scenario: Consecutive Unknown clears ROI

- **WHEN** `_consecutiveUnknown` reaches `MaxConsecutiveUnknown` (3)
- **THEN** `_roiRect` and `_currentBaseline` are cleared
- **AND** the next scroll re-runs `RoiSelector.Select` to pick a fresh ROI

## REMOVED Requirements

### Requirement: TryHandleScroll UIA fingerprint fast-path

`TryHandleScrollAsync` SHALL cast `ctx.ScreenState` to `IObservableScreenStateProvider` and, when the cast succeeds, SHALL dump the UIA hierarchy before the swipe (`preSwipe` via `RefreshAsync()`), execute the swipe, then dump again after (`postSwipe` via `RefreshAsync(previousHierarchyXml, afterScroll: true)`). An unchanged hierarchy fingerprint with `postSwipe.IsEndOfList = true` SHALL short-circuit with "end reached" without running the vision re-analysis and seen-set judgment.

#### Scenario: UIA fingerprint fast-path short-circuits end-of-list

- **WHEN** the pre/post swipe UIA hierarchy fingerprints are unchanged and `postSwipe.IsEndOfList` is true
- **THEN** `TryHandleScrollAsync` returns "end reached" without re-analyzing the page via vision

#### Scenario: UIA unavailable falls back to seen-set diff judgment

- **WHEN** `ctx.ScreenState` is not an `IObservableScreenStateProvider` or `preSwipe.HierarchyXml` is empty
- **THEN** `TryHandleScrollAsync` judges scroll progress via seen-set diff of the re-analyzed page elements

**Reason:** The D5 fast-path depends on the Android-only UIAutomator hierarchy dump and runs as a redundant second observation channel alongside the vision pipeline (approximately 30 lines in `InterceptionHandler`). UIAutomator is unavailable on WebView pages and car systems and blocks cross-platform migration. Per the PRD (§4.2) the `IObservableScreenStateProvider` cast plus preSwipe/postSwipe UIA hierarchy dump and fingerprint comparison are deleted — scroll-end detection becomes vision-only ROI aggregation comparison.

**Migration:** Delete the `IObservableScreenStateProvider` cast and the preSwipe/postSwipe UIA hierarchy dump block from `TryHandleScrollAsync`. `IObservableScreenStateProvider.RefreshAsync` SHALL drop the `previousHierarchyXml`/`afterScroll` parameters, and `IAdbSession.DumpUiHierarchyAsync()` with its implementations (`ProcessAdbSession`, `AdvancedSharpAdbSession`) SHALL be deleted alongside `AdbScreenStateProvider`. The scroll judgment SHALL use the modified ROI flow (S0 → swipe → S1 → compare → same → reduced-distance swipe → S2 → three-pair comparison → Scrolled/EndReached/Unknown). The seen-set diff mechanism (`RecordSeenElementIds`) remains available as a complementary element-level check per PRD D-7 but is no longer `TryHandleScrollAsync`'s judgment authority.
