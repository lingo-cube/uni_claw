# Spec: open-world-container-inventory-completeness

## ADDED Requirements

### Requirement: Agent-owned Container inventory completeness evidence

The Runtime SHALL add one immutable Agent-owned `ContainerInventoryCompletenessEvidence` value that represents a Runtime-verified claim that the current Container’s discoverable child inventory has been fully enumerated within the approved deterministic exploration boundary.

The value SHALL include enough validated information to prove:
- the Container identity,
- the source Observation sequences accepted for this Container,
- the unique canonical child semantic page identities discovered,
- deterministic exploration exhaustion,
- unresolved candidate disposition.

The value SHALL NOT itself represent authorization, traversal, subtree completion, GoalEvidence, or Run completion.

#### Scenario: INV-1 all children visible in initial viewport with exhaustion

- **WHEN** all discoverable children are visible in the initial accepted viewport and deterministic exploration exhaustion is immediately established
- **THEN** Agent may produce Container inventory completeness evidence with the complete unique child inventory

#### Scenario: INV-2 additional child exists below fold

- **WHEN** the initial viewport is accepted but an additional child exists below the fold and exploration has not exhausted
- **THEN** Container inventory completeness SHALL NOT be proven

### Requirement: Canonical unique child normalization

Container inventory completeness SHALL normalize discovered children using the existing canonical semantic page identity.

Same canonical child identity observed in multiple viewports SHALL count as one inventory identity. Same identity observed multiple times in the same viewport SHALL count as one inventory identity.

The Runtime SHALL NOT introduce visual fingerprint identity, OCR similarity identity, alias inference, LLM identity merging, or a global page registry.

#### Scenario: INV-6 same semantic child identity appears in multiple accepted observations

- **WHEN** the same canonical child semantic identity appears in multiple accepted viewport observations
- **THEN** the unique child inventory contains exactly one entry for that identity

#### Scenario: INV-14 same child appears before and after scroll

- **WHEN** a child identity is visible before a scroll and also after the scroll
- **THEN** the unique identity count remains one and completeness is not inflated

### Requirement: Cycle and ancestor identities do not become new subtree work

A discovered child identity that resolves to an ancestor/cycle page may be accounted for as discovered evidence, but SHALL NOT become a new unique subtree work item.

Existing `OPEN_WORLD_TRAVERSAL_IDENTITY_SAFE` remains authoritative for rejecting entry.

#### Scenario: INV-7 cycle child points to ancestor

- **WHEN** a discovered child resolves to a page already in the current ancestry
- **THEN** the identity is accounted for as a cycle/non-traversable child and does not become new subtree work

### Requirement: Deterministic exploration exhaustion required for completeness

Agent SHALL NOT accept Container inventory completeness unless deterministic exploration exhaustion has been established from accepted Container-local observations and continuity evidence.

RunOpenWorldAsync SHALL acquire this exhaustion evidence through the existing/reused deterministic viewport exploration mechanism, not from caller declaration.

#### Scenario: EXH-4 scroll reaches deterministic terminal state

- **WHEN** accepted viewport observations and verified scroll/continuity evidence reach a deterministic terminal exhaustion state
- **THEN** RunOpenWorldAsync may use that exhaustion evidence for Container inventory completeness

Depth cutoff, safety cutoff, timeout, rejected dispatch, unresolved continuity, or “no child visible right now” SHALL NOT by themselves prove Container discovery exhaustion.

#### Scenario: INV-4 scroll reaches deterministic terminal exhaustion

- **WHEN** accepted viewport observations, scroll progress, continuity, and deterministic exhaustion evidence together prove that no further discoverable child possibility remains
- **THEN** Agent may produce Container inventory completeness evidence

#### Scenario: INV-5 exploration stops due to depth/safety budget

- **WHEN** exploration stops because of a depth bound, safety budget, or other cutoff
- **THEN** Container inventory completeness SHALL NOT be produced

### Requirement: Unresolved potential child blocks completeness

If accepted observations contain a candidate that may represent a child but its canonical semantic child identity cannot be resolved, Container inventory completeness MUST remain unproven.

The unresolved candidate MUST NOT be silently omitted and MUST NOT be treated as completed.

#### Scenario: INV-13 unresolved potential child exists at exhaustion boundary

- **WHEN** exhaustion evidence exists but an unresolved potential child candidate remains
- **THEN** completeness is blocked and the unresolved disposition is recorded

#### Scenario: INV-15 empty initial viewport but page is scrollable

- **WHEN** the initial viewport has no children but the Container is known/observed to be scrollable
- **THEN** the Container is NOT proven to be a leaf and completeness is not claimed

### Requirement: Discovery remains separate from authorization

A discovered child with fully resolved canonical identity may remain in the complete discovered inventory even when `CandidateAuthorizationEvidence` is `false`.

It SHALL be recorded as:
- DISCOVERED
- NOT_AUTHORIZED
- NOT_VISITED
- NOT_COMPLETED

Its presence SHALL NOT invalidate inventory completeness merely because Agent refuses to enter it.

#### Scenario: INV-8 discovered child identity known but authorization false

- **WHEN** a child is discovered and its canonical identity is resolved, but CandidateAuthorization is `false`
- **THEN** the inventory may be complete with that child accounted as discovered but non-traversable, and no child completion is fabricated

