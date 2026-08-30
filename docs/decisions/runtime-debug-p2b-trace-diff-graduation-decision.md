# Runtime Debug P2b — Trace Diff (packet × packet) — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P2B-TRACE-DIFF` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p2b-trace-diff/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain P2 分析面（结构化差分 + 终结因果链）。

双 EvidencePacket 的 EvidenceChain 逐节差分（present/statusAxis/refsAxis，UNCHANGED/CHANGED/ADDED/REMOVED）、firstMechanicallyChangedStage、refs goodOnly/badOnly、双包 stored LastGood/FirstBad 原样投影；缺链 fail-closed INSUFFICIENT_TRACE_COVERAGE。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**207 passed + 13 subtests**（本片测试在列，CLI 测试文件 39 项）。
- 契约测试覆盖：checkbox vs fusion-noop：raw 判机械首变、canonical UNCHANGED、双包 LastGood/FirstBad 投影；生成包（无链）fail-closed；缺失包 EVIDENCE_UNAVAILABLE。
- `openspec validate runtime-debug-p2b-trace-diff` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

机械首变显式标注（不推断语义首变）；链序确定性（good 序 + bad 独有追加）；仅投影 stored 事实。

## 4. Deferred scope

语义 first-change 判定（Agent）、跨链语义对齐。

## 5. Final conclusion

**GRADUATED.** P2 该切片已验证并归档。
