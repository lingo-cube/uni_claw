# AGENTS.md — UniClaw.Runtime（Greenfield 构建区）

> 本文件是 **map, 不是 manual**（Harness Engineering: "AGENTS.md as a map, not a manual"）。
> 只回答：这个目录是什么、动它之前必读什么、谁拥有什么。
> 完整行为指导 = [docs/system/greenfield-runtime-charter.md](../../docs/system/greenfield-runtime-charter.md)（60 节按职责分类）

## 这是什么

UniClaw Agent Runtime 的生产代码 — 一个运行在真实 GUI / Device 上的智能执行 Runtime。

```
                Agent
                  │
                  ▼
             World Belief
                  │
         ┌────────┴────────┐
         │                 │
      Decide          Active Container
                           │
                           ▼
                       Traversal
                           │
                           ▼
                       Environment
                           │
                           ▼
                      Observation
                           │
                           └──────────────→ Reconcile
```

## 动手前必读（按影响范围递减）

1. **[宪章](../../docs/system/greenfield-runtime-charter.md)** — 完整行为指导：职责、生命周期、Trap/Recovery、场景、建设路线
2. **[Architecture Contract](../../docs/system/constitution/runtime-architecture-contract.md)** — 14 条不可违反 invariants
3. **[ArchitectureGuardTests](../../tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs)** — 机械约束（零 ProjectReference / 零旧 namespace / 文档存在）
4. **`../../scripts/check-consistency.sh`** — 文档级机械检查（宪章 60 节、Contract 12 条、导航完整）

## 逻辑职责边界（目录仅在 Scenario 需要时创建）

> 下表描述责任边界，不代表这些目录或类型必须预先存在；由 Vertical Slice 证明需要后再创建。

| 目录 | 职责 | 状态 owner | 不拥有 |
|------|------|-----------|--------|
| `Agent/` | Run 级控制者：Goal/Plan/World Belief/Container 管理/Trap Scope/Agent Recovery | Run 生命周期、World Belief | 元素匹配、点击实现、OCR |
| `Startup/` | §19 启动程序：Attach→Launch→Observe→Resolve→Initial Container→RecoveryAnchor→Ready | 启动过程执行状态 | 运行期决策 |
| `Container/` | 语义页面级局部状态域：Semantic Identity/Local Progress/局部恢复 | 页面局部状态、Local Traversal Graph | 全局目标、世界真相 |
| `Traversal/` | 确定性执行 Kernel：Select→Check→Execute→Verify→Branch | 单步执行状态 | 世界级语义理解、Agent Goal |
| `Recovery/` | 统一 Recovery 机制：Request→Planner→Plan→Runtime→Result | Recovery 执行状态 | 决策 authority（Authority 不共享） |
| `World/` | World Belief / Observation / Drift 等模型与纯逻辑 | 无 — Agent 明确拥有 World Belief 实例；`World/` 仅提供模型定义和 reconciliation capability | — |
| `Environment/` | 外部世界能力边界 Port：Observation + Action capabilities | Adapter 内部（fake 在测试侧） | 任务决策 |
| `Planning/` | Plan 是 hypothesis, 不是 reality | Plan 结构 | 现实世界事实 |
| `Memory/` | 过去知识：Prior/Advice/Evidence, 不是 truth | Memory 内容 | 当前现实判断 |
| `Capabilities/` | 外部能力 Port：`Vision/` `Device/` `AI/` `External/` — 实现是 Adapter | Adapter 内部 | 任务决策 |
| `Model/` | 纯不可变模型：`Observation/` `Graph/` `Actions/` | 无（不可变） | Runtime 实现引用 |
| `Observability/` | Trace 因果链：RunId/ContainerId/StepId/ActionId | Trace 写入 | 业务判断 |

### Runtime 内部 consolidation map

> 这些是同一既有 owner 内的文件边界，不是新组件、Facade、状态 owner 或 decision authority。

| 文件 | 内部职责 | 边界说明 |
|------|----------|----------|
| `Agent/Agent.cs` | Agent 依赖、Run 级 mutable state、公共状态面与共享终结 helper | `Agent` 仍是唯一 public run-level semantic authority |
| `Agent/Agent.PlanRun.cs` | 既有确定性 Plan Run 主循环 | partial 文件拆分，不产生第二 lifecycle owner |
| `Agent/Agent.OpenWorld.cs` | 既有 bounded open-world 执行路径 | open-world 状态仍由同一个 `Agent` 实例持有 |
| `Agent/Agent.Recovery.cs` | Agent-scope recovery 决策与恢复后续跑 | Recovery mechanism 仍在 `Recovery/`；decision 仍在 Agent |
| `Agent/Agent.SemanticRun.cs` | 结构化 semantic goal closed loop | capability selection、action authorization、goal satisfaction 仍属于 Agent |
| `Agent/ActionAuthorizer.cs` | 已选 action 的无状态内部校验 | `internal` helper；唯一 public authority surface 仍是 `Agent.AuthorizeAction` |
| `World/BindingAnalysis.cs` | observation-scoped object-binding evidence + reconciliation | 无状态、无 truth authority、只依赖 Model |
| `World/BindingReconciler.cs` | binding evidence → immutable binding proposals | 无状态；Container 仍是 binding state 唯一 owner |
| `World/StateBeliefReducer.cs` | current observation + bindings → immutable state-belief proposal | 无状态；Container 仍是 belief state 唯一 owner/applier |
| `Traversal/SemanticActionLowerer.cs` | 已授权 semantic action → execution-action proposal | 无状态；Agent 仍授权，Traversal 仍执行/验证 |
| `Traversal/TargetGrounder.cs` | legacy 或 criterion target resolution | 无状态、无 retry/dispatch authority；criterion failure 保持 fail-closed |

## 铁律（详见宪章 §29-31 / §50）

- **一个 mutable state 只有一个 owner**；跨 owner 只传不可变快照 / 消息
- **一个 decision 只有一个 authority**；低层只能 escalate, 不得偷取高层权威
- **依赖方向**: Agent → Container → Traversal → Environment; 低层不得反向依赖
- **Observation 是 evidence, 不是 truth; Fingerprint 是 evidence, 不是 identity**
- **Recovery 不是 PressBack, 是 observe→verify→reconcile 闭环**
- **Completion 必须由 Goal Evidence 证明**
- 禁止: God Object / 多组件维护 CurrentPage / FSM 里调 LLM / Service Locator

## 阶段状态

- Phase 0 已完成（工程边界 + 机械 Guard）— 见 `openspec/changes/greenfield-agent-runtime/`
- 当前阶段: Phase 1 — Deterministic Runtime（Normal Scenario, Fake Environment）
- 未实现的功能不提前建 stub；新核心类先回答宪章 §48 的九个问题

## 原则

### 设计默认原则

- 围绕单一、明确的语义职责保持高内聚。
- 在不同职责边界、所有权边界和决策权边界之间尽量保持低耦合。
- 明确每一份可变状态的唯一所有者，以及每一类决策的唯一裁决者。
- 优先使用职责窄、能力明确的接口，避免使用承载大量无关状态和能力的“大上下文对象”。
- 避免为了未来可能出现的需求提前创建抽象、扩展点、框架或模型字段。
- 不要机械套用 SOLID、高内聚低耦合等通用设计原则；当它们与架构不变量、正式规范或已批准的场景约束发生冲突时，以更高优先级的规则为准。

### 决策优先级

架构不变量
> 已批准的正式规范（OpenSpec SHALL）
> 已批准的场景验收约束
> 领域设计规则
> 通用设计原则
> 实现层偏好
