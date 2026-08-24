# Agent Handoff Contract — 通信协议

> 定位: Agent 之间交接任务的**通信协议**（消息格式）。
> **只是通信协议，不是 Architecture Contract** — 不产生架构权威，不修改任何
> invariant / gate / authority 边界（权威定义见 `.ai/development-protocol.md`）。
> 上级: AGENTS.md「Agent Handoff Contract」。

## 为什么存在

Agent 之间不能只传 "Implement xxx" — 接收方没有目标、事实、约束和验证标准，
无法安全执行；缺字段会迫使接收方猜测，猜测即引入 Agent assumption（权威最低层）。

## 交接消息必须包含

| 字段 | 要求 |
|------|------|
| Goal | 这次交接要实现什么（**可验证的结果**，不是动作描述） |
| Context | 相关背景：所在 change / scenario / 相关文件 |
| Facts | 已确认的事实，**带出处**（文件 / 行号 / 证据） |
| Unknowns | 尚未确认、需要接收方查证或上报的项 |
| Decision | 已做出的决策（及 authority 归属） |
| Constraints | 边界：禁止修改什么、必须保持什么 |
| Expected Result | 完成后应该出现的可观察结果 |
| Verification | 如何验证（测试 / 命令 / 检查） |

## 规则

- 缺字段的交接 = 不合格交接：接收方应要求补齐，**不自行猜测**。
- Facts 与 Unknowns 必须分离；未证实的信息必须标为 Assumption。
- 交接**不转移 authority**：决策 authority 仍按 `.ai/development-protocol.md`。
- 本文件是通信协议，不是 Architecture Contract — 它不建立新的 Decision 或 Gate。
