# Runtime Debug P2c — Terminal Chain — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P2C-TERMINAL-CHAIN` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p2c-terminal-chain/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain P2 分析面（结构化差分 + 终结因果链）。

P0 packet 机械终结因果链视图：stored TerminalState + 有序链节 + LastGood/FirstBad + storedDiagnostics（GapKind/Owner/Disposition/Confidence 仅当 stored 时投影并标注 STORED，Owner 限 status/domain/seam/basis）；生成包 terminal-only + 诚实空链/空诊断。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**207 passed + 13 subtests**（本片测试在列，CLI 测试文件 39 项）。
- 契约测试覆盖：checkbox fixture 全字段（7 节链、firstBad=semanticAdmission、GapKind=CONTRACT_REGRESSION、Owner.domain、note 标注 STORED）；生成包 terminal-only。
- `openspec validate runtime-debug-p2c-terminal-chain` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

绝不重算诊断（STORED 标记 + note 纪律）；无数据=空投影而非 SCHEMA 失败（与 malformed 区分）。

## 4. Deferred scope

跨 run 诊断合成、自动修复判定。

## 5. Final conclusion

**GRADUATED.** P2 该切片已验证并归档。
