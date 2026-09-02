## Purpose

Define fresh visible Slices, current-Container lifecycle LocalModel accumulation, safe correlation evidence, fresh action grounding, and evidence-based coverage completeness that remains separate from semantic and subtree completion.

## ADDED Requirements

### Requirement: CurrentSlice is a fresh visible window
CurrentSlice SHALL reference the fresh accepted observation window used for current action grounding. Slice geometry, ordinal, text, StableKey, and alignment SHALL be evidence only and SHALL NOT establish cross-observation or cross-run item identity.

#### Scenario: Current action uses fresh occurrence
- **WHEN** an action targets an item visible in the current accepted observation
- **THEN** dispatch grounding SHALL use the current Slice occurrence and its fresh bounds rather than historical LocalModel bounds

#### Scenario: Historical bounds are stale
- **WHEN** a LocalModel item has bounds from an earlier Slice but no corresponding fresh current occurrence
- **THEN** those bounds SHALL NOT authorize dispatch

### Requirement: LocalModel is node-lifecycle accepted knowledge
Each working node SHALL own or reference one current-lifecycle LocalModel derived from accepted Slices for that node. The LocalModel SHALL support inventory, traversal, semantic context, and completeness evidence but SHALL NOT create cross-run stable item identity.

#### Scenario: Adjacent slices accumulate inventory
- **WHEN** accepted adjacent Slices expose A-B-C-D and then C-D-E-F with supported same-Container continuity
- **THEN** the LocalModel SHALL retain evidence for A through F without treating any single correlation feature as identity truth

#### Scenario: Same label in another Container
- **WHEN** a different Container exposes an item with the same text, ordinal, bounds, or StableKey
- **THEN** its evidence SHALL remain scoped to that Container lifecycle and SHALL NOT merge solely on those properties

### Requirement: Slice correlation uses combined bounded evidence
Slice merging MAY use semantic content, relative order, bounds, spacing, item type, action context, and adjacent-frontier evidence. No individual feature SHALL be sufficient item identity, and Scroll SHALL be only a strong same-Container prior rather than truth.

#### Scenario: No-overlap scroll preserves unresolved gap
- **WHEN** a Scroll is followed by a fresh Slice with no direct overlap and no hard different-Container conflict
- **THEN** the Runtime SHALL be able to represent known region plus coverage gap plus known region without forcing a new Container or pretending the gap is resolved

#### Scenario: Scroll conflicts with fresh destination
- **WHEN** a Scroll prior expects same Container but fresh evidence establishes an independent destination
- **THEN** the fresh destination SHALL win and the Scroll expectation SHALL remain prior evidence only

### Requirement: Coverage completion requires frontier exhaustion evidence
Container coverage SHALL become complete only when relevant traversal frontiers are proven exhausted by settled action evidence, a fresh accepted observation, supported same-Container continuity, frontier overlap or bounded coverage reconciliation, no new inventory beyond the frontier, and bounded stability confirmation. `NewItems == 0` alone SHALL NOT prove coverage completion.

#### Scenario: One empty delta does not complete coverage
- **WHEN** one settled Slice yields no new items but frontier continuity or stability is unproven
- **THEN** coverage SHALL remain incomplete or unresolved

#### Scenario: Bounded frontier exhaustion completes coverage
- **WHEN** all required frontier conditions are proven from fresh accepted evidence across the bounded stability window
- **THEN** coverage MAY be marked complete with explicit evidence references

### Requirement: Coverage, semantic resolution, subtree, and Goal completion remain distinct
Coverage completion SHALL NOT imply that every item is semantically resolved, that child subtrees are complete, or that GoalEvidence is satisfied. Unknown items MAY remain in a coverage-complete Container and SHALL be exposed as a separate semantic obligation.

#### Scenario: Coverage complete with Unknown item
- **WHEN** all traversal frontiers are proven exhausted but one accepted item remains semantically Unknown
- **THEN** coverage SHALL be complete, semantic resolution SHALL remain incomplete, and subtree/Goal completion SHALL require their own evidence
