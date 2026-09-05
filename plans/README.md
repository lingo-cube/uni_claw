# plans/ — Plan 工件

> Durable Artifact：Plan 阶段的产物；ToWorkItems 的输入。

## 收录范围

- 架构计划（含 before/after 与迁移步骤）。
- 模块/结构设计（codebase-design 产出）。
- 领域模型记录（domain-modeling 产出，未冻结前暂存）。
- WorkItem DAG 总览（WorkItem 明细在 workitems/）。

## 命名

`YYYY-MM-DD-<slug>.md`，文件头声明 `> PlanType / Status: DRAFT | ADOPTED |
SUPERSEDED / References`。

## 规则

- Plan 引用 docs/adr/ 中的既有 ADR，不得与之冲突；冲突即停止并升级。
- Plan 不是实现授权；实现必须经 workitems/ 中的 WorkItem。
- 被采纳的 Plan 变更需记录修订，不静默改写。
