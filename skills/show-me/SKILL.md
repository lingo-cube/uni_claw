---
name: show-me
description: 辅助认知可视化。把架构、状态机、调用链、数据流、trace、before/after 渲染为最小有效表示，用于人/agent 认知对齐。不是强制 gate。
---

# show-me

## Trigger

- 需要对齐理解时：架构全景、状态机转移、调用链、数据流。
- trace/证据时间线需要人读。
- before/after 对比（重构、迁移、行为变化）。
- 大型 refactor 的结构演进说明。

不设为每个任务的强制环节；按需使用。

## Inputs

- 待表示的对象：代码结构 / 状态定义 / trace 数据 / diff。
- 受众与目的（决定抽象层级）。

## Procedure

1. 选择**最小有效表示**：
   - 结构/依赖 → mermaid graph；
   - 状态机 → stateDiagram；
   - 时序/调用链 → sequenceDiagram；
   - 数据流/trace → 时间线表；
   - 对比 → 双栏 before/after 或 diff 摘要。
2. 标注关键节点：owner、authority、不变量、First Divergence Point。
3. 产出到对话（临时对齐）或 `evidence/`（需要留存时，mermaid 源码进文件）。
4. 用一句话说明「看这张图要回答什么问题」。

## Verification

- 表示可**独立解读**：不依赖当前对话上下文也能看懂。
- 关键决策点/不变量有标注，无误导性简化。
- 图与事实一致：每个节点/边都能对应到真实代码或数据。

## Failure / Escalation

- 对象过于复杂无法一张图表达 → 拆多张分层图，不强行压缩。
- 可视化需要的数据缺失 → 先补数据（回 Explore），不凭记忆画图。

## Constraints

- 可视化不改变语义、不建立权威；与正文冲突时以正文/契约为准。
- 不作为完成的必要条件（不是 gate）。
- 不创建第二套 task system；不决定 Model Routing。
