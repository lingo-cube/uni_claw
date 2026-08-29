## Purpose

Defines `VIEWPORT_EXHAUSTION_CONFIRMATION`: the Runtime's viewport-union normalization
contract distinguishes an EXTENDING window (discovery progress) from a
CONSISTENT_CONFIRMATION window (fresh, stable, provably-tail-consistent, zero-new-source
exhaustion-confirmation evidence) from an UNRESOLVED window (fail-closed). This buys
exactly one thing: the ability to prove true bounded-list exhaustion on stable real
observations. It creates no authority, no completion fact, and no recovery.

## ADDED Requirements

### Requirement: Three-way window classification in viewport-union normalization

`SourceEquivalenceNormalizer.Normalize` SHALL classify every accepted window after the
first as exactly one of: EXTENDING_WINDOW (the existing unique suffix(union)↔
prefix(window) overlap holds — semantics unchanged), CONSISTENT_CONFIRMATION_WINDOW
(all conditions of the "Consistent-confirmation conditions" requirement hold), or
UNRESOLVED_WINDOW (normalization fails closed exactly as today). Zero new sources alone
SHALL NOT be sufficient for any classification.

#### Scenario: Extending window is discovery progress

- **WHEN** a window extends the accumulated union with a unique overlap
- **THEN** it is classified EXTENDING_WINDOW and the union grows as today

#### Scenario: Unresolvable window still fails closed

- **WHEN** a window neither extends nor satisfies every confirmation condition
- **THEN** normalization is Unresolved with the existing failure semantics

### Requirement: Consistent-confirmation conditions

A window SHALL be classified CONSISTENT_CONFIRMATION_WINDOW if and only if ALL hold:
(a) fresh (sequence greater than the previous accepted window); (b) its ordered
navigation-signature sequence is element-wise identical to the immediately preceding
accepted window's sequence; (c) that sequence is a contiguous suffix of the accumulated
canonical union order; (d) it contains no logical source absent from the union; (e)
signature identity is exact with no in-frame duplicates (existing rules); and (f) the
count of consecutive confirmation windows does not exceed a bounded explicit constant.
Any unmet condition SHALL yield UNRESOLVED_WINDOW.

#### Scenario: True end-of-list stable confirmation (the STOP-2 shape)

- **WHEN** the final scroll of an exhausted bounded list yields a window identical to
  its predecessor and aligned with the union tail
- **THEN** it is classified CONSISTENT_CONFIRMATION_WINDOW and normalization resolves

#### Scenario: Repeated identical window before exhaustion is proven

- **WHEN** identical windows repeat mid-list (the union tail is NOT what is visible)
- **THEN** condition (c) fails and the window is UNRESOLVED_WINDOW

#### Scenario: Partial overlap with identity conflict

- **WHEN** a window overlaps the tail but a signature differs in identity (text or type)
- **THEN** it is UNRESOLVED_WINDOW (no conflict-masking deduplication exists)

#### Scenario: Type flip

- **WHEN** the same text appears with a different PerceptionType than in the union
- **THEN** signatures are not identical; the window is UNRESOLVED_WINDOW

#### Scenario: Reorder or ambiguous alignment

- **WHEN** the window's sequence is reordered relative to the canonical union order or
  admits multiple tail alignments
- **THEN** it is UNRESOLVED_WINDOW

#### Scenario: Transient mid-scroll frame

- **WHEN** a mid-scroll frame shows a shifted/scrolling-in-progress subset
- **THEN** it neither extends nor matches its predecessor identically; UNRESOLVED_WINDOW

#### Scenario: Genuinely new source appears

- **WHEN** the window contains any source absent from the union
- **THEN** it cannot be a confirmation; it must extend or fail

#### Scenario: Bounded consecutive confirmations

- **WHEN** more than the explicit consecutive-confirmation bound of confirmation windows
  occur consecutively
- **THEN** normalization is Unresolved (fail-closed)

#### Scenario: Multiple distinct confirmations across separate extensions

- **WHEN** confirmation windows occur at different list positions, each after fresh
  extensions and each satisfying all conditions
- **THEN** each is classified independently and normalization resolves

### Requirement: Confirmations are inert evidence

A CONSISTENT_CONFIRMATION_WINDOW SHALL add no logical source to the union, create no
grounding, authorization, visitation, or completion, and confer no dispatch authority on
any element. DISCOVERED, GROUNDED, CURRENTLY_VISIBLE, AUTHORIZED, VISITED, and COMPLETED
sets SHALL be unaffected by confirmation windows. Exhaustion confirmation SHALL NOT be
GoalEvidence and SHALL NOT constitute subtree completion.

#### Scenario: No dispatch authority from confirmation

- **WHEN** a container's completeness is backed by confirmation windows
- **THEN** the pending-branch set and dispatch decisions derive only from
  discovery-derived branches as today; no element becomes dispatchable by confirmation

### Requirement: Completeness accepts confirmation-backed normalization

Container completeness SHALL accept a resolved normalization whose trailing windows are
confirmations, recording the confirmation windows (sequences and classification) as
exhaustion-confirmation backing on the completeness evidence. All other completeness
semantics (inventory acceptance, unknown-affordance accounting, parent-return
resolution, depth rules) SHALL be unchanged.

#### Scenario: Exhaustion proof completes with confirmation backing

- **WHEN** all discovery branches are handled and the terminal windows are
  confirmations
- **THEN** completeness evidence records ExplorationExhausted with the confirmation
  backing and the traversal proceeds exactly as an extension-only exhaustion would

### Requirement: No scenario-specific truth

The classification SHALL use only signature identity, ordering, and window-union
relationships. It SHALL NOT reference UI text, coordinates, resource identifiers,
application identities, structured/hierarchy channels, or any Settings-specific
knowledge; fail-closed behavior SHALL be preserved everywhere.

#### Scenario: Classification is world-agnostic

- **WHEN** the same window shapes occur in any application
- **THEN** classification depends only on the geometric-free signature/ordering facts

## Explicit Non-Claims

This change does not purchase: generic recovery; Memory; Planner; UniAgent; Assisted
Exploration; dynamic depth; any new wire method or Runtime API; any change to Phase 2.6
validation acceptance criteria; any weakening of SourceIdentity, occurrence identity,
ambiguity handling, or fail-closed semantics.
