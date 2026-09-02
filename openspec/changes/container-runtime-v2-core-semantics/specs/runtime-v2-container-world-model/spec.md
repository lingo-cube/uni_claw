## Purpose

Define a bounded evidence-backed Container world model that records observed working locations and trigger-bearing relations without becoming a planner, action authority, current-world truth, or persistent topology.

## ADDED Requirements

### Requirement: ContainerGraph is an evidence world model only
The Runtime SHALL represent observed Container nodes and relations in a Run-local ContainerGraph. The Graph SHALL contain evidence and derived assessments only; it SHALL NOT select routes, authorize actions, declare current location, complete goals, own recovery, or treat historical structure as current world truth.

#### Scenario: Known relation cannot authorize action
- **WHEN** the Graph contains a previously observed relation from Source to Destination
- **THEN** the Runtime SHALL require the existing fresh grounding and Agent action-authorization path before dispatch and SHALL NOT dispatch from the relation alone

#### Scenario: Historical relation conflicts with fresh world
- **WHEN** a historical relation predicts Destination A but fresh accepted evidence establishes a different working Destination B
- **THEN** CurrentContainer and the transition occurrence SHALL reflect B while the historical relation remains non-authoritative evidence

### Requirement: Working nodes may exist before semantic identity is proven
After a correlated fresh accepted observation establishes an independent working Container, the Graph SHALL allow a node with a stable Run-local NodeRef and `INITIALIZED` derived trust even when semantic identity is unresolved. Node existence SHALL NOT imply proven identity, completeness, action authorization, or memory publication.

#### Scenario: Unresolved child first frame creates working node
- **WHEN** a may-enter action is followed by fresh accepted evidence of an independent destination whose semantic identity is unresolved
- **THEN** the Runtime SHALL create or retain an `INITIALIZED` working node and SHALL NOT fabricate a semantic identity or block transition completion solely on missing identity trust

#### Scenario: Working node folds into source
- **WHEN** later resolution for the same evidence revision proves the observation remained in the source Container
- **THEN** the working node evidence SHALL be reconciled into the source without leaving a second current-location truth

### Requirement: Relations are first-class and trigger-bearing
A relation SHALL identify its Source node, Destination node, trigger occurrence or entry-affordance evidence, and append-only supporting evidence. Same Destination SHALL NOT by itself mean same relation, and trigger display text SHALL NOT be a permanent relation identity.

#### Scenario: Same destination through two entries
- **WHEN** Desktop reaches Settings through a Settings icon and Search reaches the same Settings node through a search result
- **THEN** the Graph SHALL represent two independent relations with the same Destination and distinct Source/trigger evidence

#### Scenario: Same text does not merge relations
- **WHEN** two trigger occurrences have equal text but different Source or independently grounded affordance evidence
- **THEN** the Graph SHALL NOT merge them solely because their text matches

### Requirement: Relation confidence is derived from occurrence evidence
Relations SHALL retain append-only occurrence evidence and SHALL expose any observed/support/challenge/prior-eligibility state as a derived assessment. The Runtime SHALL NOT require or maintain a relation maturity FSM.

#### Scenario: Repeated occurrences support one relation
- **WHEN** multiple accepted transition occurrences from the same Source/trigger relation reach the same Destination
- **THEN** the occurrences MAY support one relation while each occurrence remains independently readable

#### Scenario: Challenged node does not rewrite history
- **WHEN** later assessment challenges a node identity or relation interpretation
- **THEN** original node/relation evidence SHALL remain recorded and the current assessment SHALL be derived without recursively rewriting historical occurrences

### Requirement: Abnormal occurrences do not automatically become normal relations
Every accepted off-path, transient, external, or unexpected transition MAY be recorded as occurrence evidence, but the Graph SHALL NOT promote it to a normal reusable relation without an explicit evidence assessment that remains non-authoritative.

#### Scenario: Off-path launcher observation
- **WHEN** a trigger expected to enter Wallpaper is followed by an accepted Launcher destination classified off-path
- **THEN** the occurrence SHALL remain available for diagnosis and SHALL NOT become a normal Wallpaper relation or action prior
