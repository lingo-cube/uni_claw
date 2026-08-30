## Purpose

定义对显式 Runtime Debug Evidence Packet 执行离线摘要与 occurrence 查询的最小只读能力，使重复证据读取可确定复现，同时不获得 Runtime、Trace、repair 或 lifecycle authority。

## ADDED Requirements

### Requirement: Explicit supported evidence packet input
The `runtime-debug` P1a commands SHALL accept exactly one explicitly named local `runtime-debug-evidence-packet.v0` JSON file as source, SHALL NOT select an implicit latest run, and SHALL fail closed when the file is missing, malformed, unsupported, or insufficient for the requested projection.

#### Scenario: Supported packet is explicit
- **WHEN** the caller supplies one readable P0 Evidence Packet v0 file
- **THEN** the command SHALL evaluate only that file and SHALL identify the result by the packet and source identities stored in it

#### Scenario: Input is implicit or unsupported
- **WHEN** the caller omits the source, names a directory, supplies multiple sources, or supplies an unsupported packet version
- **THEN** the command SHALL return `INVALID_INPUT`, `EVIDENCE_UNAVAILABLE`, or `SCHEMA_VIOLATION` as applicable and SHALL NOT search the repository for a substitute

### Requirement: Deterministic summary projection
The `summarize` command SHALL project only stored source identity, terminal state, evidence availability, target scope, missing evidence, and repair blockers into canonical JSON; it SHALL NOT infer root cause, choose an Owner, authorize repair, or strengthen stored confidence.

#### Scenario: Same packet is summarized twice
- **WHEN** identical packet bytes and the same P1a contract version are summarized twice
- **THEN** both successful stdout payloads SHALL be byte-equivalent, including stable object and array ordering, and SHALL contain no wall-clock timestamp

#### Scenario: Packet records unresolved evidence
- **WHEN** the packet contains missing evidence, unresolved target scope, or repair blockers
- **THEN** the summary SHALL preserve those facts without converting them into a confirmed diagnosis or repair instruction

### Requirement: Typed occurrence selection
The `occurrence` command SHALL require exactly one explicit selector kind from `OccurrenceId`, `StableKey`, `RowId`, or `EvidenceRef`, SHALL match only facts stored in the packet's `TargetOccurrence` and `EvidenceIndex`, and SHALL NOT use text, bounds, array index, or string-shape guessing as identity evidence.

#### Scenario: Exactly one supported selector matches
- **WHEN** one typed selector matches one stored occurrence candidate
- **THEN** the command SHALL return that candidate with its stored correlation status, proof, counterevidence, identity fields, and linked EvidenceRefs

#### Scenario: Selector count is not one
- **WHEN** zero or more than one selector kinds are supplied
- **THEN** the command SHALL return `INVALID_INPUT` without choosing a selector by precedence

#### Scenario: StableKey is the only correlator
- **WHEN** a `StableKey` selector matches but the stored evidence does not prove same occurrence identity
- **THEN** the result SHALL remain `CANDIDATE` or unresolved as stored and SHALL NOT be promoted to confirmed identity

### Requirement: Ambiguity and coverage fail closed
The occurrence projection SHALL distinguish no match, multiple candidate identities, identity mismatch, and insufficient trace coverage by closed command status, and SHALL NOT resolve any of them through implementation-code inspection or heuristic guessing.

#### Scenario: Multiple distinct candidates match
- **WHEN** the selected value maps to multiple distinct candidate identities in the explicit packet
- **THEN** the command SHALL return `AMBIGUOUS_OCCURRENCE` with all candidates in deterministic order and SHALL NOT select a winner

#### Scenario: Packet lacks requested occurrence facts
- **WHEN** the packet references the selector but lacks the evidence required to construct the requested occurrence projection
- **THEN** the command SHALL return `INSUFFICIENT_TRACE_COVERAGE` and identify the unavailable evidence without manufacturing facts

#### Scenario: Selector has no match
- **WHEN** the typed selector matches neither the target occurrence nor an indexed EvidenceRef
- **THEN** the command SHALL return `EVIDENCE_UNAVAILABLE`

### Requirement: Closed result and process outcome contract
Every command result SHALL use exactly one status from `OK`, `INVALID_INPUT`, `EVIDENCE_UNAVAILABLE`, `IDENTITY_MISMATCH`, `AMBIGUOUS_OCCURRENCE`, `INSUFFICIENT_TRACE_COVERAGE`, or `SCHEMA_VIOLATION`, SHALL emit one canonical JSON document to stdout, and SHALL use a stable nonzero process exit code for every non-`OK` status.

#### Scenario: Command succeeds
- **WHEN** projection completes without a fail-closed condition
- **THEN** stdout SHALL contain one canonical JSON result with status `OK` and the process exit code SHALL be zero

#### Scenario: Command fails closed
- **WHEN** validation or projection produces a non-`OK` status
- **THEN** stdout SHALL still contain one canonical JSON result, stderr SHALL contain no competing machine result, and the process exit code SHALL be nonzero

### Requirement: Read-only authority boundary
The P1a tooling SHALL be offline, read-only, deterministic, and without Runtime authority; it SHALL NOT mutate evidence or trace, contact a device or network, launch a Runtime process, change production dependencies, execute repair, choose authority, or alter lifecycle state.

#### Scenario: Commands read an evidence packet
- **WHEN** either P1a command is executed against a valid packet
- **THEN** the packet and referenced artifacts SHALL remain byte-identical and no Runtime, DriverHost, Harness, PhysicalHost, device, or network operation SHALL be initiated

#### Scenario: EvidenceRef points outside the packet
- **WHEN** a packet contains a URI to an external stage, frame, trace, receipt, or report artifact
- **THEN** P1a SHALL project the reference metadata only and SHALL NOT open, copy, validate, or mutate the referenced large artifact
