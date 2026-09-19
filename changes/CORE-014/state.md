# CORE-014 — 恢复消费编排（docket：恢复入口 / pending 消费裁决）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: done
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

## Decisions（2026-09-19 用户裁决，基本认同四项建议）

### DECIDE-Q1 — 恢复入口：c，批准（含用户补充）

kernel 零改动；恢复消费归 Host/UniAgent 编排。**用户补充（buyer 形态）**：
恢复消费将以 **Session 层**为基础组织——Session 及其关联元数据承载恢复
上下文。边界精确化：Trace 可作为 Session 关联的诊断/关联材料
（correlation context），但**可靠恢复依据仍是执行源 journal**
（CORE-009 契约不变量 + ADR-0023：Trace 非恢复权威、与执行源零供给
边界不变）。KernelRunDriver 新 phase 仅记为有真实 buyer 后的演进。

### DECIDE-Q2 — pending 身份：B，批准

advisory-only：pending 不进 Run State、不进 AgentDecisionContext；
需要时走既有 re-observe → re-ground → 新 binding 路径。

### DECIDE-Q3 — v1 合法后继：B，批准

发现 + 查询 + 显式未决声明（AppendReceipt null）；LinkRetry /
LinkCompensation 决策逻辑 deferred（策略从未被裁决，不为无需求实现）。

### DECIDE-Q4 — journal 生命周期：B，批准

产品默认（必注入与否、路径/retention）deferred 至 Product Host Change；
生命周期归 composition root，不归执行源自身。

## 零代码收口

四项裁决合计 = 零产品代码改动；本 Change 以决策记录收口，无实现轮。

## Verification

```yaml
level: CONTRACT
method: docket 复审（ADR-0023 约束映射 + 仓库 buyer 事实核对）
expected: 四问各有显式裁决；若零代码方案成立则本 Change 直接收口
actual: >
  四问裁决已记录（Q1=c+Session 层补充 / Q2=B / Q3=B / Q4=B）；
  零产品代码，本 Change 收口
evidence: evidence/2026-09-19-core-014-human-review-docket.md
```

## Status log

2026-09-19 · created · ADR-0023 + CONTEXT 落档后起草恢复消费 docket；
CORE-014 保持 planned，等待用户逐问裁决。
2026-09-19 · decisions-recorded · closed · 用户基本认同四项建议并补充
Q1 buyer 形态：恢复消费基于 Session 层及其关联元数据组织，Trace 仅作
诊断/关联材料（可靠恢复依据 = 执行源 journal，边界不变）。四项合计
零代码，本 Change 收口；自动重试/补偿策略与 Product Host journal
默认保持 deferred。
