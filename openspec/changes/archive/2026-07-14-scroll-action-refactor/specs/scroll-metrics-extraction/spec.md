## REMOVED Requirements

### Requirement: ScrollableMockActionExecutor provides GetScrollUpCount method

**Reason**: `ScrollableMockActionExecutor.ScrollDown`/`ScrollUp`/`ScrollHistory`/`GetScrollCount`/`GetScrollUpCount` are removed — scroll is performed via the standard `IActionExecutor.SwipeAsync` and counted from the resulting ActionRecords. Scroll-up count is now derived from ActionHistory in the baseline collector (see `baseline-scroll-metrics`).
**Migration**: Any caller of `GetScrollUpCount()`/`GetScrollCount()`/`ScrollHistory` switches to counting swipe ActionRecords by direction.

### Requirement: Collector extracts scroll metrics from mock services

**Reason**: `BaselineReportCollector` no longer calls mock-specific `GetScrollCount()`/`GetScrollUpCount()` extraction methods. Scroll metrics are extracted from `IActionExecutor.GetHistory()` (ActionHistory) as specified in the modified `baseline-scroll-metrics` capability.
**Migration**: Collector wiring that passed mock services for per-method extraction is replaced with ActionHistory counting + optional viewport-progress read.

### Requirement: Advanced scroll metrics return zero in Phase 1

**Reason**: `JumpDetected`/`JumpRecovered`/`AdaptiveStepIncreases` are removed from the `NumericAnchor` schema (C-11), so there is nothing to return zero. The "Phase 3 future work" hook (integration with `ScrollHandler.Statistics`/`AdaptiveStepCalculator`) is also removed since those classes are deleted.
**Migration**: Remove all references to these three fields from metric construction and verification.

### Requirement: Scroll metrics are derived from existing data structures

**Reason**: The "existing data structures" referenced (`ScrollHistory`, `ScrollState`) are removed/migrated as part of the dynamic-content-source refactor. Scroll metrics are now derived from ActionHistory (swipe records) and the `SimulatedScreen` viewport, not from `ScrollHistory`/`ScrollState`.
**Migration**: Metric derivation moves to ActionHistory counting; `ScrollState`-based distance tracking is replaced by viewport-progress arithmetic.
