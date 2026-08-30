# Proposal: Greenfield Agent Runtime

| 属性 | 内容 |
|------|------|
| Change ID | `greenfield-agent-runtime` |
| 状态 | Proposed |
| 类型 | **Greenfield Runtime Build**（不是 TraversalEngine 原地重构） |
| 日期 | 2026-08-07 |
| 分支 | `feature/agent-runtime` |

## 动机

旧 `UniClaw.Core` 的 Runtime 控制结构（TraversalEngine / StepOrchestrator / InterceptionHandler /
TraversalRuntimeContext）经过多轮修补已经背负了难以剥离的耦合：FSM 同时承担 protocol 与 intelligence、
Context 是隐性 God Object、Pop 判据在三个位置各自实现。继续原地重构成本高、风险大。

本 change 建立**独立的**新 Agent Runtime 工程边界，让新 Runtime 从零生长出自己的
Agent → Container → Traversal → Environment Spine，旧系统只作为能力参考。

## 旧 UniClaw.Core 的定位

- **Reference Implementation** — 行为参考，不是代码模板
- **Capability Source** — 成熟能力（Domain 模型、Observability、AI 接口）未来**选择性**迁移
- **Regression Baseline** — 旧测试继续作为行为基线

**不是** New Runtime 的 Architecture Template。

## 目标（本 change 范围）

1. 建立独立工程边界：`src/UniClaw.Runtime/` + `tests/UniClaw.Runtime.Tests/`
2. 第一阶段 `UniClaw.Runtime` **不引用** `UniClaw.Core`（Greenfield isolation，机械约束）
3. 建立 Architecture Contract（12 条 invariants，`docs/system/constitution/`）
4. 建立机械 Architecture Guards（`UniClaw.Runtime.Tests`）
5. AGENTS.md 增加唯一导航入口（指向 Contract + 本 change）
6. 全部流程走 OpenSpec：本 change 是后续每个 Vertical Slice 的根

## 非目标（Deferred，本 change 不解决）

- 任何 Runtime 业务类型（Agent / Container / TraversalFSM / WorldBelief / Recovery …）
- 复用决策：IActionExecutor / PageAnalysis / UniBrain / Graph / SourceGen / Foundation project
- Recovery Runtime / Memory / LLM-VLM / Android / Vision / DynamicMatch
- 旧代码迁移、Container 最终类名、ContainerFSM 是否存在、TraversalFSM 状态设计

## 验收

- `dotnet build src/UniClaw.Core.sln` — 0 错误（基线同样 0 错误）
- `dotnet test src/UniClaw.Core.sln` — 基线测试无回归；新 Guard 测试通过
- 新 Runtime Guard 验证：csproj 零 ProjectReference；源码零旧 Runtime namespace 引用；契约文档 + 导航存在
