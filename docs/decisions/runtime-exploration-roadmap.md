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

---

# 2. Current Capability Baseline

## Phase 0 — Execution Reliability（已完成）

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

# 3. Phase 1 — Exploration Model

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

# 4. Phase 2 — Exploration Runtime

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

支持：

```
Depth = 1

Root only


Depth = 2

Root + children


Depth = N

Full exploration
```


---

# 5. Phase 3 — Exploration Memory

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

# 6. Phase 4 — General Exploration Intelligence

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

# 10. Current Next Step

进入：

```
Phase 1 — Exploration Model
```

优先设计：

1. Exploration Plan Contract
2. Node Exploration State Model
3. Completion Evidence Model

暂不实现：

- UniAgent Planner
- 自动策略生成
- 场景知识库

先让 RuntimeAgent 跑通抽象探索闭环。


