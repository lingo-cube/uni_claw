# Runtime Debug P4a — Replay Facts — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P4A-REPLAY-FACTS` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p4a-replay-facts/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain replay fixture 面（P4 最小切片）。

capture bundle → runtime-debug-replay.v0 fixture 的机械提取（steps=records/AssetRefs/trace 摘要/确定性 digest）与 fail-closed 校验/摘要（replay-extract / replay 命令）；minimize 明确保留为后续契约，本片不执行、不最小化。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**242 passed + 1083 subtests**。
- AgentWorkflow 242 passed + 1083 subtests；extract→validate 闭环（step/asset/span 数一致）、两次提取字节相同、损坏 SCHEMA_VIOLATION、缺失 EVIDENCE_UNAVAILABLE。
- `openspec validate runtime-debug-p4a-replay-facts` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

fixture 只含 stored facts（无推断状态）；digest 按 P0 约定；零回放执行、零变异。

## 4. Deferred scope

replay 执行引擎（P4b）、minimize（RED→repair→GREEN）、scope 语义字段（需生产者标注）。

## 5. Final conclusion

**GRADUATED.** P4a 已验证并归档。
