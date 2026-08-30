## ADDED Requirements

### Requirement: Mechanical replay fixture extraction
The Toolchain SHALL build a `runtime-debug-replay.v0` fixture from one capture bundle: ordered steps from the validated records, AssetRef entries, a trace summary (when a trace is attached), and a deterministicInputDigest over stored facts. Extraction SHALL be read-only and byte-deterministic.

#### Scenario: Fixture round-trips through validation
- **WHEN** an extracted fixture is saved and re-read by the replay validator
- **THEN** the validator SHALL report the same step/asset/span counts and OK status

#### Scenario: Extraction is deterministic
- **WHEN** the same bundle is extracted twice
- **THEN** both fixtures SHALL be byte-identical including the digest

### Requirement: Fail-closed fixture validation
The replay validator SHALL fail closed on malformed fixtures (`SCHEMA_VIOLATION`: schema/non-empty ids/unique positive step order/asset object shape) and missing files (`EVIDENCE_UNAVAILABLE`). The tool SHALL NOT execute or minimize replay fixtures in this slice.

#### Scenario: Malformed and missing fixtures fail closed
- **WHEN** a fixture file is not valid UTF-8 JSON or absent
- **THEN** the command SHALL return the corresponding closed status without a summary
