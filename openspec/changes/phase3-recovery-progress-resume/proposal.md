## Why

Verified Agent Recovery restores a trusted world position, but it does not prove that Agent-owned branch-progress evidence obtained before external drift remains valid in the recovered world. SC-P3-CAND-005 requires the smallest branch-scoped evidence criterion so fresh recovered-world Observation evidence—not historical sequence age, page identity, or Recovery success—can decide whether prior progress may contribute.

## What Changes

- Add the approved SC-P3-CAND-005 formal contract for one verified Agent Recovery inside one bounded parent scope.
- Add exactly one immutable semantic field, `PlanStep.BranchEffectEvidenceEvaluator: Func<Observation, bool?>?`, on the Scenario-approved branch-entry step.
- Define `true` as fresh positive proof that the branch effect still holds, `false` as fresh positive contradiction, and `null` or an absent evaluator as unresolved.
- Require the evaluator to be deterministic, side-effect-free, Observation-only, and incapable of reading or mutating Runtime owners or the external environment.
- Treat branch progress whose evidence predates the Agent-scope drift boundary as retained history that cannot contribute until its criterion is freshly revalidated.
- Prevent contradicted and unresolved retained evidence from supporting subtree or Goal completion.
- Continue with the remaining approved sibling when prior completion is revalidated, without blindly redispatching the already-completed child work represented by that evidence.
- Preserve Agent ownership of progress validity, recovery/resume decisions, GoalEvidence evaluation, and final RunState; preserve Recovery ownership of restoration mechanics only.

## Capabilities

### New Capabilities

- `recovery-progress-resume`: Defines evidence-validated use of retained Agent-owned branch progress after one verified Recovery, including revalidated, contradicted, unresolved, no-blind-replay, and deterministic-replay branches.

### Modified Capabilities

None.

## Impact

- Expected production surface: one optional immutable field on existing `PlanStep` plus existing Agent recovery/resume and branch-progress control flow, subject to approved tasks.
- Expected verification surface: deterministic Recovery/progress Scenario Fake and SC-P3-CAND-005 positive, contradicted, unresolved, blind-replay, completion-authority, and replay proofs.
- Production delta budget: model types +0; fields +1; enums +0; interfaces +0; components +0; mutable-state owners +0.
- Ownership delta: none. Authority delta: none.
- SC-P2-001 world-position Recovery and SC-P3-CAND-004 sibling-progress behavior remain frozen prerequisites.
- No evidence/recovery state enum, freshness field, recovery epoch, ResumeToken, checkpoint, snapshot/progress manager, Recovery planner, action-idempotence taxonomy, navigation graph/stack, EffectRegistry, generic predicate/validity/retry/recovery framework, autonomous safety, Capstone implementation, Harness change, or Runtime refactor is purchased.
