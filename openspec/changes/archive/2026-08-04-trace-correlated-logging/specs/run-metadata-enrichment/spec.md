## MODIFIED Requirements

### Requirement: TraceTool surfaces run metadata in JSON output

> Modified: `RunResult` gains `RunLogPath` — `result.json` outputs `runLogPath` (relative path `trace/{runId}/run.log`, mirroring the `TracePath` precedent; schemaVersion unchanged, readers fall back to the default when absent; a V1 run without the file resolves to "no log").

The run result JSON SHALL carry the paths of the run's diagnostic artifacts: `tracePath` (event stream) and `runLogPath` (correlated log). Both SHALL be relative paths resolved by the read-side layout model; explicit CLI parameters override metadata (per D-217); missing fields fall back to defaults, never fail. `RunResult` SHALL NOT carry runtime statistics (event counts, durations, completeness) — those belong to the event/log domain per D-214 and are derived from the trace itself, never duplicated into metadata.

#### Scenario: Analyzer discovers both diagnostic files from one metadata read
- **WHEN** an analyzer reads a V2 run's `result.json`
- **THEN** it finds `tracePath` and `runLogPath` and can open both `trace/{runId}/trace.jsonl` and `trace/{runId}/run.log`

#### Scenario: Old reader tolerates the new field
- **WHEN** a reader built before this change reads a run whose `result.json` contains `runLogPath`
- **THEN** the extra field is ignored and the reader behaves as before
