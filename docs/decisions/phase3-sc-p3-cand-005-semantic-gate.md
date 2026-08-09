# SC-P3-CAND-005 Reopened Semantic Gate — Evidence-Validated Progress Resume After Agent Recovery

> Date: 2026-08-08 | Status: APPROVED (REOPENED) | Decision: `SEMANTIC_PURCHASE_REQUIRED`
> Scope: bounded Scenario registration and Semantic Gate only. OpenSpec, implementation tasks, Runtime code, Runtime tests, Harness changes, Capstone implementation, and refactor are not authorized by this decision.

## Candidate

- ID: `SC-P3-CAND-005`
- Title: **Evidence-Validated Progress Resume After Agent Recovery**
- Evidence confidence: `HIGH`
- Dependency: frozen SC-P2-001 world-position Recovery plus frozen SC-P3-CAND-004 Agent-owned sibling/subtree progress.

## Reconciliation Challenge and Superseded Decision

The initial `BEHAVIOR_PURCHASE_ONLY` / zero-delta decision is superseded. Normative reconciliation proved that it could identify when retained evidence was obtained but could not express what durable external proposition made a particular branch complete or evaluate that proposition against fresh recovered-world evidence.

- `Goal.EvidenceEvaluator` evaluates whole-Goal completion; reusing it for a child claim would conflate branch validity with final completion.
- `GoalEvidence.Satisfied == false` does not distinguish a positively contradicted branch effect from evidence that cannot observe the effect.
- `PlanStep` contains only target/action descriptions and carries no branch-effect criterion.
- `BranchProgressEvidence` associates branch claims with historical Observation sequences, but a sequence proves age/order rather than semantic truth.
- `Observation` / `ObservedElement` describe one observation, `WorldBelief` describes page-level belief, and Traversal/Action results describe local protocol or dispatch outcomes. None identifies the durable proposition behind a branch-completion claim.

Fresh parent identity, Container continuity, or `RecoveryResult.Verified` therefore cannot update a retained completion sequence without fabricating branch validity. The missing semantic is a branch-scoped evidence criterion, not a persistent validity state.

## Reality Evidence

- SC-P2-001 proves Agent Recovery can restore and verify a trusted world position, resume from a suspended Plan index, and preserve append-only journal history.
- SC-P3-CAND-004 proves Agent-owned `BranchProgressEvidence` can distinguish approved sibling inventory from proven sibling completion across Containers.
- Current Recovery verifies `RecoveryAnchor.VerificationCriteria`, then rebinds the entry/suspended Container. `Container.Bind` resets page-local progress.
- Agent's immutable branch-progress dictionary survives Recovery in memory, but its stored Observation sequences predate the recovered world and are not revalidated by `RecoveryResult.Verified`.
- The Recovery position-restore loop replays `Plan[0..suspendedIndex)`. In a multi-branch Run that prefix may include already-completed, non-idempotent work.
- Current Recovery resume control flow does not update or revalidate `BranchProgressEvidence`.
- Legacy pause/resume and traversal-context snapshots prove only that state can persist mechanically. They do not prove that retained progress remains valid after external drift and verified world restoration.

Core distinction:

```text
world position recovered
!=
prior progress evidence valid in the recovered world
```

## Existing Semantic Audit

- **Agent** owns Plan, Goal, cross-Container progress, active Container transitions, recovery/resume decisions, GoalEvidence consumption, and final RunState. It is already the correct validity authority.
- **BranchProgressEvidence** stores parent identity, approved sibling-inventory evidence, and completed-sibling evidence by source Observation sequence.
- **Trap** already records the drift Observation boundary through `Trap.Observed`; Agent retains `LastTrap`, owns the Recovery phase, and knows whether later evidence was produced only after `RecoveryResult.Verified`.
- **Recovery** owns restore/observe/verify mechanics only. `RecoveryResult.Verified` proves its injected entry criteria, not durability of earlier branch effects.
- **Container** owns page-local identity and progress. `Bind` intentionally resets `ExecutedSteps` and `IsLocalComplete`; it cannot validate cross-Container evidence.
- **Traversal** owns deterministic local execution and journal history. Historical dispatch or journal presence is not current semantic proof.
- **Environment** supplies external Observations and dispatch outcomes only.
- **Observation / WorldBelief** can provide fresh recovered-world evidence, but neither describes which branch effect that evidence must prove.

