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

1. **[宪章](docs/system/greenfield-runtime-charter.md)** — 完整行为指导：职责、生命周期、Trap/Recovery、场景、建设路线
2. **[Architecture Contract](docs/system/constitution/runtime-architecture-contract.md)** — 12 条不可违反 invariants
3. **[ArchitectureGuardTests](../../tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs)** — 机械约束（零 ProjectReference / 零旧 namespace / 文档存在）
4. **`scripts/check-consistency.sh`** — 文档级机械检查（宪章 60 节、Contract 12 条、导航完整）

## 目录职责（谁拥有什么可变状态）

| 目录 | 职责 | 状态 owner | 不拥有 |
|------|------|-----------|--------|
| `Agent/` | Run 级控制者：Goal/Plan/World Belief/Container 管理/Trap Scope/Agent Recovery | Run 生命周期、World Belief | 元素匹配、点击实现、OCR |
| `Container/` | 语义页面级局部状态域：Semantic Identity/Local Progress/局部恢复 | 页面局部状态、Local Traversal Graph | 全局目标、世界真相 |
| `Traversal/` | 确定性执行 Kernel：Select→Check→Execute→Verify→Branch | 单步执行状态 | 世界级语义理解、Agent Goal |
| `Recovery/` | 统一 Recovery 机制：Request→Planner→Plan→Runtime→Result | Recovery 执行状态 | 决策 authority（Authority 不共享） |
| `World/` | World Belief / Observation / Drift 判断模型 | World Belief（Agent 代持） | — |
| `Planning/` | Plan 是 hypothesis, 不是 reality | Plan 结构 | 现实世界事实 |
| `Memory/` | 过去知识：Prior/Advice/Evidence, 不是 truth | Memory 内容 | 当前现实判断 |
| `Capabilities/` | 外部能力 Port：`Vision/` `Device/` `AI/` `External/` — 实现是 Adapter | Adapter 内部 | 任务决策 |
| `Model/` | 纯不可变模型：`Observation/` `Graph/` `Actions/` | 无（不可变） | Runtime 实现引用 |
| `Observability/` | Trace 因果链：RunId/ContainerId/StepId/ActionId | Trace 写入 | 业务判断 |

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
