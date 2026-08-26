# Runtime Exploration Capability Roadmap

## 1. Purpose

本文定义 UniClaw Runtime 从“可靠执行动作”向“基于计划自主探索环境”的能力演进路线。

目标：

让 Runtime 能够接收抽象探索目标，根据 Exploration Plan 自主发现环境结构、执行必要操作、记录状态，并基于 Evidence 判断探索完成。

核心原则：

```
Intent
  ↓
Exploration Plan
  ↓
RuntimeAgent Execution
  ↓
Observation / Evidence
  ↓
World Knowledge Update
  ↓
Completion Proof
```

Runtime 不负责决定探索策略。

- UniAgent：负责意图理解和计划生成。
- RuntimeAgent：负责执行计划并保证过程可靠。
- Environment/Semantic Capability：提供观察和理解能力。

本 Roadmap 仅描述能力演进方向，不定义 Runtime 语义，也不产生新的架构、协议、Owner、生命周期或实现授权。已毕业能力的语义以批准的 Graduation Decision、Capability Baseline 和 Spec/Test Evidence 为准。

## Lifecycle Status

Current Next Gate: `PHASE3_MEMORY_HUMAN_GATE`（下一议题；未授权 Apply）

| Phase | Capability | Status |
|---|---|---|
| Phase 0 | Execution Reliability | `COMPLETED` |
| Phase 1 | Exploration Model | `GRADUATED` |
| Phase 2 | Exploration Runtime | `GRADUATED / ACTIVE / CHANGE SET ARCHIVED` |
| Phase 2.5 | UniAgent Emulator Validation | `GRADUATED / ACTIVE / CHANGE ARCHIVED` |
| Phase 3 | Exploration Memory | `READY_FOR_SEPARATE_HUMAN_GATE / NOT_APPLIED` |
| Phase 4 | Dynamic Depth / Advanced Exploration | `NOT_AUTHORIZED` |

生命周期状态来源：[`runtime-exploration-phase2-final-graduation-decision`](runtime-exploration-phase2-final-graduation-decision.md)、[`phase2.5 graduation decision`](uniagent-emulator-validation-harness-graduation-decision.md)（`PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED`；Tier B=Real Emulator，Tier C Physical=WAIVED_BY_HUMAN；2026-08-26 统一归档）与 [`current-gates`](../work/active/current-gates.md)。

Phase 3/4 的 Roadmap 内容仅是未来能力方向，不表示已获授权、已进入设计或已创建实现入口。

---

# 2. Current Capability Baseline

## Phase 0 — Execution Reliability（Completed）

目标：

Runtime 能可靠执行单次动作，并正确判断结果。

已具备：

### Action Execution

- DeviceAction 语义模型
- Adapter Execution Profile
- Scroll Motion Profile

### Observation Reliability

- Scroll Stability Confirmation
- Observation Freshness
- Grounding Before Dispatch

### Recovery

- Adaptive Revisit
- Coverage Ledger
- External Transition Settle
- Fail-closed Recovery

当前 Runtime 已具备：

```
Reliable Action Execution

而不是：

Exploration Intelligence
```

---

# 3. Phase 1 — Exploration Model（Graduated）

## 目标

定义 Runtime 如何理解“探索”。

当前缺口：

Runtime 可以执行动作，但是不知道：

- 什么需要进入
- 什么只需要记录
- 什么代表完成
- 什么代表已经探索过


## 核心能力

### 1. Exploration Plan Contract

定义：

UniAgent → RuntimeAgent 的通信协议。


Plan 描述：

- Exploration Goal
- Depth
- Exploration Rules
- Completion Criteria


示例：

```
Goal:
Explore Settings

Depth:
2

Rules:

Container:
expand

Leaf:
record only

Completion:
all containers visited
```


禁止：

Plan 不包含：

- 坐标
- 点击序列
- 固定页面路径
- UI 文本


---

### 2. Node Exploration Model

统一节点状态：

```
Unknown

 ↓ discover

Discovered

 ↓ classify

Expandable / RecordOnly

 ↓ execute

Visited

 ↓ verify

Completed
```

定义：

Visited ≠ Clicked

Visited:

表示该节点满足当前 Exploration Rule。

---

### 3. Completion Evidence

探索完成必须有证明：

不是：

```
scroll 10 times
```

而是：

```
Container:

discovered:
20

processed:
20

pending:
0

unknown frontier:
0

completion:
true
```

---

# 4. Phase 2 — Exploration Runtime（Graduated / Active / Not Archived）

## 目标

RuntimeAgent 根据 Exploration Plan 自主完成遍历。


## 核心能力


## Exploration Ledger

统一记录：

```
Container

├── discovered nodes
├── visited nodes
├── pending nodes
├── unresolved nodes
└── frontier
```


来源：

