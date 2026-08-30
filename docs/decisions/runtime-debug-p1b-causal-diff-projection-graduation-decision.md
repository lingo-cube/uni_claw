# Runtime Debug P1b — Causal Tree / Evidence Chain / Diff — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P1B-CAUSAL-DIFF-PROJECTION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p1b-causal-diff-projection/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain 只读确定性查询/投影面（Debug Query Core 的增量能力）。

EvidenceChain→causal/evidence 树（prune-only、--only-decisions/--only-evidence）、EvidenceRef 跨 stage 链查询、packet-scoped Good/Bad+LastGood/FirstBad stored-facts 差分投影。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**199 passed + 13 subtests**（本片测试在列）。
- 契约测试覆盖：同套件全绿；causal 顺序/prune 字节不变/decision 过滤、chain 成功与未知 ref、diff 投影均断言；closed statuses 全走 envelope。
- `openspec validate runtime-debug-p1b-causal-diff-projection` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

prune 仅隐藏（输入字节不可变）；diff 只投影 stored 事实不计算；chain 不 dereference URI；`INSUFFICIENT_TRACE_COVERAGE` fail-closed。

## 4. Deferred scope

EXECUTION tree（需 span 数据源）、multi-run compare、FDP/Owner 计算（属 Agent diagnosis）。

## 5. Final conclusion

**GRADUATED.** 本切片对应能力已验证并归档；deferred 项需各自独立 gate。
