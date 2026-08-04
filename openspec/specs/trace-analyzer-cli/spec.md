## ADDED Requirements

### Requirement: trace CLI exposes 8 subcommands (formerly: trace CLI exposes 6 subcommands)

`uni-claw trace` SHALL expose exactly 8 subcommands:
- `list` — discover run directories (`--dir`, `--status`, `--task-id`, `--limit`)
- `timeline` — step timeline + AI latency distribution (`--run`, `--threshold`)
- `diagnose` — fault root-cause inference (`--run`)
- `diff` — cross-run structured comparison (`--run-a`, `--run-b`)
- `report` — Markdown / Mermaid export (`--run`, `--format`, `--out`)
- `interactive` — Terminal.Gui TUI browser (`--run`)
- `verify` — verification rule engine over a run (`--run`, `--dir`, `--status`, `--task-id`) — full contract defined in `trace-based-validation`
- `watch` — poll a run-id until pending_verification then auto-verify (`--run-id`, `--dir`, `--interval`) — full contract defined in `trace-based-validation`

All subcommands SHALL accept `--format json` (default human-readable table). Non-TTY output SHALL be free of table decoration.

#### Scenario: list discovers runs with filters
- **WHEN** `uni-claw trace list --dir artifacts/runs --status failure --limit 10` is invoked
- **THEN** it prints at most 10 failed runs with status, duration, and taskId

#### Scenario: timeline shows step table
- **WHEN** `uni-claw trace timeline --run <dir> --threshold 10` is invoked
- **THEN** it prints one row per engine.step with duration and AI call count, highlighting steps exceeding the threshold

#### Scenario: non-TTY output is decoration-free
- **WHEN** a command runs with stdout redirected to a pipe
- **THEN** no ANSI/box-drawing characters are emitted

#### Scenario: verify command listed and usable
- **WHEN** `uni-claw trace verify --run <dir>` is invoked on a pending run
- **THEN** the verify subcommand executes per the trace-based-validation contract

### Requirement: --format json emits stable machine-readable schema

`--format json` output SHALL be a single JSON document on stdout containing `schemaVersion` (current "1"). Logs and warnings SHALL go to stderr only. `diagnose --format json` SHALL include: `runId`, `status`, `run` context object (`runId`, `taskId`, `purpose`, `system`, `machine`), `verdict` (`cause`, `failingStep`, `summary`, `confidence`), `evidence` array (bounded, default max 5 entries), `suggestions` array, `artifactPaths` object.

#### Scenario: diagnose json carries run context
- **WHEN** `uni-claw trace diagnose --run <dir> --format json` is invoked on a failed run
- **THEN** stdout is valid JSON with schemaVersion, status, verdict, and run.system populated from manifest when present

#### Scenario: evidence bounded
- **WHEN** a run has more than 5 evidence items
- **THEN** the JSON evidence array contains at most 5 items

#### Scenario: stdout/stderr separation
- **WHEN** a json command emits a warning about a corrupt trace line
- **THEN** stdout contains only the JSON document and the warning appears on stderr

### Requirement: exit codes follow a stable contract (formerly: exit codes follow a stable contract)

For the analyze command family (list/timeline/diagnose/diff/report/interactive), exit codes SHALL be: 0 = success (diagnosis completed); 1 = `diff` detected behavioral differences; 2 = usage error or run directory not found; 3 = empty trace (no spans). The `verify`/`watch` commands SHALL follow their own contract defined in `trace-based-validation`: 0 = verified · 1 = not_verified · 2 = usage/dir error · 3 = evidence missing.

#### Scenario: diff regression signals via exit code
- **WHEN** `uni-claw trace diff --run-a <a> --run-b <b>` finds step or metric differences
- **THEN** exit code is 1

#### Scenario: missing run directory fails fast
- **WHEN** `uni-claw trace diagnose --run <nonexistent>` is invoked
- **THEN** exit code is 2 and an error message is printed to stderr

#### Scenario: empty trace exit code
- **WHEN** a run directory contains no trace spans
- **THEN** exit code is 3 with a "no spans found" message

#### Scenario: verify exit codes are distinct from analyze family
- **WHEN** `uni-claw trace verify --run <dir>` returns a not_verified verdict
- **THEN** exit code is 1 and when evidence is missing exit code is 3 (per trace-based-validation, not the analyze-family semantics)

### Requirement: interactive TUI refuses non-terminal environments

`interactive` SHALL detect `TERM=dumb` (or non-TTY stdin) and refuse to start, printing an error and exiting 2. The TUI SHALL show a step list (left) with duration-highlighted slow steps and a detail pane (right) with the selected step's AI calls and screenshot path. Screenshot SHALL be opened via the system viewer (`open` on macOS, `xdg-open` on Linux), not embedded.

#### Scenario: TUI starts in terminal
- **WHEN** `uni-claw trace interactive --run <dir>` runs under a real terminal
- **THEN** the TUI renders the step list and detail panes

#### Scenario: TUI refuses dumb terminal
- **WHEN** `uni-claw trace interactive --run <dir>` runs with `TERM=dumb`
- **THEN** it exits 2 without initializing the UI

#### Scenario: screenshot opens in system viewer
- **WHEN** the user presses Enter on a step with a screenshot
- **THEN** the system viewer command opens the screenshot path

### Requirement: diagnose supplements issue fingerprints from issues.jsonl

`diagnose` SHALL include an `issue_fingerprints` evidence entry when the run has issue records AND `result.json`'s `issueFingerprints` is empty. The evidence text SHALL carry the fingerprint and the issue summary, which embeds the D-192 failure detail (e.g. `target_page_identity_not_verified: Post-action page identity '<empty>' did not match the scenario success identities.`) so the real failure reason is consumable without reading issues.jsonl directly. When `result.json` already carries fingerprints, issues.jsonl SHALL NOT duplicate them. If issues exist but provide no fingerprint (malformed/absent field), the evidence entry SHALL be omitted rather than emit an empty fingerprint.

#### Scenario: verification failure with empty result fingerprints gets issue evidence

- **WHEN** `uni-claw trace diagnose --run <dir> --format json` runs on a run whose result.json has empty `issueFingerprints` but issues.jsonl contains one verification issue with fingerprint + summary embedding the failure detail
- **THEN** the JSON `evidence` array contains an `issue_fingerprints` entry whose text includes the fingerprint and the issue summary, and the verdict confidence is raised above the empty-evidence floor

#### Scenario: result fingerprints present prevent duplication

- **WHEN** result.json's `issueFingerprints` is non-empty and issues.jsonl also exists
- **THEN** the evidence reflects only the result.json fingerprints (no duplicate entries from issues.jsonl)

#### Scenario: issues without fingerprints are omitted

- **WHEN** issues.jsonl entries lack a usable fingerprint
- **THEN** no `issue_fingerprints` evidence entry is emitted
