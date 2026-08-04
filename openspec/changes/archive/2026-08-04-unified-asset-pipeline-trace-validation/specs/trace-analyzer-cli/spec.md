## MODIFIED Requirements

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
