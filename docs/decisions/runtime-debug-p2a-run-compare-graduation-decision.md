# Runtime Debug P2a — Run Compare — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P2A-RUN-COMPARE` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p2a-run-compare/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain P2 分析面（结构化差分 + 终结因果链）。

双 capture bundle 的结构事实差分：terminal/records/assets 三轴 UNCHANGED/CHANGED、资产 added/removed/CHANGED（ArtifactId+sha256 对齐）、双 run deterministicInputDigest；显式不推断 FIRST_SEMANTICALLY_RELEVANT。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**207 passed + 13 subtests**（本片测试在列，CLI 测试文件 39 项）。
- 契约测试覆盖：AgentWorkflow 207 passed + 13 subtests；结构差异（axes CHANGED、added=[x-extra]、changedOrSame 含 CHANGED）、全同 bundle 三轴 UNCHANGED、缺失 bundle EVIDENCE_UNAVAILABLE。
- `openspec validate runtime-debug-p2a-run-compare` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

只投影 stored 事实；资产按 id+hash 机械对齐；fail-closed 配对；不推断语义变化点。

## 4. Deferred scope

语义首变推断、时序比较（bundle 无时间戳）、跨 packet 对齐（P2b 覆盖）。

## 5. Final conclusion

**GRADUATED.** P2 该切片已验证并归档。
