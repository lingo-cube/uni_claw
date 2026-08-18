# recovery-progress-resume Specification

## Purpose
TBD - created by archiving change phase3-recovery-progress-resume. Update Purpose after archive.

## Requirements

### Requirement: Represent one bounded branch-effect evidence criterion
The Runtime SHALL add exactly one optional immutable `PlanStep.BranchEffectEvidenceEvaluator` field with semantic shape `Func<Observation, bool?>?`. On an approved branch-entry step, the field SHALL represent the observable external proposition whose proof justified that bounded branch's completion claim. The criterion SHALL remain a hypothesis until evaluated against Observation evidence and SHALL NOT itself prove branch completion.

#### Scenario: Approved branch entry carries A effect criterion
- **WHEN** the bounded parent P Plan identifies approved sibling A and A can become evidence-backed complete
- **THEN** A's branch-entry Plan step carries the Observation-only criterion needed to evaluate whether A's required external effect still holds after Recovery

#### Scenario: Missing criterion is unresolved
- **WHEN** retained completion evidence exists for A but its approved branch-entry step has no branch-effect evaluator
- **THEN** Agent treats A's recovered-world validity as unresolved and does not permit the retained completion to contribute

### Requirement: Preserve deterministic three-way criterion semantics
The branch-effect evaluator SHALL be deterministic, side-effect-free, and dependent only on its supplied Observation. It SHALL return `true` only when that Observation positively proves the required effect holds, `false` only when it positively proves the required effect does not hold, and `null` when the effect cannot be determined. It SHALL NOT read or mutate Agent, Recovery, Container, Traversal, or Environment state.

#### Scenario: Fresh evidence positively proves A effect
- **WHEN** the evaluator receives a fresh post-verified-Recovery Observation that positively proves A's required external effect
- **THEN** it returns `true`

#### Scenario: Fresh evidence positively contradicts A effect
- **WHEN** the evaluator receives a fresh post-verified-Recovery Observation that positively proves A's required external effect does not hold
- **THEN** it returns `false`

#### Scenario: Fresh evidence cannot establish A effect
- **WHEN** the evaluator receives a fresh post-verified-Recovery Observation that contains insufficient evidence to determine A's required external effect
- **THEN** it returns `null` rather than conflating unobservability with contradiction

### Requirement: Treat pre-Recovery branch progress as retained historical evidence
After an Agent-scope drift, any `BranchProgressEvidence` completion whose source Observation sequence is at or before `Trap.Observed` SHALL remain historical evidence only and SHALL NOT contribute to recovered-world subtree or Goal evaluation until its branch criterion evaluates fresh evidence obtained after `RecoveryResult.Verified` to `true`.

#### Scenario: Historical sequence alone cannot validate A
- **WHEN** A was proven complete before Agent-scope drift and Recovery later returns `Verified`
- **THEN** A's pre-Recovery completion sequence, correct parent identity, Recovery success, and fresh parent inventory do not independently authorize A to contribute

#### Scenario: Pre-Recovery Observation cannot be reused as fresh truth
- **WHEN** A's criterion would return `true` for its historical completion Observation but no post-Recovery Observation has evaluated the criterion
- **THEN** Agent does not revalidate A from the historical Observation

### Requirement: Revalidate a retained branch effect from fresh evidence
After one verified Recovery to the bounded parent P, Agent SHALL evaluate each retained completed branch's criterion against the fresh recovered-world Observation. When A's criterion returns `true`, Agent SHALL associate A's current completion evidence with the fresh Observation sequence and MAY permit A to contribute to bounded progress. Revalidation SHALL NOT independently complete the Goal.

#### Scenario: A effect survives Recovery
- **WHEN** A was proven complete before drift, Recovery verifies and reconciles a fresh Observation after the drift boundary, and A's criterion returns `true`
- **THEN** Agent revalidates A with the fresh sequence, permits A to contribute, and keeps final completion dependent on satisfied GoalEvidence

