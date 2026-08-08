# FSM Protocol Pattern

## 1. 定义

FSM 在 UniClaw 中用于：

> 表达有限生命周期或执行协议，并约束合法状态转换。

---

## 2. FSM 负责

FSM 的职责包括：

- current phase；
- legal transition；
- lifecycle；
- protocol；
- deterministic progression；
- observability。

---

## 3. FSM 不负责

FSM 不负责：

- Semantic Reasoning；
- Planning；
- World Model；
- Memory；
- AI Decision；
- Page Identity；
- Container Identity；
- 高层 Recovery Strategy。

原则：

```text
State belongs to Runtime.
Truth belongs to World Model.
Decision belongs to Agent / Policy.
Transition belongs to FSM.
```

---

## 4. 不机械创建 FSM

不要因为系统有多个层，就机械创建：

- AgentFSM
- ContainerFSM
- TraversalFSM
- RecoveryFSM

只有当：

- 生命周期清晰；
- 状态有限；
- 转换值得约束；
- 状态变化值得测试和 Trace；

时才创建 FSM。

---

## 5. Global Lifecycle 与 Traversal Protocol

Global Lifecycle 用于 Run lifecycle。

Traversal FSM（如果存在）用于局部步骤协议。

两者不能通过"FSM 当前状态"来证明外部世界真实状态。

FSM 是协议骨架，不是 World Model。
