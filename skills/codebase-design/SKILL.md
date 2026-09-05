---
name: codebase-design
description: 代码库结构设计。在 Plan 阶段设计模块边界、公共 seam、依赖方向与集成策略（tracer-bullet 优先）。适用于新模块、大型重构的结构设计。
---

# codebase-design

## Trigger

- Plan 阶段需要确定结构：模块划分、目录布局、公共接口。
- 大型重构需要 before/after 结构图。
- 新能力接入需要确定放在哪个边界内。

## Inputs

- Plan 输入（domain model 或需求）。
- 现有结构与依赖现状（Explore 产物）。
- 相关 `frozen_decisions`。

## Procedure

1. 从能力出发（不是从技术分层出发）列出系统要做的事。
2. 识别 **public seams**：外部可观察/可测试的接缝清单——这是 TDD 的落点。
3. 划分模块边界：每模块单一职责、清晰 owned paths、独立测试门。
4. 确定依赖方向：显式、单向、无环；反向依赖必须显式论证。
5. 设计集成策略：**tracer-bullet 优先**——先打通一条端到端最薄路径
   （Model → Runtime → Test → Evidence），再横向加厚。
6. 记录设计到 `plans/`（含 before/after 与迁移步骤）。

## Verification

- 每个边界与 ownership 一致（无两个模块声称拥有同一状态）。
- 依赖方向显式且无环。
- 存在一条可执行的端到端 tracer-bullet 路径。
- 每个公共 seam 都有对应可测试行为。

## Failure / Escalation

- 设计需要突破既有不变量/冻结决策 → 停止，升级 UniFlow Decision。
- 问题大到一个 Context 无法完成设计 → 先拆 Decision WorkItems
  （wayfinder 原则），不是硬拆实现任务。

## Constraints

- 设计是 Plan 工件，不是代码授权；实现须经 WorkItem。
- 不创建第二套 task system；不决定 Model Routing。
