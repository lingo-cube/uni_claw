# CORE-014 — 恢复消费编排（docket：恢复入口 / pending 消费裁决）

lifecycle_state: planned · disposition: none · depth: decision-heavy · base: working-tree
triage_label: awaiting-human-review
parent_change: CORE-013
adr: docs/adr/0023-reliable-execution-source-is-effect-boundary-owned-with-append-only-file-journal.md

## Intent

裁决 CORE-013 之后执行源只写面的恢复消费问题：谁触发 DiscoverPending、
跨 Run pending 的身份、v1 合法后继动作范围、journal 生命周期归属。
按建议方案（Q1=c/Q2=B/Q3=B/Q4=B），结论是「零产品代码 + 约束记档」——
恢复编排的决策系统（自动重试/补偿策略）与 kernel 内自动化均无真实
buyer，deferred。

## Scope

- 产出四问 docket（仓库事实 + 选项 + 建议裁决）。
- 记录用户对 `DECIDE-Q1..Q4` 的裁决；偏离建议方案时的实现授权另行
  单独给出。

## Out of Scope（授权前禁止）

- 修改 `src/`；新增自动重试/补偿策略；P25（AgentDecisionContext）
  扩展；KernelRunDriver 新 phase；Product Host 组合根。

## Decisions

pending——见 docket 的 `DECIDE-Q1..Q4` 槽位与建议摘要。

## Verification

```yaml
level: CONTRACT
method: docket 复审（ADR-0023 约束映射 + 仓库 buyer 事实核对）
expected: 四问各有显式裁决；若零代码方案成立则本 Change 直接收口
actual: pending human review
evidence: evidence/2026-09-19-core-014-human-review-docket.md
```

## Status log

2026-09-19 · created · ADR-0023 + CONTEXT 落档后起草恢复消费 docket；
CORE-014 保持 planned，等待用户逐问裁决。
