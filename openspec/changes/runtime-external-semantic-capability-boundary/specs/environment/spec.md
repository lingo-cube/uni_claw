## ADDED Requirements

### Requirement: Observation evidence sources have explicit authority tiers

Environment observation composition MUST distinguish primary perception evidence
from optional auxiliary evidence. Screenshot/Vision perception MUST remain the
primary perception path. ADB UI hierarchy dump MUST be represented as an
auxiliary source with explicit source identity, availability, capture freshness,
and frame/display metadata.

#### Scenario: Source tier survives observation composition
- **WHEN** Vision and ADB hierarchy evidence are captured for one observation cycle
- **THEN** downstream admission can distinguish the primary visual evidence from the auxiliary ADB evidence and trace both to their capture metadata

### Requirement: ADB hierarchy is optional and non-equivalent

ADB UI hierarchy capture MAY be unsupported, permission-denied, empty,
incomplete, stale, or structurally incompatible. Its absence or failure MUST be
reported as auxiliary-source unavailability and MUST NOT be treated as equivalent
to primary Vision failure or make an otherwise sufficient visual observation
fail.

#### Scenario: ADB dump unavailable
- **WHEN** primary Vision perception succeeds but UIAutomator dump is unavailable or invalid
- **THEN** Environment reports the ADB auxiliary source as unavailable and the visual observation remains usable

#### Scenario: Required primary perception unavailable
- **WHEN** a Run requires primary visual evidence and Vision is unavailable
- **THEN** the system fails closed or requests assistance rather than silently promoting ADB hierarchy evidence to an equivalent primary source

### Requirement: ADB-only evidence has no execution or completion authority

Evidence originating solely from ADB UI hierarchy MUST NOT authorize Action,
serve as the sole proof of verified Container identity, prove exploration or
coverage completeness, satisfy GoalEvidence, or cause a lifecycle transition.

#### Scenario: ADB-only actionable-looking node
- **WHEN** ADB hierarchy reports a clickable node with bounds but no sufficient fresh primary visual evidence corroborates the target
- **THEN** no action is authorized and no completion or lifecycle state changes

### Requirement: Coordinate normalization is source-local and contract-shared

The Observation contract MUST define one canonical full-frame coordinate system.
Each evidence producer MUST map its native coordinates into that contract:
Vision maps visual/model coordinates and the ADB adapter maps UIAutomator pixel
bounds. Normalization MUST be mechanical, source-qualified, and testable; it MUST
NOT add semantic role, target identity, or action authority.

#### Scenario: ADB pixel bounds are normalized as auxiliary geometry
- **WHEN** a valid ADB hierarchy node supplies pixel bounds and matching display/frame metadata
- **THEN** the adapter may map those bounds to the canonical frame while preserving auxiliary provenance and without assigning semantic role or execution authority

### Requirement: Primary Vision occurrences are independently groundable

Every fresh primary Vision occurrence MUST be representable as a canonical
observation occurrence without requiring ADB hierarchy, accessibility metadata,
or another structured source. Environment and Runtime MUST NOT manufacture a
primary occurrence from auxiliary structured evidence.

#### Scenario: Vision-only observation remains groundable
- **WHEN** Vision supplies fresh bounded elements and the auxiliary hierarchy source is absent
- **THEN** source normalization produces primary-supported canonical occurrences usable by generic Runtime and Agent grounding

#### Scenario: Synthetic primary promotion is forbidden
- **WHEN** only auxiliary structured occurrences are available
- **THEN** no adapter, fixture, semantic binding, or Runtime normalizer may convert them into synthetic primary Vision occurrences

### Requirement: Auxiliary corroboration never becomes a prerequisite

Current-frame auxiliary occurrences MAY be attached as provenance-preserving
corroboration to an unambiguous primary canonical occurrence. Their absence,
ambiguity, or failure MUST NOT invalidate that primary occurrence or reduce its
grounding eligibility.

#### Scenario: Vision plus ADB corroboration
- **WHEN** current-frame Vision and ADB occurrences correlate unambiguously
- **THEN** the canonical occurrence retains Vision primary authority and records ADB only as auxiliary support
