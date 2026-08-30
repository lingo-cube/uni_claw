## MODIFIED Requirements

### Requirement: Agent-owned run-local traversal identity evidence

The Runtime SHALL maintain Agent-owned, run-local identity evidence during RunOpenWorldAsync, consisting of at least:
- a current ancestry membership view derived from `ActiveAncestorPath` plus `ActiveExecutionContainer`, and
- a visited set of semantic page identities accepted during the open-world run.

The Runtime SHALL NOT separately maintain a mutable current-ancestry set after `ActiveContainerContext` consolidation. This evidence SHALL be discarded when the run ends and SHALL NOT be exposed as a global graph, persistent route model, or new state owner.

#### Scenario: OWI-3 unique page traversal completes

- **WHEN** an open-world traversal visits each required semantic page exactly once and all existing branch/return/GoalEvidence requirements are satisfied
- **THEN** the run may complete through the existing evidence-gated completion path, with unique page coverage recorded in Agent-owned run-local identity evidence

#### Scenario: Closed-world PlanRun remains unchanged

- **WHEN** a closed-world PlanRun executes
- **THEN** no open-world visited identity evidence is introduced, the active ancestor path remains empty unless existing recursive execution requires it, and existing PlanRun behavior is unchanged

### Requirement: Cycle rejection before child Container entry

Before creating a child Container from fresh reconciled evidence in RunOpenWorldAsync, if the child semantic page identity already exists in the current ancestry membership view derived from `ActiveAncestorPath` plus `ActiveExecutionContainer`, the Runtime SHALL reject the transition as a cycle and SHALL NOT create the child Container, dispatch another action, or claim progress.

#### Scenario: OWI-1 A → B → A cycle rejected

- **WHEN** the open-world traversal is on page B and fresh reconciled evidence identifies page A as a child while page A is already in the derived current ancestry
- **THEN** the Runtime rejects the child transition as a cycle, creates no Container for the duplicate A, dispatches no child action, and fails closed with explicit cycle evidence