### Requirement: Null authorization does not hide unresolved identity

If `CandidateAuthorization` is `null`, the Runtime SHALL determine whether the child identity itself is fully resolved.

If the identity is unresolved or the candidate meaning remains ambiguous, completeness SHALL remain unproven.

Null authorization SHALL NOT make an unresolved candidate disappear from accounting.

#### Scenario: INV-17 null authorization with unresolved identity blocks completeness

- **WHEN** a discovered candidate has null authorization and its canonical child identity cannot be fully resolved
- **THEN** Container inventory completeness SHALL remain unproven and the unresolved candidate SHALL remain explicitly accounted for

### Requirement: Truthful leaf proof

A Container SHALL be considered a bounded semantic leaf only when:
- Container exploration is exhausted,
- complete unique child inventory is established,
- unique discoverable child count is zero.

Initial empty viewport, depth limit, safety rejection, and unresolved inventory SHALL NOT prove a leaf.

#### Scenario: INV-16 true exhausted Container with zero discoverable children

- **WHEN** deterministic exhaustion is proven and the complete unique child inventory is empty
- **THEN** the bounded semantic leaf proof succeeds

### Requirement: Runtime rejects caller completeness without proof

The caller MAY propose candidate branch semantics and interpret accepted evidence under the existing contract, but the caller SHALL NOT be able to declare Container inventory complete without Runtime-verifiable exhaustion/completeness evidence.

#### Scenario: INV-9 caller proposes incomplete inventory despite visible child

- **WHEN** the caller’s proposed inventory omits a child visible in accepted evidence
- **THEN** the Runtime rejects the completeness claim

#### Scenario: INV-10 inventory references unaccepted/stale Observation

- **WHEN** proposed inventory evidence references an Observation not accepted by the current Container
- **THEN** the Runtime rejects the completeness evidence

### Requirement: Completed children do not imply unexplored Container completeness

Even if every currently known child is completed, Container inventory completeness SHALL NOT be proven unless Container exploration exhaustion and unique child accounting are also proven.

#### Scenario: INV-11 all unique children completed but viewport exploration not exhausted

- **WHEN** all currently known children are completed but exploration is not exhausted
- **THEN** Container inventory completeness SHALL NOT be claimed

### Requirement: Existing subtree and full-tree mechanisms compose unchanged

Once Container inventory completeness exists, existing BranchProgress, identity safety, parent return, child completion evidence, and open-world termination SHALL compose with it.

This change SHALL NOT create a new subtree completion or full-tree termination subsystem.

#### Scenario: INV-12 all Container inventories exhausted recursively

- **WHEN** each Container inventory is complete and every unique child subtree is accounted for through existing mechanisms
- **THEN** existing subtree/termination semantics compose successfully

### Requirement: Preserve authority and architecture boundaries

External World SHALL remain truth authority. Observation SHALL remain evidence. Container SHALL remain accepted local mutable evidence owner. Agent SHALL remain inventory completeness acceptance authority. Traversal SHALL remain exploration execution authority. CandidateAuthorization SHALL remain traversal permission. GoalEvidence SHALL remain completion authority.

No global Settings graph, page database, crawler, inventory manager subsystem, planner, persistent visited registry, new Container owner, new truth authority, LLM, or VLM SHALL be introduced.

#### Scenario: Existing OpenWorld regression remains green

- **WHEN** this change is implemented
- **THEN** existing OpenWorld, U2, Capstone, branch progress, discovered-branch, identity-safety, and closed-world regressions remain green

### Requirement: Caller source provenance contract

When a caller declares required branches for the current Container, each branch SHALL be grounded to an independently discovered navigation source occurrence via an immutable `NavigationSourceOccurrenceReference` (accepted Observation sequence + observation-local occurrence identity) carried in `BranchSourceGroundingEvidence`. The Agent-owned `SourceGroundingValidator` SHALL be the only authority that accepts a grounding, and SHALL reject it unless: the referenced Observation belongs to the current run, belongs to the current Container's accepted viewport observations, is an accepted viewport observation, the occurrence actually exists, the occurrence is a NAVIGATION_CANDIDATE, and the occurrence resolves to a run-local logical source via the current source-equivalence normalization result. Callers SHALL NOT assert equivalence, declare logical sources, or reconcile by title/count/destination.

#### Scenario: PROV-1..PROV-14 valid/invalid grounding

- **WHEN** a caller grounding satisfies all six validation conditions
- **THEN** the Agent SHALL accept it and ground the branch to the normalized logical source
- **AND** caller omission, fabrication, foreign Container, previous run, LOCAL_CONTROL, UNKNOWN, ambiguous equivalence, and duplicate grounding SHALL be rejected
- **AND** destination-UNKNOWN SHALL still ground
- **AND** grounding SHALL NOT authorize, complete, or create GoalEvidence

#### Scenario: Structured-evidence environments only

- **WHEN** accepted Observations carry structured element evidence (fixture / real device path)
- **THEN** branch selection SHALL be occurrence-grounded and fail closed on invalid grounding
- **AND** legacy Elements-only environments SHALL retain pre-contract bounded selection until explicit grounding becomes mandatory
