# persisted-trace-capture-read Specification

## Purpose

Defines a Harness-owned fail-closed reader for one explicitly identified published capture and its optional immutable hierarchical TraceRun attachment.

## Requirements

### Requirement: Capture lookup uses explicit safe identity
The reader SHALL locate a capture only by an explicit `CaptureSessionId` interpreted as one safe path segment beneath the configured capture root. It MUST NOT scan for a run, infer a capture from `RunId`, `TraceRunId`, Goal, Scenario, prompt, or latest-directory order, or follow a path outside that root.

#### Scenario: Safe published capture is requested
- **WHEN** a caller supplies the exact identifier of one published capture beneath the configured root
- **THEN** the reader SHALL inspect only that capture directory

#### Scenario: Unsafe identifier or filesystem indirection is requested
- **WHEN** an identifier contains traversal, separators, a staging name, or resolves through a symbolic link or other indirection outside the configured root
- **THEN** the reader SHALL reject the request before returning capture content

### Requirement: Published capture is reconstructed and validated as one unit
The reader SHALL validate the supported capture schema, publication state, manifest identity, record order, declared files, artifact byte counts and content hashes, checksum entries, and attached TraceRun structure before returning an immutable reconstructed bundle. Any validation failure MUST reject the whole read and MUST NOT return a partially trusted bundle.

#### Scenario: Valid capture with artifacts and trace is read
- **WHEN** a supported published capture has complete records, artifacts, checksums, and a structurally valid TraceRun attachment
- **THEN** the reader SHALL return one immutable bundle whose artifact contents are loaded from the validated files and whose trace identities match the persisted attachment

#### Scenario: Persisted content is corrupt or incomplete
- **WHEN** required JSON is malformed, a declared file is missing, record order is invalid, an artifact byte count or hash differs, a checksum entry is missing or unknown, or the TraceRun hierarchy is invalid
- **THEN** the reader SHALL return typed validation issues and no bundle

### Requirement: Compatibility and absence are explicit
Unsupported schema versions SHALL fail with an explicit compatibility result. A valid older or current capture with no hierarchical trace attachment SHALL remain readable and SHALL report trace absence without synthesizing spans.

#### Scenario: Unsupported schema is read
- **WHEN** the capture or attached TraceRun declares an unsupported schema version
- **THEN** the reader SHALL reject it without mutation, silent upgrade, field invention, or partial read

#### Scenario: Valid capture has no trace attachment
- **WHEN** a valid supported capture predates trace attachment or intentionally contains none
- **THEN** the reader SHALL return the capture with an explicit trace-absent state and SHALL NOT manufacture a TraceRun from records, results, or diagnostic strings

### Requirement: Trace correlation is exact and non-inferential
When a caller additionally requires a specific `TraceRunId`, the reader SHALL compare it exactly with the optional attached TraceRun after capture validation. Missing or different identity MUST return a typed not-found or mismatch result; optional `RunId` correlation MUST remain absent when not persisted.

#### Scenario: Required trace identity matches
- **WHEN** the validated capture contains a TraceRun whose `TraceRunId` exactly matches the caller's required identity
- **THEN** the reader SHALL return that immutable trace without deriving any missing run correlation

#### Scenario: Required trace identity does not match
- **WHEN** no trace is attached or its `TraceRunId` differs from the required identity
- **THEN** the reader SHALL return a typed trace-absent or identity-mismatch result and SHALL NOT search another capture

### Requirement: Read failures cannot mutate persistence or Runtime
Reading and validation SHALL be side-effect free with respect to published captures and Runtime semantics. The reader MUST NOT repair, rewrite, overwrite, quarantine, catalog, replay, dispatch, retry, recover, or derive GoalEvidence from a capture.

#### Scenario: Invalid capture is read repeatedly
- **WHEN** a corrupt or incompatible capture is read one or more times
- **THEN** its files, catalog visibility, Runtime state, action count, recovery state, and GoalEvidence SHALL remain unchanged
