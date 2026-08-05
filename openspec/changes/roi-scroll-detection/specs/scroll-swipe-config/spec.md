# scroll-swipe-config delta Specification

## MODIFIED Requirements

### Requirement: ScrollSwipeConfig includes MaxEmptyScrollRetries field

`ScrollSwipeConfig` SHALL add 11 new fields for ROI-based scroll detection. The existing 6 fields (`StartX`, `StartY`, `EndX`, `EndY`, `DurationMs`, `MaxEmptyScrollRetries`) SHALL remain unchanged.

The 11 new fields SHALL be:

- `int StableSampleMaxRetries = 5` — maximum retries for stable frame capture
- `int StableSampleIntervalMs = 100` — target interval between consecutive screenshots
- `int StableSampleMaxTimeMs = 3000` — absolute timeout for stable frame capture
- `int RoiSnapshotWidth = 0` — snapshot width; 0 = auto-detect from aspect ratio
- `int RoiSnapshotHeight = 0` — snapshot height; 0 = auto-detect from aspect ratio
- `int HashDistanceThreshold = 10` — dHash Hamming distance threshold, value range 0-64
- `double MadThreshold = 12.75` — MeanAbsoluteDifference threshold, value range 0-255 grayscale
- `double PixelNoiseThreshold = 15.0` — pixel change noise threshold, value range 0-255 grayscale
- `double ChangedPixelRatio = 0.1` — changed pixel ratio threshold, value range 0-1
- `int MaxConsecutiveUnknown = 3` — maximum consecutive `Unknown` results before ROI is cleared
- `double SecondSwipeDistanceRatio = 0.5` — second swipe distance = original distance × this ratio

The `MaxEmptyScrollRetries` field SHALL keep its existing behavior: it controls how many consecutive empty-scroll-diff observations are required before `IsEndOfList` is confirmed in `InterceptionHandler.TryHandleScrollAsync`. A value of 0 SHALL restore the current behavior (immediate conclusion after one empty diff).

#### Scenario: Default value preserves current effective behavior

- **WHEN** `ScrollSwipeConfig` is constructed with defaults
- **THEN** `MaxEmptyScrollRetries` is 1, meaning 2 consecutive empty diffs are required (current behavior = 1 confirmation after initial swipe)

#### Scenario: Zero restores immediate conclusion

- **WHEN** `MaxEmptyScrollRetries` is set to 0
- **THEN** `InterceptionHandler.TryHandleScrollAsync` confirms end-of-list after a single empty diff (immediate conclusion)

#### Scenario: Custom N requires N+1 confirmations

- **WHEN** `MaxEmptyScrollRetries` is set to 3
- **THEN** 4 consecutive empty diffs are required before end-of-list is confirmed

#### Scenario: New fields have documented defaults

- **WHEN** `ScrollSwipeConfig` is constructed with defaults
- **THEN** `StableSampleMaxRetries` is 5, `StableSampleIntervalMs` is 100, `StableSampleMaxTimeMs` is 3000, `RoiSnapshotWidth` is 0, `RoiSnapshotHeight` is 0, `HashDistanceThreshold` is 10, `MadThreshold` is 12.75, `PixelNoiseThreshold` is 15.0, `ChangedPixelRatio` is 0.1, `MaxConsecutiveUnknown` is 3, and `SecondSwipeDistanceRatio` is 0.5

#### Scenario: Stable frame capture retry exhaustion returns Unknown

- **WHEN** `StableFrameCapturer` fails to obtain a stable frame within `StableSampleMaxRetries` (5) retries
- **THEN** the capture SHALL return null and `InterceptionHandler.TryHandleScrollAsync` SHALL return `Unknown`

#### Scenario: Absolute timeout aborts stable frame capture

- **WHEN** stable frame capture exceeds `StableSampleMaxTimeMs` (3000) absolute timeout
- **THEN** the capture SHALL return null and `InterceptionHandler.TryHandleScrollAsync` SHALL return `Unknown`

#### Scenario: Zero snapshot dimension auto-detects from aspect ratio

- **WHEN** `RoiSnapshotWidth` and `RoiSnapshotHeight` are 0
- **THEN** `RoiSnapshotGenerator` SHALL use 256×128 for landscape (width > height) snapshots and 128×256 for portrait (height > width) snapshots

#### Scenario: Composite comparison requires all thresholds satisfied

- **WHEN** `HashDistance` ≤ `HashDistanceThreshold` AND `MeanAbsoluteDifference` ≤ `MadThreshold` AND `ChangedPixelRatio` ≤ `ChangedPixelRatio`-threshold
- **THEN** `SnapshotComparer.Compare` SHALL return `IsSame = true`

#### Scenario: Any threshold exceeded marks frame different

- **WHEN** any of `HashDistance`, `MeanAbsoluteDifference`, or `ChangedPixelRatio` exceeds its configured threshold
- **THEN** `SnapshotComparer.Compare` SHALL return `IsSame = false`

#### Scenario: Second swipe distance is scaled by ratio

- **WHEN** `SecondSwipeDistanceRatio` is 0.5 and the first swipe comparison shows `Same`
- **THEN** the second swipe distance SHALL be the original distance × 0.5, in the same direction

#### Scenario: Consecutive Unknown clears ROI

- **WHEN** `MaxConsecutiveUnknown` (3) consecutive `Unknown` results occur
- **THEN** the cached ROI and baseline snapshot SHALL be cleared, and ROI SHALL be re-selected on the next scroll
