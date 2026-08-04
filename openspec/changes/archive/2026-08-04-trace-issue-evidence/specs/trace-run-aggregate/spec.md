## ADDED Requirements

### Requirement: TraceRun aggregates issues.jsonl records

`TraceRunLoader.LoadAsync(runDir)` SHALL additionally load `issues.jsonl` when present, exposing a collection of run issues with `Fingerprint`, `Summary` (embedding the D-192 failure detail), `Category`, `Phase`, `Severity`, and `StepNumber` (when recorded) — matching the Host `RunIssue` record contract. Absence of `issues.jsonl` SHALL NOT fail the load — the collection is empty. Issues SHALL be the only run-artifact evidence source read in addition to result.json/manifest.json/trace/steps (each issue line is deserialized with System.Text.Json; malformed lines are skipped consistently with result.json handling).

#### Scenario: issues.jsonl present loads issue records

- **WHEN** a run directory contains `issues.jsonl` with N well-formed issue lines
- **THEN** the TraceRun exposes an issues collection with N entries, each carrying fingerprint, summary (with the failure detail), category, phase, severity, and step number when recorded

#### Scenario: missing issues.jsonl yields empty collection

- **WHEN** a run directory has no `issues.jsonl`
- **THEN** the TraceRun exposes an empty issues collection and the load succeeds (no warning required)

#### Scenario: malformed issue line is skipped

- **WHEN** `issues.jsonl` contains a line that fails to deserialize
- **THEN** the load succeeds and the malformed line is excluded from the issues collection
