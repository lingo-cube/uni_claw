# sibling-branch-progress Specification

## Purpose
TBD - created by archiving change phase3-sibling-branch-progress. Update Purpose after archive.

## Requirements

### Requirement: Represent bounded cross-Container progress evidence
The Runtime SHALL represent progress for one bounded semantic parent scope as exactly one immutable production model value containing parent semantic identity, complete approved sibling-inventory evidence, and proven sibling-completion evidence. Agent SHALL own exactly one production state field containing immutable progress snapshots. Completed sibling identities SHALL be a subset of the approved sibling inventory.

#### Scenario: One value distinguishes inventory from completion
- **WHEN** fresh evidence for parent P identifies approved siblings A and B and only A has valid completion evidence
- **THEN** the progress value records A and B in the approved inventory, records only A as proven complete, and represents P/subtree as incomplete without a separate completion enum or boolean field

### Requirement: Establish sibling inventory from fresh parent evidence
Agent SHALL accept the bounded approved sibling inventory only from a fresh Observation reconciled to the correct parent semantic identity and the Scenario-approved traversal boundary. A Plan entry, stale Observation, dispatch result, or historical visit SHALL NOT by itself prove that a sibling exists or that the bounded inventory is complete.

#### Scenario: Fresh P evidence establishes A and B
- **WHEN** a fresh parent Observation reconciles to P and exposes the complete approved bounded sibling affordances A and B
- **THEN** Agent records evidence-backed inventory for A and B under P

#### Scenario: Stale or conflicting parent evidence is rejected
- **WHEN** inventory evidence is absent, stale, or reconciles to a semantic parent other than P
- **THEN** Agent does not establish or replace P's approved sibling inventory from that evidence

### Requirement: Record child completion only from child-local proof
Agent SHALL record a child branch complete only when the active child Container has valid local-completion evidence before the approved parent-return step and the subsequent fresh Observation reconciles to the correct parent scope. Parent return, action dispatch, a new Observation, or a page visit SHALL NOT independently prove child completion.

#### Scenario: A completes before returning to P
- **WHEN** child A has valid Container-local completion evidence before the approved return action and fresh post-return evidence reconciles to P
- **THEN** Agent records A complete under P while preserving B as incomplete

#### Scenario: Early return does not complete A
- **WHEN** Runtime returns from A before A has valid local-completion evidence
- **THEN** Agent does not record A complete merely because P is observed again

### Requirement: Preserve progress across sibling navigation without duplication
Valid completion evidence for A SHALL remain present when Runtime returns to P and traverses B. Re-observing P or revisiting A SHALL NOT fabricate a new sibling identity, duplicate completion, or count the revisit as additional distinct progress.

#### Scenario: A remains complete while B is visited
- **WHEN** A is proven complete, Runtime returns to P, and then enters B
- **THEN** A's completion evidence remains associated with P and B remains separately pending until its own proof exists

#### Scenario: Revisiting A is idempotent
- **WHEN** Runtime revisits A after A already has valid completion evidence
- **THEN** the approved sibling count and number of distinctly completed siblings do not increase merely because another Observation exists

### Requirement: Derive honest bounded subtree completion
The Runtime SHALL treat a bounded parent/subtree as complete only when fresh approved-inventory evidence exists and every approved sibling has valid completion evidence. Local child completion SHALL NOT directly produce final Agent Goal completion, and only Agent evaluation of GoalEvidence SHALL set `RunState.Completed`.

#### Scenario: One unvisited sibling forbids parent completion
- **WHEN** A is proven complete and approved sibling B has no completion evidence
- **THEN** P/subtree remains incomplete and the Runtime does not fabricate Goal completion

#### Scenario: All approved siblings support bounded completion
- **WHEN** fresh P inventory evidence identifies only approved siblings A and B and both have valid completion evidence under P
- **THEN** higher-level evidence may treat the bounded P subtree as complete while final Run completion remains Agent/GoalEvidence controlled

### Requirement: Reject cross-scope identity conflicts
Progress evidence SHALL remain associated with its reconciled parent semantic identity. Evidence from another parent, an Unknown page, or a conflicting child identity SHALL NOT be silently attached to P or erase P's valid prior evidence.

#### Scenario: Wrong-parent evidence cannot mutate P progress
- **WHEN** fresh evidence resolves to a different parent or conflicts with the active child/parent relationship
- **THEN** P's existing progress remains unchanged and the conflicting evidence proves no new branch completion

### Requirement: Keep parent return as existing execution mechanics
SC-P3-CAND-004 SHALL use existing approved visible affordances and existing action semantics for child-to-parent return. It SHALL NOT require a new Back action, navigation graph/tree/stack, Container hierarchy, manager, FSM, or generic workflow engine.

#### Scenario: Existing Tap returns to P
- **WHEN** the deterministic child page exposes an approved parent-return affordance
- **THEN** Traversal may use the existing Tap action and normal Execute → Observe → Verify protocol to return without purchasing a new navigation semantic

### Requirement: Preserve the approved production and architecture budget
SC-P3-CAND-004 SHALL add exactly one immutable production model type, three immutable fields on that value, and one Agent-owned state field. It SHALL add no enum, interface, component, or mutable-state owner and SHALL preserve Agent, Container, Traversal, Environment, Recovery, GoalEvidence, and RunState ownership and authority boundaries.

#### Scenario: Minimum purchase proves both branches
- **WHEN** deterministic positive and negative branches execute
- **THEN** the approved value plus existing Observation, WorldBelief, Container local completion, Traversal journal, GoalEvidence, Trace, and RunState surfaces prove the Scenario without deferred production capabilities

### Requirement: Replay sibling progress deterministically
The Runtime SHALL produce deterministic SC-P3-CAND-004 evidence when RunId, bounded world input, approved Plan, and action sequence are equal.

#### Scenario: Equal branch inputs replay equally
- **WHEN** the same SC-P3-CAND-004 input is executed twice with equal RunId, parent/child world transitions, approved actions, and Observation sequence
- **THEN** progress snapshots, ActionHistory, Observations, journal, Trace, GoalEvidence, and final RunState are equal

### Requirement: Preserve deferred boundaries
SC-P3-CAND-004 SHALL NOT decide post-Recovery progress validity or introduce autonomous discovered-candidate safety, Capstone implementation, graph/stack/tree/hierarchy models, visited-set semantic types, TraversalContext, ResumeToken, managers, FSM, new Recovery semantics, or Runtime refactoring.

#### Scenario: Deferred pressure remains outside the capability
- **WHEN** the bounded sibling Scenario completes
- **THEN** no result claims recovery-progress resume, autonomous safety, SC-S0-CAPSTONE-001 completion, or a generalized navigation framework