### Requirement: Exclude contradicted retained progress
When fresh post-verified-Recovery evidence causes A's criterion to return `false`, Agent SHALL exclude A's historical completion from current bounded progress. Historical Trace and journal evidence SHALL remain historical, and the contradicted branch SHALL NOT contribute to subtree or Goal completion.

#### Scenario: Fresh evidence disproves A effect
- **WHEN** A was complete before drift and A's criterion evaluates the fresh recovered-world Observation to `false`
- **THEN** Agent excludes A from current completion, does not fabricate subtree or Goal completion, and preserves historical Trace/journal provenance

### Requirement: Escalate unresolved retained progress without blind replay
When A's criterion is absent or returns `null`, Agent SHALL treat A's current validity as unresolved, SHALL NOT permit its retained completion to contribute, SHALL NOT blindly redispatch A's completed work, and SHALL produce an explicit existing Agent-level non-completion/escalation outcome.

#### Scenario: A effect is unobservable after Recovery
- **WHEN** Recovery verifies the expected parent P but A's required effect cannot be determined from the fresh Observation
- **THEN** A contributes nothing, Agent does not redispatch A, and the Run exposes an explicit unresolved failure/escalation rather than fabricated completion

### Requirement: Resume remaining bounded work without replaying revalidated completion
When verified Recovery directly restores the suspended bounded parent P and fresh evidence revalidates completed sibling A, Agent SHALL bypass the historical A-entry, A-work, and A-return prefix and resume the remaining approved execution with sibling B. A's completed external-effect action SHALL appear exactly once in ActionHistory.

#### Scenario: Revalidated A continues with B
- **WHEN** A is freshly revalidated at recovered parent P and B remains approved and incomplete
- **THEN** Agent continues with B without redispatching A's completed action, and eventual subtree completion requires valid evidence for B as well

#### Scenario: Position proof alone cannot skip or replay work
- **WHEN** Recovery reaches P but A is contradicted or unresolved
- **THEN** Agent neither treats A as complete nor silently replays A to repair the uncertainty

### Requirement: Preserve Agent progress and completion authority
Agent SHALL remain the sole owner of `BranchProgressEvidence`, retained-progress validity interpretation, resume/escalation decisions, GoalEvidence evaluation, and final RunState. Recovery SHALL own only restore/observe/verify mechanics; Container SHALL remain page-local; Traversal SHALL remain deterministic local execution. No lower scope SHALL decide branch validity or Goal completion.

#### Scenario: Recovery verification is not branch or Goal success
- **WHEN** Recovery returns `Verified`
- **THEN** Recovery reports only position-verification evidence and Agent alone evaluates branch criteria and final GoalEvidence

### Requirement: Replay SC-P3-CAND-005 deterministically
The Runtime SHALL produce deterministic SC-P3-CAND-005 evidence when RunId, bounded world input, Plan including branch criteria, disturbance schedule, and action sequence are equal.

#### Scenario: Equal recovery-progress inputs replay equally
- **WHEN** the positive, contradicted, or unresolved branch is executed twice with equal inputs
- **THEN** criterion outcomes, branch-progress snapshots, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState are equal

### Requirement: Preserve the approved semantic and deferred budget
SC-P3-CAND-005 SHALL add exactly one immutable production field on `PlanStep` and SHALL add no production model type, enum, interface, component, or mutable-state owner. It SHALL preserve ownership and authority and SHALL NOT introduce a persistent validity state, EvidenceState/RecoveryState, recovery epoch, ResumeToken, checkpoint, manager, Recovery planner, action-idempotence taxonomy, navigation graph/stack, EffectRegistry, generic predicate/validity/retry/recovery framework, autonomous safety, Capstone implementation, Harness change, or Runtime refactor.

#### Scenario: Formal proof stays inside the one-field purchase
- **WHEN** all SC-P3-CAND-005 positive, contradicted, unresolved, no-blind-replay, ownership, and replay assertions pass
- **THEN** the production delta remains exactly one `PlanStep.BranchEffectEvidenceEvaluator` field and all deferred capabilities remain absent
