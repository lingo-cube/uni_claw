# u2-open-world-settings-traversal Specification

## Purpose
TBD - created by archiving change u2-open-world-settings-traversal. Update Purpose after archive.

## Requirements

### Requirement: Resolved open-world envelope has a bounded execution entry
The Runtime SHALL expose a deterministic upstream execution entry that accepts
only a resolved `OPEN_WORLD_TYPE_LEVEL` envelope, preserves the supplied Goal
and `TypeLevelTraversalSpecification`, and forwards their validated structured
boundary to Agent without constructing a concrete Plan or route. The entry MUST
remain outside `Agent.RunAsync` and MUST NOT parse intent, observe the world,
invent defaults or authority, select targets, or decide completion.

#### Scenario: Valid open-world projection enters the bounded Agent path
- **WHEN** a resolved envelope contains a navigation-only exhaustive Settings specification and a traversal-shaped Goal
- **THEN** the execution entry forwards the supplied structured values to the bounded Agent path without manufacturing a concrete Plan, page list, target list, coordinate, route, or work inventory

#### Scenario: Closed-world representation is rejected by the open-world entry
- **WHEN** the resolved envelope contains `CLOSED_WORLD_CONCRETE`
- **THEN** the open-world execution entry rejects it before Startup, Observation, or action dispatch

### Requirement: Agent performs bounded fresh-evidence branch traversal
For the open-world entry, Agent SHALL derive the current complete required
branch inventory from accepted fresh Container evidence, validate every source
Observation sequence, and select at most one pending required branch after
positive existing candidate authorization. Container and Traversal SHALL retain
local selection, dispatch, fresh post-action Observation, and first local
verification ownership. Agent MUST NOT pre-enumerate future pages, targets,
inventory, or route.

#### Scenario: Two siblings are discovered and executed without a concrete route
- **WHEN** fresh root evidence proves the complete in-scope inventory `{A, B}` and neither branch appears in a pre-execution Plan
- **THEN** Agent executes A and B one at a time through existing local mechanics and obtains fresh evidence after every action

#### Scenario: Required inventory is unresolved
- **WHEN** current accepted evidence does not prove a complete in-scope inventory
- **THEN** Agent dispatches no discovered branch and records explicit unresolved non-completion evidence

#### Scenario: Required branch is unsafe or unauthorized
- **WHEN** a required current branch lacks positive existing authorization
- **THEN** Agent dispatches no action for that branch and does not fabricate completion

### Requirement: Parent return and sibling continuation require exact proof
Agent MUST require exact parent-return proof after a child subtree is terminal
within the declared bounded scope. Agent SHALL permit a return only when the current fresh child Observation contains
exactly one target matching the expected parent semantic identity and existing
authorization is positive. Traversal SHALL execute and freshly verify the local
step. Agent SHALL record child completion only after the post-action Observation
reconciles exactly to the expected parent and is accepted by that parent
Container.

#### Scenario: Verified return preserves A while B remains pending
- **WHEN** A has bounded terminal evidence and a unique authorized parent target returns to fresh root evidence containing pending B
- **THEN** Agent records A exactly once, preserves it while B remains pending, and continues to B without completing early

#### Scenario: Parent return is ambiguous or rejected
- **WHEN** the current child evidence contains zero or multiple matching parent targets, or authorization is not positive
- **THEN** Agent dispatches no return Tap, records no child completion, performs no blind redispatch, and does not complete

#### Scenario: Post-return evidence identifies the wrong parent
- **WHEN** the return Tap is dispatched but fresh evidence does not reconcile to the expected parent
- **THEN** Agent records no child completion, performs no blind redispatch, and fails with explicit continuity evidence

### Requirement: Traversal-shaped completion remains evidence-gated
Agent MUST keep traversal-shaped completion evidence-gated. Agent SHALL invoke
the existing `Goal.EvidenceEvaluator` on the current fresh root Observation only after it has derived
verified bounded traversal completion: every runtime-discovered in-scope node
was visited as required, every explored branch has a verified terminal state,
all parent returns and sibling continuations are complete, no unresolved
in-scope work remains, the run-local parent frames are empty, and declared
scope/depth/safety boundaries were respected. Only the conjunction of that
Agent-derived condition and satisfied existing fresh `GoalEvidence` SHALL
complete the Run. Agent MUST NOT add a second public progress-aware Goal
evaluator or expose partial branch progress through a hidden closure.

#### Scenario: Complete bounded traversal satisfies its explicit Goal
- **WHEN** A and B both have verified terminal evidence, both parent returns are verified, root inventory has no pending in-scope work, and the existing evaluator returns satisfied GoalEvidence from the fresh root Observation
- **THEN** Agent completes the traversal-shaped Goal

#### Scenario: One sibling remains pending
- **WHEN** A is complete but B remains unresolved or unauthorized
- **THEN** Agent does not invoke final Goal evaluation and does not complete

#### Scenario: Existing fresh GoalEvidence remains unsatisfied
- **WHEN** Agent derives verified bounded traversal completion but the existing evaluator returns unsatisfied GoalEvidence from the fresh root Observation
- **THEN** Agent fails explicitly and does not treat traversal mechanics as success

### Requirement: Cutoffs and failures are not exhaustion
The Runtime MUST preserve the distinctions `intermediate progress !=
completion`, `visited known nodes != all in-scope nodes visited`, `local branch
exhaustion != global traversal completion`, `observation failure !=
exhaustion`, and `ambiguity != exhaustion`. Depth or safety cutoff MAY exclude
work outside the declared bounded scope, but MUST NOT be recorded as
discovered-world or whole-world exhaustion.

#### Scenario: Visible deeper candidate is beyond the maximum depth
- **WHEN** a child at the maximum declared depth exposes a deeper navigable candidate
- **THEN** the candidate receives zero dispatch, bounded completion may continue for the declared scope, and Trace does not claim discovered-world exhaustion

#### Scenario: Dangerous visible candidate is outside the navigation-only scope
- **WHEN** fresh evidence exposes a state-changing candidate alongside required safe navigation branches
- **THEN** the dangerous candidate receives zero dispatch and does not prevent honest completion of the declared navigation-only scope

#### Scenario: Fresh observation protocol fails
- **WHEN** Traversal cannot obtain strictly fresh post-action evidence
- **THEN** Agent does not classify the branch as terminal or exhausted and does not invoke final Goal evaluation

### Requirement: Open-world traversal replays deterministically and preserves regressions
The open-world traversal MUST replay deterministically and preserve existing regressions. Equal run identifiers, structured envelopes, Goal criteria, external-world
inputs, and action outcomes SHALL produce equal actions, Observations, journal,
Trace, branch progress, GoalEvidence, and final RunState. Existing
`Agent.RunAsync(Goal, Plan, ...)`, closed-world CP-14 projection, non-traversal
Goal completion, and frozen Phase 1–3/CP12 behavior SHALL remain unchanged.

#### Scenario: Equal positive inputs replay equally
- **WHEN** the complete two-sibling Scenario runs twice with equal inputs
- **THEN** actions, Observations, journal, Trace, progress, GoalEvidence, and final state are equal

#### Scenario: Existing closed-world execution remains unchanged
- **WHEN** a caller invokes the existing concrete Plan path
- **THEN** all existing execution and completion semantics remain unchanged