融合已有：

- Branch Progress
- Revisit Coverage
- Evidence Ledger


---

## Exploration Loop

形成：

```
Observe

 ↓

Update World State

 ↓

Select Next Frontier

 ↓

Apply Rule

 ↓

Execute

 ↓

Verify

 ↓

Update Evidence
```


RuntimeAgent 不生成计划，只执行计划。

---

## Depth Control

Depth 在一个 Run 内不可变，不允许动态调整。已冻结语义为：

```
Depth = 0

Root RecordOnly
No child expansion
Record root evidence

Depth = 1

Expand Root
Direct children RecordOnly
No recursive entry into child containers

Depth >= 2

Bounded Recursive Exploration
Exhaustive cutoff remains fail-closed
No unbounded recursion
```

Depth 语义来源：[`runtime-exploration-phase2-capability-baseline-freeze`](runtime-exploration-phase2-capability-baseline-freeze.md)。Roadmap 不得反向重定义已冻结的 Runtime 深度语义。

## Phase 2 Frozen Capability Boundary

Phase 2 已冻结能力：

- Exploration Ledger
- Visited Semantics
- Completion Evidence
- Depth Control

Phase 2 不包含：

- Exploration Memory
- Dynamic Depth
- Safety Knowledge
- UniAgent Planner

Exploration Ledger 仅是只读 Evidence projection，不拥有完成权；Completion Evidence 不替代 Agent-owned GoalEvidence，最终完成仍通过既有 FSM authorization path 决定。


---

# 5. Phase 3 — Exploration Memory（Waiting for Human Gate）

状态：`NOT_AUTHORIZED`。本节仅保留未来能力方向，不构成 Phase 3 设计、Owner 选择或实现授权。

## 目标

让 Runtime 从一次探索变成持续学习。


## Safety Knowledge

记录：

```
Node:

Factory Reset

Risk:

High

Previous:

Destructive action

Future Policy:

Observe only
```


---

## Known Environment Knowledge

记录：

```
Settings.Location

Known Container

Expansion safe

Previous exploration complete
```


影响：

下一次 Plan：

```
Intent

+

Historical Knowledge

↓

Optimized Plan
```

---

# 6. Phase 4 — General Exploration Intelligence（Not Authorized）

状态：`NOT_AUTHORIZED`。Dynamic Depth 与其他高级探索能力必须经过新的 Human Gate 和独立授权。

## 目标

支持开放世界探索。


能力：

## Dynamic Depth

根据：

- uncertainty
- risk
- historical knowledge

动态调整探索深度。


---

## Unknown Handling

处理：

- 未知节点
- 新出现页面
- 动态内容
- 不确定语义


---

## Exploration Strategy Selection

支持：

不同目标：

```
Explore all

Find specific capability

Verify security state

Audit configuration
```

对应不同探索策略。

---

# 7. Human Decision Gates

以下情况必须暂停人工确认：

## Gate 1 — New Contract

例如：

- UniAgent ↔ RuntimeAgent 协议变化
- Plan schema 变化


---

## Gate 2 — Ownership Change

例如：

- Agent 是否拥有新决策权
- Runtime 是否承担 Planner 职责


---

## Gate 3 — Architecture Change

例如：

- 新状态系统
- 新 Memory owner
- 新 Evidence owner


---

## Gate 4 — Scenario Knowledge Introduction

例如：

- Settings 特殊规则
- 固定页面逻辑
- 特殊 UI 处理


---

# 8. Development Strategy

推进原则：

```
先模型

↓

再 Contract

↓

再 Runtime 能力

↓

最后场景验证
```

禁止：

```
看到场景失败

↓

增加特殊逻辑

↓

形成场景自动化脚本
```

---

# 9. Success Criteria

最终 Runtime 能达到：

输入：

```
Explore Settings
Depth=2
```

输出：

```
Exploration Completed

Evidence:

Container:
SettingsRoot

Depth:
2

Discovered:
42

Visited:
42

Skipped:
8

SafetyBlocked:
3

Unknown:
0

Completion:
Verified
```

并且：

- 不依赖固定路径
- 不依赖 UI 文本
- 不依赖坐标
- 不依赖场景知识
- 所有行为可通过 Evidence 解释

---

# 10. Current Lifecycle Gate

当前状态：

```
Phase 2 — GRADUATED / ACTIVE / NOT_ARCHIVED
Phase 3 — WAITING_FOR_HUMAN_GATE
Phase 4 — NOT_AUTHORIZED
```

当前不进入 Phase 3/4，不创建相关设计或实现。任何后续推进必须先取得 Human Gate，并按项目治理要求获得独立授权。

仍未授权：

- UniAgent Planner
- 自动策略生成
- Exploration Memory
- Safety Knowledge
- Dynamic Depth
- 场景知识库
