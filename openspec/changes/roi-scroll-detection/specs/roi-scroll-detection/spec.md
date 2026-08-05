# roi-scroll-detection

ROI-based visual scroll end detection for the C# traversal engine (`InterceptionHandler.TryHandleScrollAsync`), replacing the UIAutomator XML dump channel. Detailed design context: `docs/prd/2026-08-04-roi-scroll-detection-prd.md`.

## ADDED Requirements

### Requirement: RoiSelector picks the best ROI from a page with YOLO bounding boxes

`RoiSelector` SHALL score candidate sliding windows over a page using a composite score built from YOLO density (count of items with `yoloId != null` inside the window), texture (Laplacian variance of the raw screenshot pixels in the window), and non-solid ratio (proportion of non-uniform pixels). It SHALL return the highest-scoring window as a `RoiRect` with concrete pixel coordinates, or null when no window qualifies. Selection SHALL use the raw full-screen screenshot, not the compressed/resized variant.

#### Scenario: Multi-candidate page
- **WHEN** a page contains multiple content regions with different YOLO bbox densities
- **THEN** the candidate window with the highest composite density + texture + non-solid score is returned as `RoiRect`, and scoring respects the configured minimum density threshold

#### Scenario: Dynamic elements are excluded during selection
- **WHEN** a candidate window's dominant content is a dynamic element whose `type` matches the static blacklist (`loading`, `banner`, `carousel`, `progressbar`, `video`, configurable via `ScrollSwipeConfig`)
- **THEN** that window is deprioritized or excluded so the selected ROI is stable across scroll frames, and the blacklist SHALL be configurable without code changes

#### Scenario: Window size is clamped to the screen
- **WHEN** the sliding window at the screen edge would extend beyond the capture bounds
- **THEN** the window is clamped to the valid screenshot area and still scored, never discarded for being at the edge

### Requirement: RoiSelector degrades to pure texture scoring when no YOLO bounding boxes exist

When a page yields no items with `yoloId != null`, `RoiSelector` SHALL fall back to scoring windows with texture score and non-solid ratio only, without density. The degraded path SHALL still return a `RoiRect` when a genuinely textured region exists, and SHALL NOT select near-uniform regions such as plain gradient backgrounds.

#### Scenario: All-OCR page
- **WHEN** every recognized item has `yoloId == null` (OCR-only page) and the page has real content
- **THEN** ROI selection uses pure texture + non-solid scoring and returns the region with the highest Laplacian variance

#### Scenario: Gradient background is not selected
- **WHEN** a page is a smooth gradient with high Laplacian variance but low non-solid ratio and no YOLO boxes
- **THEN** the composite texture + non-solid score stays below the selection threshold and the window is not returned

### Requirement: RoiSelector returns null for blank pages

`RoiSelector` SHALL return null when no window meets the minimum thresholds — for example a blank, near-black, or fully uniform page. A null return SHALL NOT be retried with the same page snapshot.

#### Scenario: Blank or solid-color page
- **WHEN** the page has no detectable texture and no YOLO boxes anywhere
- **THEN** `RoiSelector` returns null and the scroll handler proceeds without a selected ROI instead of picking a random region

### Requirement: RoiSnapshotGenerator produces standardized snapshots

`RoiSnapshotGenerator` SHALL transform the raw full-screen screenshot's ROI crop into a standardized `RoiSnapshot` containing: grayscale conversion, resize to 256x128 for landscape or 128x256 for portrait orientation, gaussian blur, a dHash digest over an internal 9x8 grid (64 bits), and a `GrayPixels` array of 0-255 byte values. The snapshot SHALL always derive from the original full-resolution screenshot of the same capture.

#### Scenario: Landscape page
- **WHEN** the device orientation is landscape
- **THEN** the snapshot's gray pixel buffer is exactly 256x128 and the dHash encodes a 9x8 downscaled grid

#### Scenario: Portrait page
- **WHEN** the device orientation is portrait
- **THEN** the snapshot's gray pixel buffer is exactly 128x256 and the dHash encodes the same 9x8 grid

#### Scenario: Rejected source image
- **WHEN** the ROI crop or source screenshot is missing, empty, or too small to resample
- **THEN** snapshot generation fails with a domain error rather than producing a degraded or undersized snapshot

### Requirement: SnapshotComparer identifies same and different frames with AND semantics

`SnapshotComparer` SHALL compare two `RoiSnapshot` values with three metrics: Hamming distance on the 64-bit dHash (0-64 range), MeanAbsoluteDifference on `GrayPixels` (0-255 range), and `ChangedPixelRatio` (fraction of pixels whose difference exceeds a noise threshold). `IsSame` SHALL be true only when all three metrics pass their configured thresholds; any single metric failing SHALL make `IsSame` false. All thresholds SHALL come from `ScrollSwipeConfig`.

#### Scenario: Identical frames
- **WHEN** two snapshots are generated from the same stable screen
- **THEN** Hamming distance, MAD, and changed pixel ratio all remain within thresholds and `IsSame` is true

