# SC-S0-CAPSTONE-001 — Four-Level Settings Traversal with Safety and Recovery

> Status: Registered S0 Graduation Scenario | Semantic Status: `CANDIDATE`
> Scenario Role: `CAPSTONE` | Capstone Readiness: `PREREQUISITES_MAPPED`
> Evidence Maturity: `S0 TARGET` — not yet executed or approved for implementation.
> Purpose: Architecture integration target; this document is not an OpenSpec purchase, implementation task, or Phase completion claim.

## Intent

Prove that UniClaw can perform realistic autonomous UI work in a deterministic Settings world by integrating already-purchased Runtime capabilities with the minimum remaining progress/navigation semantics.

## Given

- Runtime starts in Android Settings.
- Runtime receives traversal intent, allowed scope, a depth bound, and safety constraints, but not one fixed linear list that pre-enumerates every page and navigation action.
- The deterministic external world exposes an approved semantic navigation tree with safe reachable pages to at least four levels.
- Traversal progress accumulates during the Run.
- Some visible candidates are dangerous state-mutating operations and are not approved executable actions.
- Exactly one local Popup/Overlay obstruction and exactly one external drift to Launcher/desktop are scheduled by the world.

## Goal

Traverse all approved reachable safe Settings branches to depth `<= 4` without executing dangerous state-mutating operations.

## Required Integration Behavior

### Normal Traversal

- Navigate through approved Settings pages and sibling branches.
- Discover branch candidates from fresh external-world evidence within the approved scope; the complete route must not be encoded up front merely to make the Capstone pass.
- Preserve meaningful, evidence-backed progress.
- Do not count already-verified semantic work repeatedly as new progress.
- Do not report completion while an approved reachable safe branch remains unresolved.

### Dangerous Candidate

The world exposes at least one conceptually destructive reset, delete/clear-data, uninstall, or equivalent mutation candidate.

```text
visible candidate
!=
approved executable action
```

Static/preauthorized safety is covered for S0: a caller-approved traversal boundary may exclude destructive actions and the Scenario can prove that none were dispatched.

The frozen SC-P3-CAND-006 capability uses one Agent-owned Goal criterion to distinguish fresh observed candidates from authorized execution with explicit authorized/rejected/unresolved evidence. Runtime behavior, zero-dispatch denial, GoalEvidence authority, formal deterministic replay, tasks 4/4, and independent validation are complete within the exact one-type/three-field budget. This capability does not purchase SafetyManager, RiskEngine, RiskLevel, universal interception, generalized candidate discovery, or a safety-policy framework.

### Popup Disturbance

Reuse SC-P3-002:

```text
Popup appears
→ bounded local handling
→ fresh Observation
→ verify underlying Container continuity
→ preserve progress
→ continue
```

### External Drift

Reuse SC-P2-001 for world recovery:

```text
Launcher / desktop observed
→ current local Container invalid
→ Agent-scope escalation
→ re-enter Settings
→ restore a trusted semantic position
→ Observe
→ Verify
→ Reconcile
```

The frozen SC-P3-CAND-005 capability resolves the required distinction with exactly one immutable branch-effect evidence evaluator field: re-entering the world is not progress continuation, and retained evidence contributes only when its criterion evaluates fresh recovered-world evidence to true. Runtime behavior, formal deterministic Scenario proof, and independent validation are complete within budget.

### Viewport Movement

Reuse SC-P3-003 for one bounded forward viewport movement whose changed element snapshot remains within the same semantic Container when fresh identity evidence proves continuity.

Reuse the frozen SC-P3-CAND-007 capability when one semantic Container requires repeated bounded exploration: Container retains accepted fresh same-Container evidence, Agent decides continue/exhausted/unresolved under Goal scope, each positive continuation authorizes at most one existing movement, and bound consumption remains unresolved unless exhaustion is independently proven. This does not purchase a Viewport identity, graph, stack, manager, Fingerprint authority, generic scrolling policy, or dynamic planner.

