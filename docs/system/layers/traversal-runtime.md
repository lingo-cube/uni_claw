# Traversal Runtime Layer

## 1. 定义

Traversal 是局部、确定性的执行 Kernel。

它负责：

> "已经知道当前要执行一个局部步骤以后，如何可靠执行这一小步。"

典型执行协议：

```text
Select
→ Check
→ Execute
→ Verify
→ Branch / Complete
```

具体状态命名可以根据实现调整。

---

## 2. Traversal 负责

Traversal 可以负责：

- candidate selection；
- precondition check；
- target resolve；
- operation execute；
- result verify；
- retry；
- re-resolve；
- re-observe；
- step bookkeeping；
- action result；
- structured failure / trap emission。

---

## 3. Traversal 不负责

Traversal 不负责：

- 世界级语义理解；
- Agent Goal；
- App 是否已经退出；
- 当前 Plan 是否应该重写；
- Container Semantic Identity 最终裁决；
- Agent-level Recovery；
- 直接调用 LLM 做高层决策。

Traversal 应成为 execution kernel，而不是"聪明的大脑"。

---

## 4. 目标属性

Traversal 最终目标：

```text
Deterministic
Testable
Observable
Replayable as much as practical
```

---

## 5. Step Scope

Step Scope 用于局部动作问题，例如：

- click timeout
- coordinate stale
- temporary target missing

Traversal 可以尝试：

```text
Retry
ReResolve
ReObserve
```

如果仍然无法证明可以安全继续，应返回结构化 Trap / Result 向上升级。

Traversal 不得自行执行 Agent Scope Recovery，例如：

- Launch App
- Go Home
- Re-plan Goal

---

## 6. FSM 与 Traversal

Traversal 可能使用 FSM，但不是必须。

只有当：

- 生命周期清晰；
- 状态有限；
- 转换值得约束；
- 状态变化值得测试和 Trace；

时才引入 FSM。

FSM 只负责 Protocol Transition。

语义判断和策略决策必须位于 FSM 外部。
