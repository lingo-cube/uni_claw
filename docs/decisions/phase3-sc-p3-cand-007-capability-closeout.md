# SC-P3-CAND-007 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-09
> Scope: SC-P3-CAND-007 only — this is not a Phase 3 freeze or Capstone authorization.
> Authority: acceptance receipt for `openspec/changes/phase3-viewport-exploration-exhaustion/`; it does not replace the approved Scenario, Spec, Architecture Contract, prior frozen capability closeouts, or S0 roadmap gates.

## Capability

**Evidence-Based Repeated Viewport Exploration and Honest Exhaustion**

Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`

Viewport Model Test: `MINIMUM_VIEWPORT_EVIDENCE_REQUIRED`

## Proven Behavior

```text
bounded accepted same-Container Observation evidence
→ Agent-owned criterion returns continue / exhausted / unresolved
→ continue: authorize at most one existing ScrollForward Plan step
→ fresh Observe + SC-P3-003 continuity verification
→ decide again
→ exhausted: stop viewport movement
→ unresolved or bound reached: stop honestly without fabricated exhaustion
→ only independently satisfied GoalEvidence may complete the Run
```

The accepted slice proves:

- Current visible work, movement dispatch, changed evidence, exploration progress, semantic exhaustion, movement-budget exhaustion, local completion, and Run completion remain distinct.
- Container retains only accepted fresh same-Container Observation evidence in deterministic order and remains its sole mutable owner.
- Observation sequence proves evidence order/freshness only; element text/index and snapshot equality do not become stable content identity.
- Agent is the sole authority consuming the Goal-owned deterministic criterion and interpreting `true` / `false` / `null` as continue / positively exhausted / unresolved.
- Every `true` authorizes at most one already-approved `ScrollForward`; every movement still requires SC-P3-003 Execute → fresh Observe → semantic continuity before another decision.
- The positive V1 → V2 → V3 branch records `true` → `true` → `false`, preserves the same Container and prior local progress, dispatches exactly two viewport actions, and never dispatches the third approved movement.
- Positive end/boundary evidence can prove bounded forward exhaustion; same visible evidence alone returns unresolved.
- Rejected dispatch, stale evidence, semantic continuity conflict, no new text, and movement-bound consumption do not become positive exhaustion.
- `null` stops without blind repeat or fabricated completion.
- A final `true` after the approved movement bound is consumed produces unresolved/incomplete evidence rather than exhaustion.
- Positive exhaustion stops viewport movement only. Unsatisfied GoalEvidence cannot complete the Run; satisfied GoalEvidence remains the independent completion cause.
- An absent evaluator preserves frozen fixed-Plan behavior.
- Equal inputs replay to equal retained evidence, outcomes/reasons, ActionHistory, Observations, journal, Trace, GoalEvidence, and RunState.

## Production Delta

- Model types: exactly +1 immutable `ViewportExplorationEvidence` value.
- Fields: exactly +4 total — `ContinueExploration`, `Reason`, optional `Goal.ViewportExplorationEvaluator`, and one Container-owned retained-evidence field.
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- New mutable-state owners: +0.
- Behavior: one opt-in bounded Agent decision branch plus existing-Container evidence retention after accepted viewport continuity.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Environment reports external Observations and dispatch outcomes only.
- Traversal retains one movement's deterministic Execute → Observe → Verify mechanics and journal ownership.
- Container remains the sole owner of semantic-page continuity, local progress, current Observation, and bounded retained same-Container exploration evidence.
- Agent remains the sole authority for Goal relevance, continue/stop/escalate decisions, active Container changes, GoalEvidence consumption, and final RunState.
- Recovery remains unchanged and receives no viewport-exploration authority.

## Frozen Boundary

| Criterion outcome | Frozen meaning |
|---|---|
| `true` | Positive bounded evidence authorizes at most one next already-approved `ScrollForward`, followed by fresh verification and another decision. |
| `false` | Positive bounded evidence proves forward exploration exhaustion and stops further viewport movement; it is not local or Goal completion. |
| `null` | Evidence proves neither continuation nor exhaustion; stop/escalate without blind movement or fabricated completion. |
| bound consumed while `true` | Exploration remains unresolved/incomplete; budget exhaustion is not semantic exhaustion. |
| evaluator absent | Existing fixed-Plan behavior remains unchanged. |

## Explicitly Not Purchased

- Production `Viewport`, `ViewportId`, stable viewport/content identity, hierarchy, graph, stack, manager, or progress framework;
- Fingerprint authority, gesture geometry, reverse scrolling, generic ScrollPolicy, retry or uncertainty framework;
- dynamic planning, arbitrary multi-viewport candidate discovery, generalized enumeration, or multi-Container exploration state;
- new Recovery semantics, FSM, Capstone implementation, Harness change, Runtime refactor, S1/S2/S3 work, or Phase completion.

## Structural Pressure

Agent now contains another bounded optional decision branch, and Container retains bounded immutable Observation snapshots for one semantic page. Ownership and authority remain unambiguous and the approved Scenario is fully expressed within budget, so this is non-blocking structural pressure and does not authorize extraction, compression, or refactor.

## Acceptance Receipt

- OpenSpec: strict validation passed; artifacts complete.
- Tasks: 4/4 complete.
- Independent validation: PASS.
- Build: 0 warnings, 0 errors.
- Tests: 300/300 passed.
- SC-P3-CAND-007 fixture/behavior/formal tests: 30/30 passed.
- Formal Scenario tests: 12/12 passed.
- Architecture Guards: 8/8 passed.
- Consistency checks: C1–C9 ALL PASS.
- Production delta: exactly one approved immutable type, four approved fields, one existing-Agent control-flow adjustment, and one existing-Container retained-evidence behavior.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_CAND_007_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN`, `S0_BASELINE_READY`, `S0_GRADUATED`, `CAPSTONE READY`, or `PHASE_COMPLETE`. Legacy baseline classification, Capstone authorization/execution, OpenSpec archive, and any new Scenario require separate authority.
