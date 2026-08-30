## 1. Runtime Source Audit

- [x] 1.1 Audit scenario-specific literals under `src/UniClaw.Runtime` and classify each production finding as documentation leakage or executable dependency.
  Evidence (2026-08-24): `grep -rn "Settings|Wi-Fi|Wifi" src/UniClaw.Runtime --include="*.cs"` (excluding obj/bin) returns zero matches; no executable scenario dependency exists in Runtime production source.
- [x] 1.2 Stop without cleanup if any scenario literal is required by Runtime behavior or ownership boundaries.
  Evidence (2026-08-24): zero findings required by Runtime behavior or ownership boundaries; documentation cleanup confirmed already applied in working tree (prior session, uncommitted).

## 2. Approved Documentation Cleanup

- [x] 2.1 Remove the concrete scenario example from `SemanticEvidence` XML documentation without changing its model shape.
  Verified (2026-08-24): `SemanticEvidence` documentation contains no scenario example; model shape unchanged (0 "Settings" occurrences).
- [x] 2.2 Remove the concrete scenario example from `SemanticCandidate` XML documentation without changing its model shape.
  Verified (2026-08-24): `SemanticCandidate` documentation contains no scenario example; model shape unchanged.
- [x] 2.3 Remove the concrete scenario example from `SemanticCorpus` XML documentation without changing its API.
  Verified (2026-08-24): `SemanticCorpus` documentation contains no scenario example; API unchanged.
- [x] 2.4 Verify no Agent, Traversal, FSM, GoalEvidence, Recovery, Strategy, or authority-bearing production file was modified by this change.
  Evidence (2026-08-24): full deterministic suite (including authority, Agent, Traversal, FSM, GoalEvidence, Recovery, Strategy guards) 1971/1971 green; no authority-bearing behavior modified by the doc cleanup.

## 3. Regression Validation

- [x] 3.1 Run the Runtime scenario-knowledge architecture guard.
  Evidence (2026-08-24): scenario-knowledge/authority guards pass in full deterministic run.
- [x] 3.2 Run Semantic and Strategy Contract tests.
  Evidence (2026-08-24): Semantic tests 32/32 green; Strategy Contract tests green in full run.
- [x] 3.3 Run RuntimeAgent Phase 1-4, SETTINGS-TREE deterministic, and OpenWorld regressions.
  Evidence (2026-08-24): full deterministic Runtime suite 1971/1971 green (Phase 1-4, SETTINGS-TREE deterministic, OpenWorld regressions included).
- [x] 3.4 Run repository consistency checks, `git diff --check`, and strict OpenSpec validation.
  Evidence (2026-08-24): `scripts/check-consistency.sh` ALL PASS; `git diff --check` PASS; `openspec validate --all --strict` 60/60.

## 4. Documentation Sync and Handoff

- [x] 4.1 Complete the Knowledge System documentation-sync checkpoint and record explicit NO_CHANGE decisions where architecture contracts and projections remain unchanged.
  Evidence (2026-08-24): architecture contracts and projections unchanged by this doc-only cleanup — NO_CHANGE recorded; `check-consistency.sh` C1-C12 ALL PASS confirms constitution/contract/projection integrity.
- [x] 4.2 Report actual test limitations and leave graduation to Sol independent verification.
  Evidence (2026-08-24): 7 RealDevice/RealEmulator tests fail-closed on absent ADB device (hardware availability, by design); graduation NOT CLAIMED, left to Sol independent verification.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/` | `docs/architecture/vision.md` |
| Runtime authority boundary | `docs/system/constitution/runtime-architecture-contract.md` |
| Tests | `docs/TEST_GUIDE.md` + `test_results/README.md` (if present) |
