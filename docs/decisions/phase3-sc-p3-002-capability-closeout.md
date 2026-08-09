# SC-P3-002 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-08
> Scope: SC-P3-002 only — this is not a Phase 3 freeze.
> Authority: acceptance receipt for `openspec/changes/phase3-popup-local-recovery/`; it does not replace the Scenario, Spec, Architecture Contract, or frozen Phase 2 decisions.

## Capability

**Popup Obstruction Recovery with Container Continuity**

Semantic Gate: `BEHAVIOR_PURCHASE_ONLY`

## Proven Behavior

```text
local obstruction
→ bounded local handling
→ fresh Observation
→ Container continuity verification
```

The accepted slice proves:

- A Popup or local obstruction does not by itself prove semantic-page drift.
- Local handling is bounded and follows the existing Container → Traversal → Environment direction.
- Dispatch outcome does not prove that the obstruction was handled or that Container continuity holds.
- A fresh post-handling Observation must advance beyond the obstruction evidence.
- Continuity requires compatible foreground evidence, acceptance by the existing `Container.IsStillMine` rule, and reconciled semantic-page evidence that does not contradict the active Container.
- When continuity is proven, the same Container remains active, its pre-obstruction local progress is preserved, and execution may continue.
- When handling fails or continuity cannot be proven, no local success or progress reset is fabricated; Container-scope evidence is escalated and Agent retains higher authority.
- Goal completion still requires satisfied `GoalEvidence`; local handling does not complete the Run.
- Equal RunId, deterministic Environment input, and action sequence replay to equal ActionHistory, Observation sequence, journal, Trace, continuity evidence, progress, GoalEvidence, and final Run state.

## Production Delta

- Allowed change: existing Container and Agent control-flow/method behavior only.
- Production model types: +0.
- Production fields: +0.
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- Mutable state: +0.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Environment remains the external evidence and action boundary.
- Traversal remains the owner of the local deterministic execution protocol.
- Container remains the owner of local semantic state, local progress, obstruction classification, continuity judgement, and Container-scope evidence.
- Agent remains the authority for active Container rebind/invalidation, Agent Recovery decisions, GoalEvidence evaluation, and final RunState.
- Recovery remains the existing recovery mechanism owner.
- Recovery → Container/Traversal remains **FORBIDDEN**.

## Frozen Boundary

| Branch | Frozen meaning |
|---|---|
| Continuity proven | Preserve the same Container and local progress, then allow existing execution to continue. |
| Continuity unproven | Preserve available local progress, emit Container-scope evidence, and escalate; Agent owns the higher-scope response. |

## Explicitly Not Purchased

- PopupManager;
- PopupRecoveryEngine;
- generic local recovery framework;
- generic retry framework;
- generic uncertainty framework;
- Fingerprint;
- new Confidence behavior;
- Scroll;
- FSM or state-machine abstraction;
- multi-container progress;
- new Recovery semantics.

## Acceptance Receipt

- OpenSpec: strict validation passed; state `all_done`.
- Tasks: 4/4 complete.
- Independent validation: PASS.
- Build: 0 warnings, 0 errors.
- Tests: 187/187 passed.
- Architecture Guards: 9/9 passed.
- Consistency checks: ALL PASS.
- Production model delta: 0.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_002_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN` or `PHASE_COMPLETE`. Other Phase 3 candidate scenarios remain unstarted and require their own authority.
