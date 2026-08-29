---
name: uniagent-evolution-loop
description: 编排 UniAgent 模拟、证据收集、First Divergence 与 Owner 路由的受控演进工作流；不改变 Runtime 权威、协议或生命周期。
---

# UniAgent Evolution Loop

用于按 UniFlow 执行一次有界的 UniAgent 演进闭环。Skill 只编排开发证据，不成为架构、协议、生命周期或 Runtime 权威。

## 工作流

1. 先从用户可见目标、当前界面和人类最短可行操作路径建立可证伪假设，再读取当前 OpenSpec、WorkItem、工作树状态和适用契约；确认本次入口、Owner、禁止范围与 Human Gate。先用界面与 trace 证据定位 First Divergence，再从所属 seam 进入最小必要代码，不从无界或漫长调用链开始。该假设不得把硬编码坐标、固定点击序列、偶然标签或偶然路径变成 Runtime 权威或 scenario knowledge。
2. 通过现有 ValidationHarness 启动有界 Simulation Campaign；每个 Run 只能通过已授权入口调用一次 `run.strategy.start`，不得在 Run 内微操。
3. 收集 Snapshot、Events、Evidence、GoalEvidence、Terminal Result 及可信执行回执；区分 UniAgent 监督/解释权与 RuntimeAgent 观察、授权、执行、恢复、证据和完成权。
4. 对每个失败或偏差记录 Expected Reality、Observed Reality、Reality Gap、First Divergence Point 和 Owner。
5. 按 Owner 路由：UniAgent/Harness 问题留在当前范围；Perception/Runtime/Protocol 问题停止当前 Campaign 并交给对应 OpenSpec；权责、协议或架构压力进入 Human Gate。
6. 修复后使用同一 Scenario 重放并比较证据。只有满足已冻结验收条件时，才报告完成建议；不得自动毕业、自动改架构或自动决定 Human Gate。

## 停止条件

- 发现契约、Invariant、Owner 或 WorkItem 范围冲突时立即停止，返回 `BLOCKED_*`、First Divergence Point、已尝试动作和证据。
- 不修改 Runtime、DriverHost、Strategy Contract、GoalEvidence、SourceIdentity 或未授权 OpenSpec 任务/证据。
- 不创建第二套模拟器、Runtime 协议、MCP/DSH 插件或跨 Run Runtime 状态。

## 复用入口

优先复用现有 UniFlow、ValidationHarness、OpenSpec 和证据链；Skill 只提供本流程的发现与执行提示，不复制它们的真相源。完成后输出精简 WorkResult，包含状态、改动文件、验证证据与未解决项。
