# Runtime Debug P4c — Minimize — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P4C-MINIMIZE` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p4c-minimize/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain minimize 面（P4 收尾；RED 循环的 falsifier 前提）。

replay fixture 的确定性机械最小失败保留切片（minimize）：复用 P4b 失败谓词（firstMechanicallyFailedStep 不变=仍失败），失败步固定、尾随步丢弃、向前贪心试删；无失败 no-op；只读。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**249 passed + 1083 subtests**。
- AgentWorkflow 249 passed + 1083 subtests；Rejected fixture 机械最小=[Rejected]（removed 1/2/4）、no-failure no-op、只读、缺失 fail-closed。
- `openspec validate runtime-debug-p4c-minimize` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

minimal ≠ 语义最小（显式 note，语义充分性 deferred）；同一失败规则跨 replay/minimize 复用；零仿真/变异。

## 4. Deferred scope

语义充分性判定、可执行重放引擎、RED→repair→GREEN 自动循环。

## 5. Final conclusion

**GRADUATED.** P4c 已验证并归档。
