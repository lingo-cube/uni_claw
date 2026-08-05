## ADDED Requirements

### Requirement: Run layout declares schemaVersion "2" and restructures storage spaces

`RunAssetVocabulary.SchemaVersion` SHALL be "2" and manifest.json SHALL carry top-level `"schemaVersion": "2"`. The V2 layout SHALL be: run root metadata (manifest/result/issues/plan/scenario.snapshot/**criteria.json**) + `trace/{runId}/trace.jsonl` + `trace/{runId}/run.log` (event-stream space; run.log is the trace-correlated logging output — stream-append text diagnostics, NOT pipeline assets, same directory and format contract as console) + `assets/{runId}/` (asset space, first level = runId, symmetric with trace/). The layout model SHALL expose a run.log relative-path resolution helper for readers. `steps/` + `analysis.jsonl` SHALL move from run root into `assets/{runId}/`; `vision-evidence-{stepSpanId}[-{seq}].json` SHALL be a new gated asset; `criteria.json` SHALL carry the verificationCriteria snapshot.

#### Scenario: V2 run writes the new layout

- **WHEN** a run completes under V2
- **THEN** the run directory contains `assets/{runId}/steps/{n:D4}/`, `assets/{runId}/analysis.jsonl`, `trace/{runId}/trace.jsonl`, `criteria.json`, and manifest schemaVersion "2"

#### Scenario: runId is the first-level key in both storage spaces

- **WHEN** a V2 run's asset/trace paths are inspected
- **THEN** both `trace/{runId}/` and `assets/{runId}/` are keyed by the same runId (== traceId) as their first level — the stable storage key unchanged if the backend switches to object storage

#### Scenario: Run log lives with the event stream
- **WHEN** a V2 run completes
- **THEN** `trace/{runId}/run.log` exists next to `trace/{runId}/trace.jsonl` and contains log lines matching the unified format contract

#### Scenario: Layout helper resolves the run log path
- **WHEN** a reader asks the layout model for the run log location
- **THEN** it receives the relative path `trace/{runId}/run.log` without composing strings

### Requirement: Safety decisions do not persist to files

`safety-decisions.jsonl` and `steps/{n}/safety-decision.json` SHALL NOT be produced in V2 (zero readers). Safety decisions SHALL live in the trace only — `TraceSafetyDecisionSink` writes full fields (policyId/policyVersion/policyHash/ruleId/reason/pageFingerprint/source/normalizedTarget/pageIdentity/confidence) into `safety.*` events. If a consumer later needs a field, the trace event SHALL be extended — file persistence SHALL NOT be restored.

#### Scenario: V2 run has no safety persistence files

- **WHEN** a V2 run is inspected
- **THEN** no `safety-decisions.jsonl` and no `steps/{n}/safety-decision.json` exist, and manifest's asset list contains no safetyDecimals entry

#### Scenario: safety decisions remain fully recorded in trace

- **WHEN** a safety gate decision occurs during a run
- **THEN** the trace contains a `safety.*` event with the full decision fields

### Requirement: Old tools refuse loudly, new tools dual-read

Tools that do not support V2 SHALL detect `"schemaVersion": "2"` in manifest.json and **refuse loudly** ("unsupported run layout version 2 — upgrade the analyzer") — never silently misread; `"1"` SHALL follow the legacy path. New tools SHALL dispatch by schemaVersion: "1" → V1 parser (existing code path preserved), "2" → V2 parser (assets/-aware), unknown → loud error. Writers SHALL emit V2 only.

#### Scenario: old tool refuses a V2 run

- **WHEN** an analyzer without V2 support reads a V2 manifest
- **THEN** it errors loudly with the upgrade message and does not attempt to parse

#### Scenario: dual parsers coexist

- **WHEN** a V1-constructed run and a V2 run are both loaded by a new tool
- **THEN** each dispatches to its own parser and reads correctly

### Requirement: trace line format is decoupled from layout version

trace.jsonl line format SHALL be independent of the layout version — the V2 layout change SHALL NOT change event line contracts.

#### Scenario: V2 run reuses V1 event lines

- **WHEN** a V2 run's trace.jsonl is compared to V1 line format
- **THEN** the record_type/discriminator and field contracts are identical (only the directory location changed)
