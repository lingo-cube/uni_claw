# workitems/ — WorkItem 派发载荷（Leader → SubAgent 协议）

> WorkItem 是 Leader 下发给 SubAgent 的可移植执行意图（schema：
> `schemas/work-item.schema.json`）。本目录只存放**被派发**的 WorkItem 载荷，
> 不是任务追踪系统——直接执行的工作不产生 WorkItem；工作意图的持久层是
> `plans/` 与 git 历史（见 `docs/adr/0002-workitem-is-dispatch-protocol.md`）。

## 规则

- 仅当选择委派（Fresh Context 隔离 / 并行 / 上下文卸载）时才落盘：
  `WI-<ID>.json`，状态内嵌（pending | in_progress | done | blocked |
  rejected），由 UniFlow 维护。
- 依赖（`dependencies`）指向其他 WorkItem id；DAG 有环即派发失败。
- 修改 acceptance / forbidden / frozen_decisions 属重新决策，回 UniFlow
  Decision，不得由 Worker 现场改。
- 完成判定证据在 `evidence/`；本目录只承载派发载荷与状态。
- 载荷生命周期 = transient：所属 change closed 且未被 `evidence/`、
  `docs/`、`plans/` 引用的载荷随 closure 删除；被引用的载荷保留为证据
  （如 FSV-001 / WI-P* 符合性引用，见
  `docs/analysis/harness-v2-compatibility-matrix.md`）。
