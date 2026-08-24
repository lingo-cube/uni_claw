# UniAgent Architecture v1 — Core Development Guide

> Status: FROZEN | Agent concept amendment: `PROJECT_LEADER_AGENT_CONCEPT_MODEL_V1_ALIGNMENT` (2026-08-21)
> Subordinate semantic model: [Agent Concept Model v1](agent-concept-model-v1.md)

## 1. 当前阶段

UniAgent 顶层 Architecture v1 已完成主要语义收敛。

当前不再进行开放式架构探索，下一阶段应：

**Architecture Freeze → Global Architecture Alignment → Architecture Cleanup → Protocol Consolidation**

架构语义由职责、Authority、Lifecycle、Ownership、Component Boundary 和 Dependency 决定；具体场景用于验证架构，而不是继续发明顶层概念。协议阶段再恢复更强的 scenario / evidence-driven 方法。

---

## 2. Core Architecture

```text
User / Application
       │
   [ Task ]
   暂不讨论
       │
       ▼
    Session
       │
       ▼
Composition Host
       │
 ┌─────┼───────────────┐
 │     │               │
 ▼     ▼               ▼
AgentHost          Capabilities       Integrations
 │
 ▼
UniAgent
 │
 │ Stable Semantic Protocol
 ▼
RuntimeAgent
 │
 ▼
Physical World
```

DSH 不属于逻辑架构。

DSH 当前可以实现 Composition Host、AgentHost、UniAgent、Capability Hosting、Control/Data Integration 和 Operations UI，但它只是 v1 的实现框架，而不是 Architecture 本身。

---

## 3. Authority

### UniAgent

UniAgent 是顶层监督与编排 Agent。

核心职责：

- Goal 理解
- 全局决策
- RuntimeAgent supervision
- 高级异常裁决
- 策略纠偏
- Capability 编排
- Memory 使用
- 必要时调用 Brain / Operator

UniAgent 的主要能力来自：

```text
Orchestration Intelligence
```

Brain 只是增强智能 Capability，不拥有最终编排权。

### RuntimeAgent

RuntimeAgent 是固定执行范式内具有 bounded autonomy 的 specialist agent。

负责：

```text
Observe
→ Belief
→ Local Decomposition
→ FSM / Execution Pattern
→ Grounding
→ Action
→ Verification
→ Bounded Recovery
```

拥有：

- 当前 Run
- Run-local State
- Observation / Belief
- Grounding / Physical Action
- Fresh Verification
- Local Recovery
- Local Safe-stop Authority

核心 Authority：

> **RuntimeAgent owns bounded autonomy; UniAgent owns supervisory autonomy.**

UniAgent 可以改变高层策略，但不得绕过 RuntimeAgent 的事实、执行、安全和验证权威。

### Agent Concept Model v1 alignment

Architecture v1 使用以下分层解释，不引入第二套架构或新的 wire surface：

- **Primary Goal** 属于 Session 语境，由 UniAgent 理解并最终评价；
- **Execution Goal** 是 Directive 携带的 bounded Runtime 目标，当前
  `SemanticGoalInput` 属于这一层；
- UniAgent 可以维护 **Supervisory Plan**，RuntimeAgent 可以维护可修正的
  **Runtime-local Plan**；当前 Runtime Protocol 只传递 Directive；
- RuntimeAgent 独占 Run terminal outcome 与 GoalEvidence authority；
- UniAgent 产生 **Goal Evaluation**，分别评价 Completion 与 Satisfaction，
  但不得改写 Runtime Outcome；
- Agent Loop 是有限的行为/lifecycle activity，不等于 Session，也不要求
  永久存活的 Agent object。

详细术语、碰撞处理与 DSH / UniClaw 对象映射由
[Agent Concept Model v1](agent-concept-model-v1.md) 定义，并受本架构与
Protocol v1 共同治理。本补充不增加顶层 invariant 数量。

---

## 4. Session

Session 是一次 UniAgent 协作活动的 **correlation root**。

负责：

- Context
- Lightweight Summary
- Run References
- Decision References
- Capability Interaction References
- Artifact / Evidence References
- Navigation / Index

但：

```text
Session ≠ Message Bus
Session ≠ Runtime State
Session ≠ Event Store
Session ≠ Agent
```

Session 历史采用 append-oriented 模型。

已经发生的事实不改写；`latestRunRef`、`latestDecisionRef`、`summary` 等可以作为 Projection / Index 更新。

Producer 只能追加自己产生的事实：

```text
RuntimeAgent → Runtime facts
UniAgent     → Decisions / Goal Evaluations
Operator     → Operator decisions
Memory       → Summary projection
```

Session 是共享关联空间，而不是共享可变状态对象。

UniAgent 与 RuntimeAgent 的实时通信不经过 Session。

```text
UniAgent ←── Runtime Protocol ──→ RuntimeAgent

        \                      /
         └────── Session ─────┘
             history/index
```

Session 只负责 continuity、correlation、navigation 和 traceability。

---

## 5. Capability

必须区分三个维度：

```text
Agent      = 行为 / 自治性质
Capability = 对外语义能力
Plugin     = 实现 / 装载方式
```

### Brain

```text
Enhanced Intelligence Capability
```

用于：

- 深度语义推理
- 复杂诊断
- 慢智能
- 歧义裁决
- 专项 reasoning

Brain 不拥有 Session、Goal、Run lifecycle 或最终编排权。

### Vision

```text
Perception Capability
```

RuntimeAgent 拥有的是：

```text
Perception Contract
```

而不是 Vision implementation。

