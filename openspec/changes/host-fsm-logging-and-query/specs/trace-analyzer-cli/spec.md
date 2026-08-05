## ADDED Requirements

### Requirement: diagnose evidence includes run.log as primary forensic source

`diagnose --run <runDir>` SHALL include `run.log` (`trace/{runId}/run.log`) as the first-priority
source in its `artifactPaths` output when the file exists. The `evidence` array SHALL include a
`run_log_present` or `run_log_absent` entry describing whether the file was found.

#### Scenario: run.log present in artifact paths
- **WHEN** `uni-claw trace diagnose --run <dir> --format json` is invoked and `trace/{runId}/run.log` exists
- **THEN** the JSON `artifactPaths` object includes `logPath` with the relative path to the log file

#### Scenario: run.log absent
- **WHEN** `uni-claw trace diagnose --run <dir> --format json` is invoked and no `run.log` exists
- **THEN** the JSON `evidence` array includes a `run_log_absent` entry with description "run.log not found"
- **THEN** `artifactPaths.logPath` is absent

### Requirement: trace-analyzer agent Step 3 uses run.log for cross-referencing

The trace-analyzer agent's Step 3 (forensic evidence gathering) SHALL query `run.log` by `spanId`
to cross-reference trace events with runtime log lines. The agent SHALL use `grep "s=<spanId>"
<runDir>/trace/<runId>/run.log` to locate log entries correlated with a specific trace span.

#### Scenario: Agent cross-references error span with log
- **WHEN** a trace span with `spanId=X` shows an execution error, and the agent needs to determine
the error context
- **THEN** the agent queries `run.log` for `s=X` to retrieve the corresponding log lines (FSM state,
action result, or exception details) from the same span

### Requirement: trace-analyzer agent Step 4 includes run.log completeness check

The trace-analyzer agent's Step 4 (trace completeness self-assessment) SHALL include a row for
`run.log` with three tiers: complete (has both run start and run end records), partial (file exists
but missing start or end), incomplete (file does not exist).

#### Scenario: Complete run.log
- **WHEN** `run.log` contains both a "Run <RunId> started" and a "Run <RunId> ended" line
- **THEN** the completeness assessment marks `run.log` as "complete"

#### Scenario: Partial run.log
- **WHEN** `run.log` exists but is missing either the start or end record
- **THEN** the completeness assessment marks `run.log` as "partial" and notes the gap's impact on confidence
