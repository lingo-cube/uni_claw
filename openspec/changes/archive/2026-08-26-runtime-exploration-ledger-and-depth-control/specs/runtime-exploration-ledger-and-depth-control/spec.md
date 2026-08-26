## Purpose

Defines a read-only, evidence-derived Exploration Ledger projection and bounded semantic depth control for accepted Strategy Runs, without transferring planning, action, completion, or lifecycle authority to RuntimeAgent and without creating a new state system or scenario knowledge.

## ADDED Requirements

### Requirement: Evidence-derived exploration ledger projection

The system SHALL provide an immutable `ExplorationLedgerView` per accepted Strategy Run, compiled deterministically on demand from existing evidence records (branch-progress evidence, revisit-coverage records, structural-progress facts, and observation sequence correlations). The ledger MUST NOT be a mutable state system, MUST NOT own evidence, and MUST NOT mutate any source evidence record.

#### Scenario: Ledger reflects unified node accounting

- **WHEN** a Strategy Run has branch-progress and coverage evidence recording discovered and processed nodes
- **THEN** the ledger reports per-scope discovered, visited, pending, unresolved, and unknown-frontier counts consistent with those records

#### Scenario: Same evidence yields the same ledger

- **WHEN** the ledger is compiled twice from identical evidence records
- **THEN** both compilations produce identical ledgers with an identical digest

### Requirement: Closed exploration rule vocabulary

The system SHALL interpret the accepted strategy's exploration intent into a closed vocabulary of per-node rules — `ExpandContainer` and `RecordOnly` — at strategy admission. RuntimeAgent MUST apply these rules during node classification using existing generic semantic capability output and MUST NOT author, invent, or specialize rules. A node whose classification is unavailable MUST be recorded as unresolved in the ledger rather than guessed.

#### Scenario: Container rule is applied

- **WHEN** a discovered node is classified as a semantic container and the accepted strategy declares container expansion
- **THEN** the node is processed under the `ExpandContainer` rule

#### Scenario: Unclassifiable node fails closed

- **WHEN** a discovered node cannot be classified by available generic semantic capability output
- **THEN** the node is recorded as unresolved in the ledger and no rule is inferred for it

#### Scenario: No scenario-specific classification

- **WHEN** any node is classified
- **THEN** classification uses only generic semantic capability contracts and no scenario label, fixed page path, UI text, or coordinate

### Requirement: Visited means rule-satisfied, not clicked

The system SHALL record a node as `Visited` only when the applied exploration rule is satisfied with evidence: `RecordOnly` nodes by fresh-observation record, and `ExpandContainer` nodes by verified subtree-return or verified boundary disposition. A dispatch or click event alone MUST NOT mark a node visited.

#### Scenario: Click does not imply visited

- **WHEN** a node was dispatched but its rule-satisfaction evidence is absent
- **THEN** the ledger does not count the node as visited

#### Scenario: Record-only node visited by observation

- **WHEN** a `RecordOnly` node is recorded in a fresh accepted observation
- **THEN** the node counts as visited without any dispatch

### Requirement: Bounded semantic depth control

The system SHALL map the accepted strategy's declared maximum depth to bounded exploration semantics at admission: depth `0` explores the root record-only, depth `1` additionally records direct children, and depth `N ≥ 2` bounds recursive expansion to `N` with nodes at the boundary processed record-only when the strategy declares bounded-record semantics. Exhaustive-strategy semantics MUST preserve the existing fail-closed depth cutoff. Depth MUST NOT mutate mid-Run, and no dynamic depth adjustment is introduced.

#### Scenario: Bounded-record boundary is recorded, not failed

- **WHEN** a bounded-record strategy at depth `N` discovers a container whose expansion would exceed `N`
- **THEN** the container is processed record-only and the ledger records the unknown frontier beyond the declared depth

#### Scenario: Exhaustive cutoff still fails closed

- **WHEN** an exhaustive strategy's in-scope inventory requires traversal beyond the declared depth
- **THEN** the Run fails closed with the existing bounded-cutoff failure and no record-only conversion occurs

#### Scenario: Depth is immutable for the Run

- **WHEN** a Run is active with a declared maximum depth
- **THEN** no component can change the declared depth for that Run

### Requirement: Ledger is evidence input, never completion authority

The system SHALL expose the ledger as an Agent-readable evidence projection on existing snapshot or evidence surfaces. The ledger MUST NOT assert Run completion, MUST NOT create or mutate GoalEvidence, and MUST NOT trigger any FSM transition. Terminal completion remains exclusively Agent-owned GoalEvidence verified through FSM authorization.

#### Scenario: Satisfied ledger does not complete a Run

- **WHEN** the ledger reports zero pending and zero unknown-frontier entries
- **THEN** the Run's terminal state is unchanged and completion still requires Agent-owned GoalEvidence and FSM authorization

### Requirement: Ledger neutrality and authority guards

The system SHALL include mechanical guards proving ledger and classification components contain no DeviceAction generation, no concrete target selection, no route definition, no FSM command, no RunState mutation, no recovery decision, no completion assertion, and no scenario-specific knowledge, and that the ledger derives exclusively from existing evidence record types.

#### Scenario: Ledger types carry no authority

- **WHEN** ledger and rule-vocabulary types are inspected by reflection guard
- **THEN** they expose no authorize, complete, transition, dispatch, or action members and reference no mutable World or DFS internals

#### Scenario: Scenario-neutral classification source

- **WHEN** the classification and ledger source files are scanned
- **THEN** they contain no scenario labels, selectors, routes, or fixed page paths
