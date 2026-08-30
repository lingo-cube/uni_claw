## Purpose

Expose the observed-versus-execution Container state and immutable transition evidence through authority-free read models and Debug Toolchain references without adding a second Runtime state owner.

## ADDED Requirements

### Requirement: Container context read model is read-only and truth-source classified
The Runtime/DriverHost read model SHALL expose `CurrentObservedLocation`, `ActiveExecutionContainer`, ordered `ActiveAncestorPath`, latest committed transition event when available, existing `CompletenessRef`, and `EvidenceRef`. Every field SHALL identify its truthful source and SHALL be an immutable copy or derived projection; the read model SHALL NOT expose mutable Runtime/Container references or gain Runtime authority.

#### Scenario: Premature return is directly visible
- **WHEN** observed location is `SettingsRoot`, active execution Container is incomplete `Display`, and the latest transition is `PREMATURE_RETURN_TO_ACTIVE_PARENT`
- **THEN** one read-model snapshot SHALL display all three facts plus the existing incomplete completeness reference without parsing a reason string

#### Scenario: Projection cannot mutate Runtime
- **WHEN** DriverHost or a debugging consumer reads the snapshot
- **THEN** it SHALL be unable to change WorldBelief, active execution context, branch progress, Container observations, action authorization, recovery, or completion

### Requirement: Latest transition is derived from immutable event history
The latest transition read-model field SHALL be derived from the latest committed immutable transition event in the explicit Run projection. Trace/history MAY retain transition events, but transition history SHALL NOT become mutable execution truth and missing history SHALL be reported as unavailable rather than reconstructed from reason strings.

#### Scenario: Transition event is missing
- **WHEN** an older run has no structured Container transition event
- **THEN** the read model SHALL mark the latest transition unavailable and SHALL NOT infer it from free-form diagnostic text

### Requirement: Transition evidence links to assets by reference
Each transition's `FreshObservationRef` SHALL be correlatable to the existing EvidenceRef chain and, when captured evidence assets exist, to one or more AssetRefs for the source frame/screenshot and derived crop/overlay. The projection SHALL keep refs only, SHALL report missing links explicitly, and SHALL NOT embed asset bodies or turn an AssetRef into world truth.

#### Scenario: Fresh transition frame is captured
- **WHEN** the accepted transition observation has a captured screenshot/frame asset
- **THEN** the transition projection SHALL link `TransitionRef` to `FreshObservationRef`, `EvidenceRef`, and `AssetRef` without copying image bytes

#### Scenario: Historical r5 lacks an image asset
- **WHEN** an existing r5 transition has structured observation evidence but no screenshot/logcat AssetRef
- **THEN** the read model SHALL preserve the available refs and explicitly report the missing asset instead of inventing one

### Requirement: Debug tooling remains a downstream buyer
Runtime Debugging Toolchain projections SHALL consume the read model and transition refs as non-authoritative inputs. Any future CLI or TUI surface SHALL remain read-only, use the shared query/analysis core, and SHALL NOT alter Runtime behavior, choose recovery, or authorize Apply.

#### Scenario: Container context panel renders a run
- **WHEN** a Debug Console renders Container context and transition information
- **THEN** it SHALL consume the same immutable projection as other tooling and SHALL NOT reimplement transition classification from strings