## Completion Evidence

Agent may complete the Run only when GoalEvidence proves all of:

1. every approved reachable safe branch within depth `<= 4` is complete;
2. dangerous visible actions were not dispatched;
3. no approved branch remains unresolved;
4. Popup handling was followed by fresh verified Container continuity;
5. external drift recovery was followed by fresh verification and reconciliation;
6. already-proven traversal progress was neither fabricated nor silently discarded;
7. equal RunId, external-world inputs, disturbance schedule, and action sequence replay to equal progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState.

Plan exhaustion, action dispatch, Recovery dispatch, a changed viewport snapshot, or local Container completion cannot independently satisfy this Goal.

## Capability Prerequisites

| Capability | Status |
|---|---|
| SC-P1 normal lifecycle and GoalEvidence | `COVERED` at S0 |
| SC-P1 target disambiguation | `COVERED` at S0 |
| SC-P2-001 external drift recovery | `COVERED` at S0 |
| SC-P2-003 recovery failure honesty | `COVERED` at S0 |
| SC-P3-001 uncertain post-action outcome | `COVERED` at S0 |
| SC-P3-002 local Popup continuity | `COVERED` at S0 |
| SC-P3-003 viewport identity continuity | `COVERED` at S0 |
| SC-P3-CAND-004 multi-page/sibling progress | `COVERED` at S0; frozen capability |
| SC-P3-CAND-005 recovery-progress resume | `COVERED` at S0; frozen capability, tasks 4/4, independent validation PASS |
| Safe parent return/backtracking | Mechanics within SC-P3-CAND-004; no generic Back/graph/stack purchase implied |
| SC-P3-CAND-006 autonomous discovered-candidate safety | `COVERED` at bounded S0; frozen capability, tasks 4/4, independent validation PASS |
| SC-P3-CAND-007 repeated viewport exploration and honest exhaustion | `COVERED` at bounded S0; frozen capability, tasks 4/4, independent validation PASS |

## S0 World Boundary

The deterministic simulation may define visible elements, dispatch outcomes, world transitions, Observation data, Popup appearance, and Launcher drift. It must not encode production conclusions such as Container identity, Recovery authority, progress completion, or Goal success.

## Legacy Evidence Boundary

Legacy simulations and emulator artifacts are evidence sources only. Old FSM names, Popup handlers, StateRestorer, Frame, navigation graph/stack, completion enums, or safety components do not become Capstone requirements merely because a legacy test used them.

## Explicitly Not Authorized

- Capstone Runtime implementation or tests;
- reopening or expanding frozen SC-P3-CAND-006/007 without new evidence and Gate authority;
- a graph, stack, navigation manager, safety manager, risk enum, or progress framework;
- Runtime refactor;
- Harness H4-4, automatic Scenario selection, multi-Scenario orchestration, daemon, or service;
- S1 replay migration, S2 integration, or S3 emulator execution;
- `S0_GRADUATED`, `PHASE_3_FROZEN`, or `PHASE_COMPLETE` claims.

A `CAPSTONE` integrates frozen capabilities and does not directly purchase new production semantics. If execution exposes a new Reality Distinction, stop, extract one bounded Candidate Scenario, run its Semantic Gate, prove/freeze that capability, and only then return to the Capstone.

## Capstone Readiness

```text
PREREQUISITES_MAPPED
```

Readiness sequence: `REGISTERED → DECOMPOSED → PREREQUISITES_MAPPED → READY_FOR_S0_RUN → S0_GRADUATED`.

SC-S0-CAPSTONE-001 becomes `S0_GRADUATED` only after its prerequisites are frozen and an approved integration run independently passes the full deterministic Scenario and replay contract.

All currently mapped Runtime capability prerequisites are now frozen. Readiness remains `PREREQUISITES_MAPPED` because independent legacy simulation baseline classification and a separate Capstone authorization are still required; this document does not start either workflow.
