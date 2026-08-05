## MODIFIED Requirements

### Requirement: Run layout declares schemaVersion "2" and restructures storage spaces

> Modified: layout gains `trace/{runId}/run.log` (trace-correlated logging, stream-append text diagnostics — NOT pipeline assets; per 2026-08-04-trace-correlated-logging-prd.md §4.6).

The V2 run layout SHALL declare top-level `schemaVersion: "2"` and organize run artifacts into two storage spaces under the run root: `trace/{runId}/` as the event-stream space (sync-appended records including reference events) and `assets/{runId}/` as the asset space (pipeline-batched bytes). The event-stream space SHALL contain `trace.jsonl` and `run.log` (trace-correlated logging output, same directory, same format contract as console). `criteria.json` and `pending_verification` status SHALL live at the run root. The layout model SHALL expose a run.log relative-path resolution helper for readers.

#### Scenario: Run log lives with the event stream
- **WHEN** a V2 run completes
- **THEN** `trace/{runId}/run.log` exists next to `trace/{runId}/trace.jsonl` and contains log lines matching the unified format contract

#### Scenario: Layout helper resolves the run log path
- **WHEN** a reader asks the layout model for the run log location
- **THEN** it receives the relative path `trace/{runId}/run.log` without composing strings
