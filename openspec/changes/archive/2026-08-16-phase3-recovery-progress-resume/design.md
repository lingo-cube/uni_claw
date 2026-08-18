## Context

SC-P2-001 proves one Agent-scope external-drift Recovery can restore and verify a trusted world position. SC-P3-CAND-004 proves Agent-owned immutable `BranchProgressEvidence` across a bounded parent P with approved siblings A and B. The current Agent retains branch progress through Recovery but restores position by replaying `Plan[0..suspendedIndex)`, which can repeat A's already-completed external work. Its retained completion sequence says when A was proven, not which durable external proposition justified that proof.

The reopened Semantic Gate supersedes the earlier zero-delta decision. Existing whole-Goal evaluation, page-level WorldBelief, Container continuity, Traversal verification, dispatch outcomes, and Observation sequence references cannot distinguish a freshly revalidated branch effect from a contradicted or unobservable effect.

The approved purchase is exactly one optional immutable `PlanStep.BranchEffectEvidenceEvaluator: Func<Observation, bool?>?` field. Model types, enums, interfaces, components, and mutable-state owners remain unchanged. Agent retains progress-validity, resume/escalation, GoalEvidence, and final RunState authority; Recovery retains restore/observe/verify mechanics only.

## Goals / Non-Goals

**Goals:**

- Associate each approved bounded branch-entry Plan step with the observable proposition that justified the branch's completion claim.
- Evaluate that criterion only against fresh Observation evidence obtained after `RecoveryResult.Verified`.
- Derive revalidated (`true`), contradicted (`false`), and unresolved (`null` or absent) without storing a validity enum or mutable status.
- Permit revalidated A evidence to contribute and continue with B without replaying A's completed work.
- Prevent contradicted or unresolved A evidence from contributing to subtree or Goal completion.
- Keep prior Observation sequences as historical provenance rather than current truth.
- Preserve deterministic replay and all frozen ownership, authority, Recovery, and completion boundaries.

**Non-Goals:**

- Add an EvidenceState/RecoveryState model, freshness epoch, validity field, registry, action-idempotence taxonomy, checkpoint, ResumeToken, snapshot/progress manager, Recovery planner, navigation graph/stack, FSM, or generic predicate/validity/retry/recovery framework.
- Generalize beyond one bounded parent, two approved sibling branches, and one verified Recovery.
- Revalidate arbitrary Goal, Container, or page state.
- Move branch validity into Recovery, Container, or Traversal.
- Implement autonomous safety, SC-S0-CAPSTONE-001, Runtime refactoring, Harness changes, or real-device/Vision behavior.

## Decisions

### Carry one branch-effect criterion on the approved branch-entry PlanStep

`PlanStep` receives exactly one optional immutable field:

```csharp
Func<Observation, bool?>? BranchEffectEvidenceEvaluator
```

For the bounded parent P, the branch-entry step whose `TargetDescription` identifies A carries A's effect criterion. The criterion is an Agent-owned Plan hypothesis, not evidence or truth. The historical completion remains in `BranchProgressEvidence.CompletedSiblingEvidence`; no criterion is duplicated into progress state.

The evaluator contract is normative:

- `true`: the supplied fresh Observation positively proves the required branch effect holds;
- `false`: the supplied fresh Observation positively proves the required branch effect does not hold;
- `null`: the supplied Observation cannot determine the effect;
- absent evaluator: unresolved.

The evaluator must be deterministic, side-effect-free, and depend only on its Observation argument. It cannot inspect or mutate Runtime owners, call the Environment, or turn Plan presence into proof.

Alternative rejected: reuse `Goal.EvidenceEvaluator`. It evaluates whole-Goal completion and its boolean result cannot distinguish contradiction from unobservability without changing GoalEvidence meaning.

Alternative rejected: store a criterion string. A string would need an unpurchased parser/interpreter and would not itself provide deterministic three-way evaluation.

