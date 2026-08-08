# Observability, Results and Completion Evidence

## 1. Observability 是一等能力

Observability 不是后补日志。

至少需要追踪：

- Run
- Startup
- Observation
- WorldBelief changes
- Container lifecycle
- Traversal Step
- Action
- Verification
- Trap
- Recovery
- AI Decision
- Completion

推荐统一关联 ID：

- RunId
- ContainerId
- StepId
- ObservationId
- ActionId
- RecoveryId

系统必须能够回答：

> "为什么系统做了这个动作？"

而不仅是：

> "系统做了什么动作？"

---

## 2. Result 类型必须表达语义

优先使用明确 Result，例如：

- TraversalStepResult
- ContainerResult
- RecoveryResult
- StartupResult

不要大量依赖：

- bool
- null
- magic string
- mutable flags

Result 应能表达：

```text
Success
Incomplete
Retryable
Trap
Failed
Completed
```

但不要为了类型丰富创建几十种无意义 wrapper。

---

## 3. Completion Evidence

Completion 必须与 Goal 对齐。

禁止：

```text
Traversal graph exhausted
= Goal Completed
```

例如：

```text
Goal:
Enable WiFi

Completion Evidence:
Observed WiFi State = ON
```

Container Completion 与 Goal Completion 必须区分。

最终 Goal Completion Authority 属于 Agent。