#### Scenario: Scrolled content
- **WHEN** the second snapshot captures visibly different content after a swipe
- **THEN** at least one metric exceeds its threshold and `IsSame` is false

#### Scenario: Single metric violation
- **WHEN** dHash and MAD pass but the changed pixel ratio exceeds the noise threshold (e.g., a local animation)
- **THEN** the frames are still reported as different because all three metrics must pass

### Requirement: StableFrameCapturer collects stable frames with dynamic delay

`StableFrameCapturer` SHALL capture the pre-scroll baseline as one pair of two consecutive identical frames, and the post-scroll sample as two consecutive identical pairs (three frames). Between captures it SHALL wait a dynamic delay computed from the measured ADB screenshot latency (target interval minus elapsed time), bounded by a per-attempt maximum, and SHALL abort with a stability failure when no stable sample is obtained within the absolute timeout `StableSampleMaxTimeMs` (default 3000ms).

#### Scenario: Pre-scroll baseline
- **WHEN** the scroll handler needs a before-sample
- **THEN** two consecutive captures must compare as the same frame; otherwise the capturer retries up to the configured maximum retries before failing

#### Scenario: Post-scroll sample
- **WHEN** the scroll handler needs an after-sample
- **THEN** three captures forming two consecutive same-pairs must be collected; a pair break restarts the pairing sequence up to the retry budget

#### Scenario: Slow rendering page
- **WHEN** the page keeps changing (lazy loading, network images) so stability is never reached
- **THEN** the capturer stops at `StableSampleMaxTimeMs` and reports a stability failure, which surfaces as Unknown rather than blocking forever

### Requirement: Scroll detection treats a different first pair as Scrolled

After capturing S0 (before) and S1 (after the first swipe), `InterceptionHandler.TryHandleScrollAsync` SHALL compare S0 and S1. When they are different, the flow SHALL conclude Scrolled immediately, SHALL update the scroll baseline snapshot to S1, SHALL invalidate child nodes whose DynamicMatch is stale, and SHALL NOT issue the second swipe.

#### Scenario: Content moved after swipe
- **WHEN** S0 and S1 are different snapshots
- **THEN** the result is Scrolled, the baseline is updated to S1, and child invalidation is applied without a second swipe

### Requirement: Scroll detection reports EndReached when all three pairs are the same

When S0 and S1 are the same, the flow SHALL issue a second swipe at 50% of the first swipe distance in the same direction, capture S2, then compare all three pairs (S0-S1, S0-S2, S1-S2). When all three pairs are the same, the flow SHALL conclude EndReached. When any pair differs, the flow SHALL conclude Scrolled.

#### Scenario: Second swipe at reduced distance
- **WHEN** the first swipe produced no visible change (S0 equals S1)
- **THEN** the flow issues a second swipe at exactly 50% of the first swipe's distance in the same direction before capturing S2

#### Scenario: All three pairs identical
- **WHEN** S0, S1, and S2 are pairwise the same
- **THEN** the flow concludes EndReached and the container stops scrolling

#### Scenario: Any pair differs after the second swipe
- **WHEN** at least one of the three pairs compares as different
- **THEN** the flow concludes Scrolled and updates the baseline from the pair evidence

### Requirement: Scroll detection reports Unknown on conflicting evidence with a consecutive Unknown threshold

When the pairwise comparisons are internally inconsistent (for example S0 equals S1 but S1 differs from S2), the flow SHALL conclude Unknown instead of guessing. The flow SHALL track consecutive Unknown outcomes; when the count reaches the configured threshold (`MaxEmptyScrollRetries`), it SHALL clear the current ROI and re-run `RoiSelector` on a fresh snapshot before further attempts, then reset the counter.

#### Scenario: Conflicting pairwise results
- **WHEN** pairwise comparison results disagree (e.g., S0≈S1 but S1≠S2)
- **THEN** the result is Unknown and no baseline update or child invalidation is performed

#### Scenario: Consecutive Unknowns trigger ROI reselection
- **WHEN** Unknown outcomes reach the configured consecutive threshold
- **THEN** the current ROI is cleared, a fresh page snapshot is taken, `RoiSelector` runs again, and the consecutive Unknown counter resets

### Requirement: ROI is invalidated on container switch, orientation change, or repeated failures

The selected ROI SHALL be invalidated and re-selected when the traversal moves to a different Container, when the device orientation changes, or when stable capture / comparison failures recur beyond the configured budget. A stale ROI MUST NOT be reused across these transitions.

#### Scenario: Container switch
- **WHEN** traversal enters a new Container after the ROI was selected for the previous one
- **THEN** the old ROI is discarded and selection restarts from a fresh page snapshot of the new Container

#### Scenario: Orientation change
- **WHEN** the device orientation flips between captures
- **THEN** the ROI is invalidated and re-selected because the 256x128 vs 128x256 snapshot geometry no longer matches the stored ROI

#### Scenario: Recurrent capture failure
- **WHEN** stable frame capture fails repeatedly within the same Container beyond the retry budget
- **THEN** the ROI is cleared and selection restarts instead of reusing the failing ROI indefinitely
