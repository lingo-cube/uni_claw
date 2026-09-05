# workitems/ — WorkItem 实例

> Durable Artifact：UniFlow 的执行单元实例；schema 见
> `schemas/work-item.schema.json`。

## 收录范围

- 每个 WorkItem 一个 JSON 文件：`WI-<ID>.json`。
- 生命周期状态内嵌于文件（`status`: pending | in_progress | done | blocked |
  rejected），由 UniFlow 维护。

## 规则

- 依赖（`dependencies`）指向其他 WorkItem id；DAG 有环即派发失败。
- 修改 WorkItem 的 acceptance/forbidden/frozen_decisions 属于重新决策，
  必须回 UniFlow Decision，不得由 Worker 现场改。
- WorkItem 完成的判定证据在 evidence/，本目录只记录状态与定义。
