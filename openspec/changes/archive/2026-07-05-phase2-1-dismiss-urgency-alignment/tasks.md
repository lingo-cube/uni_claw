# Tasks: DismissStrategy + UrgencyLevel Python Alignment

## Implementation Tasks

- [x] T1: Delete `PopupClassifier.DismissStrategyMap` field from PopupHandler.cs
- [x] T2: Rewrite `DetermineDismissStrategy(PopupType popupType)` → `DetermineDismissStrategy(PopupType popupType, string? dismissTarget)` with conditional logic (has target → AutoClose; no target → type fallback per Python)
- [x] T3: Update `PopupClassifier.Classify()` call site to pass `dismissTarget` to `DetermineDismissStrategy`
- [x] T4: Update `PopupActionExecutor` 5 Default methods to conditional logic (check `ctx.Classification.DismissTarget`)
- [x] T5: Remove `UrgencyLevel.Critical` from enum definition (4→3 values)
- [x] T6: Update Guard test `UrgencyLevel_Has4Values` → `UrgencyLevel_Has3Values`
- [x] T7: Expand PopupClassifierTests: add 6 tests covering 5 PopupType × (has/no target) combos
- [x] T8: Update `docs/system/constitution/locked-enums.md` (UrgencyLevel 4→3, DismissStrategy cascade, Python↔C# bias table)
- [x] T9: Update `docs/system/charter-specification.md` §2.2, §6.1 enum count tables (UrgencyLevel=3)
- [x] T10: Update `docs/system/patterns/handler-pipeline.md` (DismissStrategyMap→conditional logic, UrgencyLevel 3)
- [x] T11: Update `docs/system/layers/state-machine.md` (UrgencyLevel 3)
- [x] T12: Update `openspec/specs/popup-handler/spec.md` (D-10 conditional logic, D-11 UrgencyLevel 3)
- [x] T13: Update `openspec/specs/enum-value-guards/spec.md` (UrgencyLevel 3)
- [x] T14: Record D-10/D-11/D-12/D-13 decisions in `docs/system/decisions/log.md`
- [x] T15: Build + test verification (438 pass, 0 fail)

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Core/StateMachine/` | `docs/system/layers/state-machine.md` + `docs/system/patterns/handler-pipeline.md` |
| `openspec/specs/` | `docs/system/constitution/locked-enums.md` |
