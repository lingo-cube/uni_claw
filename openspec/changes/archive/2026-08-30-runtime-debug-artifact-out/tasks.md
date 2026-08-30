# Tasks — runtime-debug-artifact-out

## 1. CLI

- [x] 1.1 Add `--out` to `packet-generate` and `replay-extract`; `_write_artifact` helper (bundle-external, no-overwrite, atomic)
- [x] 1.2 README notes

## 2. Contract verification

- [x] 2.1 Add 4 tests (round-trips ×2; bundle-internal rejected; overwrite rejected)
- [x] 2.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/cli.py` | `openspec/changes/runtime-debug-artifact-out/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-artifact-out/design.md` |
