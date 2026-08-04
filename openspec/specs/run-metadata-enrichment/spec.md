## ADDED Requirements

### Requirement: RunManifestInput and RunManifest carry optional purpose and taskId

`RunManifestInput` SHALL accept optional `Purpose` (string, run intent free text) and `TaskId` (string, CI job / agent session correlation id). `RunManifest` SHALL persist both fields (null when not provided). Purpose SHALL be injectable via Host run command CLI option `--purpose` or env `UNICLAW_RUN_PURPOSE`; TaskId via CLI option `--task-id` or env `UNICLAW_TASK_ID`.

#### Scenario: purpose and taskId flow into manifest
- **WHEN** a run is created with Purpose="PR #42 验收" and TaskId="ci-run-1234"
- **THEN** manifest.json contains both values

#### Scenario: absent metadata persists as null
- **WHEN** a run is created without purpose or taskId
- **THEN** manifest.json contains null for both fields and the run proceeds normally

#### Scenario: env var injection
- **WHEN** `UNICLAW_TASK_ID` is set in the environment and no `--task-id` option is given
- **THEN** the manifest's TaskId equals the env var value

### Requirement: manifest carries structured system and machine info

`RunManifestInput` / `RunManifest` SHALL accept optional `RunSystemInfo` and `RunMachineInfo` records:

- `RunSystemInfo`: SdkLevel, ReleaseVersion, BuildFingerprint, Codename, Arch — collected via ADB `getprop` in emulator mode; null in local mode
- `RunMachineInfo`: Os, Arch, Runtime, Hostname — collected from `RuntimeInformation` and `Environment.MachineName`

Collection SHALL live in Host production code (not test projects), so every run carries system context. There SHALL be NO separate emulator-info.json file — manifest is the single source.

#### Scenario: emulator mode collects system info
- **WHEN** a run executes against an emulator with ADB access
- **THEN** manifest's RunSystemInfo has SdkLevel/ReleaseVersion populated from getprop

#### Scenario: local mode has null system info
- **WHEN** a run executes in local (non-ADB) mode
- **THEN** manifest's RunSystemInfo is null and the run is unaffected

#### Scenario: machine info always collected
- **WHEN** any run is created
- **THEN** manifest's RunMachineInfo has Os, Arch, Runtime, and Hostname populated

### Requirement: old runs and readers remain compatible (formerly: old runs and readers remain compatible)

All manifest fields SHALL remain optional and default to null. Readers of manifest.json (integration tests, IterationAggregator, TraceTool) SHALL tolerate absent fields. The manifest schemaVersion SHALL be "2" with a top-level `"schemaVersion": "2"` declaration (V2 layout per `run-layout-v2`). Readers without V2 support SHALL detect the version and refuse loudly ("unsupported run layout version 2 — upgrade the analyzer") — never silently misread; "1" SHALL follow the legacy path. New tools SHALL dispatch by schemaVersion: "1" → V1 parser (existing code path preserved), "2" → V2 parser (assets/-aware), unknown → loud error.

#### Scenario: old manifest reads without new fields
- **WHEN** TraceRun loads a manifest.json written before this change
- **THEN** it reports "unknown" for missing metadata and analyzes the trace normally

#### Scenario: schemaVersion declares V2
- **WHEN** a new run is created after this change
- **THEN** manifest schemaVersion is "2" with top-level `"schemaVersion": "2"`

#### Scenario: old tool refuses a V2 manifest loudly
- **WHEN** a reader without V2 support loads a manifest with schemaVersion "2"
- **THEN** it errors with the upgrade message and does not attempt to parse

### Requirement: manifest asset list reflects the V2 layout

`RunAssets` SHALL update the manifest asset reference dictionary for V2: `steps/` + `analysis.jsonl` resolve under `assets/{runId}/`, `vision-evidence-{stepSpanId}[-{seq}].json` SHALL be a new gated asset entry, and the `safetyDecimals` entry SHALL be removed (safety decisions are trace-only per `run-layout-v2`).

#### Scenario: V2 manifest lists V2 paths
- **WHEN** a V2 run's manifest is inspected
- **THEN** the asset reference dictionary points into `assets/{runId}/` and contains no safetyDecimals entry

#### Scenario: gated evidence asset listed when enabled
- **WHEN** evidence storage is enabled for a run
- **THEN** the manifest asset list contains the vision-evidence entries

### Requirement: TraceTool surfaces run metadata in JSON output

`TraceRun` SHALL expose the manifest metadata. `diagnose --format json` SHALL include the `run` context object (runId, taskId, purpose, system, machine). `list` SHALL filter by `--task-id` and `--status`.

#### Scenario: diagnose json carries run context
- **WHEN** `uni-claw trace diagnose --run <dir> --format json` runs on a run with metadata
- **THEN** the JSON `run` object contains taskId, purpose, system, and machine from the manifest

#### Scenario: list filters by taskId
- **WHEN** `uni-claw trace list --task-id ci-run-1234` is invoked
- **THEN** only runs whose manifest TaskId matches are listed
