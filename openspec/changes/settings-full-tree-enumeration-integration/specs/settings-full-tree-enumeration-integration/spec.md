# Spec: settings-full-tree-enumeration-integration

## ADDED Requirements

### Requirement: Full-tree completion is strictly more than root inventory completeness

The Runtime SHALL distinguish `ContainerComplete` (per-Container inventory
positively exhausted and proven complete), `SubtreeComplete(C)` (every
authorized child branch of C visited, recursively completed, and
verified-returned where applicable), and `FullTreeComplete` (SubtreeComplete of
the Root PLUS fresh external GoalEvidence / tree-completion evidence on the
fresh accepted root observation).

Root inventory complete SHALL NOT by itself imply full-tree complete.

#### Scenario: FTE-1 root inventory complete does not equal full-tree complete

- **WHEN** the Root Container inventory is proven complete but child branches
  remain untraversed
- **THEN** FullTreeComplete SHALL NOT be claimed

#### Scenario: FTE-2 recursive subtree completion composes

- **WHEN** each Container inventory is complete and every authorized child
  subtree is recursively completed with verified returns
- **THEN** SubtreeComplete(Root) holds and FullTreeComplete additionally
  requires fresh external GoalEvidence / tree-completion evidence
- **AND** `GoalEvidence == true` alone SHALL NOT infer SubtreeComplete(Root):
  the dependency is fixed as recursive subtree proof PLUS fresh external
  completion evidence -> FullTreeComplete (no reverse derivation)

### Requirement: Real Settings root entry evidence

The Runtime SHALL establish the real Android Settings root entry from real
evidence: application identity `com.android.settings`, a resolved semantic root
identity from the first real observation (structured-first, OCR fallback),
initial structured sources, per-source classification, scrollability, and
foreground ownership. COMPOSE-05 SHALL NOT be used as full-tree evidence.

#### Scenario: FTE-3 real Settings root identity established

- **WHEN** the first real Settings observation is reconciled
- **THEN** the semantic root identity, initial structured sources, and
  foreground ownership are recorded from real evidence

### Requirement: Recursion contract

