# Runtime Architecture Contract — UniClaw.Runtime

> 版本: v1.1 | 日期: 2026-08-07 | 状态: Active
> 定位: 新 Runtime 的**边界契约**（Architecture Invariants），不是详细设计说明书。
> 读者: 所有修改 `src/UniClaw.Runtime/` 代码的 AI Coding Agent / 开发者 — 动手前必读。
> 导航: AGENTS.md「Agent Runtime（新）— Greenfield」段；OpenSpec: `openspec/changes/greenfield-agent-runtime/`

## 0. 定位

- 这是 **Greenfield Runtime Build**，不是 TraversalEngine 原地重构。
- 旧 `UniClaw.Core` 定位: **Reference Implementation / Capability Source / Regression Baseline**，不是 Architecture Template。
- 新 Runtime **不继承**旧 TraversalEngine / StepOrchestrator / InterceptionHandler / TraversalRuntimeContext 的控制结构。
- 第一阶段 `UniClaw.Runtime` **不引用** `UniClaw.Core`（机械约束: ArchitectureGuardTests Guard 1/2）。
- 复用成熟能力时，未来单独走 OpenSpec 决策（Extract Foundation / Create Adapter / Reuse Contract），本契约不预设。

## 1. Invariants（不可违反）

### I-1 — Agent → Container → Traversal → Environment 是核心运行责任方向
责任从 Agent（意图与生命周期）向下流经 Container（帧/子树组织）与 Traversal（步骤推进）到
Environment（设备与世界交互）。反向依赖需要显式论证。

### I-2 — 一个 mutable state 只能有一个 owner
任何可写状态必须声明唯一 owner（Agent / Container / Traversal / Environment 之一）。
禁止共享可变对象跨 owner 传递；跨 owner 边界只能传不可变快照或消息。

### I-3 — 一个 decision 只能有一个 authority
每个决策（选哪个节点、是否滚动、是否恢复、是否完成）有且只有一个权威判定方。
其他组件只能提供 evidence，不能重复判定。

### I-4 — Observation 是 evidence，不是 semantic truth
观测结果是决策依据，不是事实陈述。同一观测可以因时因境被不同决策层不同解读；
任何把 Observation 当 truth 直接使用的代码都是违约。

### I-5 — Plan 是 hypothesis，不是 reality
执行计划是当前假设的世界模型。环境变化时计划可以失效，执行者必须有能力偏离、
修订或放弃计划，而不是把计划当现实强推。

### I-6 — Fingerprint 是 evidence，不是 identity
指纹（页面/内容指纹）用于比较"是否变化"，不能当作页面或元素的稳定身份。
用指纹做身份判断的地方必须显式说明假设。

### I-7 — FSM 负责 protocol transition，不负责 intelligence
状态机只做协议化迁移（当前状态 → 合法目标状态），不做业务智能决策
（文本解析、重试策略、advisor 调用、熔断判断等一律在 FSM 之外）。
旧系统教训: FSM handler 膨胀成决策器。

### I-8 — Lower scope 可以向上 escalate，但不能偷偷取得更高 scope 的 authority
低层组件遇到自身无法解决的状况必须显式上报（escalate），由高 scope 决策；
禁止低层通过旁路（全局状态、共享可变对象、回调改决策）自行取得高层权威。

### I-9 — Recovery 是 act → observe → verify → reconcile，不是单个 PressBack
恢复是一个证据驱动的闭环: 执行动作 → 观测结果 → 验证是否恢复 → 对账修正。
"按 Back 键"只是动作之一，不是恢复本身。

### I-10 — Completion 必须由 Goal Evidence 证明
"任务完成"只能由目标证据（goal evidence）触发并记录原因；
禁止用无证据的启发（步数耗尽、栈空、页面未变）冒充完成判定。

### I-11 — 不继承旧 Runtime 控制结构
不复制/仿造旧 TraversalEngine / StepOrchestrator / InterceptionHandler /
TraversalRuntimeContext 的控制流、状态字段与耦合模式。
（机械约束: Guard 2 禁止 `UniClaw.Core.Traversal` / `UniClaw.Core.StateMachine` namespace 引用）

### I-12 — 没有 Requirement 支撑的复杂度，不提前实现
YAGNI。当前场景未提出需求的功能（Memory、Recovery Runtime、动态匹配、弹窗处理等）
不提前实现、不提前设计接口，避免重蹈旧系统"先建框架再找需求"的覆辙。

### I-13 — Observation / WorldBelief / RuntimeState / Memory 不得重新聚合成 God Context
四个模型各有明确边界（Observation=证据采集、WorldBelief=现实判断、RuntimeState=内部簿记、
Memory=历史知识）。禁止将它们重新聚合成一个巨大的可变 Context 对象。
旧系统教训: 单一巨型 Context 是所有组件互相耦合的根源。

### I-14 — AI 是可插拔能力，不是 Runtime 唯一路径，也不是世界真相来源
LLM / VLM 是可插拔语义增强能力。确定性 Runtime 核心必须能在 AI unavailable 时运行到合理程度。
AI 输出是 Semantic Evidence → Agent Decision → World Belief，不能直接成为世界事实。
（与 I-7 互补: FSM 不做智能决策；AI 不做协议转换。）

| 方向 | 规则 | 机械保证 |
|------|------|---------|
| UniClaw.Runtime → 任何现有 project | 禁止（含 UniClaw.Core） | Guard 1: csproj 零 ProjectReference |
| UniClaw.Runtime 源码 → 旧 Runtime namespace | 禁止（UniClaw.Core.Traversal / UniClaw.Core.StateMachine） | Guard 2 |
| UniClaw.Runtime → BCL / .NET 运行时 | 允许 | — |
| UniClaw.Runtime.Tests → UniClaw.Runtime | 允许（唯一 ProjectReference） | Guard 1 只扫生产工程 |

## 3. 相关入口

- OpenSpec change: `openspec/changes/greenfield-agent-runtime/`（本阶段地基 + 后续 Vertical Slice 根）
- Guard Tests: `tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs`
- AGENTS.md: 「Agent Runtime（新）— Greenfield」导航段
