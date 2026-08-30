# Tasks — runtime-debug-p4c-minimize

## 1. Core + CLI

- [x] 1.1 Add `minimize_fixture` (validation+projection first; greedy backwards; failing step fixed; no-op on clean fixtures; never mutates)
- [x] 1.2 Add `minimize <fixture>` CLI sharing the replay reader/validator

## 2. Contract verification

- [x] 2.1 Add 4 tests (mechanical minimal slice; no-failure no-op; read-only; missing fail-closed)
- [x] 2.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/replay.py` + `cli.py` | `openspec/changes/runtime-debug-p4c-minimize/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p4c-minimize/design.md` |
