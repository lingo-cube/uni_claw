# Open-World Traversal Identity Safety Specification

## Purpose

Define run-local semantic page identity safety for RunOpenWorldAsync, preventing ancestry cycles and duplicate page traversal without introducing a global graph, changing Container ownership, or altering GoalEvidence authority.

## Requirements

### Requirement: Agent-owned run-local traversal identity evidence

The Runtime SHALL maintain Agent-owned, run-local identity evidence during RunOpenWorldAsync, consisting of at least:
- a current ancestry set of semantic page identities for the active parent stack, and
- a visited set of semantic page identities accepted during the open-world run.

This evidence SHALL be discarded when the run ends and SHALL NOT be exposed as a global graph, persistent route model, or new state owner.

#### Scenario: OWI-3 unique page traversal completes

- **WHEN** an open-world traversal visits each required semantic page exactly once and all existing branch/return/GoalEvidence requirements are satisfied
- **THEN** the run may complete through the existing evidence-gated completion path, with unique page coverage recorded in Agent-owned run-local identity evidence

#### Scenario: Closed-world PlanRun remains unchanged

- **WHEN** a closed-world PlanRun executes
- **THEN** no open-world identity evidence is introduced and existing PlanRun behavior is unchanged

### Requirement: Cycle rejection before child Container entry

Before creating a child Container from fresh reconciled evidence in RunOpenWorldAsync, if the child semantic page identity already exists in the current ancestry set, the Runtime SHALL reject the transition as a cycle and SHALL NOT create the child Container, dispatch another action, or claim progress.

#### Scenario: OWI-1 A → B → A cycle rejected

- **WHEN** the open-world traversal is on page B and fresh reconciled evidence identifies page A as a child while page A is already in the current ancestry
- **THEN** the Runtime rejects the child transition as a cycle, creates no Container for the duplicate A, dispatches no child action, and fails closed with explicit cycle evidence

### Requirement: Duplicate semantic page identity across branches fails closed by default

If a child semantic page identity is already present in the run-local visited set from a different branch and no explicit merge rule is provided by the caller, the Runtime SHALL fail closed as ambiguous duplicate identity and SHALL NOT silently re-traverse the same semantic page as new work.

#### Scenario: OWI-2 duplicate semantic page identity across branches rejected

- **WHEN** the same semantic page identity is reached through two different branches and no explicit merge rule is supplied
- **THEN** the Runtime rejects the second occurrence as ambiguous duplicate identity, creates no duplicate child Container, and fails closed without claiming duplicate work complete

### Requirement: Parent return after rejected cycle remains valid

If a cycle or duplicate-identity rejection occurs, valid parent-return mechanics SHALL remain available for the current child when the child itself has no unresolved in-scope work. The rejection SHALL NOT invalidate the existing parent-child return evidence path.

#### Scenario: OWI-4 parent return after rejected cycle remains valid

- **WHEN** a child page contains a cyclic or duplicate child candidate that is rejected, and the child otherwise has bounded terminal evidence and a unique authorized parent return
- **THEN** the Runtime may still perform the verified parent return and preserve valid completed sibling evidence for that child

### Requirement: GoalEvidence authority and completion unaffected

The new identity evidence SHALL NOT create GoalEvidence, infer completion, or replace the existing fresh GoalEvidence evaluation. Run completion SHALL remain possible only when existing verified bounded traversal completion and satisfied fresh GoalEvidence both hold.

#### Scenario: OWI-5 Goal completion evidence unaffected

- **WHEN** an open-world traversal with unique page coverage satisfies all traversal requirements
- **THEN** completion is granted only through the existing fresh GoalEvidence path, with SourceObservationSequence from fresh verified observation evidence
