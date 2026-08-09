## ADDED Requirements

### Requirement: Represent one bounded immutable discovered-branch effect criterion
The Runtime SHALL add exactly one immutable `BranchEffectCriterion` production value with non-empty `BranchIdentity` and non-null `Evaluator` fields. `Evaluator` SHALL have semantic shape `Func<Observation, bool?>`, SHALL be deterministic and side-effect-free, and SHALL depend only on the supplied Observation plus immutable values captured by the caller. The carrier SHALL represent a durable branch-effect hypothesis, not inventory membership, authorization, historical completion, current validity, lifecycle state, Recovery state, completion state, GoalEvidence, or proof by itself.

#### Scenario: Carrier remains criterion rather than proof
- **WHEN** a carrier exists before fresh recovered-world evidence is observed
- **THEN** its presence proves no branch membership, completion, validity, or Goal outcome

### Requirement: Carry exactly one optional discovered-branch association on Goal
Goal SHALL add exactly one optional immutable `DiscoveredBranchEffectCriterion` field of type `BranchEffectCriterion?`. The field SHALL carry at most one branch identity-to-criterion association for the bounded SC-P3-CAND-009 Scenario. An absent field SHALL preserve existing behavior and SHALL make discovered-branch effect validity unresolved after Recovery. The field SHALL NOT be a collection, registry, resolver service, identity authority, route model, or replacement for `Goal.EvidenceEvaluator`.

#### Scenario: Discovered branch need not become a PlanStep
- **WHEN** inventory evidence discovers required branch A and A is absent from the immutable Plan
- **THEN** A may use the independently matched Goal-held carrier without adding A to Plan or requiring a PlanStep

#### Scenario: Absent carrier is unresolved
- **WHEN** A has historical completion but Goal has no discovered-branch carrier
- **THEN** A contributes nothing after Recovery, is not blindly redispatched, and remains explicitly unresolved

### Requirement: Associate the criterion only with independently established bounded identity
Agent SHALL remain the sole authority that matches `BranchEffectCriterion.BranchIdentity`. A match SHALL require the same exact branch identity to be present in accepted SC-P3-CAND-008 inventory evidence and historical SC-P3-CAND-004 completion provenance under the same active parent scope. The carrier SHALL NOT establish branch discovery, membership, authorization, parent association, or historical completion. Missing, mismatched, stale, conflicting, or ambiguously parented identity evidence SHALL remain unresolved and SHALL NOT trigger fuzzy matching, generated identity, registry lookup, or new identity authority.

#### Scenario: Exact bounded association succeeds
- **WHEN** P inventory and P progress both identify completed branch A and the singular carrier identity is exactly A
- **THEN** Agent may associate A with the carrier for post-Recovery evaluation

#### Scenario: Identity mismatch cannot attach a criterion
- **WHEN** the carrier identity differs from the completed required branch identity or parent scope is ambiguous
- **THEN** Agent does not evaluate or attach the carrier and A remains unresolved

### Requirement: Evaluate only fresh evidence after verified Agent Recovery
Agent SHALL evaluate the matched criterion only after one Agent-scope drift, one `RecoveryResult.Verified`, and reconciliation of a fresh recovered-world Observation after that verification boundary. The evaluator SHALL receive only that fresh Observation. Historical completion evidence, pre-Recovery Observation, parent identity, refreshed inventory, Container continuity, dispatch history, and `RecoveryResult.Verified` SHALL NOT independently validate the branch effect.

#### Scenario: Recovery verification is not effect verification
- **WHEN** Recovery verifies the expected parent but no matched criterion evaluates fresh evidence to true
- **THEN** A does not contribute to current completion merely because Recovery succeeded

#### Scenario: Pre-Recovery evidence is stale for effect validation
- **WHEN** A's criterion could evaluate historical evidence positively but fresh post-Recovery evidence is absent
- **THEN** the historical result cannot validate A in the recovered world

