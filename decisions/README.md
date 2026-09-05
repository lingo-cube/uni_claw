# decisions/ — 冻结决策

> Durable Artifact：决策一旦冻结，routine 修复不得修改；变更需回 UniFlow
> Decision 阶段（材料性边界构成 Human Gate）。

## 收录范围

- 架构/流程决策记录：背景、选项、裁决、理由、影响面。
- Human Gate 裁决（七类材料性边界）。
- 从 legacy 记录中提取并确认仍有效的决策。

## 命名

`YYYY-MM-DD-<slug>.md`，文件头声明：

```text
> DecisionType: ARCHITECTURE | PROCESS | GATE
> Status: FROZEN | SUPERSEDED（被取代时保留原文，链接后继）
> Date / Context / Decision / Consequences
```

## 禁止

- 不存放实现细节（属 plans/ 或代码）。
- 不存放过程性讨论（属 evidence/）。
- SUPERSEDED 记录不删除——历史是证据层。
