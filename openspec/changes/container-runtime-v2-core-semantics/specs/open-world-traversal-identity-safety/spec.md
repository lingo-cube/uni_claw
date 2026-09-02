## MODIFIED Requirements

### Requirement: Agent-owned run-local traversal identity evidence
The Runtime SHALL maintain Agent-owned, run-local traversal obligation and loop-prevention evidence during RunOpenWorldAsync. It SHALL distinguish current execution-path membership, visited transition/relation occurrences, and semantic identity assessments. This evidence SHALL be discarded when the run ends and SHALL NOT become ContainerGraph current-world truth, a persistent route model, or a new action/completion authority.

#### Scenario: OWI-3 unique page traversal completes
- **WHEN** an open-world traversal satisfies each required relation/obligation and all existing branch/return/GoalEvidence requirements
- **THEN** the run may complete through the existing evidence-gated completion path, with relation occurrences recorded in Agent-owned Run-local evidence

#### Scenario: Closed-world PlanRun remains unchanged
- **WHEN** a closed-world PlanRun executes without buying the V2 open-world seam
- **THEN** no V2 open-world traversal obligation evidence is introduced and existing PlanRun behavior is unchanged

### Requirement: Cycle rejection is an Agent obligation policy and does not deny node existence
Before dispatching a child obligation that would repeat the active execution-path obligation without a separately authorized loop/re-entry contract, the Agent SHALL fail closed or skip according to existing obligation policy. The loop check SHALL NOT establish canonical parent, erase an accepted transition occurrence, deny that a working Graph node exists, or make semantic identity equality alone a world-truth claim.

#### Scenario: OWI-1 A to B to A obligation cycle rejected
- **WHEN** traversal on B proposes an unauthorized obligation back to active-path A
- **THEN** the Agent SHALL dispatch no child action and SHALL record explicit loop-prevention evidence while preserving any already accepted node/occurrence evidence

### Requirement: Same semantic node through different relations is legal
If the same Destination node is reached through a different Source or independently grounded trigger relation, the Runtime SHALL preserve the distinct relation and EntryContext. Semantic identity equality alone SHALL NOT reject the second occurrence, create a duplicate trusted node, or claim duplicate work complete. Agent obligation policy MAY avoid redundant traversal only with explicit relation/goal evidence.

#### Scenario: OWI-2 same Settings node from Desktop and Search
- **WHEN** Desktop and Search independently enter the same Settings destination through different grounded triggers
- **THEN** the Graph SHALL retain one reconciled Destination node with two relations and the Agent SHALL NOT fail solely because the semantic page identity was already visited

#### Scenario: Ambiguous duplicate identity remains fail closed
- **WHEN** two working nodes have an equal semantic identity candidate but insufficient evidence to bind them as one node or distinguish their obligations
- **THEN** the Runtime SHALL preserve both evidence records as unresolved and SHALL NOT silently merge them, authorize action, or claim duplicate work complete
