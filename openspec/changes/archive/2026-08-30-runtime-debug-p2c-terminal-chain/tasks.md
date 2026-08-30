# Tasks — runtime-debug-p2c-terminal-chain

## 1. Core + CLI

- [x] 1.1 Add `query.terminal_chain` (TerminalState, ordered stages, LastGood/FirstBad, storedDiagnostics marked STORED)
- [x] 1.2 Add `terminal-chain <packet>` CLI

## 2. Contract verification

- [x] 2.1 Add tests: historical packet full projection + STORED marker; generated packet terminal-only with empty chain/diagnostics
- [x] 2.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/query.py` + `cli.py` | `openspec/changes/runtime-debug-p2c-terminal-chain/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p2c-terminal-chain/design.md` |