The Runtime SHALL traverse a Container's authorized child sources recursively:
Enter C → prove inventory(C) → for each authorized child source S: fresh reach,
dispatch, settle child C', recurse C', prove SubtreeComplete(C'), verified
return C → all required children complete → SubtreeComplete(C).

Existing run-local ancestry/visited identity safety SHALL remain authoritative
(duplicate semantic page identity → fail closed). Depth/budget bounds SHALL
remain fail-closed. Blind redispatch and historical dispatch SHALL NOT occur.

#### Scenario: FTE-4 recursion genuinely occurs at depth ≥ 3

- **WHEN** a real Settings branch is traversed to a grandchild (Root → Child →
  Grandchild) or recursion is otherwise proven
- **THEN** the recursion contract holds and SubtreeComplete is proven at each
  level with verified returns

### Requirement: Child classification

Each discovered source SHALL resolve to exactly one of
AUTHORIZED_CHILD | UNAUTHORIZED | LOCAL_CONTROL | UNRESOLVED. Only
AUTHORIZED_CHILD carries a recursive completion obligation. A
NAVIGATION_CANDIDATE SHALL NOT automatically be an authorized child;
authorization is a separate decision.

#### Scenario: FTE-5 unauthorized child recorded, not completed

- **WHEN** a discovered child is authorization-false
- **THEN** it is recorded as DISCOVERED/NOT_AUTHORIZED/NOT_VISITED/NOT_COMPLETED
  and does not block inventory completeness or fabricate completion

### Requirement: Truthful leaf

A real Settings leaf is proven only when inventory is complete, zero unresolved
interaction remains, and zero authorized-child obligation exists. "No navigation
candidate currently visible" alone SHALL NOT prove a leaf.

#### Scenario: FTE-6 truthful leaf

- **WHEN** exhaustion + complete empty inventory + zero unresolved interaction
  hold
- **THEN** LeafSubtreeComplete = TRUE

### Requirement: Alias boundary

Run-local identity safety (duplicate semantic page identity → fail closed) SHALL
be preserved. Alias merging SHALL NOT be purchased. Two different sources
resolving to the same semantic destination SHALL be classified as
`SETTINGS_DESTINATION_ALIAS_PRESSURE` and the run SHALL stop.

#### Scenario: FTE-7 duplicate destination pressure stops

- **WHEN** two different sources resolve to the same semantic destination
- **THEN** the run stops and classifies SETTINGS_DESTINATION_ALIAS_PRESSURE
  without relaxing identity safety

### Requirement: External navigation boundary

A Settings source leading outside Settings ownership SHALL be classified as
OWNED_CHILD | EXTERNAL_BOUNDARY | UNRESOLVED. Foreground drift SHALL NOT be
treated as an ordinary child traversal; EXTERNAL_BOUNDARY sources carry no
recursive obligation and are recorded as boundaries.

#### Scenario: FTE-8 external boundary recorded

- **WHEN** a source transitions the foreground outside Settings ownership
- **THEN** it is recorded as EXTERNAL_BOUNDARY with no recursive obligation,
  is excluded from RequiredChildren, and MUST carry a VerifiedBoundaryDisposition
  (source/provenance reference + verified boundary evidence + disposition) so
  the obligation is explicitly discharged, never silently dropped

### Requirement: Dynamic Settings inventory

If traversal mutates the parent inventory (source added/removed, interactive
Unknown appears, logical-source mapping changes), the frozen-inventory
consistency SHALL fail closed. Dynamic graph mutation recovery SHALL NOT be
implemented; classify `SETTINGS_DYNAMIC_INVENTORY_PRESSURE`.

#### Scenario: FTE-9 dynamic mutation fails closed

- **WHEN** the parent inventory mutates during traversal
- **THEN** the frozen-inventory consistency fails closed with
  SETTINGS_DYNAMIC_INVENTORY_PRESSURE

### Requirement: Completion ledger

The Runtime SHALL keep an Agent-owned run-local completion ledger recording ONLY
proven facts: ContainerIdentity, ContainerCompletenessEvidence,
RequiredChildren (recursive AUTHORIZED_CHILD obligations only),
CompletedChildren, SubtreeComplete, AND VerifiedBoundaryDispositions — one entry
per verified EXTERNAL_BOUNDARY source (source/provenance reference, verified
external-boundary evidence, disposition). The ledger is completion bookkeeping,
NOT a world-truth authority, NOT a graph edge, NOT a recursive child completion,
and NOT an authorization authority; no global persistent graph. EXTERNAL_BOUNDARY
sources SHALL NOT recurse and SHALL NOT enter RequiredChildren; UNRESOLVED
sources still fail closed.

#### Scenario: FTE-10 ledger records only proven facts

- **WHEN** subtrees complete
- **THEN** the ledger records only evidence-backed entries and adds no truth
  authority

### Requirement: First real integration scenario

The first integration scenario SETTINGS-TREE-01 SHALL traverse the real Settings
root to at least three semantic depths (Root → Child → Grandchild) or otherwise
prove genuine recursion, with verified returns and subtree completion — not a
Root + one-layer replay of COMPOSE-05.

#### Scenario: FTE-11 SETTINGS-TREE-01 runs on the real emulator

- **WHEN** SETTINGS-TREE-01 executes on the real emulator
- **THEN** recursion genuinely occurs at depth ≥ 3 with verified returns and
  SubtreeComplete evidence at each level

### Requirement: Failure classification

Real runs SHALL classify the first production failure into exactly one of:
ROOT_EVIDENCE_GAP | SOURCE_AUTHORIZATION_GAP | RECURSIVE_COMPLETION_GAP |
SETTINGS_DESTINATION_ALIAS_PRESSURE | SETTINGS_DYNAMIC_INVENTORY_PRESSURE |
EXTERNAL_BOUNDARY_PRESSURE | DEPTH_BUDGET_PRESSURE | EXISTING_MECHANISM_DEFECT.
Each run SHALL fix at most the first real production failure.

#### Scenario: FTE-12 first-failure-only repair

- **WHEN** a real run stops
- **THEN** exactly one pressure class is reported and only the first real
  production failure is repaired
