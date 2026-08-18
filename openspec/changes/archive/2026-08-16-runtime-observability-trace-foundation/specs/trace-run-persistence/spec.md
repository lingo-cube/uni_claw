## ADDED Requirements

### Requirement: Existing Harness persistence boundary owns TraceRun storage
A finalized `TraceRun` SHALL be persisted only through the existing Harness capture bundle and `ITraceCaptureStore` lifecycle. Runtime and Runtime components SHALL NOT reference a trace store or initiate trace persistence.

#### Scenario: Captured run includes hierarchical observability
- **WHEN** the Harness finalizes both a capture session and its per-run trace recorder
- **THEN** it SHALL attach the immutable `TraceRun` to the capture bundle and persist both through one existing append-only store operation

#### Scenario: Runtime completes without Harness capture
- **WHEN** Runtime is invoked without a Harness capture/store composition
- **THEN** Runtime SHALL NOT attempt filesystem, database, network, or other TraceRun persistence

### Requirement: TraceRun and TraceCaptureSession remain distinct
`TraceRun` SHALL represent finalized hierarchical diagnostic evidence and `TraceCaptureSession` SHALL retain ownership of environment-call/artifact capture lifecycle. Neither model SHALL absorb the other's mutable lifecycle or semantic responsibilities.

#### Scenario: Trace recorder fails while capture records remain valid
- **WHEN** hierarchical trace projection fails but environment capture records and artifacts finalize successfully
- **THEN** the Harness SHALL report the trace failure separately and SHALL NOT rewrite valid capture records or the Runtime outcome

#### Scenario: Capture fails after Runtime trace finalizes
- **WHEN** capture persistence fails after a valid immutable `TraceRun` is produced
- **THEN** the TraceRun SHALL remain an immutable diagnostic value while the capture persistence result reports failure

### Requirement: Append-only versioned serialization
The Harness SHALL serialize TraceRun schema version, hierarchy, attribution, outcomes, events, and monotonic nanosecond timing deterministically inside the capture publication. Existing capture IDs SHALL fail closed on overwrite, and unsupported trace schema versions SHALL fail validation without mutation or silent upgrade.

#### Scenario: Existing capture ID is reused
- **WHEN** a capture containing a TraceRun is saved under an already published capture session ID
- **THEN** the store SHALL reject the save and SHALL leave the existing publication unchanged

#### Scenario: Unsupported trace schema is loaded
- **WHEN** a persisted TraceRun declares an unsupported schema version
- **THEN** the Harness SHALL return an explicit compatibility failure and SHALL NOT invent or rewrite fields

### Requirement: Backward-compatible optional attachment
Existing capture bundles without hierarchical TraceRun data SHALL remain readable, and the Harness SHALL represent their trace attachment as absent rather than synthesizing hierarchy from environment records or semantic reason strings.

#### Scenario: Pre-foundation capture is loaded
- **WHEN** the Harness loads a valid capture created before hierarchical trace support
- **THEN** it SHALL preserve the capture data and report that no TraceRun is attached

### Requirement: Persistence failure cannot affect Runtime semantics
Trace serialization, staging, validation, or publication failure SHALL NOT change Runtime state, action dispatch count, retry behavior, GoalEvidence, or final Runtime result.

#### Scenario: Trace artifact write fails
- **WHEN** storage fails while publishing a capture with a valid TraceRun
- **THEN** the Harness SHALL return a persistence failure after the run and SHALL NOT redispatch an action or alter the completed Runtime result

