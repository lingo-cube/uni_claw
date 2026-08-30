# Tasks — runtime-debug-p3-tui

## 1. View models (stdlib, testable)

- [x] 1.1 Add `tui/view_models.py`: open_run / tree_view / filter_state / diagnosis_view (all derived from Core; no framework import)
- [x] 1.2 Add `tui/__init__.py` re-exports

## 2. Shell

- [x] 2.1 Add `tui/app.py`: textual deferred-import shell (EXECUTION/CAUSAL trees, errors-only, AssetRef panel, diagnosis panel, quit)
- [x] 2.2 Add `tools/runtime-debug-tui` entry + README notes

## 3. Contract verification

- [x] 3.1 Add 5 view-model/framework-isolation tests
- [x] 3.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/tui/` | `openspec/changes/runtime-debug-p3-tui/design.md` |
| `tools/runtime-debug-tui` | `openspec/changes/runtime-debug-p3-tui/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p3-tui/design.md` |
