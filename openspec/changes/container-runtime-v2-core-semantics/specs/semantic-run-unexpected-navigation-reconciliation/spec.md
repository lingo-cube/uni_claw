## MODIFIED Requirements

### Requirement: Known-page reconciliation after non-Scroll semantic action
When a non-Scroll semantic action is dispatched and the fresh accepted Observation establishes a different independent Container, SemanticRun SHALL update CurrentContainer and record the TransitionOccurrence instead of returning SemanticContradiction solely because of the boundary. If semantic identity is known it SHALL bind or reconcile the working node; the SAME Goal remains pending unless separately proven.

#### Scenario: SetSwitch causes unexpected page B
- **WHEN** a SetSwitch action is dispatched from Container A and the fresh accepted Observation establishes known page B
- **THEN** SemanticRun SHALL reconcile CurrentContainer to B, preserve the SAME Goal, record the occurrence, invalidate stale A grounding, and SHALL NOT treat the transition as SetSwitch success

### Requirement: Stale grounding invalidation
After an independent destination becomes CurrentContainer, all action grounding from the prior CurrentSlice SHALL be stale. New Binding and StateBelief SHALL derive from the fresh CurrentSlice, and historical LocalModel bounds SHALL NOT authorize action.

#### Scenario: Old Binding not reused
- **WHEN** Container A had a valid Binding but fresh accepted evidence establishes working Container B
- **THEN** the old Binding and historical bounds SHALL NOT authorize any action

### Requirement: Unknown page fails semantic authority closed while preserving physical occurrence
If fresh accepted evidence establishes an independent destination whose semantic identity is Unknown, SemanticRun SHALL create or retain an `INITIALIZED` working CurrentContainer and record the occurrence. It SHALL NOT fabricate a trusted identity, satisfy the Goal, authorize an action from the Unknown evidence, or leave current physical location on the prior Container.

#### Scenario: Unknown page after action
- **WHEN** fresh accepted evidence establishes an independent destination but semantic identity cannot be resolved
- **THEN** CurrentContainer SHALL reference an `INITIALIZED` working node, the semantic Goal path SHALL remain fail-closed/pending, and no trusted identity or action authority SHALL be fabricated
