# Tasks — runtime-debug-p4a-replay-facts

## 1. Core

- [x] 1.1 Add `replay.py`: `build_replay_fixture` (steps/AssetRefs/trace summary/digest) + `validate_replay_fixture` (fail-closed) + `read_fixture_file`
- [x] 1.2 Keep minimization a reserved contract (no code in this slice)

## 2. CLI + verification

- [x] 2.1 Add `replay-extract <bundle> --case-id X` and `replay <fixture.json>` commands
- [x] 2.2 Add 4 contract tests (round-trip, determinism, malformed SCHEMA_VIOLATION, missing EVIDENCE_UNAVAILABLE)
- [x] 2.3 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/replay.py` + `cli.py` | `openspec/changes/runtime-debug-p4a-replay-facts/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p4a-replay-facts/design.md` |
