## Context

Engine scroll integration is currently bypassed: both `TraversalFSM.TryHandleScroll` and `StepOrchestrator.TryHandleScroll` comment "直接执行滚动,不使用 ScrollHandler(简化逻辑)" and hardcode `stepPercent=0.3`, calling `ScrollableMockVisionService.SimulateScroll` directly. The 9-class `StateMachine/Scroll/` pipeline (ScrollHandler/JumpDetector/Recovery/Adaptive/Classifier/Decider/Detector/ActionExecutor/Statistics) is built and unit-tested but never on the engine path — cold code. Worse, both call sites do `is not ScrollableMockVisionService` / `is ScrollableMockActionExecutor` runtime downcasts, so the engine is hard-coupled to Simulation mocks and a real service cannot plug in. Two divergent `TryHandleScroll` implementations and a dead `ScrollAwareNodeSelector` compound the mess. Full background, the holistic integrity check vs the charter, and the governance footprint (supersede D-32~D-48, C-11 schema change) are in the detailed design: `docs/refactor/2026-07-14-scroll-as-action-refactor-design.md`.

## Goals / Non-Goals

**Goals:**
- Treat scroll as **action + post-action screenshot judgment**: `SwipeAsync` → `AnalyzeCurrentPageAsync` → seen-element-set diff. Identical engine code path for mock and real services.
- Remove the engine→Simulation concrete coupling; add a CI guard forbidding any `UniClaw.Core.Simulation` reference in `StateMachine/`/`Traversal/`/`Domain/` production code (strengthens C-5).
- Delete the cold 9-class pipeline + dead `ScrollAwareNodeSelector`; unify the two `TryHandleScroll` into one engine site.
- Make the mock **reusable and data-driven**: dynamic paged content source (`IScrollContentSource`/`PagedItemGenerator`) + `ScrollBehaviorProfile`, so one mock + config simulates dense/sparse/jump scenarios without rebuilding static fixtures.
- Coordinate swipe (mutation) and analyze (observation) through a single shared `SimulatedScreen` (mock-only), removing cross-adapter concrete references.

**Non-Goals:**
- Build a real (non-mock) `VisionService`/`ActionExecutor` — only preserve the seam.
- Derive scroll-region swipe coordinates from `PageAnalysis` (v1 uses a default center vertical swipe).
- Adaptive step sizing (fixed/configurable step this round).
- Upward/back-to-top termination semantics (different from forward discovery; deferred to a specialized scenario).

## Decisions

1. **Scroll = SwipeAsync + Analyze + seen-set diff (not a pipeline).** Alternatives considered: (a) wire the existing ScrollHandler 7-step pipeline; (b) a combined "scroll-and-capture" operation. Rejected (a) — it re-elevates scroll to a special domain and relies on progress/threshold concepts a real service can't supply; the pipeline's jump-detection/recovery/adaptive are not needed when termination is empirical. Rejected (b) — it conflates mutation and observation, hides the real "wait for settle" step, and contradicts the "action then judge" model. Swipe is already on `IActionExecutor`; no new enum/interface method.

2. **Termination = cumulative per-frame seen-element-set diff.** A scroll that reveals no unseen element id = end of scrollable content. This subsumes both the old progress-delta check (D-38) and element-count check (D-39) in one mechanism, removes the `_visitedScrollRanges` dedup (D-41), and is robust for real services where `IsEndOfList` is unreliable.

3. **`SimulatedScreen` as mock-only shared state.** Swipe and analyze are two independent interface calls that must act on one screen state. A shared `SimulatedScreen` (referenced by both thin adapters at construction) is the standard coordination answer; it also removes the `ScrollableMockActionExecutor → ScrollableMockVisionService` concrete coupling and gives `ScrollBehaviorProfile` a single home. The engine never sees `SimulatedScreen` — enforced by the new C-5 guard.

4. **Dynamic paged content source over static `ScrollDataStore`.** `IScrollContentSource.GetPage(i)` is a pure deterministic function; `PagedItemGenerator(totalCount, pageSize, fillRatio, namePrefix)` + `ScrollBehaviorProfile` express all scenarios configurally. Replaces per-scenario pre-built segment fixtures (D-32/D-45) with configuration-only reuse.

5. **No new enum.** `ScrollBehaviorProfile` uses `bool Cumulative`, `int PagesPerSwipe`, and a sealed `ScrollJump` record + static factories. Avoids touching the locked-enum constitution surface entirely. `ScrollActionType` (not guard-locked) is deleted and logged.

6. **Metrics to ActionHistory.** `ScrollCount`/`ScrollUpCount` from `IActionExecutor.GetHistory()` swipe records; `FinalProgress`/`ScrollDistance` from the mock viewport. `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` removed from the C-11-locked `NumericAnchor` schema (no data source).

## Risks / Trade-offs

- **[Jump scenarios lose explicit recovery]** → Acceptable: in the action+judgment model a jump simply means some elements are never observed (same as a real fast scroll); mitigation is smaller step / `fillRatio`, not a recovery pipeline. Validated by a new Windowed+Jump baseline asserting the loop still terminates.
- **[Baseline recalibration churn]** → LongList/sparse/dense fixtures and expected JSON change (step model + metrics source differ). Bounded: recalibrate per the established procedure (D-67); all metrics are informational (non-CI-blocking) per NumericAnchor semantics.
- **[C-11 schema change governance]** → Removing 3 `NumericAnchor` fields is constitution-level; mitigated by routing through the documented C-11 flow (spec + decision log), not a silent drop.
- **[Large deletion footprint]** → ~17 decisions (D-32~D-48) superseded. Mitigated by append-only decision-log entries referencing each superseded decision; git history preserves the removed code.
- **[Real-service settle wait]** → Real swipe→analyze may need a wait for UI animation; mock is synchronous. Mitigated by existing `WaitAsync`/page-stability machinery; the engine loop sequence is identical.

## Migration Plan

Phased, each phase green before next (detailed in `docs/refactor/...md` §11):
1. Introduce unified `TryHandleScroll` (swipe+analyze+seen-set); wire Step 8/9; delete FSM `TryHandleScroll`/`_visitedScrollRanges` + dead `ScrollAwareNodeSelector`.
2. Extract `SimulatedScreen`; convert both mock adapters to thin delegates; remove `ScrollableMockActionExecutor.ScrollDown/Up/History`.
3. Delete the 9 pipeline classes + dependent types; migrate `ScrollHandlerConfig.ProgressEpsilon` into `ScrollBehaviorProfile`.
4. Add `IScrollContentSource`/`PagedItemGenerator` + `ScrollBehaviorProfile` (Cumulative/Windowed/Jump); migrate `ScrollDataStore`/`ScrollSegment` scenarios to generator configs, then delete.
5. Metrics → ActionHistory; recalibrate baselines; add Windowed+Jump baseline.
6. Add the C-5 architecture guard; update `traversal.md`/`decisions/log.md`/`simulation-baseline.md`; supersede D-32~D-48.

Rollback: each phase is an independent commit; revert per-phase if a regression appears. The architecture guard (phase 6) is added last so it cannot block intermediate phases.

## Open Questions

- Exact lifecycle of the per-frame seen-element set in `TraversalRuntimeContext` (clear on frame pop vs. per-NodeId) — resolved as an implementation detail during phase 1.
- Whether `ScrollableMockVisionService.GetScrollDistance`/`GetScrollProgress` remain on the adapter or move onto `SimulatedScreen` — resolved in phase 2 (lean toward `SimulatedScreen` ownership, adapter delegates).
- Final set of recalibrated baseline numeric values — determined empirically in phase 5 (same calibration procedure as D-67).
