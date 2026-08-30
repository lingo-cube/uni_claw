## ADDED Requirements

### Requirement: Shared single core for CLI and TUI
The `runtime-debug` CLI and the TUI Debug Console SHALL consume the same Query/Analysis core; neither SHALL implement its own correlation or analysis logic. CLI commands SHALL cover: `runs`, `run latest|summary|blockers|compare`, `trace tree|causal|query|path|ancestors|descendants`, `evidence occurrence|observation|chain|packet`, `diff observation|occurrence|trace`, `logs` (with --from/--to/--around/--span/--event/--observation/--occurrence/--owner/--type), `assets` / `asset show|related`, and `diagnose`. All commands SHALL be READ_ONLY and DETERMINISTIC, SHALL emit canonical JSON (Markdown only as a derived view), and SHALL use the closed status vocabulary.

#### Scenario: CLI diagnose is read-only
- **WHEN** `runtime-debug diagnose <run>` runs
- **THEN** it SHALL consume the same Query/Analysis core as any other surface and SHALL NOT start a Runtime, modify artifacts, or produce repair authorization

#### Scenario: TUI does not reimplement analysis
- **WHEN** a TUI panel (tree/timeline/filter/evidence/asset/logs/diff/diagnosis) renders a view
- **THEN** it SHALL query the shared core and SHALL NOT derive correlation or analysis locally