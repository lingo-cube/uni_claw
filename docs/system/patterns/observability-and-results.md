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

---

## 4. Trace / Span 只读查询边界

DriverHost 可以对一个显式 `runId` 的 finalized `TraceRun` 提供进程内只读
summary 和 cursor-paged span projection。分页序列由
`(StartOffsetNs, SpanId)` 稳定生成，filter 只接受冻结字段的精确匹配；查询
不得根据 Goal、Scenario、prompt、reason 或 diagnostic 推断 run、truth 或结果。

初始 live-run placeholder 不等于 finalized trace；终态 trace 即使包含零个
span，也仍是可读取的诊断值。`TraceSpan.Outcome` 只描述局部结构性活动，不是
Runtime Result、GoalEvidence 或 Goal Evaluation。

Harness 可以按显式 `CaptureSessionId` fail-closed 读取一个已发布 capture，验证
schema、manifest、records、artifact、checksum 与可选 TraceRun。读取不扫描、
不修复、不重放、不写回，也不产生 Scenario 选择或 Runtime authority。

这些都是进程内 Data Plane / Harness 能力；当前不增加 DriverHost wire、DSH、
CLI 或 UI 查询方法。