Existing evidence primitives are therefore `INSUFFICIENT`. The `Goal.EvidenceEvaluator` injection pattern is structurally reusable, but its whole-Goal, boolean `GoalEvidence` meaning is not admissible at branch scope. SC-P3-CAND-005 requires one smaller branch-scoped criterion with an explicit three-way evaluation contract.

## Replay Risk Classification

Decision: `SAME_SCENARIO_PRESSURE`

The bounded positive branch requires valid A evidence to survive Recovery and execution to continue with B without redispatching A's already-completed action. Prefix replay of that action is the direct unsafe consequence of failing to interpret retained progress validity; it is not a separate generic action-taxonomy, checkpoint, or navigation-planning requirement. This Scenario purchases only the bounded no-blind-replay behavior for work already represented by the same parent-scoped progress evidence.

Navigation steps needed to restore observable position remain Recovery mechanics. No generic classification of idempotence, action taxonomy, checkpoint engine, or Recovery planner is purchased.

## Approved Reality Distinction

For one bounded parent scope after one verified Agent Recovery, Agent must distinguish:

1. stored historical progress evidence that still exists;
2. evidence freshly associated with the current recovered world;
3. fresh evidence that contradicts a previously completed effect;
4. prior evidence whose validity remains unobservable;
5. evidence permitted to contribute to bounded subtree and Goal evaluation.

Correct parent identity, entry verification, or a fresh page Observation alone does not validate a child completion effect.

## Minimum Semantic Purchase

Exactly one immutable semantic field:

`PlanStep.BranchEffectEvidenceEvaluator: Func<Observation, bool?>?`

Meaning:

- on the Scenario-approved branch-entry step, the evaluator represents the observable external proposition whose proof justifies that bounded branch's completion claim;
- `true` means fresh evidence positively proves the proposition still holds;
- `false` means fresh evidence positively proves the proposition does not hold;
- `null` means the fresh Observation cannot determine the proposition;
- absence of an evaluator means the retained branch effect cannot be revalidated and is therefore unresolved;
- evaluation must be deterministic, side-effect-free, and depend only on the supplied Observation; it cannot read or mutate Recovery, Container, Traversal, Agent state, or the external environment;
- the evaluator is a criterion/hypothesis carried by the Agent-owned Plan, never proof by itself. Only evaluation against fresh post-verified-Recovery Observation evidence produces a validity judgement.

The field is associated with the branch-entry `PlanStep` whose target identifies the approved sibling in the one-parent bounded Scenario. `BranchProgressEvidence` continues to retain the historical completion sequence; the Agent-held Plan retains the corresponding durable criterion across Recovery. This avoids duplicating the criterion into progress state or creating a new evidence model.

Why a smaller representation fails:

- an Observation sequence gives temporal provenance but not the proposition to re-evaluate;
- parent/Container identity proves position, not the child effect;
- one boolean cannot distinguish contradiction from unobservability unless the evaluator's result is nullable;
- `GoalEvidence` has whole-Goal meaning and cannot be reinterpreted as branch evidence;
- a string criterion would require unpurchased parsing semantics and could not provide deterministic three-way evaluation.

Approved budget:

- New production model types: 0.
- New production fields: 1 (`PlanStep.BranchEffectEvidenceEvaluator`).
- New enums: 0.
- New interfaces: 0.
- New components: 0.
- New mutable state owners: 0.
- Ownership delta: NONE.
- Authority delta: NONE.

Conceptual outcomes are represented without an enum:

- **Retained:** prior inventory/completion entries remain in the immutable snapshot, but their source sequences are at or before the existing Agent-scope drift boundary and cannot contribute.
- **Revalidated:** after Recovery verification, the branch criterion evaluates fresh evidence to `true`; Agent may refresh the corresponding completion evidence beyond the drift boundary and permit it to contribute.
- **Contradicted:** the criterion evaluates fresh evidence to `false`; Agent excludes/removes that completion from current progress while historical Trace/journal evidence remains historical only.
- **Unresolved:** the criterion is absent or evaluates fresh evidence to `null`; the old completion remains retained historical evidence, contributes nothing, and produces an explicit non-completion/escalation outcome without blind redispatch.

Completion claim and Observation remain distinct:

