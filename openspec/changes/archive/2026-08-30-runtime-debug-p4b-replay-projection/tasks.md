# Tasks — runtime-debug-p4b-replay-projection

## 1. Core + CLI

- [x] 1.1 Add `project_replay_run` (validation-first; trajectory/counts/mechanical first failure)
- [x] 1.2 Add `replay-run <fixture>` CLI sharing the replay reader/validator

## 2. Contract verification

- [x] 2.1 Add 3 tests (trajectory+failure; clean fixture; missing fail-closed)
- [x] 2.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/replay.py` + `cli.py` | `openspec/changes/runtime-debug-p4b-replay-projection/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p4b-replay-projection/design.md` |
