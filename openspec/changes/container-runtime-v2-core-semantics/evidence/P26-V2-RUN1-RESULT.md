# P26-V2 Phase 2.6 Fast-Only Campaign — Run 1 Record

STATUS: `PHASE_2_6_FAST_ONLY_CAMPAIGN_SINGLE_RUN_OBSERVED` (user-directed
single round; full multi-run campaign and Task 10.3 comparison NOT yet
performed)

RUN_CLASS: `NOT_COMPLETED` (fail-closed, correct)

## Environment

- Emulator started by the Leader (`p26_pixel`, Android 15 / API 35,
  `emulator-5554`, headless, fresh boot; `-no-snapshot-save`).
- `ENVIRONMENT_GATE_DEVICE_REQUIRED` lifted for this run.
- Pre-run harness/environment repair (allowed class; no Runtime change):
  `platforms/perception/governance/artifacts/current-active-identity.json`
  carried a placeholder all-FF `modelId` (receipt written during parallel
  perception work without pinning the deployed detection model). Rebuilt
  truthfully via `governance/build_active_identity.py`; active `modelId` now
  `3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782` —
  exactly the deployed `best.pt` sha256 and the live `/version` report.
  Vision host then verified identity and reached Healthy.

## Run evidence chain

| Required field | Value |
|---|---|
| RunRef | `run-1` (strategy `p26-stageA-r1`, conservative Stage-A posture, declaredMaximumDepth 1) |
| Scenario / Buyer | `real-android-settings-emulator-35` / bounded conservative round (composition proof) |
| ActionEvidence | 7 × `ActionDispatched` (Action-1..Action-6 `ScrollForward()` on Container `Settings`; event stream seq 1–14) |
| ObservationRefs | 13 observed frames, observation sequences through seq 19 (`p26-frames.json`, `p26-observation-timestamps.json`) |
| CurrentContainer | Settings root — `Agent.Belief.SemanticPage = "Settings"` (V2-derived compatibility projection) |
| TransitionOccurrence / EntryContext / Graph evidence | No cross-container transition this run (single root container; no child entry, no return); V2 occurrence/entry surfaces exercised at root scope |
| Fast assessment | Production Fast path live underneath; no Fast-trust incident observed this run |
| obligation/progress | Exploration ledger: `Scopes = []` — root epoch never closed (fail-closed before scope completion); digest `743F8999AF42EF78B505A188FEE96C7ED96BB82D6EEEC6889150460A0DE8ABB1` |
| GoalEvidence | Absent — `latestGoalEvidence: Unavailable`, "no completion evidence on Agent public surface" (honest) |
| LastGood | Root Settings page identified and held across 13 observations / 7 scrolls (`currentSemanticPage=Settings` stable) |
| FirstDivergence | Root-epoch completeness: interaction affordances remained Unknown after the bounded conservative scroll budget → `RunFailed` |
| ExpectedReality | Root epoch completes (or exhausts budget with all affordances resolved) |
| ObservedReality | Fail-closed: "Unknown interaction affordances remain; completeness cannot be proven." |
| BlockerCategory | `PERCEPTION` (affordance-level sensing variance at root scope; page identity itself WAS resolved) |
| Owner | Perception channel (per-element affordance resolution); Runtime correctly fail-closed — no runtime defect indicated |
| EvidenceRef | `evidence/p26-v2-run1/` (run1.log, p26-frames.json, p26-fusion-traces.json, p26-observation-timestamps.json, p26-observation-integrity.json) |
| TerminalDisposition | Run terminal `Failed` (RunFailed seq 14); campaign termination `BoundedScopeExhaustion` — "planned conservative round budget reached (1); composition proof complete" |
| Completed / NotCompleted | **NotCompleted** |

## Verification posture (all pass)

Autonomy assertion PASS; all four campaign invariants PASS
(HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH,
HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY,
RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS,
AUTONOMOUS_EXCEPTION_DISPOSITION != UNIVERSAL_RECOVERY); boundary
prohibitions all positive (NoRuntimeStateMutation / NoActionInjection /
NoFsmControl / NoEvidenceFabrication); gates G1–G4 PASS. Zero emulator
mid-run intervention.

Harness-side honest gaps observed (recorded, not run blockers):
snapshot diagnostics note `MISSING_EVIDENCE`/`MISSING_ASSET` for transition
observation/asset — no EvidenceCatalog registration in this tier
composition; CurrentGoal null with derived-read-model classification.

## Single-run reading (NOT a Task 10.3 conclusion; n=1)

- The full production V2 Fast-only path ran end-to-end on a real emulator:
  UDS vision host (identity-verified) → ADB screenshot source → local vision
  perception → production Agent (V2 sole physical-current owner) → ADB
  dispatch, with zero mid-run control.
- Compared with the old baseline's dominant root-page failure class
  (root Unknown / identity unresolved), this run resolved and held the root
  Settings identity and failed later, at per-element affordance
  completeness — a different, later failure point. One run cannot establish
  migration; the multi-run campaign remains required for Task 10.3 A–J.
- No false identity, no false trust, no forced completion, no recovery
  fabrication observed; the fail-closed discipline held.

## Next

- Additional fresh rounds (same buyer/scenario) to build the comparable
  sample for Task 10.3; Slow stays Disabled; no Runtime patching between
  runs. Not GRADUATED.