```text
historical Observation #20
→ branch-local proof records A complete at sequence 20
and A's approved PlanStep retains the external-effect criterion X
→ Recovery crosses the Trap.Observed boundary
→ fresh Observation #42 evaluates X
→ true / false / null is derived for the current world
```

Observation #20 is provenance for the historical claim, criterion X is the durable semantic proposition, and Observation #42 is fresh evidence. None is interchangeable with another.

## Formal Scenario Boundary

### Positive

```text
fresh P inventory proves A/B
→ A is locally proven and recorded complete
→ external Launcher drift
→ SC-P2-001 Agent Recovery restores and verifies position
→ A's branch criterion evaluates fresh recovered-world evidence to true
→ Agent revalidates A against the recovered-world boundary
→ continue with B without redispatching A's completed action
→ B requires its own proof
→ only valid A+B evidence may support bounded subtree completion
→ final completion still requires satisfied GoalEvidence
```

### Negative — contradicted/reset

```text
A recorded complete
→ verified Recovery
→ A's branch criterion evaluates fresh recovered-world evidence to false
→ A cannot contribute to subtree completion
→ no fabricated Goal completion
→ no stale sequence is promoted to current truth
```

### Ambiguous — unobservable

```text
A recorded complete
→ verified Recovery reaches the expected entry/page
→ A's branch criterion is absent or evaluates fresh recovered-world evidence to null
→ retained A evidence remains untrusted and contributes nothing
→ no blind redispatch of A
→ explicit unresolved/escalated outcome through existing Agent/Trace/RunState surfaces
```

Additional negatives:

- a pre-Recovery Observation sequence cannot establish post-Recovery validity;
- correct parent identity alone cannot validate child completion;
- `RecoveryResult.Verified` alone cannot validate branch progress;
- stale retained evidence cannot satisfy derived subtree completion;
- unresolved evidence cannot be counted as complete progress;
- position restoration cannot blindly redispatch completed non-idempotent work represented by valid retained progress.

## Ownership and Authority

- Progress-validity interpretation: Agent.
- Progress state owner: Agent, using immutable `BranchProgressEvidence` snapshots.
- Restoration mechanics and injected entry verification: Recovery.
- Page-local identity/progress: Container.
- Deterministic local execution/journal: Traversal.
- Final completion: Agent consuming satisfied GoalEvidence.

Recovery must not own or mutate branch progress and must not depend on Container or Traversal. No ownership or authority movement is required.

## Architecture Shape

Agent remains under `NON_BLOCKING_STRUCTURAL_PRESSURE`. The bounded Scenario adds temporal interpretation around existing Recovery and progress evidence, but ownership and authority stay unambiguous. No refactor or Architecture Review is purchased.

## Capstone Impact

- Sibling/subtree progress: `COVERED` at S0 by frozen SC-P3-CAND-004.
- World Recovery: `COVERED` at S0 by frozen SC-P2-001/SC-P2-003.
- Recovery Progress Resume: `SEMANTIC_PURCHASE_REQUIRED`; one immutable `PlanStep` criterion field is approved, while OpenSpec, implementation, and S0 proof remain absent.
- Autonomous discovered-candidate safety: `RESEARCH`.
- SC-S0-CAPSTONE-001: remains `CAPSTONE / PREREQUISITES_MAPPED`.

## Explicitly Not Purchased

- `EvidenceState` or `RecoveryState` enum;
- `ResumeToken`, checkpoint, snapshot/progress manager, Recovery planner;
- navigation graph/stack, `TraversalContext`, FSM, workflow engine;
- generic evidence-validity/predicate framework, EffectRegistry, or action-idempotence taxonomy;
- a new recovery epoch, freshness-boundary, persistent validity-state field, or validity enum;
- another criterion field on `BranchProgressEvidence`, Goal, Recovery, Container, or Traversal;
- Recovery ownership of progress or Recovery → Container/Traversal dependencies;
- more than one Recovery cycle;
- Runtime refactor, Harness changes, Capstone implementation, real-device/Vision behavior, or autonomous safety.

## Next Decision

```text
RECONCILE_SPEC
```

OpenSpec reconciliation must preserve the exact one-field semantic budget, nullable three-way branch-effect evaluation contract, bounded one-Recovery Scenario, no-blind-replay requirement, existing ownership/authority, and all explicit deferred boundaries.
