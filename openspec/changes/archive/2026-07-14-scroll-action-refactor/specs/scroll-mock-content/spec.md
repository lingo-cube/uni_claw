## ADDED Requirements

### Requirement: IScrollContentSource provides deterministic paged scroll content

The system SHALL define an `IScrollContentSource` interface in the `Simulation` layer that produces scrollable list content on demand (pagination-style). It SHALL expose `int? TotalCount` (null = unknown/infinite stream), `int PageSize`, and `ImmutableArray<MockItem> GetPage(int pageIndex)`. `GetPage` SHALL be a pure deterministic function of `pageIndex` (no randomness, no hidden state) so results are reproducible and cacheable.

#### Scenario: GetPage returns deterministic page content
- **WHEN** `GetPage(pageIndex)` is called twice with the same `pageIndex`
- **THEN** both calls return equal `ImmutableArray<MockItem>` content (pure function)

#### Scenario: Last page may contain fewer than PageSize items
- **WHEN** `TotalCount` is not divisible by `PageSize` and the final `pageIndex` is requested
- **THEN** `GetPage` returns only the remaining items (count < PageSize), with no padding

#### Scenario: Unknown total count represents an infinite stream
- **WHEN** `TotalCount` is null
- **THEN** `GetPage` SHALL return a full `PageSize` page for any non-negative `pageIndex` (stream never ends; termination is driven by the engine seen-set, not TotalCount)

### Requirement: PagedItemGenerator configures content via parameters for scenario reuse

The system SHALL provide a `PagedItemGenerator` (sealed class) implementing `IScrollContentSource`, constructed with `totalCount`, `pageSize`, `fillRatio` (double 0.0–1.0 controlling deterministic sparse vs dense distribution), and `namePrefix`. Different constructor parameters SHALL produce different scroll effects (sparse/dense/long/short) **without reconstructing per-scenario static fixture data**. `fillRatio < 1.0` SHALL deterministically leave slots empty by index (e.g. modulo), never randomly.

#### Scenario: Dense vs sparse content via fillRatio
- **WHEN** two `PagedItemGenerator` instances are created with `fillRatio = 1.0` and `fillRatio = 0.5` respectively (same totalCount/pageSize)
- **THEN** the sparse instance yields fewer items per page than the dense instance at the same `pageIndex`

#### Scenario: One generator infrastructure reuses across scenarios
- **WHEN** long-list, sparse, and dense scenarios are each constructed as `new SimulatedScreen(new PagedItemGenerator(...), profile)`
- **THEN** no per-scenario pre-built segment/fixture data structure is constructed (configuration-only reuse)

### Requirement: SimulatedScreen holds shared mutable mock device state

The system SHALL provide a `SimulatedScreen` (sealed class, `Simulation` layer, mock-only) that owns the complete shared mutable simulated device state: `currentPageId`, navigation history, viewport position (`pageIndex`), an `IScrollContentSource`, and a `ScrollBehaviorProfile`. `ScrollableMockVisionService` and `ScrollableMockActionExecutor` SHALL both be constructed with the SAME `SimulatedScreen` instance so that a swipe (mutation) and the subsequent page analysis (observation) act on one consistent state. `ApplySwipe` SHALL advance the viewport per the profile; `GetPageAnalysis` SHALL return visible elements per the profile's visibility model.

#### Scenario: Swipe then analyze reflects the new viewport
- **WHEN** `ScrollableMockActionExecutor.SwipeAsync` is called, then `ScrollableMockVisionService.AnalyzeCurrentPageAsync` is called
- **THEN** the returned `PageAnalysis` reflects the post-swipe viewport (the two calls coordinated through the shared `SimulatedScreen`)

#### Scenario: Independent scroll state per page
- **WHEN** multiple pages have scrollable content
- **THEN** each page maintains its own viewport position and seen-element accumulation within the `SimulatedScreen`

### Requirement: ScrollBehaviorProfile controls scroll effect without introducing enums

The system SHALL provide a `ScrollBehaviorProfile` (sealed record class) controlling scroll behavior via `bool Cumulative` (true = cumulative visibility 0..currentPage, false = windowed/current-page-only), `int PagesPerSwipe` (default 1), `ScrollJump Jump` (sealed record `(double OvershootFactor = 1.0, int SkipPages = 0)` with static `ScrollJump.None`), and `double ProgressEpsilon`. The profile SHALL express all scroll-effect variation (faithful/sparse/dense/jump/overshoot) using bool / sealed record / static factory (`ScrollBehaviorProfile.Paged`, `.PagedWithJump(...)`, `.Cumulative`) and SHALL NOT introduce any new enum.

#### Scenario: Cumulative vs windowed visibility
- **WHEN** a page is scrolled with `Cumulative = true` vs `Cumulative = false` (windowed)
- **THEN** cumulative returns all elements from page 0..current, while windowed returns only the current page's elements

#### Scenario: Jump overshoot advances the viewport past a single page
- **WHEN** `Jump = ScrollJump.Overshoot(factor: 2.0)` and a single swipe is applied
- **THEN** the viewport advances by more than `PagesPerSwipe` pages (simulating a jump), so some elements never appear in any observed page

### Requirement: Mock vision/action adapters are thin wrappers over SimulatedScreen

`ScrollableMockVisionService.AnalyzeCurrentPageAsync` SHALL delegate to `SimulatedScreen.GetPageAnalysis`. `ScrollableMockActionExecutor.SwipeAsync` SHALL delegate to `SimulatedScreen.ApplySwipe` and append an `ActionRecord`. `ScrollableMockActionExecutor` SHALL NOT hold a reference to the `ScrollableMockVisionService` concrete type (both adapters depend only on `SimulatedScreen`). `ScrollableMockActionExecutor.ScrollDown`/`ScrollUp`/`ScrollHistory`/`GetScrollCount`/`GetScrollUpCount` SHALL be removed (scroll is performed via `SwipeAsync`).

#### Scenario: Analyze delegates to SimulatedScreen
- **WHEN** `ScrollableMockVisionService.AnalyzeCurrentPageAsync` is called
- **THEN** the result equals `SimulatedScreen.GetPageAnalysis()` for the shared instance

#### Scenario: Swipe delegates and records an action
- **WHEN** `ScrollableMockActionExecutor.SwipeAsync(...)` is called
- **THEN** `SimulatedScreen.ApplySwipe` is invoked and a swipe `ActionRecord` is appended to history

#### Scenario: No concrete cross-adapter reference
- **WHEN** `ScrollableMockActionExecutor` source is scanned
- **THEN** it contains no field/parameter of type `ScrollableMockVisionService`
