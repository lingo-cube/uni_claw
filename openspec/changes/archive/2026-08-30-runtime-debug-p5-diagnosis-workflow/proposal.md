## Why

Foundation §14 P5：failure → automatic tooling → Debug IR → diagnosis → Gate。P5 最小面：`diagnose` 一键聚合既有 Core 产出（结构差/生成包/失败 span/replay 事实）+ `evidence_gate` 投影 §12 实现门（FDP/Owner/EvidenceRefs 存在性 → EVIDENCE_COLLECTION/INSUFFICIENT_EVIDENCE），并把工具链命令序列写进 evidence-driven-debugging Skill 的 routing reference——Agent/Harness 不再手工拼命令。

## What Changes

- `workflow.py`（纯函数，零分析零 authority）：
  - `diagnose_workflow(good_bundle, bad_bundle, case_id, minimize=False)` 聚合：run-compare 三轴、packet-generate、execution-tree --only-errors 的 FAILED spans（递归收集）、replay extract/dry-run/（可选 minimize）；
  - `evidence_gate(report)`：确定性投影 fdpPresent（axes CHANGED ∨ failedSpans ∨ dry-run 机械失败）、ownerPresent（stored Owner seam/domain）、evidenceRefsPresent（evidenceIndex 非空）→ disposition ∈ {EVIDENCE_COLLECTION, INSUFFICIENT_EVIDENCE} + blockedBy；显式 note：门是投影，语义 FDP/Owner/GapKind 判定归 Agent。
- CLI `diagnose <good-bundle> <bad-bundle> --case-id X [--minimize]`。
- Skill 路由接线：`.ai/skills/evidence-driven-debugging/references/runtime/toolchain-routing.md`（命令序列 + gate 语义 + NO_FDP/NO_OWNER 规则）。
- 契约测试 3 项：red pair 聚合+gate（EVIDENCE_COLLECTION + GAPKIND_UNKNOWN blocker）、无事实 → INSUFFICIENT_EVIDENCE+FDP_ABSENT、gate 为投影非权威。

## Capabilities

### New Capabilities

- `runtime-debug-diagnosis-workflow`: 一键诊断素材聚合 + §12 evidence gate 的确定性投影 + Skill 路由引用。

### Modified Capabilities

无。

## Impact

- `tools/runtime_debug/workflow.py` + `cli.py` + README + skill reference（新增路由文档）。
- `tests/AgentWorkflow/test_runtime_debug_cli.py` +3 契约测试。
- 无 Runtime/Harness/wire/Trace 变更；无新依赖。
