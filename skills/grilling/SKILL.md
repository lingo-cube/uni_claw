---
name: grilling
description: 深度质询。在 Explore/Decision 前对需求、假设与验收标准进行结构化盘问，暴露隐藏假设与歧义。适用于任何进入 Plan 前的意图澄清。
---

# grilling

## Trigger

- 任务即将从 Explore 进入 Decision/Plan，但需求仍有歧义。
- 验收标准不明确，或「完成」无法客观判定。
- 存在未经声明的假设（尤其是被当作事实的假设）。
- 用户意图与已有 decisions/plans 工件可能冲突。

## Inputs

- 任务描述（Intent 阶段产物）。
- 相关 `decisions/`、`plans/` 工件。
- 草案 acceptance / forbidden。

## Procedure

1. 列出任务依赖的关键主张与假设（显式编号）。
2. 对每个假设找**最近的 falsifier**：什么证据能证明它错？
3. 提出判别性问题——优先设计成「两个选项即可分辨」的形式，避免开放式漫谈。
4. 区分两类问题：
   - 用户必须裁决（产品级选择、真实 Human Gate）；
   - 可用仓库证据/实验回答（交回 Explore）。
5. 汇总为**最小问题集**：只问会改变方案的问题，不问满足好奇心的 问题。
6. 把澄清结果写入 decision/plan 的输入（不写实现）。

## Verification

- 每个材料性歧义都有显式问题或已裁决的 decision 记录。
- 没有未声明的假设进入 Plan。
- 问题集里没有「无论怎么答都不改变方案」的问题。

## Failure / Escalation

- 发现架构级分叉（新抽象/边界/不变量）→ 停止质询，升级 UniFlow Decision；
  材料性边界形成 Human Gate。
- 用户不可达且无法继续 → 记录 BLOCKED 与已澄清部分，冻结待裁决问题。

## Constraints

- 本 Skill 只产出问题与澄清，不产出实现、不派生任务。
- 不创建第二套 task system；不决定 Model Routing。
- 质询结果必须持久化到工件，不能只留在对话中。
