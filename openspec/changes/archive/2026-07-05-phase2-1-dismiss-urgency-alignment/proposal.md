# Proposal: DismissStrategy + UrgencyLevel Python Alignment (D-10/D-11/D-12/D-13)

## Summary

Align C# PopupClassifier dismiss strategy and UrgencyLevel enum with Python's actual behavior, resolving 4 findings from the Python↔C# design gap review (docs/refactor/12-python-csharp-design-gaps.md).

## Motivation

Four Python↔C# alignment issues identified in the design gap review:

1. **D-10**: `PopupClassifier.DismissStrategyMap` is a static dictionary that doesn't distinguish whether a dismiss target exists. Python uses conditional logic: has target → AutoClose; no target → type-specific fallback. All 5 PopupType fallback values are wrong.
2. **D-11**: `UrgencyLevel` has 4 values (Low/Medium/High/Critical) but Critical is unreachable — `DetermineUrgency()` never assigns it, and no code references it. Python has only 3 values (LOW/MEDIUM/HIGH).
3. **D-12**: `CompletionReason` has 4 values, Python `CompletionStatus` has 5 (includes ERROR). However, ERROR is also dead in Python — `CompletionDetector.detect_completion()` never assigns it. Decision: don't add Error, align with Python's **actual** 4-value usage.
4. **D-13**: D-1 removed PreconditionCheck→Branch from TraversalFSM TransitionMatrix. Python VALID_TRANSITIONS includes this path, but `_handle_precondition_check()` never returns BRANCH. Decision: keep the removal — it's correct tightening of a dead transition path.

## Changes

- **D-10 (code)**: Delete `DismissStrategyMap`; rewrite `DetermineDismissStrategy(PopupType, string? dismissTarget)` as conditional logic; sync `PopupActionExecutor` Default methods to same logic.
- **D-11 (code)**: Remove `UrgencyLevel.Critical`; update Guard test to `Has3Values`; update locked-enums.md, charter §2.2/§6.1.
- **D-12 (decision only)**: Record decision in log.md — no Error value added, deferred until ErrorHandler has a direct completion path.
- **D-13 (decision only)**: Record decision in log.md — PreconditionCheck→Branch stays removed, verified Python handler never returns BRANCH.

## Impact

| Module | Impact |
|--------|--------|
| `src/UniClaw.Core/StateMachine/PopupHandler.cs` | D-10: DismissStrategy conditional logic + D-11: UrgencyLevel 3-value enum |
| `src/UniClaw.Core/StateMachine/ContainerHandler.cs` | D-12: No change (decision only) |
| `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | D-13: No change (decision only) |
| `tests/.../Architecture/ArchitectureGuardTests.cs` | D-11: UrgencyLevel_Has3Values |
| `tests/.../StateMachine/StateMachineTests.cs` | D-10: PopupClassifier tests expanded |
| `docs/system/constitution/locked-enums.md` | UrgencyLevel 4→3, DismissStrategy cascade update |
| `docs/system/charter-specification.md` | §2.2/§6.1 enum count tables |
| `docs/system/patterns/handler-pipeline.md` | DismissStrategy decision description |
| `docs/system/layers/state-machine.md` | UrgencyLevel value count |
| `openspec/specs/popup-handler/spec.md` | D-10 conditional logic spec |
| `openspec/specs/enum-value-guards/spec.md` | UrgencyLevel 3-value spec |

## Decisions Extract

| ID | Decision | Rationale | Status |
|----|----------|-----------|--------|
| D-10 | DismissStrategyMap deleted → conditional logic `DetermineDismissStrategy(PopupType, string? dismissTarget)` | 5/5 static map values wrong vs Python; Python has target → auto_close, no target → type fallback | Fixed |
| D-11 | UrgencyLevel.Critical removed, 4→3 values | Critical unreachable (zero references), Python has 3 values, dead value = design noise | Fixed |
| D-12 | CompletionReason stays 4 values, Error NOT added | Python ERROR also dead (never assigned by CompletionDetector); adding dead value contradicts D-11 principle; defer until ErrorHandler has direct completion path | Fixed · Deferred Error value |
| D-13 | PreconditionCheck→Branch stays removed (D-1 unchanged) | Python handler `_handle_precondition_check` never returns BRANCH; dead path in matrix declaration, removal is correct tightening | Fixed |
