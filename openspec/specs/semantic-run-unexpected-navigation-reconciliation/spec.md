# Semantic Run Unexpected Navigation Reconciliation Specification

## Purpose

Define the Runtime Agent behavior for reconciling a fresh, different KNOWN semantic page after a non-Scroll semantic action or Scroll F5 transition, while preserving the same Goal, rejecting unknown pages and same-page contradictions, and preventing stale A grounding from authorizing subsequent actions.

## Requirements

### Requirement: Known-page reconciliation after non-Scroll semantic action

When a non-Scroll semantic action (e.g., SetSwitch) is dispatched and the fresh Observation resolves to a DIFFERENT KNOWN semantic page, SemanticRun SHALL reconcile to the new page instead of returning SemanticContradiction solely because of the page transition.

#### Scenario: SetSwitch causes unexpected page B

- **WHEN** a SetSwitch action is dispatched from Container A and the fresh Observation resolves to known page B
- **THEN** SemanticRun SHALL create/reconcile Container B
- **AND** SHALL preserve the SAME Goal
- **AND** SHALL NOT treat the page transition as SetSwitch success

### Requirement: Stale grounding invalidation

After known-page reconciliation, all grounding from the old Container SHALL be considered stale. New Binding and StateBelief SHALL derive from the fresh Observation on Container B.

#### Scenario: Old Binding not reused

- **WHEN** Container A had a valid Binding but fresh Observation shows page B
- **THEN** the old Binding SHALL NOT authorize any action

### Requirement: Same Goal preservation

Known-page reconciliation SHALL preserve the same SemanticGoalInput, DesiredValue, and Goal authority.

#### Scenario: Goal continues on new page

- **WHEN** Container A is reconciled to known page B after unexpected navigation
- **THEN** the SAME SemanticGoal SHALL continue with the same ObjectIdentity, StateDimension, and DesiredValue

### Requirement: Unknown page fail closed

If fresh Observation page is UNKNOWN, SemanticRun SHALL fail closed and SHALL NOT fabricate a Container.

#### Scenario: Unknown page after action

- **WHEN** fresh Observation cannot resolve to a known page
- **THEN** SemanticRun SHALL return SemanticContradiction (fail closed)

### Requirement: Same-page contradiction unchanged

If fresh Observation resolves to the SAME page but continuity cannot be proven, SemanticRun SHALL continue to return SemanticContradiction.

#### Scenario: Same page continuity failure

- **WHEN** fresh Observation resolves to the same page but TryVerifyLocalContinuity fails
- **THEN** SemanticRun SHALL NOT reconcile to a new Container
- **AND** SHALL return SemanticContradiction

### Requirement: No GoalEvidence from reconciliation

Known-page reconciliation SHALL NOT create GoalEvidence or infer DesiredValue.

#### Scenario: Reconciliation does not complete Goal

- **WHEN** a known-page transition is reconciled
- **THEN** no GoalEvidence SHALL be created
- **AND** the Goal SHALL remain unsatisfied until fresh verification proves DesiredValue
