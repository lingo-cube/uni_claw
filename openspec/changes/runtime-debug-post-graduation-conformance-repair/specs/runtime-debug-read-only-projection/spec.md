## ADDED Requirements

### Requirement: Full P0 packet conformance validation
Before any projection, the Toolchain SHALL fail closed unless the input satisfies the complete frozen `runtime-debug-evidence-packet.v0` and `runtime-debug-ir.v0` shapes, required fields, closed vocabularies, explicit-absence states, unique EvidenceRef identities, repair-gate consistency, and resolution of every internal EvidenceRef in terminal, target, comparison, chain, divergence, owner, confidence, and derived-view fields. Validation SHALL NOT dereference EvidenceRef URIs.

#### Scenario: Malformed nested Debug IR is rejected
- **WHEN** a packet contains an unknown chain stage, invalid stage status, wrong ref collection type, invalid closed value, missing required field, or forbidden extra field
- **THEN** every command SHALL return `SCHEMA_VIOLATION` before producing a projection

#### Scenario: Dangling nested EvidenceRef is rejected
- **WHEN** any internal Debug IR or derived-view EvidenceRef does not resolve to exactly one evidenceIndex entry
- **THEN** every command SHALL return `SCHEMA_VIOLATION` without projecting the dangling reference

#### Scenario: External artifact remains metadata only
- **WHEN** a valid EvidenceRef URI identifies a trace, stage, frame, receipt, report, or other artifact
- **THEN** packet validation SHALL validate only the stored reference metadata and SHALL NOT open or copy that artifact
