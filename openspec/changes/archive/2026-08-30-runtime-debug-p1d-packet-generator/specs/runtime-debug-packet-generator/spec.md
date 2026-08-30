## ADDED Requirements

### Requirement: Mechanical base packet generation
The Toolchain SHALL generate a base Evidence Packet from one capture bundle: terminal state from stored manifest facts, an `UNRESOLVED` target observation (explicitly selected or the final recorded observation), a candidate target occurrence without occurrence identity, an evidenceIndex carrying one `CAPTURE_ASSET` entry per artifact (relative uri, `sha256:<ContentHash>` digest, mediaType, selector with observationSeq from FrameId→record join), a MissingEvidence list naming the semantic facets a raw bundle cannot supply, and `repairGate.eligible=false`. The generator SHALL NOT emit ExpectedReality, ObservedReality, Good/Bad comparisons, LastGood/FirstBad, GapKind, Owner, Disposition, Confidence, or an EvidenceChain; absent facets are declared as MissingEvidence, never fabricated.

#### Scenario: Generated packet round-trips through the readers
- **WHEN** the generated packet is persisted and read by `summarize`, `occurrence`, and `evidence` commands
- **THEN** all three SHALL succeed (OK statuses) using only the stored facts, and the terminal state SHALL reflect the stored RuntimeOutcome

#### Scenario: Assets enter the packet as evidence refs
- **WHEN** a bundle stores artifacts with FrameId resolving to a record SequenceNumber
- **THEN** their evidenceIndex entries SHALL carry `kind=CAPTURE_ASSET`, `observationSeq`, and `frameId`, and the target occurrence SHALL reference them as evidenceRefs

### Requirement: Deterministic generation with no diagnostic fabrication
Repeated generation over one bundle SHALL be byte-identical; `generation.deterministicInputDigest` SHALL follow the P0 digest convention; an explicitly requested observation sequence that is not recorded SHALL fail closed with `EVIDENCE_UNAVAILABLE`.

#### Scenario: Byte-deterministic regeneration
- **WHEN** a bundle is generated twice
- **THEN** both results SHALL be byte-identical including the deterministicInputDigest

#### Scenario: Unknown target sequence fails closed
- **WHEN** a requested observation sequence is not present in the bundle records
- **THEN** the command SHALL return `EVIDENCE_UNAVAILABLE` without emitting a packet