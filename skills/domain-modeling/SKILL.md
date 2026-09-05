---
name: domain-modeling
description: 领域建模。在 Large/架构级变更的 Decision/Plan 阶段建立实体、关系、所有权与不变量模型。适用于新领域、语义边界不清或数据流复杂时。
---

# domain-modeling

## Trigger

- Large/Architecture 级变更进入 Decision/Plan 前。
- 新领域概念出现，或既有概念的语义边界不清。
- 数据流/状态流复杂到无法靠直觉保持一致。

## Inputs

- 领域叙述（用户语言）、现实约束。
- 已有 `decisions/` 中相关冻结决策。
- 已知场景（正例与反例/falsifier）。

## Procedure

1. 从领域叙述提取名词（候选实体）与动词（候选关系/操作）。
2. 建立实体与关系；标注基数与生命周期。
3. 对每个实体指定**唯一 owner**（mutable state 单一所有者）与**唯一 decision
   authority**（每个决策单一权威）。
4. 提取不变量（invariants）：哪些性质在任何转移下都不得违反。
5. 划定边界：哪些在模型内、哪些是外部世界（observation ≠ truth）。
6. 用全部已知场景验证模型——重点用反例攻击；模型解释不了的场景就是缺口。
7. 产出 domain model 记录，写入 `decisions/` 或作为 `plans/` 输入。

## Verification

- 模型能解释全部已知场景（含反例）。
- 每个 mutable state 恰好一个 owner；每个 decision 恰好一个 authority。
- 每条不变量可陈述为可检验的断言。
- 模型中没有任何实体仅为「将来可能有用」而存在。

## Failure / Escalation

- 发现不可调和的语义冲突 → 冻结无争议部分，冲突点升级 UniFlow Decision。
- 现有冻结决策与模型矛盾 → 停止，升级 Human Gate（决策变更属材料性边界）。

## Constraints

- 模型不是实现授权；进入实现必须经 ToWorkItems 生成 WorkItem。
- 不控制 UniFlow 生命周期；不决定 Model Routing。
