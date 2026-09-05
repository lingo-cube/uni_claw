---
name: code-review
description: 统一评审能力。分开回答 Built Right?（造得对不对）与 Built The Right Thing?（造的是不是对的东西）。适用于 WorkItem 完成后、Verify 前；高风险用 Fresh Review Context。
---

# code-review

## Trigger

- WorkItem 执行完成后、Verify 判定前。
- 高风险 WorkItem（触及 frozen_decisions、公共契约、不变量）必须使用
  **Fresh Review Context**（未参与实现的上下文）。

## Inputs

- WorkItem（objective / acceptance / forbidden / frozen_decisions / scope）。
- 变更 diff 与关联测试。
- 声明的 Evidence。

## Procedure

先分开回答两个问题，再走清单：

1. **Built Right?**（工程质量）：代码标准、复杂度、命名、测试质量、
   无隐蔽控制流、无 dead code。
2. **Built The Right Thing?**（意图符合）：对照 WorkItem objective 与
   acceptance；确认做的是要求的事，不是「顺手多做的事」。

检查清单（逐项过）：

| 维度 | 检查 |
|---|---|
| Code Standards | 符合仓库既有风格；无格式/空白问题 |
| WorkItem Intent | 实现与 objective 一致；无范围外功能 |
| Acceptance | 每条 acceptance 都有对应验证证据 |
| Frozen Decisions | 未触碰 frozen_decisions 与既有决策 |
| Scope | 改动都在 scope.write 内；越界即 blocking |
| Architecture Boundary | 未引入错误依赖方向 / 双 owner / 双 authority |
| Evidence Sufficiency | 证据足以支撑完成声明；自述不算数 |

## 输出

```text
APPROVE               全部通过
APPROVE-WITH-NOTES    通过，附非阻断性备注
REJECT                越界 / 证据不足 / 违反冻结决策 / 破坏边界（blocking）
```

REJECT 必须给出具体条目与证据位置。

## Verification

- 评审结论持久化到 `evidence/`（含Reviewer 判定与依据）。
- REJECT 项可机械核对（能指出文件/行为/证据）。

## Failure / Escalation

- 评审中发现架构级问题 → 升级 UniFlow Decision，不在 review 内现场改架构。
- 无法获得 Fresh Context（无第二执行环境）→ 显式记录限制后可内联评审，
  但高风险项必须标注「未独立评审」。

## Constraints

- Review 不重写实现；REJECT 后回到 UniFlow 重派或修复。
- 不创建第二套 task system；不决定 Model Routing。
