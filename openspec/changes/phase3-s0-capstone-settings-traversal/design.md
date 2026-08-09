## Context

SC-S0-CAPSTONE-001 is registered as the S0 graduation scenario (`docs/system/scenarios/06-s0-capstone-settings-traversal.md`): `CANDIDATE` / `CAPSTONE` role / readiness `PREREQUISITES_MAPPED`. All 13 capability prerequisites are frozen with independent validation PASS. `S0_BASELINE_READY` is declared (HUMAN `ACCEPT_S0_BASELINE_READY_AUTHORIZE_CAPSTONE_OPENSPEC`, 2026-08-09) and the Capstone OpenSpec purchase is authorized.

The Capstone composes frozen capabilities; it does not purchase new production semantics. The design below fixes only the integration contract: world construction, disturbance schedule, completion evidence, replay, and the self-gating clause.

## Goals / Non-Goals

**Goals:**

- Prove frozen capabilities compose end-to-end in one deterministic four-level Settings world.
- Fix the Fake-world construction contract (external-world-only) and the disturbance schedule.
- Fix completion evidence 1–7 as a GoalEvidence conjunction.
- Fix deterministic replay for the integration run.
- Provide the stop-extract-gate clause for any new Reality Distinction.
- Keep the production delta at exactly zero.

**Non-Goals:**

- Any production model type, field, enum, interface, component, or mutable state.
- Graph, stack, navigation manager, safety manager, risk enum, progress framework, DynamicPlan, planner, or FSM.
- Reopening or expanding any frozen capability (CAND-006/007/008/009 and all others) without new evidence and Gate authority.
- Harness H4-4, automatic Scenario selection, multi-Scenario orchestration, daemon, or service.
- Runtime refactor, S1 replay migration, S2 integration, S3 emulator execution, `S0_GRADUATED`, `PHASE_3_FROZEN`, or `PHASE_COMPLETE`.

## Decisions

### 1. Compose frozen capabilities; purchase zero production semantics

The integration run reuses each frozen capability through its existing surfaces and never modifies production code. The frozen-slice regressions (13 capabilities) are the acceptance baseline; the Capstone adds only test-side fixtures and harness.

| Capstone requirement | Frozen coverage |
|---|---|
| Normal traversal through approved pages and sibling branches | SC-P1-001; SC-P3-CAND-004 |
| Discover branch candidates from fresh evidence; route not pre-encoded | SC-P3-CAND-008 |
| Preserve evidence-backed progress; no double-count; no completion while unresolved | SC-P3-CAND-004; GoalEvidence |
| Dangerous candidate never dispatched | SC-P3-CAND-006; static preauthorized safety |
| Exactly one Popup obstruction | SC-P3-002 |
| Exactly one external Launcher drift; re-enter; restore; verify; reconcile | SC-P2-001 + SC-P3-CAND-005 + SC-P3-CAND-009 |
| One bounded forward viewport movement; repeated exploration with honest exhaustion | SC-P3-003 + SC-P3-CAND-007 |
| Safe parent return/backtracking | Mechanics within SC-P3-CAND-004 (no generic Back/graph/stack purchase) |
| Completion evidence 1–7 incl. replay | GoalEvidence + all frozen replay contracts |

### 2. Fake-world construction contract (external-world-only)

The deterministic S0 world may define: visible elements, dispatch outcomes, world transitions, Observation data, Popup appearance, Launcher drift, and depth-bounded Settings semantics. It MUST NOT encode production conclusions: Container identity, Recovery authority, progress completion, or Goal success. The world exposes an approved semantic navigation tree with safe reachable pages to at least four levels, at least one dangerous visible mutation candidate, exactly one Popup, and exactly one external drift to Launcher/desktop.

### 3. Disturbance schedule

Exactly one local Popup/Overlay obstruction (SC-P3-002 path) and exactly one external Agent-scope drift to Launcher/desktop (SC-P2-001 path) are scheduled by the world at deterministic points of the run.

### 4. Completion evidence 1–7

Agent may complete the Run only when GoalEvidence proves all of: (1) every approved reachable safe branch within depth `<= 4` complete; (2) dangerous visible actions not dispatched; (3) no approved branch unresolved; (4) Popup handling followed by fresh verified Container continuity; (5) external drift recovery followed by fresh verification and reconciliation; (6) already-proven traversal progress neither fabricated nor silently discarded; (7) equal RunId, external-world inputs, disturbance schedule, and action sequence replay to equal progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState.

### 5. Deterministic replay

The replay contract is the conjunction of the frozen replay contracts composed in run order; the integration fixture must be fully deterministic (no time, no randomness) and replayable with equal inputs.

### 6. Stop-extract-gate clause

If the integration run exposes a Reality Distinction not purchasable by the frozen composition, execution stops immediately: extract one bounded Candidate Scenario, run its Semantic Gate (human), prove/freeze that capability, and only then return to the Capstone. The Capstone change does not pre-approve any such candidate.

### 7. Zero production budget

Model types +0; fields +0; enums +0; interfaces +0; components/services +0; mutable-state fields +0; mutable-state owners +0; ownership delta NONE; authority delta NONE. Test-side delta is unlimited in fixture/harness shape but constrained to the approved integration contract.

## Design Docs

| Module | Design Doc |
|--------|------------|
| `docs/system/scenarios/06-s0-capstone-settings-traversal.md` | Capstone registration (authoritative semantics) |
| `docs/system/scenarios/s0-roadmap-coverage.md` | Roadmap §5 matrix, §8 `S0_BASELINE_READY`, §11 boundary |
| `docs/decisions/s0-baseline-ready-capstone-authorization.md` | HUMAN gate decision (authorization) |
| `tests/UniClaw.Runtime.Tests/Scenario/` | Capstone fixtures/harness (this change) |
