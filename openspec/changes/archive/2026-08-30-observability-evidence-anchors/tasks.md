# Tasks — observability-evidence-anchors

## 1. Emission

- [x] 1.1 ObserveAsync span: `observation.seq` + `observation.frame` tags (fail-open)
- [x] 1.2 ExecuteAsync span: `action.kind` tag

## 2. Toolchain consumption

- [x] 2.1 execution-tree: anchor extraction + span→AssetRef join by observation seq (sorted; empty when none)
- [x] 2.2 tree_view: pass observationSeq / frameAssetRefs / actionKind

## 3. Verification

- [x] 3.1 C# test: observe/execute anchor attributes (ambient-root recorder)
- [x] 3.2 Python tests: anchors surfaced + AssetRef join (with/without asset)
- [x] 3.3 Full AgentWorkflow (uv pytest), C# Observability scope, strict OpenSpec validation, consistency checks

## Design Docs

> Auto-generated from proposal Impact section.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime.Adapters/PhysicalEnvironment.cs` | `openspec/changes/observability-evidence-anchors/design.md` |
| `tools/runtime_debug/query.py` · `tui/view_models.py` | `openspec/changes/observability-evidence-anchors/design.md` |
| 测试 | `openspec/changes/observability-evidence-anchors/design.md` |
