# Runtime Agent Map

> 本文件是 **map, 不是 manual**。只回答：这个目录负责什么 / 不负责什么 / 修改前读什么 / 禁止什么 / 怎么验证。
> 完整行为指导: [Greenfield 宪章](../../docs/system/greenfield-runtime-charter.md)（60 节 / 13 Parts）。
> 边界契约: [Runtime Architecture Contract](../../docs/system/constitution/runtime-architecture-contract.md)（I-1..I-14）。
> 顶层架构基线: [UniAgent Architecture v1](../../docs/architecture/uniagent-architecture-v1-core-development-guide.md)（本文件的 "Agent" = v1 的 "RuntimeAgent"）。
> 本文件不产生架构权威、不定义 invariant、不改变 OpenSpec 生命周期。

## 1. Responsibility

`UniClaw.Runtime` 是 Greenfield Runtime execution layer — 运行在真实 GUI / Device Environment 上的智能执行 Runtime 的生产代码。

负责：

- Runtime lifecycle
- execution orchestration
- state reconciliation
- recovery coordination
- Runtime-level contracts implementation

当前阶段: `POST_DETERMINISTIC_SEMANTIC_RUNTIME_PROGRESS`；已毕业能力与当前 gate 以根 `AGENTS.md`、`docs/snapshots/latest.md` 和相关 OpenSpec 为准。未实现功能不提前建 stub（见宪章 §48 九个问题）。

## 2. Ownership Boundary

**Runtime owns:**

- runtime execution state
- lifecycle coordination
- runtime orchestration

**Runtime does not own:**

- user / business goal definition
- external device implementation
- semantic truth source
- model capability implementation

问题属于哪一层，就只改哪一层；禁止通过修改其他层补偿问题（见根 [AGENTS.md](../../AGENTS.md) §7 Ownership Boundary）。

## 3. Architecture Entry

修改 Runtime 前阅读（路径引用，不复制内容）：

1. [UniAgent Architecture v1](../../docs/architecture/uniagent-architecture-v1-core-development-guide.md) — frozen 顶层基线，RuntimeAgent 边界
2. [Runtime Architecture Contract](../../docs/system/constitution/runtime-architecture-contract.md) — 14 条不可违反 invariants
3. [Greenfield Runtime Charter](../../docs/system/greenfield-runtime-charter.md) — 完整行为指导（职责 / 生命周期 / Trap-Recovery / 编码纪律）
4. [Architecture index](../../docs/architecture/README.md) — canonical index；上下文加载顺序见 [context-loading-guide](../../docs/context-loading-guide.md)
5. 相关 Scenario / OpenSpec change — `../../openspec/changes/<change>/`（实施与进度真源）

机械约束（修改后必须仍通过）：

- [ArchitectureGuardTests](../../tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs) — 零 ProjectReference / 禁旧 namespace / 导航存在
- `../../scripts/check-consistency.sh` — 宪章 60 节 / Contract 14 条 / 导航完整

## 4. Runtime Spine

高层闭环（不解释每一步）：

```
Observe → Reconcile → Decide → Execute → Verify → Update
```

异常路径:

```
Trap → Determine Scope → Recovery → Resume
```

## 5. Development Rules

基于根 [AGENTS.md](../../AGENTS.md) §4 Agent Operating Principles（Think Before Coding / Simplicity First / Surgical Changes / Goal Driven Execution），本目录特化：

**Before changing Runtime:**

- identify owner — 谁拥有该 mutable state / decision authority（宪章 §29-31）
- locate contract — §3 条目 2；invariant 不可变通（I-1..I-14）
- verify scenario — 修改必须有 Scenario / OpenSpec 支撑；没有 Scenario 购买的能力 = I-12 违约

**Prefer:**

- existing Runtime abstraction
- minimal change
- scenario evidence

**Avoid:**

- hidden lifecycle — 绕过 FSM / 生命周期 owner
- bypassing Runtime boundary — 低层偷取高层 authority（I-8：只能 escalate）
- workaround instead of fixing owner — 先确认 First Divergence Point 与 Owner 再改（Debugging Gate 见 `.ai/skills/evidence-driven-debugging`）

## 6. Verification

Runtime 修改完成必须：

- `dotnet build src/UniClaw.Runtime.sln`（0 error）
- `dotnet test src/UniClaw.Runtime.sln`（all green + guards）
- scenario validation — 相关 scenario 套件全绿

测试规则见 `../../tests/UniClaw.Runtime.Tests/`（测试验证能力，不验证脚本）— 不在此复制。

## 7. Directory Navigation

每项一句职责；详细行为见宪章对应节。

| 目录 | 一句职责 |
|------|---------|
| `Agent/` | Run 级控制者：Goal/Plan/World Belief/Container 管理/Trap Scope/Agent Recovery（Run 生命周期、World Belief owner） |
| `Startup/` | §19 启动程序：Attach→Launch→Observe→Resolve→Initial Container→RecoveryAnchor→Ready |
| `Container/` | 语义页面级局部状态域：Semantic Identity/Local Progress/局部恢复（页面局部状态 owner） |
| `Traversal/` | 确定性执行 Kernel：Select→Check→Execute→Verify→Branch（单步执行状态） |
| `Recovery/` | 统一 Recovery 机制：Request→Planner→Plan→Runtime→Result（Recovery 执行状态） |
| `World/` | World Belief / Observation / Drift 模型与纯逻辑（无状态；Agent 拥有 World Belief 实例） |
| `Environment/` | 外部世界能力边界 Port：Observation + Action capabilities（Adapter 内部） |
| `Planning/` | Plan 是 hypothesis, 不是 reality（Plan 结构） |
| `Memory/` | 过去知识：Prior/Advice/Evidence, 不是 truth |
| `Capabilities/` | 能力域：Brain（推理，无编排权威）/ Perception（外部→可观测证据）/ Operator（授权执行意图→物理操作） |
| `Model/` | 纯不可变模型：Observation / Graph / Actions 等（无 owner） |
| `Observability/` | Trace 因果链：RunId/ContainerId/StepId/ActionId（Trace 写入） |
