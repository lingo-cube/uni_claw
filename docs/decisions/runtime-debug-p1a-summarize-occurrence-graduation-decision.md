# Runtime Debug P1a — Summarize / Occurrence — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P1A-SUMMARIZE-OCCURRENCE` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p1a-summarize-occurrence/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain 只读确定性查询/投影面（Debug Query Core 的增量能力）。

P0 Evidence Packet 上 `summarize`（terminal/target/evidenceAvailability/missing/blockers 受限投影）与 typed `occurrence` 查询的只读确定性实现；closed statuses、canonical envelope、输入字节不可变。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**199 passed + 13 subtests**（本片测试在列）。
- 契约测试覆盖：全量 AgentWorkflow 199 passed + 13 subtests；P0 五 fixtures 全过 reader；负例（坏包/缺失/多 selectors/未知 ref/歧义）fail-closed；stdlib-only、无 Runtime/device 访问。
- `openspec validate runtime-debug-p1a-summarize-occurrence` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

identity 不升级（StableKey/RowId 仅候选关联、AMBIGUOUS_OCCURRENCE fail-closed）；envelope 无绝对路径/时间戳；未计算 FDP/Owner/Disposition。本片不实现 trace-diff/terminal-chain/packet-generator（后续分片）。

## 4. Deferred scope

trace-diff / terminal-chain / packet generator / 多 run 输入 / TUI / replay。

## 5. Final conclusion

**GRADUATED.** 本切片对应能力已验证并归档；deferred 项需各自独立 gate。
