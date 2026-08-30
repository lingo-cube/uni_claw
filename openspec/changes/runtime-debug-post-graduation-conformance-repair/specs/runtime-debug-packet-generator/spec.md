## MODIFIED Requirements

### Requirement: Mechanical base packet generation
The Toolchain SHALL generate a complete `runtime-debug-evidence-packet.v0` from one validated capture bundle. Stored facts SHALL populate source identity, terminal state, target observation, and FRAME or STAGE_ARTIFACT EvidenceRefs. Semantic facts unavailable from the raw bundle SHALL be represented by the frozen Debug IR's explicit absence states: comparisons `NOT_AVAILABLE`, all seven chain stages `MISSING`, divergence `UNRESOLVED`, GapKind `UNKNOWN`, Owner `UNRESOLVED/UNKNOWN`, Confidence `UNASSESSED`, and Disposition `EVIDENCE_COLLECTION`, with matching MissingEvidence and repair blockers. The generator SHALL NOT infer FDP, occurrence identity, owner, repair eligibility, or any semantic success claim.

#### Scenario: Generated packet round-trips through the readers
- **WHEN** the generated packet is validated against the frozen P0 JSON Schemas and then supplied to `summarize`, `occurrence`, `trace`, `diff`, `evidence`, and `terminal-chain`
- **THEN** Schema validation and all stored-fact projections SHALL succeed without omitted required fields or invented diagnosis

#### Scenario: Assets enter the packet as evidence refs
- **WHEN** a bundle stores artifacts with frameId resolving to a record sequenceNumber
- **THEN** their evidenceIndex entries SHALL use a P0 closed EvidenceRef kind, verified digest, observationSeq, and frameId, and relevant target fields SHALL reference only resolvable refIds

### Requirement: Deterministic generation with no diagnostic fabrication
Repeated generation over one immutable validated bundle SHALL be byte-identical; `generation.schemaDigest` SHALL identify the frozen packet Schema and `generation.deterministicInputDigest` SHALL follow the P0 digest convention. An explicitly requested observation sequence that is not recorded SHALL fail closed with `EVIDENCE_UNAVAILABLE`.

#### Scenario: Byte-deterministic regeneration
- **WHEN** a validated bundle is generated twice
- **THEN** both results SHALL be byte-identical including schemaDigest and deterministicInputDigest

#### Scenario: Unknown target sequence fails closed
- **WHEN** a requested observation sequence is not present in the bundle records
- **THEN** the command SHALL return `EVIDENCE_UNAVAILABLE` without emitting a packet
