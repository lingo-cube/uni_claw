## Purpose

Defines the deterministic internal interpretation and evidence-integrity behavior required to bind exploration rules, depth boundaries, and ledger accounting to one accepted Strategy Run without changing the frozen Strategy Contract schema or transferring Runtime authority.

## ADDED Requirements

### Requirement: Existing Strategy fields determine one immutable exploration interpretation

For every supported accepted Strategy Run, RuntimeAgent SHALL derive exactly one immutable exploration interpretation during admission from the existing typed objective, exploration intent, completion criterion, and maximum depth. RuntimeAgent MUST NOT choose a mode from observed UI content, invent missing semantics, or add a wire field. The interpretation SHALL obey this closed table:

- depth `0`: accept the root-scope inventory record-only with no child expansion;
- depth `1`: expand root containers and process direct-child scope inventory record-only;
- depth `N >= 2` with `ExploreScope` + `ExhaustiveWithinScope` + `ExhaustiveCoverageWithinScope`: recursively expand within the bound and fail closed when required in-scope expansion would exceed `N`;
- depth `N >= 2` with `InspectMatchesWithinScope` + `InspectMatchesWithinScope` + `AllDiscoveredMatchesInspected`: recursively expand within the bound and process boundary inventory record-only while recording unknown frontier beyond `N`.

The admitted interpretation MUST remain immutable for the Run and MUST be carried with the runtime-local execution intent.

#### Scenario: Exhaustive strategy receives fail-closed boundary semantics

- **WHEN** a supported depth-`N` exhaustive Strategy Run is admitted with `N >= 2`
- **THEN** its immutable runtime-local interpretation expands containers within the bound and preserves fail-closed cutoff semantics beyond `N`

#### Scenario: Match-inspection strategy receives bounded-record boundary semantics

- **WHEN** a supported depth-`N` match-inspection Strategy Run is admitted with `N >= 2`
- **THEN** its immutable runtime-local interpretation expands within the bound and processes boundary inventory record-only with unknown-frontier evidence

#### Scenario: No semantic mode is guessed

- **WHEN** the accepted typed fields do not match one row of the closed interpretation table
- **THEN** admission rejects the strategy deterministically and creates no fallback interpretation or Run

### Requirement: Admitted exploration rules govern the real execution path

The Agent execution path SHALL consume the admission-derived exploration interpretation. A semantic container below an expandable boundary SHALL use `ExpandContainer`; a leaf or record-only boundary node SHALL use `RecordOnly`. A `RecordOnly` rule SHALL be satisfied by a fresh accepted semantic observation and MUST NOT dispatch a click, tap, state mutation, or child traversal. An unavailable classification MUST remain unresolved and MUST NOT receive an inferred rule.

#### Scenario: Record-only leaf is observed without dispatch

- **WHEN** a fresh accepted observation contains a classified leaf governed by `RecordOnly`
- **THEN** the leaf receives record-only satisfaction evidence and no device action is dispatched for that rule

#### Scenario: Container expansion still requires Agent authorization

- **WHEN** a classified container below the depth boundary is governed by `ExpandContainer`
- **THEN** it can enter traversal only through the existing Agent grounding and authorization path

#### Scenario: Unclassifiable node remains unresolved

- **WHEN** the configured generic classifier cannot classify a discovered node
- **THEN** the node is recorded unresolved, receives no exploration rule, and is neither authorized nor dispatched

### Requirement: Ledger provenance is bound to the accepted Strategy Run

The exploration ledger SHALL be compiled only for the accepted Strategy Run whose immutable execution interpretation produced the evidence. Run identity, runtime-execution-intent reference, exploration interpretation, and declared depth MUST come from that accepted Run context and MUST NOT be caller-substitutable projection parameters. A legacy non-Strategy open-world Run MUST NOT be represented as a Strategy-bound ledger.

#### Scenario: Accepted Run metadata is preserved

- **WHEN** a ledger is compiled for an accepted Strategy Run
- **THEN** its Run identity, execution-intent reference, rules, and depth semantics exactly match the immutable admitted context

#### Scenario: Mismatched provenance fails closed

- **WHEN** a caller attempts to compile or relabel evidence with a different Run identity, execution-intent reference, exploration interpretation, or depth
- **THEN** ledger compilation fails closed and returns no ledger