Vision 可以是：

```text
YOLO
OCR
VLM
其他 Perception implementation
```

RuntimeAgent 不应感知具体实现。

### Memory

Memory 独立于 RuntimeAgent：

```text
Session Memory
Long-term Memory
```

RuntimeAgent 判断当前世界必须依赖 fresh observation / evidence，而不能依赖隐式历史状态。

---

## 6. Runtime External Boundary

RuntimeAgent 对外只接受稳定、安全的语义协议。

禁止：

```text
Brain bypass RuntimeAgent
Vision bypass RuntimeAgent
Plugin directly mutate Runtime state
Control Plane directly mutate FSM
Natural language directly execute physical action
UniAgent directly overwrite Runtime Belief
```

外部能力原则：

```text
External Capability
        ↓
Safe Protocol / Hook
        ↓
RuntimeAgent
        ↓
Accept / Reject / Reconcile
```

Capability 永远不能因为被接入而获得 Runtime Authority。

v1 只保留通用、安全、受协议约束的 Extension Hook。

暂不细分 SemanticHook / DiagnosisHook / RecoveryHook 等类型。

---

## 7. Host

### Composition Host

```text
Host = Composition + Entry
```

负责：

- 创建 / 注入 Session
- 组合 AgentHost
- 绑定 RuntimeAgent
- 注册 Capability
- 绑定 integrations
- start / lifecycle / dispose

不负责：

- Goal 决策
- Runtime 决策
- Agent reasoning
- Runtime Truth
- Memory semantics

### AgentHost

只负责：

```text
Agent Lifecycle
```

v1 只需支持 UniAgent。

Multi-agent、复杂调度、长期驻留 Agent、复杂 suspend/resume 暂不设计。

Agent lifecycle 与 Session lifecycle 必须解耦。

---

## 8. System Planes

以下属于整个系统架构：

```text
Metadata Plane
Control Plane
Data Plane
```

它们不是 Agent Core 内部组件，也不要求映射成同名代码模块。

Metadata 更靠近 Task / Business / Index。

Control Plane 提供系统级控制接入。

Data Plane 提供事实外显、持久化、Replay、Analytics 等能力。

Agent Core 不应因为某个具体 DSH/Data/Control 实现而产生反向依赖。

---

## 9. v1 Lifecycle Scope

v1 默认：

```text
1 Session
1 Primary Goal
1 Primary Run
```

`Agent Loop != Run`：同一个 Primary Run 内可以包含多次 bounded execution
cycle / loop activation，但只能发生在 terminal outcome 之前；`Completed` 或
`Failed` 之后再次 dispatch Directive 需要新的 Run model，当前 v1 不授权。
RuntimeAgent 可以被描述为 UniAgent 的固定 specialist execution SubAgent；该
描述不开放通用 agent graph、SubRun、BranchRun 或 Multi-Run orchestration。

Run 内的：

```text
pause
resume
uncertainty
UniAgent adjudication
local recovery
```

默认仍属于同一个 Run。

以下全部保留为 Reserved Extension：

- Multi-agent
- Sub Run
- Branch Run
- Multi-Run orchestration
- Dynamic Capability Grant
- Typed Hook hierarchy
- Long-lived Agent Scheduling
- Complex Recovery Workflow
- Complex Completion Orchestration

不得在没有真实 buyer 的情况下提前设计。

---

## 10. Development Sequence

从当前状态开始，开发必须按照：

```text
Architecture Freeze
        ↓
Global Architecture Alignment Audit
        ↓
Architecture Cleanup
        ↓
Clean Architecture Baseline
        ↓
Protocol Consolidation
        ↓
Contract Implementation
        ↓
Migration
        ↓
Layered Validation
```

### Architecture Alignment

全仓库扫描：

- docs
- decisions
- OpenSpec
- architecture
- Runtime
- DriverHost
- DSH integration
- tests

所有现有概念分类为：

```text
ALIGNED
PARTIALLY_ALIGNED
LEGACY_BUT_USED
SUPERSEDED
CONFLICTING
ORPHANED
UNKNOWN
```

扫描阶段优先建立事实，不边扫描边重新设计。

### Architecture Cleanup

按照扫描结果：

```text
Superseded Decision → mark successor + archive
Obsolete Design     → archive
Dead Document       → delete
Duplicate Authority → consolidate
Terminology Drift   → align
Dead Abstraction    → remove when safe
Protocol Debt       → record, do not redesign yet
```

Decision 原则上优先 **supersede + archive**，而不是删除。

### Protocol Consolidation

清理完成后再进入：

```text
RuntimeAgent Protocol
Session Contract
Capability Contract
Hook Contract
```

协议设计必须：

```text
Architecture-constrained
Scenario-validated
Existing-evidence-driven
```

不从未来想象构造万能协议。

---

## 11. AI Coding Rules

任何 Project Leader / Worker 必须遵守：

```text
DO NOT redefine frozen architecture during implementation.

DO NOT promote DSH-specific concepts into architecture semantics.

DO NOT let Brain/Vision/Plugin bypass RuntimeAgent authority.

DO NOT treat Session as message bus or Runtime state.

DO NOT let storage ownership imply semantic ownership.

DO NOT introduce abstractions without a real buyer.

DO NOT open multi-agent / multi-run / typed-hook design in v1.

DO NOT redesign protocols during architecture cleanup.

DO NOT delete historical decisions merely because they are obsolete;
supersede/archive them when they still explain architectural history.

Architecture concepts define the boundary.
Scenarios validate them.
Protocols realize them.
Implementation must conform to them.
```
