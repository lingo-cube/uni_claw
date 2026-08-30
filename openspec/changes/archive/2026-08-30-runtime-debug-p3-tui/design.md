# Design — runtime-debug-p3-tui

## Context

CLI covers the query surface; the Foundation mandates one shared core for CLI/TUI/Agent. Technical-stack survey: the repo's Python tools are stdlib-only with no dependency baseline; `textual 8.2.8` installs instantly via `uv run --with textual` and provides Tree/panels/bindings on the rich rendering base.

## Goals / Non-Goals

Goals: thin textual shell over one bundle; switchable EXECUTION/CAUSAL tree; errors-only filter; AssetRef panel; diagnosis panel (failed spans); quit. View models are pure and unit-tested.

Non-Goals: image rendering (terminal protocols differ; AssetRef metadata shown instead); full diff/compare interaction (available via CLI); multi-bundle navigation; editing/authority.

## Decisions

### D1 — textual confined to the shell; view models stdlib-only
**Decision:** `tui/view_models.py` imports only `runtime_debug.query`/`sources` (stdlib); `tui/app.py` defers the textual import into `main()`. Core package never imports textual.
**Why:** keeps the Core testable without the framework and the "one Core" rule enforceable by structure; matches the survey (rich/textual as a shell-only runtime dependency via `uv --with`).

### D2 — TUI data path is one hop from Core
**Decision:** every panel reads `view_models.open_run/tree_view/filter_state/diagnosis_view` whose inputs are Core results; no UI-local formulas. Errors-only toggles map to `filter_state(only_errors=…)` → `query.execution_tree` arguments identical to the CLI.
**Why:** guarantees CLI and TUI render the same facts (the gate's "共用同一个 Core" requirement).

## Risks / Trade-offs

- [Causal view on bundles shows honest absence] → bundles carry no packet chain; the TUI shows the seven MISSING stages rather than inventing a chain (consistent with the generator).
- [textual as a new dependency] → shell-only, optional, via `uv --with`; documented in README; Runtime/Core untouched.

## Migration Plan

None — additive entry; no impact on existing commands.

## Open Questions

None that would change the contract; image preview and multi-bundle tabs are follow-up shells.
