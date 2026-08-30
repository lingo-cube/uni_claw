# Tasks — runtime-debug-p2d-execution-tree

## 1. Trace source + Core

- [x] 1.1 Add `observability-trace.json` reading to the bundle adapter (camelCase TraceRun, fail-closed schema validation, optional presence)
- [x] 1.2 Add `query.execution_tree` (EXECUTION nesting; structural absolute prune; filter prune with spine; stats + pruned summary; `EVIDENCE_UNAVAILABLE` without trace)

## 2. CLI + verification

- [x] 2.1 Add `execution-tree` CLI command with all prune flags
- [x] 2.2 Add 5 contract tests (shape; subtree cut + byte-immutability; only-errors spine; time window; no-trace fail-closed)
- [x] 2.3 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/sources/bundle.py` + `query.py` + `cli.py` | `openspec/changes/runtime-debug-p2d-execution-tree/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p2d-execution-tree/design.md` |
## 3. End-to-end acceptance (Foundation benchmark)

- [x] 3.1 Add `EndToEndDiagnosisChainTests`: good/bad bundle pair → assets → packet-generate → run-compare → execution-tree --only-errors → terminal-chain assemble deterministic diagnosis material (no source reading, no inference)
