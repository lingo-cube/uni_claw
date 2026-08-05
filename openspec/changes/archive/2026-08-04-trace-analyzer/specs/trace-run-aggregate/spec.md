## ADDED Requirements

### Requirement: TraceRun is the single aggregate entry for a run directory

`TraceRun` SHALL be the only entry point for reading a run directory. `TraceRunLoader.LoadAsync(runDir)` SHALL load: `result.json` → RunResult, `manifest.json` → RunManifest, `trace/trace.jsonl` → ITraceQuery (replayed through FileTraceStorage → InMemoryTraceService), `steps/D4/*` → StepAsset[] (lazy). Subcommands SHALL NOT read files directly.

#### Scenario: load full run directory
- **WHEN** `TraceRunLoader.LoadAsync` is called on a valid run directory
- **THEN** the returned TraceRun exposes RunResult, RunManifest, a functional ITraceQuery, and step assets

#### Scenario: trace replay yields queryable spans
- **WHEN** a TraceRun's ITraceQuery is used after load
- **THEN** `GetAllSpans()`, `GetSpansByType("engine.step")`, and `GetChildSpans(...)` return spans matching the trace.jsonl contents

#### Scenario: result.json missing degrades gracefully
- **WHEN** a run directory lacks result.json
- **THEN** TraceRun loads with a null RunResult and marks "no result.json" instead of failing

#### Scenario: corrupt trace line is skipped with warning
- **WHEN** trace.jsonl contains a malformed line
- **THEN** the load succeeds, the bad line is skipped, and a warning is surfaced (count reported), consistent with FileTraceStorage semantics

### Requirement: RunDiffer produces structured cross-run comparison

`RunDiffer.Diff(runA, runB)` SHALL produce a `RunDiff` with: step-level differences (added / missing / reordered steps between A and B), metric deltas (steps / scrolls / actions / duration), AI-level comparison (capability distribution, average latency change), and a one-line conclusion. `RunDiffer` SHALL report behavioral difference via exit code 1 when used by the `diff` command.

#### Scenario: diff identifies missing steps
- **WHEN** run B has fewer steps than run A
- **THEN** RunDiff lists the steps present in A but missing in B

#### Scenario: diff computes metric deltas
- **WHEN** both runs have result.json
- **THEN** RunDiff contains numeric deltas for steps, scrolls, actions, and duration

#### Scenario: diff detects regression
- **WHEN** `uni-claw trace diff --run-a <a> --run-b <b>` finds any step or metric difference
- **THEN** exit code is 1 and the conclusion line summarizes the change

### Requirement: diagnose rule engine reuses existing Host analyzers

`diagnose` SHALL reuse `CompletionMonitor`, `ErrorLoopAnalyzer`, and the VerificationAnalyzer classification (FailingStep / FailureCause / IssueFingerprints) from run artifacts. TraceTool SHALL only add aggregation rules (ai_call_failures grouping, timeline gaps). The TUI and the `diagnose` command SHALL share the same rule engine — a single conclusion source.

#### Scenario: diagnose classifies known failure pattern
- **WHEN** a run has a stuck error loop (≥5 consecutive skipped steps exceeding visited×4)
- **THEN** diagnose reports cause "error_loop_stuck" with the failing step and evidence list

#### Scenario: diagnose aggregates AI call failures
- **WHEN** a run contains AICallRecords with Success=false for one capability
- **THEN** the verdict evidence includes an ai_call_failure entry grouping by capability

#### Scenario: TUI and CLI share conclusions
- **WHEN** the TUI shows a step's diagnosis
- **THEN** it uses the same rule engine output as `diagnose --format json`
