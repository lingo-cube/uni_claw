# Tasks — runtime-debug-p2b-trace-diff

## 1. Core + CLI

- [x] 1.1 Add `query.diff_packets` (stage axes, first mechanically changed stage, refs lists, stored LastGood/FirstBad projection, fail-closed no-chain)
- [x] 1.2 Add `trace-diff <good-packet> <bad-packet>` CLI (two-packet envelope source)

## 2. Contract verification

- [x] 2.1 Add tests: mechanical first change (raw), UNCHANGED stages, stored LastGood/FirstBad projection; generated packets → `INSUFFICIENT_TRACE_COVERAGE`; missing packet fail-closed
- [x] 2.2 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/query.py` + `cli.py` | `openspec/changes/runtime-debug-p2b-trace-diff/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p2b-trace-diff/design.md` |
