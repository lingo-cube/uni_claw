## MODIFIED Requirements

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
