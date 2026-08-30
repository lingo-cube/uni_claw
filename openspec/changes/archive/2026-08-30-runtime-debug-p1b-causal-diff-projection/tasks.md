# Tasks — runtime-debug-p1b-causal-diff-projection

## 1. Query Core projections

- [x] 1.1 Add `causal_tree(packet, prune, only_decisions, only_evidence)` — ordered stage projection with closed vocabulary, prune-only semantics, decision/evidence filters, `INSUFFICIENT_TRACE_COVERAGE` on absent chain
- [x] 1.2 Add `evidence_chain(packet, ref_id)` — per-stage role/status positions + ref metadata; `EVIDENCE_UNAVAILABLE` on unknown ref; stored `IDENTITY_MISMATCH` surfaced; no URI dereference
- [x] 1.3 Add `compare(packet)` — stored Good/Bad comparison + LastGood/FirstBad projection (stored facts only)

## 2. CLI surface

- [x] 2.1 Register `trace <packet> [--prune --only-decisions --only-evidence]`, `evidence <packet> --evidence-ref`, `diff <packet>` as thin adapters to the Query Core
- [x] 2.2 All commands use the canonical envelope / closed statuses / exit-code mapping; no logic in the CLI

## 3. Contract verification

- [x] 3.1 Add contract tests: causal order, prune (reported + byte-immutable packet), only-decisions, evidence chain success + unknown ref, diff projection + closed-status on missing facts
- [x] 3.2 Run the full AgentWorkflow suite (uv pytest), strict OpenSpec validation, and repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/query.py` | `openspec/changes/runtime-debug-p1b-causal-diff-projection/design.md` |
| `tools/runtime_debug/cli.py` | `openspec/changes/runtime-debug-p1b-causal-diff-projection/design.md` |
| `tests/AgentWorkflow/test_runtime_debug_cli.py` | `openspec/changes/runtime-debug-p1b-causal-diff-projection/design.md` + P0 acceptance fixtures |