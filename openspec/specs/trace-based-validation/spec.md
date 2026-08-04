## ADDED Requirements

### Requirement: Validation moves out of Host — run ends pending, TraceTool judges

At run end, Host SHALL write result.json with `status="pending_verification"` + engine facts, and write the verificationCriteria snapshot (expectedPageIdentities/mode) to a separate `criteria.json`. Host SHALL delete the ScenarioCompletionVerifier locate branch (D-201 semantics ported to TraceTool); the enumerate branch SHALL remain untouched. Final success/failure judgment SHALL be produced by the TraceTool rule engine, not by Host.

#### Scenario: run ends pending_verification

- **WHEN** a locate-mode run finishes successfully from the engine's perspective
- **THEN** result.json status is `pending_verification` and criteria.json exists with the verification snapshot

#### Scenario: judgment comes from TraceTool

- **WHEN** `trace verify` runs on the finished run
- **THEN** result.json status is rewritten to `success` or `failure` (aligned with `RunAssetVocabulary.ResultStatuses`) based on the rule engine verdict — Host never wrote the final judgment

### Requirement: VerifyEngine runs a deterministic rule engine

TraceTool SHALL provide `VerifyEngine.VerifyAsync(TraceRun)` evaluating an `IVerificationRule` list; MVP SHALL be `LocateOneItemRule` — D-201 identity-fallback semantics ported verbatim: last analysis.jsonl row Items match expectedPageIdentities; targetActionExecuted = completionReason==target_found && a successful action exists. `click_target_matches_identity` evidence SHALL read trace `safety.*` events (normalizedTarget == expected identity row).

#### Scenario: success verdict

- **WHEN** the last analysis row matches an expected identity and the target action executed
- **THEN** verdict is verified with confidence high

#### Scenario: identity fallback preserved

- **WHEN** the first-match identity fails but a fallback identity from the expected set matches
- **THEN** the rule applies D-201 fallback semantics and still verifies

#### Scenario: not verified verdict with failing step

- **WHEN** the post-action identity does not match any expected identity
- **THEN** verdict cause is `target_page_identity_not_verified` with failingStep and a summary

### Requirement: verify/watch commands follow a stable contract

`trace verify --run <dir>` SHALL verify one run (status unrestricted). `trace verify --dir <root> [--status pending] [--task-id <id>]` SHALL batch-verify runs idempotently (pending only). `trace watch --run-id <id> --dir <root> [--interval 5s]` SHALL locate the run (leaf dir name == runId; >1 match → error asking for explicit path), poll until result.json shows `pending_verification` (P3: final state ⇒ assets complete), auto-verify, print verdict, and exit with verify's exit code. Exit codes SHALL be: 0 = verified · 1 = not_verified · 2 = usage/dir error · 3 = evidence missing. stdout SHALL be a single JSON document (schemaVersion "1") with `--format json`.

#### Scenario: batch verify processes pending runs only

- **WHEN** `verify --dir <root>` runs over a root containing verified, failed, and pending runs
- **THEN** only pending runs are re-verified and already-final runs are untouched

#### Scenario: watch waits for run completion

- **WHEN** `watch --run-id <id>` runs while the run is still executing
- **THEN** it polls at the interval and verifies automatically once result.json shows `pending_verification`

#### Scenario: watch resolves ambiguous run id as error

- **WHEN** more than one directory named `<id>` exists under the root
- **THEN** watch exits with a usage error asking for an explicit run path

### Requirement: Writeback is idempotent and protects final states

Writeback SHALL update result.json atomically (tmp+move, verify fields only) and append issues.jsonl on failure. Writeback SHALL happen **only when status == `pending_verification`** — `verify --run` on a non-pending run SHALL still compute and print the verdict but SHALL NOT write back (final state never overwritten). Batch mode SHALL re-read status before writeback.

#### Scenario: verify --run on a final run reports without writing

- **WHEN** `verify --run` runs on a run already marked success
- **THEN** the verdict is printed but result.json is not modified

#### Scenario: double verification never double-writes

- **WHEN** two verify invocations run against the same pending run
- **THEN** only the first writes back (the second sees a non-pending status and reports only)

### Requirement: Evidence-missing gate distinguishes pipeline failure from no output

When the last analysis.jsonl row is absent, verify SHALL return `evidence_missing` (exit 3). Attribution SHALL read issues.jsonl (`asset_write_failed`) or the `assets.sink_failure` trace event to distinguish pipeline failure from run no-output.

#### Scenario: no analysis output yields exit 3

- **WHEN** a run has no analysis.jsonl rows
- **THEN** verify exits 3 with an evidence_missing verdict

#### Scenario: pipeline failure attribution

- **WHEN** evidence is missing and issues.jsonl contains `asset_write_failed`
- **THEN** the verdict's attribution distinguishes the pipeline failure from a run that produced no output

### Requirement: Read-side assembly uses CLI params with run metadata as reference

Read-side query assembly SHALL take CLI params as the config (position arg explicit/required; backend default not fixed — normally specified per use). Run metadata (manifest: scenarioId/mode/taskId/providerId/model) SHALL serve as assembly reference/defaults (e.g. `--task-id` omitted → manifest.taskId) — explicit CLI params always override; missing manifest fields fall back to defaults, never fail. The assembly function shape SHALL be retained so a future `--backend`/`--config` swaps only the assembly source.

#### Scenario: task-id defaults from manifest

- **WHEN** `verify --dir <root>` runs without `--task-id`
- **THEN** runs are filtered using the manifest taskId as the reference default

#### Scenario: explicit params override metadata

- **WHEN** `--task-id` is passed explicitly
- **THEN** it overrides the manifest-derived default

#### Scenario: missing metadata falls back

- **WHEN** a manifest lacks a taskId
- **THEN** assembly proceeds with the default behavior (no failure)