### Requirement: Reconcile positive contradicted and unresolved outcomes honestly
When the matched criterion evaluates fresh post-Recovery evidence to true, Agent MAY treat A's historical completion as revalidated for the current reconciliation and continue independently unresolved sibling B without redispatching A. When it returns false, A SHALL NOT contribute to current subtree or Goal evaluation, while historical provenance remains observable and no repair, redispatch, or success is fabricated. When it returns null, or the carrier is absent or cannot be matched, A SHALL remain unresolved, contribute nothing, and SHALL NOT be blindly redispatched. Only independently satisfied GoalEvidence consumed by Agent MAY complete the Run.

#### Scenario: Positive evidence permits honest resume
- **WHEN** completed discovered branch A matches the carrier and its criterion evaluates fresh recovered-world evidence to true
- **THEN** A may contribute, A has zero duplicate dispatches, and Agent may continue required sibling B

#### Scenario: Contradicted evidence invalidates current contribution
- **WHEN** A's matched criterion evaluates fresh recovered-world evidence to false
- **THEN** historical A provenance remains observable but A contributes nothing and no completion or repair is fabricated

#### Scenario: Unobservable evidence remains unresolved
- **WHEN** A's matched criterion evaluates fresh recovered-world evidence to null
- **THEN** A contributes nothing, is not blindly redispatched, and Agent records an explicit existing non-completion/escalation outcome

### Requirement: Do not persist effect-evaluation lifecycle state
The Runtime SHALL consume the nullable evaluation result without adding or persisting a validity field, lifecycle field, Recovery field, completion-status field, status enum, freshness epoch, criterion registry, or new mutable dictionary. `BranchProgressEvidence` SHALL remain historical inventory/completion provenance only, `BranchInventoryEvidence` SHALL remain required-work membership only, and `GoalEvidence` SHALL remain whole-Goal completion evidence only. Existing Trace, journal, and immutable progress snapshots MAY preserve causal evidence provenance but SHALL NOT become effect-validity stores.

#### Scenario: Replay derives rather than reloads validity
- **WHEN** the same recovered-world inputs replay
- **THEN** effect validity is derived again from the same carrier and fresh Observation rather than loaded from persisted lifecycle state

### Requirement: Preserve ownership authority and Recovery boundaries
Agent SHALL remain the sole authority for criterion association and interpretation, retain/invalidate/unresolved, resume/escalation, cross-Container progress, GoalEvidence, and final RunState. Recovery SHALL remain restore, observe, and verify mechanics only and SHALL NOT own branch progress or imply branch-effect validity. Container and Traversal SHALL receive no new cross-scope ownership, authority, or Recovery dependency.

#### Scenario: Lower scopes cannot decide recovered branch validity
- **WHEN** Recovery, Container, or Traversal produces successful local evidence
- **THEN** none of those results independently retain, invalidate, resolve, resume, or complete the discovered branch

### Requirement: Replay discovered-branch effect reconciliation deterministically
The Runtime SHALL replay SC-P3-CAND-009 deterministically when RunId, Goal/carrier, initial Plan, inventory/progress evidence, candidate authorization, disturbance schedule, Recovery results, fresh Observations, and Environment transitions are equal.

#### Scenario: Equal inputs replay equal three-way outcomes
- **WHEN** positive, contradicted, unresolved, absent-carrier, or identity-mismatch branches run twice with equal inputs
- **THEN** criterion outcomes, contributing progress, duplicate-dispatch count, ActionHistory, journal, Trace, GoalEvidence, and final RunState are equal

### Requirement: Preserve the approved bounded production and architecture budget
SC-P3-CAND-009 SHALL add exactly one immutable production type and exactly three production fields total: two immutable fields on `BranchEffectCriterion` and one optional immutable Goal field. It SHALL add no enum, interface, component/service, mutable-state field, mutable-state owner, graph/tree/stack/frontier, route registry, persistent route/depth state, DynamicPlan, DynamicPlanner, BranchManager, ProgressManager, workflow engine, Recovery FSM, generalized multi-parent routing, generalized branch lifecycle, global semantic identity authority, generic predicate framework, Recovery during unfinished dynamic-discovery continuation, Runtime refactor, Capstone execution, roadmap-readiness claim, Phase completion, or S0 graduation. Ownership and authority SHALL remain unchanged.

#### Scenario: Minimum singular carrier is sufficient
- **WHEN** all SC-P3-CAND-009 branches pass
- **THEN** the capability remains within the approved one-type/three-field budget and every deferred abstraction remains absent
