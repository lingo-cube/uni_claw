## 1. Unified engine scroll loop (action + judgment)

- [x] 1.1 Add per-frame seen-element-id set to `TraversalRuntimeContext` (get/add/clear API) for scroll-loop termination; clear on frame pop
- [x] 1.2 Implement unified `StepOrchestrator.TryHandleScroll(ctx, frame)`: `SwipeAsync` → `AnalyzeCurrentPageAsync` → `Invalidate` → seen-set diff → Continue/Stop (no ScrollHandler, no mock downcast)
- [x] 1.3 Wire Step 8 (Branch) and Step 9 (NodeSelect) DynamicMatch-exhausted branches to the single `TryHandleScroll`; remove the duplicated inline scroll logic
- [x] 1.4 Delete `TraversalFSM.TryHandleScroll` + `_visitedScrollRanges`; make `HandleBranch` return `NodeSelect` for an exhausted DynamicMatch node
- [x] 1.5 Delete `Traversal/ScrollAwareNodeSelector.cs` (dead code)
- [x] 1.6 Unit-test `TryHandleScroll`: unseen-elements→Continue, all-seen→Stop, non-scrollable→complete, root vs non-root stop behavior

## 2. SimulatedScreen + thin mock adapters

- [x] 2.1 Create `Simulation/Scroll/SimulatedScreen.cs` (sealed class) owning currentPageId, navigation history, viewport pageIndex, content source, behavior profile; methods `ApplySwipe`, `GetPageAnalysis`, navigation
- [x] 2.2 Refactor `ScrollableMockVisionService` into a thin adapter: `AnalyzeCurrentPageAsync` delegates to `_screen.GetPageAnalysis()`
- [x] 2.3 Refactor `ScrollableMockActionExecutor` into a thin adapter holding `SimulatedScreen` (NOT `ScrollableMockVisionService`); `SwipeAsync` delegates to `_screen.ApplySwipe` + records ActionRecord
- [x] 2.4 Remove `ScrollableMockActionExecutor.ScrollDown`/`ScrollUp`/`ScrollHistory`/`GetScrollCount`/`GetScrollUpCount`
- [x] 2.5 Update test constructors to inject one shared `SimulatedScreen` into both adapters
- [x] 2.6 Unit-test adapter coordination: `SwipeAsync` then `AnalyzeCurrentPageAsync` reflects the new viewport; assert no `ScrollableMockVisionService` field on the action executor

## 3. Delete cold pipeline + migrate config

- [x] 3.1 Delete `StateMachine/Scroll/` classes: `ScrollHandler`, `ScrollabilityDetector`, `ScrollClassifier`, `ScrollDecider`, `ScrollActionExecutor`, `JumpDetector`, `JumpRecoveryHandler`, `AdaptiveStepCalculator`, `ScrollStatisticsCollector`
- [x] 3.2 Delete dependent types: `ScrollActionResult`, `ScrollVerifyResult`, `JumpRecoveryResult`, `ScrollContext`, `ScrollAction`, `ScrollActionType`
- [x] 3.2b Delete `OverlapStatus` if now unreferenced (compiler-verified)
- [x] 3.3 Create `ScrollBehaviorProfile` (sealed record: `bool Cumulative`, `int PagesPerSwipe`, `ScrollJump Jump`, `double ProgressEpsilon`) + `ScrollJump` (sealed record) + static factories `Paged`/`PagedWithJump`/`Cumulative`; migrate `ScrollHandlerConfig.ProgressEpsilon`
- [x] 3.4 Resolve all compilation breakages from 3.1–3.3; `dotnet build` clean (0 errors)

## 4. Dynamic paged content source

- [x] 4.1 Create `IScrollContentSource` (`int? TotalCount`, `int PageSize`, `ImmutableArray<MockItem> GetPage(int)`) in `Simulation/Scroll/`
- [x] 4.2 Implement `PagedItemGenerator` (sealed class): deterministic `GetPage`, `fillRatio` sparse/dense, last-page partial, `TotalCount=null` infinite
- [x] 4.3 Wire `SimulatedScreen` to use `IScrollContentSource` + `ScrollBehaviorProfile` for `ApplySwipe`/`GetPageAnalysis` (Cumulative vs Windowed visibility; Jump overshoot/skip)
- [x] 4.4 Migrate existing scroll scenarios (long/sparse/dense) from `ScrollDataStore`/`ScrollSegment` to `PagedItemGenerator` configs; delete `ScrollDataStore`/`ScrollSegment`/`ScrollSegmentBuilder` once unreferenced
- [x] 4.5 Unit-test `PagedItemGenerator` (determinism, partial last page, sparse vs dense, infinite TotalCount) and `ScrollBehaviorProfile` (Cumulative vs Windowed vs Windowed+Jump produce different PageAnalysis)

## 5. Metrics to ActionHistory + baseline recalibration (C-11)

- [x] 5.1 Remove `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` from the `NumericAnchor` record + `NumericAnchorDto`; update `ExpectedBehavior` construction (C-11 schema change)
- [x] 5.2 Rework `BaselineReportCollector.BuildActualNumeric`: derive `ScrollCount`/`ScrollUpCount` from `IActionExecutor.GetHistory()` swipe records, `FinalProgress`/`ScrollDistance` from viewport; drop jump fields
- [x] 5.3 Remove `jumpDetected`/`jumpRecovered`/`adaptiveStepIncreases` keys from scroll scenario JSON expected files
- [x] 5.4 Recalibrate LongList/sparse/dense expected numericAnchor values against `PagedItemGenerator` scenarios (empirical, per D-67 procedure)
- [x] 5.5 Add a Windowed+Jump baseline scenario asserting the scroll loop still terminates (no infinite loop) when jumps skip elements
- [x] 5.6 Run baseline suite; confirm `allPassed=true` and scroll metrics are non-zero where expected

## 6. Architecture guard + docs + decisions

- [x] 6.1 Add `DependencyDirectionGuardTests` (or inner class) asserting no `UniClaw.Core.Simulation` reference in `StateMachine/`/`Traversal/`/`Domain/` production `.cs` (strengthen C-5); confirm zero false positives (all current refs were in deleted Scroll files)
- [x] 6.2 Update `docs/system/layers/traversal.md` §2 to the action+judgment model; remove ScrollHandler integration text; update D-57/D-66 pointers
- [x] 6.3 Append decision-log entries **explicitly superseding D-32~D-48** (append-only; each new entry references the superseded decision) + a C-11 NumericAnchor schema-change decision
- [x] 6.4 Update `docs/system/layers/simulation-baseline.md` (remove jump metric fields; document `PagedItemGenerator` calibration)
- [x] 6.5 Full suite green: `dotnet test src/UniClaw.Core.sln` (expect 0 errors, 0 functional warnings, all tests pass); `openspec validate scroll-action-refactor`
