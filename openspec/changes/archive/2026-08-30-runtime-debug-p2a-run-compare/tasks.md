# Tasks — runtime-debug-p2a-run-compare

## 1. Core + CLI

- [x] 1.1 Add `query.compare_bundles` (terminal/records/assets axes; digests; explicit no-semantic-inference note)
- [x] 1.2 Add `run-compare <good-bundle> <bad-bundle>` CLI (fail-closed pairing)

## 2. Contract verification

- [x] 2.1 Add tests: structural diff axes + added/changed assets, identical bundles all-UNCHANGED, missing bundle `EVIDENCE_UNAVAILABLE`
- [x] 2.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/query.py` + `cli.py` | `openspec/changes/runtime-debug-p2a-run-compare/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p2a-run-compare/design.md` |
