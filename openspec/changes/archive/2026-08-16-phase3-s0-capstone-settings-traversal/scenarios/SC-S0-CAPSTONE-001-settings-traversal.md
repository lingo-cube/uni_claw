# SC-S0-CAPSTONE-001 — Four-Level Settings Traversal with Safety and Recovery

> S0 Graduation integration Scenario | Capstone role | Semantic Status: `CANDIDATE` (pending HUMAN Semantic Gate)
> Capstone Readiness: `PREREQUISITES_MAPPED` → `READY_FOR_S0_RUN` only after gate approval and implementation
> Production Delta: exactly zero (model types +0; fields +0; enums +0; interfaces +0; components +0; mutable state +0)
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/s0-capstone-settings-traversal/spec.md`

## Goal

Prove that the frozen Runtime capabilities compose end-to-end in one deterministic four-level Settings world: traverse all approved reachable safe branches to depth `<= 4`, never execute dangerous state-mutating operations, handle exactly one Popup obstruction and exactly one external Launcher drift with verified recovery and honest progress reconciliation, and replay deterministically.

## Given

- Runtime is Running with traversal intent, allowed scope, a depth bound of 4, and safety constraints; the complete route is NOT pre-enumerated.
- The deterministic external world exposes an approved semantic navigation tree with safe reachable pages to at least four levels, and at least one visible dangerous mutation candidate.
- Exactly one local Popup/Overlay obstruction and exactly one external drift to Launcher/desktop are scheduled by the world.
- All 13 capability prerequisites are frozen with independent validation PASS; `S0_BASELINE_READY` is declared (HUMAN gate, 2026-08-09).

## Required Integration Behavior

### Normal Traversal

```text
traversal intent + allowed scope + depth <= 4 + safety constraints
→ fresh external-world evidence
→ discover approved safe branches (route not pre-encoded; SC-P3-CAND-008)
→ traverse with evidence-backed progress (SC-P1-001; SC-P3-CAND-004)
→ no double-count of verified work
→ no completion while an approved reachable safe branch remains unresolved
```

### Dangerous Candidate

```text
visible candidate
!=
approved executable action
→ zero dispatch (SC-P3-CAND-006; static preauthorized safety)
→ explicit rejected/denied evidence
```

### Popup Disturbance

```text
Popup appears
→ bounded local handling (SC-P3-002)
→ fresh Observation
→ verify underlying Container continuity
→ preserve progress
→ continue
```

### External Drift

```text
Launcher / desktop observed
→ current local Container invalid
→ Agent-scope escalation
→ re-enter Settings
→ restore a trusted semantic position
→ Observe → Verify (SC-P2-001)
→ reconcile fresh evidence honestly (SC-P3-CAND-005/009)
→ re-entry is not new progress; retained progress neither fabricated nor discarded
```

### Viewport Movement

```text
one bounded forward viewport movement (SC-P3-003)
→ changed element snapshot remains within the same semantic Container
→ fresh identity evidence proves continuity
→ repeated exploration with honest exhaustion (SC-P3-CAND-007)
```

## Completion Evidence

Agent may complete the Run only when GoalEvidence proves ALL of:

1. every approved reachable safe branch within depth `<= 4` is complete;
2. dangerous visible actions were not dispatched;
3. no approved branch remains unresolved;
4. Popup handling was followed by fresh verified Container continuity;
5. external drift recovery was followed by fresh verification and reconciliation;
6. already-proven traversal progress was neither fabricated nor silently discarded;
7. equal RunId, external-world inputs, disturbance schedule, and action sequence replay to equal progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState.

Plan exhaustion, action dispatch, Recovery dispatch, a changed viewport snapshot, or local Container completion cannot independently satisfy this Goal.

## Required Assertions

1. The complete route is not encoded up front; branch discovery comes from fresh external-world evidence within the approved scope.
2. Traversal reaches every approved reachable safe branch within depth `<= 4`.
3. Dangerous visible actions are never dispatched; zero dangerous dispatch is proven by the final state.
4. No approved branch remains unresolved at completion.
5. The single Popup obstruction is handled with fresh verified Container continuity.
6. The single external drift triggers the frozen recovery path; re-entry is not counted as new progress.
7. Retained traversal progress is neither fabricated nor silently discarded after Recovery.
8. Already-verified semantic work is not counted repeatedly as new progress.
9. Plan exhaustion, action dispatch, Recovery dispatch, viewport snapshot change, or local Container completion alone do not complete the Run.
10. The production delta of the implementation is exactly zero; all 13 frozen slice regressions pass unchanged.
11. Equal inputs replay equal progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState.
12. Any new Reality Distinction stops the run and extracts exactly one bounded Candidate for its Semantic Gate; no such candidate is pre-approved here.

## S0 World Boundary

The deterministic simulation may define visible elements, dispatch outcomes, world transitions, Observation data, Popup appearance, and Launcher drift. It must not encode production conclusions such as Container identity, Recovery authority, progress completion, or Goal success.

## Legacy Evidence Boundary

Legacy simulations and emulator artifacts are evidence sources only. Old FSM names, Popup handlers, StateRestorer, Frame, navigation graph/stack, completion enums, or safety components do not become Capstone requirements merely because a legacy test used them.

## Explicitly Deferred

- Capstone Runtime implementation or tests (require Semantic Gate approval);
- reopening or expanding any frozen capability without new evidence and Gate authority;
- graph, stack, navigation manager, safety manager, risk enum, progress framework, DynamicPlan, planner, or FSM;
- Runtime refactor;
- Harness H4-4, automatic Scenario selection, multi-Scenario orchestration, daemon, or service;
- S1 replay migration, S2 integration, S3 emulator execution;
- `S0_GRADUATED`, `PHASE_3_FROZEN`, or `PHASE_COMPLETE` claims.
