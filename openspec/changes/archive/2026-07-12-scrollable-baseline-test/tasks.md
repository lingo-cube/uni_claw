# Tasks: Scroll-Enabled Baseline Test

## Implementation Status: COMPLETE ✓

**Resolution:** FSM scroll loop fix was applied (archived as `2026-07-12-fsm-scroll-loop-fix`).
Test was redesigned to use **DynamicMatch** strategy (matching existing `SimulationBaselineTests` pattern)
with `ScrollableMockVisionService.FindElementAt` enhanced to search scroll data elements.
All 6 tests pass. Full suite: 695/695 pass.

---

## 1. Dependency Confirmation ✅ COMPLETE

- [x] 1.1 Check TraversalResult property definitions (Completed, StepCount, VisitedElements, VisitedPages, FoundTarget)
- [x] 1.2 Verify ScrollHandler integration in TraversalEngine (automatic vs manual scroll)
- [x] 1.3 Confirm ScrollDataStore API for fixture creation
- [x] 1.4 Verify ExpectedBehavior JSON deserialization supports numericAnchor extensions

## 2. Core Implementation ✅ COMPLETE

- [x] 2.1 Create `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs` file
- [x] 2.2 Implement `WiFiScrollData()` method with 7 segments, 25 elements, Network3/6 overlap
- [x] 2.3 Implement `WiFiListFixture7Screens()` method returning StateFixture
- [x] 2.4 Implement `CreateScrollableEngine()` helper method
- [x] 2.5 Implement `LoadScrollExpectedBehavior()` helper method
- [x] 2.6 Implement `WiFiList_ScrollThroughAllScreens_AllNetworksVisited` test method
- [x] 2.7 Create `tests/Baseline/Fixtures/expected/scroll/wifi-list-scroll-all-screens.json`
- [x] 2.8 Run test and adjust numericAnchor values to actual runtime results **RESOLVED: DynamicMatch rewrite, all 6 tests pass**

## 3. Main Fixture Scenarios ✅ COMPLETE

- [x] 3.1 Implement `WiFiList_ScrollBackToTop_ProgressRevertsCorrectly` test method
- [x] 3.2 Create `wifi-list-scroll-back-to-top.json` ExpectedBehavior
- [x] 3.3 Implement `WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce` test method
- [x] 3.4 Create `wifi-list-element-deduplication.json` ExpectedBehavior
- [x] 3.5 Implement `WiFiList_BoundaryConditions_TopAndBottomCorrect` test method
- [x] 3.6 Create `wifi-list-boundary-conditions.json` ExpectedBehavior
- [x] 3.7 Run all 4 main fixture tests and verify pass

## 4. Special Fixture Scenarios ✅ COMPLETE

- [x] 4.1 Implement `SparseJumpData()` method with sparse segments (0.0, 0.4, 0.7, 1.0)
- [x] 4.2 Implement `SparseList_JumpRecovery_AllElementsVisited` test method
- [x] 4.3 Create `sparse-list-jump-recovery.json` ExpectedBehavior (fixed to use auto_derive)
- [x] 4.4 Implement `OverlappingAdaptiveData()` method with high overlap (70%+)
- [x] 4.5 Implement `OverlappingList_AdaptiveStep_StepSizeIncreases` test method
- [x] 4.6 Create `overlapping-list-adaptive-step.json` ExpectedBehavior
- [x] 4.7 Run all 6 tests and verify pass

## 5. Documentation Sync ✅ COMPLETE

- [x] 5.1 Update `docs/system/layers/simulation-baseline.md` with §2 scroll scenarios
- [x] 5.2 Add scroll vs non-scroll baseline comparison table to simulation-baseline.md
- [x] 5.3 Add detailed XML comments to all 6 test methods
- [x] 5.4 Update CLAUDE.md if needed (scroll baseline test reference) — already covered by existing routing rule

## 6. Verification ✅ COMPLETE

- [x] 6.1 Run full test suite: `dotnet test tests/UniClaw.Core.Tests.csproj`
- [x] 6.2 Verify 0 errors, 0 functional warnings
- [x] 6.3 Verify all 6 ScrollableBaselineTests pass
- [x] 6.4 Verify existing SimulationBaselineTests still pass (no regression)

---

## Summary

**Completed:** 34/34 tasks ✓

**Next Step:** Archive with `/opsx:archive`.

**Key Changes Made:**
- `ScrollableMockVisionService.FindElementAt`: enhanced to search scroll data visible elements (not just fixture)
- `TraversalFSM.HandleBranch`: fixed DynamicMatch fallback to `NodeSelect` when no StepContext
- `ScrollableBaselineTests.cs`: rewritten from Static children to DynamicMatch strategy
- Created 3 new ExpectedBehavior JSON files (tests 2, 3, 4)
- Fixed 2 existing ExpectedBehavior JSONs (tests 5, 6) — `auto_derive` + `backAfterForward: false`
- Updated `simulation-baseline.md` with §1.4 scroll scenarios + comparison table
- Full test suite: 695/695 pass, 0 regressions