Alternative rejected: add a new BranchEffectCriterion type or field on `BranchProgressEvidence`. The approved branch-entry Plan step already provides a bounded identity association and survives the Recovery call; another type or progress field is unnecessary.

### Treat the Agent-scope Trap Observation as the freshness boundary

`Trap.Observed` remains the existing boundary. Completion sequences at or before that boundary are retained historical evidence and cannot contribute after Recovery until their corresponding criterion evaluates a fresh post-verification Observation to `true`.

`RecoveryResult.Verified`, correct parent identity, fresh inventory, and Container continuity remain necessary positional/context evidence but are insufficient to validate A's effect. Agent performs criterion evaluation only after Recovery verifies and Agent reconciles the fresh Observation.

Alternative rejected: add a recovery epoch or validity field. Existing sequence ordering plus the Agent-owned Trap boundary identifies freshness; only the missing proposition requires purchase.

### Derive validity outcomes without storing state

For every retained completed sibling in the bounded parent snapshot, Agent finds the matching approved branch-entry Plan step and evaluates its criterion against the fresh recovered-world Observation:

- `true`: replace the completion sequence with the fresh Observation sequence through the existing immutable progress value; this revalidated claim may contribute.
- `false`: reconstruct the immutable progress snapshot without that current completion claim; historical Trace/journal evidence remains unchanged.
- `null` or missing: do not promote the old sequence; the retained historical claim cannot contribute and Agent produces an explicit existing failure/escalation outcome.

No validity result is stored as an enum or field. The derived outcome is consumed immediately by Agent control flow and is externally evidenced through progress, Trace, GoalEvidence, action history, and final RunState.

Alternative rejected: let `BranchProgressEvidence.IsSubtreeComplete` decide directly. That property describes historical evidence coverage and cannot prove current recovered-world validity by itself.

### Bound no-blind-replay to the recovered parent Scenario

The deterministic positive branch recovers directly to the same bounded parent P from which execution was suspended. Once Agent verifies the recovered position and revalidates A, it must not replay the historical A-entry, A-work, or A-return prefix. It resumes the remaining approved execution at the suspended B navigation step. A's external-effect action appears exactly once.

If A is contradicted or unresolved, Agent does not silently replay A to repair uncertainty. It returns an explicit non-completion/escalation through existing Agent/Trace/RunState surfaces. This is the previously approved `SAME_SCENARIO_PRESSURE` boundary, not a generic replay planner or idempotence policy.

Alternative rejected: classify arbitrary actions as idempotent or compute checkpoints. The Scenario already identifies A as completed evidence-backed work and requires only one parent-local resume decision.

### Preserve final completion authority

Revalidated branch progress is only admissible evidence for bounded subtree evaluation. It does not set `RunState.Completed`. Agent continues to call the existing whole-Goal evaluator on fresh post-action Observation evidence, and only satisfied GoalEvidence may complete the Run.

Recovery reports only restore/verification results. Container remains page-local. Traversal continues its deterministic Execute → Observe → Verify protocol and does not interpret branch criteria.

## Risks / Trade-offs

- [Risk] A delegate can capture mutable or nondeterministic state. → The contract requires an Observation-only, side-effect-free deterministic evaluator; formal replay tests use equal inputs and assert equal outputs/evidence.
- [Risk] Matching a criterion through `TargetDescription` could be ambiguous in a generalized multi-parent Plan. → The purchase is explicitly limited to one bounded parent with uniquely approved sibling identities; generalized routing remains deferred.
- [Risk] The recovered parent Observation may not expose enough evidence to evaluate a child effect. → The evaluator returns `null`; Agent escalates explicitly and does not replay or fabricate progress.
- [Risk] Existing `IsSubtreeComplete` can remain true for retained historical sequences before Agent consumes the recovered-world judgement. → Agent must not authorize post-Recovery contribution from that property until each retained completion has been freshly revalidated.
- [Risk] Skipping prefix replay only works when verified Recovery directly restores the suspended parent P. → That is the bounded Scenario precondition; other restore shapes are unresolved and do not authorize a graph, checkpoint, or generic planner.
