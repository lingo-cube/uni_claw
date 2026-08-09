# SC-P3-CAND-009 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-09
> Scope: SC-P3-CAND-009 only — this is not a Phase 3 freeze, S0 baseline decision, or Capstone authorization.
> Authority: acceptance receipt for `openspec/changes/phase3-discovered-branch-effect-revalidation/`; it does not replace the approved Scenario, Spec, Architecture Contract, prior frozen capability closeouts, or remaining roadmap gates.

## Capability

**Evidence-Validated Resume for a Freshly Discovered Non-Plan Branch After Verified Agent Recovery**

Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED` (Human Decision: `ACCEPT_OPTION_C_BOUNDARY`)

Carrier Model Test: `MINIMUM_BRANCH_EFFECT_CRITERION_REQUIRED`

## Proven Behavior

```text
historical A completion under parent P
→ one external Agent-scope drift
→ Recovery restores / observes / verifies P exactly once
→ Agent reconciles one fresh recovered-world Observation
→ singular carrier identity exactly matches A in accepted inventory + historical progress under P
→ A criterion evaluates the fresh Observation only
→ true: A contributes for the current reconciliation, zero duplicate A dispatch, Agent continues independently unresolved B
→ false: historical provenance remains observable, A contributes nothing, zero fabricated repair or success
→ null/absent/mismatch/stale/ambiguous: A remains unresolved, zero blind A redispatch, explicit existing non-completion/escalation
→ final completion still requires independently satisfied GoalEvidence
```

The accepted slice proves:

- Historical completion, Recovery verification, and current external-effect validity remain distinct meanings; `RecoveryResult.Verified` never implies branch-effect verification.
- The singular immutable Goal-held `BranchEffectCriterion` is a durable external-effect hypothesis, not proof of discovery, inventory membership, authorization, historical completion, current validity, lifecycle, Recovery, completion, or Goal outcome.
- A discovered branch may be freshly revalidated without becoming a PlanStep and without entering the immutable Plan; the carrier does not prove presence or select work.
- The carrier is matched only when the same exact branch identity appears in accepted SC-P3-CAND-008 inventory evidence and historical SC-P3-CAND-004 completion provenance under the same active parent scope; missing, mismatched, stale, conflicting, or ambiguously parented identity evidence remains unresolved with no fuzzy matching, generated identity, registry, or new identity authority.
- Criterion evaluation occurs only after one verified Recovery and only against the fresh recovered-world Observation that post-dates the verification boundary; stale pre-Recovery evidence, parent identity, refreshed inventory, successful local mechanics, dispatch history, and `RecoveryResult.Verified` alone never trigger evaluation or contribution.
- The evaluator is deterministic, side-effect-free, reads only the supplied Observation plus caller-captured immutable values, and returns exactly true / false / null.
- `true` permits A to contribute and B to continue with zero duplicate A dispatch; `false` preserves historical provenance while excluding A from current contribution and fabricates no repair, redispatch, or success; `null`, absent carrier, identity mismatch, stale evidence, or ambiguous parent scope leaves A unresolved and non-contributing without blind redispatch.
- The nullable evaluation result is derived and consumed by Agent control flow; it is never persisted as a validity, lifecycle, Recovery, or completion status, freshness epoch, or new mutable dictionary.
- `BranchProgressEvidence`, `BranchInventoryEvidence`, Plan, `Goal.EvidenceEvaluator`, and GoalEvidence retain their frozen meanings; only Agent consumption of independently satisfied GoalEvidence may complete the Run.
- Agent remains the sole retain/invalidate/unresolved, resume/escalation, cross-Container progress, GoalEvidence, and final RunState authority; Recovery remains restore → observe → verify mechanics only and performs no branch-progress or branch-effect interpretation.
- An absent carrier preserves the frozen behavior of every prior Scenario, including the SC-P3-CAND-008 discovered-branch route.
- Equal inputs replay equal criterion outcomes, contributing progress, duplicate-dispatch count, ActionHistory, journal, Trace, GoalEvidence, and final RunState.

## Production Delta

- Model types: exactly +1 immutable `BranchEffectCriterion` value.
- Fields: exactly +3 total — `BranchIdentity`, `Evaluator`, and optional immutable `Goal.DiscoveredBranchEffectCriterion` (`BranchEffectCriterion?`).
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- New mutable-state fields: +0.
- New mutable-state owners: +0.
- Behavior: one opt-in bounded Agent control-flow branch that, after a verified Recovery, matches the singular Goal-held carrier under the same active parent and evaluates the fresh recovered-world Observation with a three-way outcome; no new state, registry, or lifecycle surface.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Environment remains external-world Observation and dispatch-outcome authority only.
- Traversal remains the deterministic one-step Execute → Observe → Verify mechanics and journal owner.
- Container remains the semantic-page continuity, accepted same-Container evidence, and local-progress owner.
- Recovery remains restore → observe → verify mechanics and gains no branch-progress, branch-effect, route, or completion authority or dependency.
- Agent remains the sole carrier matching/interpretation, retain/invalidate/unresolved, resume/escalation, cross-Container progress, GoalEvidence, and final-RunState authority.

## Frozen Boundary

| Evidence / decision | Frozen meaning |
|---|---|
| carrier present, identity exactly A under P, criterion true | A's historical completion is revalidated for the current reconciliation; A may contribute; zero duplicate A dispatch; B may continue. |
| carrier present, criterion false | A's historical provenance remains observable; A contributes nothing; no repair, redispatch, or success is fabricated. |
| criterion null / carrier absent / identity mismatch / stale evidence / ambiguous parent | A remains unresolved, contributes nothing, and is not blindly redispatched; explicit existing non-completion/escalation. |
| carrier presence alone | Proves no discovery, inventory membership, authorization, completion, validity, lifecycle, or Goal outcome. |
| `RecoveryResult.Verified` alone | Proves no branch-effect verification and triggers no evaluation. |
| carrier absent | Existing frozen behavior (including SC-P3-CAND-008 route) remains unchanged. |

## Explicitly Not Purchased

- Graph, Tree, Stack, Frontier, route registry, persistent route/depth state, checkpoint, or ResumeToken;
- DynamicPlan, DynamicPlanner, generic planner/re-plan, arbitrary action synthesis, manager, workflow, or FSM;
- Generalized multi-parent routing, generalized branch lifecycle, global semantic identity, identity authority, criterion collection, or generic effect registry;
- New mutable-state field/owner, stored validity/lifecycle/Recovery/completion state, or freshness epoch;
- Recovery during an unfinished dynamic-discovery continuation or new Recovery ownership/dependency;
- Runtime refactor, Harness change, Capstone execution, roadmap readiness, Phase completion, or S0 graduation.

## Structural Pressure

Agent now contains another opt-in bounded control-flow branch (post-Recovery effect revalidation). The path remains inside existing Agent authority, uses only the Goal-held singular carrier and Run-local derived outcomes, adds no mutable state field or owner, and is fully expressed within the approved one-type/three-field budget. This is non-blocking structural pressure and does not authorize a planner, route abstraction, criterion registry, extraction, compression, or Runtime refactor.

## Acceptance Receipt

- OpenSpec: strict validation passed; proposal/design/specs/tasks complete.
- Tasks: 4/4 complete.
- Independent validation: PASS (fresh evidence; zero violations).
- Build: 0 warnings, 0 errors.
- Tests: 378/378 passed.
- SC-P3-CAND-009 fixture/behavior/formal tests: 36/36 passed.
- Frozen slice regressions (CAND-004/005/006/007/008): 111/111 passed.
- Architecture Guards: 8/8 passed.
- Consistency checks: C1–C9 ALL PASS.
- Semantic diagnostics: 0 warnings.
- Production delta: exactly one approved immutable type, three approved fields, and one existing-Agent opt-in control-flow adjustment.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_CAND_009_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN`, `S0_BASELINE_READY`, `S0_GRADUATED`, `CAPSTONE READY`, or `PHASE_COMPLETE`. Capstone authorization/execution, OpenSpec archive, any S1/S2/S3 work, and any new Scenario require separate authority.