#### Scenario: Legacy path cannot fabricate a Strategy ledger

- **WHEN** an open-world Run used no accepted Strategy context
- **THEN** the Strategy-bound exploration ledger surface is unavailable for that Run

### Requirement: Per-scope node accounting is identity-correct and exhaustive

For each exploration scope, the ledger SHALL derive `Discovered` from unique identities in the accepted branch inventory. Each discovered identity MUST appear in exactly one primary disposition: `Visited`, `Pending`, or `Unresolved`. An unresolved identity MUST remain part of `Discovered` and MUST NOT be added to it a second time. Unknown frontier SHALL be an overlapping annotation only on record-only `Visited` identities. Dispatch or authorization alone MUST NOT move an identity to `Visited`.

#### Scenario: Unresolved identity is not double-counted

- **WHEN** an accepted two-node inventory contains one completed container and one unclassifiable node
- **THEN** the scope reports discovered `2`, visited `1`, pending `0`, unresolved `1`, and does not report discovered `3`

#### Scenario: Classified unsatisfied identity remains pending

- **WHEN** a discovered identity is classified but has no rule-satisfaction evidence
- **THEN** the identity remains pending regardless of whether it was authorized or dispatched

#### Scenario: Boundary record is both visited and unknown frontier

- **WHEN** a bounded-record boundary identity is recorded from a fresh accepted observation
- **THEN** it is primarily visited and is additionally annotated as unknown frontier without increasing discovered

#### Scenario: Contradictory identity evidence fails closed

- **WHEN** evidence places one identity in incompatible primary dispositions or references an identity outside the accepted inventory
- **THEN** ledger compilation fails closed instead of clamping, dropping, or manufacturing counts

### Requirement: Existing structural-progress facts participate by correlation only

Existing Strategy structural-progress facts SHALL be accepted as an immutable, fail-closed correlation input when present. Their Run correlation, monotonic revision, kind, and evidence reference MUST be validated against the accepted Strategy Run context. Structural-progress facts MUST NOT directly increase or decrease node counts, assert exhaustion, create GoalEvidence, or trigger completion. Absence of optional structural facts MUST be represented explicitly and MUST NOT be replaced by synthetic per-scope meaning.

#### Scenario: Correlated structural fact is admitted without changing counts

- **WHEN** a structural-progress fact belongs to the accepted Run and has a valid monotonic revision and evidence reference
- **THEN** ledger compilation preserves its correlation while all node counts remain derived from identity-level exploration evidence

#### Scenario: Structural fact mismatch fails closed

- **WHEN** a structural-progress fact has a mismatched Run context, invalid revision, unsupported kind, or missing evidence reference
- **THEN** ledger compilation fails closed and produces no cleaner ledger

#### Scenario: Structural fact never proves completion

- **WHEN** structural-progress facts indicate bounded scope entry or later progress while GoalEvidence is unsatisfied
- **THEN** the Agent Run does not complete from those facts or from the ledger

### Requirement: Existing authority and compatibility boundaries remain unchanged

The remediation MUST NOT change the `StrategyDirective` schema, `run.strategy.start` wire operation, public protocol version, Run lifecycle, or mutable-state ownership. The Agent SHALL remain the sole owner of grounding, authorization, Run evidence, GoalEvidence evaluation, and terminal outcome; FSM SHALL remain lifecycle-transition authority; Traversal SHALL remain concrete execution owner. The remediation MUST NOT introduce scenario-specific knowledge, Phase 3 Memory, dynamic depth, mid-Run strategy mutation, a new evidence owner, or a new completion fact.

#### Scenario: Wire compatibility is preserved

- **WHEN** existing Strategy clients submit a currently valid `StrategyDirective`
- **THEN** the wire payload and protocol version remain unchanged and only the internal admitted interpretation is added

#### Scenario: Ledger and rules carry no execution authority

- **WHEN** exploration interpretation, ledger, and evidence-correlation types are inspected
- **THEN** they expose no DeviceAction generation, target selection, authorization, FSM transition, recovery decision, GoalEvidence mutation, or completion assertion

#### Scenario: Legacy execution behavior remains isolated

- **WHEN** the existing non-Strategy open-world entry executes
- **THEN** its established behavior remains unchanged and it does not acquire Strategy interpretation or Strategy-ledger provenance

