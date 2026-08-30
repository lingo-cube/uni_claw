# Tasks — runtime-debug-p5-diagnosis-workflow

## 1. Workflow + gate

- [x] 1.1 Add `workflow.py`: `diagnose_workflow` (compose compare/packet/tree/replay/minimize; recursive failed spans) + `evidence_gate` (projection; EVIDENCE_COLLECTION / INSUFFICIENT_EVIDENCE)
- [x] 1.2 Add CLI `diagnose <good> <bad> --case-id X [--minimize]`

## 2. Skill routing + verification

- [x] 2.1 Add `.ai/skills/evidence-driven-debugging/references/runtime/toolchain-routing.md` (command sequence + gate + NO_FDP/NO_OWNER)
- [x] 2.2 Add 3 contract tests (aggregation+gate, insufficient facts, projection-not-authority)
- [x] 2.3 Run full AgentWorkflow suite (uv pytest), strict OpenSpec validation, repository consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `tools/runtime_debug/workflow.py` + `cli.py` | `openspec/changes/runtime-debug-p5-diagnosis-workflow/design.md` |
| `.ai/skills/evidence-driven-debugging/references/runtime/toolchain-routing.md` | `openspec/changes/runtime-debug-p5-diagnosis-workflow/design.md` |
