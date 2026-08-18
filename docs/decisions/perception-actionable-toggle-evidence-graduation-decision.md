# Perception Actionable Toggle Evidence — Parent Graduation Decision Record

> Status: GRADUATED (INDEPENDENT DELTA-FOCUSED REREVIEW) | Date: 2026-08-16
> Decision: `PROJECT_LEADER_PERCEPTION_ACTIONABLE_TOGGLE_PARENT_GRADUATION_REREVIEW`
> Maturity: `PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED`
> Change: `perception-actionable-toggle-evidence`
> Change artifacts: `openspec/changes/perception-actionable-toggle-evidence/` (archived same day)

## Decision

`GRADUATED` — real rendered toggle pixels can deterministically become
actionable canonical toggle evidence with localized bounds and authoritative
same-frame visual state, and that evidence is consumable through the existing
Binding and StateBelief pipeline. The parent integrates the independently
graduated reality-repair evidence plus a durable test-only closure proof of
end-to-end state propagation through real production components.

## Original Buyer

`LIVE PHYSICAL SEMANTIC ACTIONABILITY` (toggle evidence for Binding +
StateBeliefReducer) — detector-class gap: YOLO on Android 15/API 35 emits no
control-class detections (`perception_type` empty), so BindingAnalysis could not
find toggles and StateBeliefReducer could not determine switch states.

## Reality-Repair Provenance

- Child: `perception-actionable-toggle-evidence-reality-repair`
  (`PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED`, graduated 2026-08-16)
- Provenance record: `docs/decisions/perception-actionable-toggle-evidence-reality-repair-graduation-decision.md`
- The child established: raw-pixel toggle candidate generation (heuristics.py),
  canonical `switch` → `toggle` mapping, PER-T1..T12 + RPER-1..12 falsifiers,
  repo-owned reality fixtures (developer-options-falsification.png + groundtruth),
  Python `switch_state` NON_AUTHORITATIVE (ImageSwitchStateProvider sole authority).

## Parent Reconciliation (11/45 → 41/45 → 45/45)

1. **11/45**: parent-native baseline + OpenSpec artifacts.
2. **41/45** (DOCS_TASK_TRUTH_RECONCILIATION_ONLY): 30 tasks truthfully satisfied
   by graduated reality-repair evidence (fusion, PER-T falsifiers, Binding
   integration, reality assets, most validation); matrix in
   `reconciliation-evidence-matrix.md`.
3. **45/45** (TEST_ONLY_PARENT_CLOSURE): added the missing 4.2 StateBeliefReducer
   end-to-end integration test; ran parent-wide validation 1.6/6.5/6.10.

## 4.2 Integration Closure

- **Test**: `tests/UniClaw.Runtime.Tests/Perception/PerceptionToggleToStateBeliefIntegrationTests.cs`
  `RealPerceptionCandidates_ToStateBelief_OnAndOff_ThroughProductionChain`
- **Test-only bridge**: `tests/UniClaw.Runtime.Tests/Perception/bridge_emit_toggle_candidates.py`
  (TRANSPORT_ONLY — runs the REAL production `_run_pipeline`, serializes
  candidate JSON; zero semantic knowledge, zero groundtruth injection)
- **Chain proven**: repo-owned fixture PNG → real Python YOLO/OCR/fusion →
  candidate bounds (pixel, same frame) → real `ImageSwitchStateProvider`
  (same PNG, authoritative) → production `BindingAnalysis`/`BindingReconciler`
  → production `StateBeliefReducer` → asserted boolean belief.

## First Graduation Review — REPAIR_REQUIRED (recorded, not hidden)

- **Blocker**: `TEST_SEMANTIC_IDENTITY_TRUTHFULNESS_GAP` — the integration test
  associated real Developer Options controls with `SemanticObject("WifiConnectivity")`
  and asserted `WifiConnectivity.Enabled`, falsely claiming Developer Options
  control == Wi-Fi semantic object. All mechanism gates passed.

## Test-Only Semantic Identity Repair

- **TestFile**: `PerceptionToggleToStateBeliefIntegrationTests.cs` (semantic
  model only; bridge and all production components untouched)
- **Repaired truthful mapping**:
  - ON: "Use developer options (master)" → `DeveloperOptionsMaster.Enabled == true`
  - OFF: "Automatic system updates" → `AutomaticSystemUpdates.Enabled == false`
- **Distinct objects**: two independent `SemanticObject`s, each with its own
  text anchor; category `DeveloperOptionsSetting`, state dimension `Enabled`.
- **Regression guard**: `AssertTruthfulSemanticModeling` forbids identity
  `WifiConnectivity`/`Bluetooth` (negative assertions); positive identity is
  enforced by the constant-backed belief keys.
- **Semantic claim boundary** (documented in test): these identities exist ONLY
  to verify production Binding→StateBelief propagation; NOT production catalog
  entries; NO claim of repository semantic registration, Agent capability
  knowledge, full Android semantic understanding, or Wi-Fi semantics from this
  fixture.

## Authority Split (unchanged, zero delta)

| Layer | Owner |
|---|---|
| Candidate/type/bounds | Python Perception (`heuristics.py` / `_run_pipeline`) |
| Visual state (ON/OFF/UNKNOWN) | C# `ImageSwitchStateProvider` (sole authority) |
| Association | `BindingAnalysis` + `BindingReconciler` |
| Belief | `StateBeliefReducer` |
| Decision | Agent |
| GoalEvidence | Kernel (frozen OBS-F9) |

- **PythonSwitchStateAuthoritative**: NO (bridge does not emit it; test does not consume it)
- **AuthorityDelta**: NONE
- **ProductionDelta**: NONE (no production file changed during closure or repair)

## Delta-Focused Rereview Result

All previously-passed fields confirmed un-contradicted after repair:
BridgeRole TRANSPORT_ONLY, CandidateOrigin PRODUCTION_PERCEPTION, no manual
injection, same-frame state extraction, canonical `switch`→`toggle`, production
Binding/StateBelief paths, ON→true / OFF→false, zero model, production catalog
zero-delta, authority zero-delta. New: `SemanticIdentityTruthfulness = PASS`.

## Validation (fresh, this rereview)

- 4.2 integration test: **1/1 PASS**
- Targeted Runtime suites (StateBelief/Binding/ImageSwitchStateProvider/
  PerceptionToSemanticBinding): **26/26 PASS**
- Python perception suite: **55 passed**
- Architecture guards: **16/16 PASS**
- `dotnet build src/UniClaw.Runtime.sln`: **0 errors**
- `scripts/check-consistency.sh`: **ALL PASS**
- `openspec validate perception-actionable-toggle-evidence --strict --no-interactive`: **PASS**
- Tasks: **45/45**

## Remaining Limitations

- Vision host environmental failures (16 in full regression: CORR_HOST/DI16/H
  series, `Python process exited before ready`) — pre-existing, unrelated to
  this change (independently reproduced without the closure files)
- The integration proof uses one reality fixture (Developer Options); it does
  NOT prove universal Android control support
- No full physical SetSwitch loop, scroll-toggle reality loop, or Agent/
  GoalEvidence semantics are claimed

## Maturity Meaning

`PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED` means: real rendered toggle
pixels can deterministically become actionable canonical toggle evidence with
localized bounds and authoritative same-frame visual state, and that evidence
is consumable through the existing Binding and StateBelief pipeline. It does
NOT mean: full semantic SetSwitch physical loop, scroll-toggle reality loop,
universal Android control support, YOLO detector training, or Agent/GoalEvidence
semantics changes.
