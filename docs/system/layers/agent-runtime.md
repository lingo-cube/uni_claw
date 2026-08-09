# Agent Runtime Layer

## 1. 定义

Agent 是一次 Run 范围内的最高控制者。

它拥有整个执行过程的目标，以及任务范围内最高语义判断权。

Agent 回答：

> "为了完成当前目标，现在应该做什么？"

---

## 2. Agent 负责

Agent 负责：

- Intent；
- Goal；
- Plan；
- Run-level lifecycle；
- World Belief；
- 当前 Container 管理；
- Container 切换；
- Container Rebind / Invalidate；
- Trap Scope 判断；
- Agent-level Recovery；
- Agent-owned cross-Container branch progress；
- Recovery 后 retained branch-effect validity 的 fresh evidence interpretation；
- 对 supplied fresh Observation 中 bounded candidates 的 Goal-intent authorization；
- Re-plan；
- Memory 协调；
- AI Decision 协调；
- 高层完成条件；
- 最终成功 / 失败判断。

Agent 是 Task-global Semantic Authority。

---

## 3. Agent 不负责

Agent 不应该直接承担：

- OCR；
- 点击实现；
- Scroll 实现；
- 坐标转换；
- 单步 Traversal 状态推进；
- 大量页面元素 bookkeeping；
- 每一种具体 App 的特殊规则。

`AgentRuntime` 是 Controller / Control Plane。

禁止把它设计成新的 God Object。

Agent 可以编排能力，但具体能力必须由明确组件提供。

---

## 4. Global Lifecycle

整个 Run 需要非常简单的生命周期，例如：

```text
Idle
Initializing
Running
Paused
Completed
Failed
Terminated
```

具体枚举允许实现时讨论。

Global Lifecycle 只回答：

> "这个 Run 当前处于什么生命周期？"

它不承担：

- 世界判断；
- 页面恢复；
- Agent Intelligence。

---

## 5. Startup

正式执行之前必须建立可信工作环境。

Startup 是明确的生命周期阶段，可能包括：

```text
Attach Device
→ Initialize Capabilities
→ Launch / Bind Application
→ Wait Until Observable
→ Observe
→ Resolve Initial Semantic World
→ Establish Initial Container
→ Establish Recovery Anchor
→ Ready
```

只有 Startup 成功以后，Runtime 才进入正式执行状态。

---

## 6. Recovery Anchor

Recovery Anchor 是 Startup 的重要产物。

它不是 Traversal Root Node。

它表示：

> "当 Agent 完全迷失时，至少可以恢复到这里重新建立可信世界。"

概念上可能包含：

```text
RecoveryAnchor
{
    ApplicationIdentity
    EntryStrategy
    ExpectedSemanticEntry
    RestoreRecipe
    VerificationCriteria
}
```

例如：

```text
Application:
Android Settings

RestoreRecipe:
ColdLaunch Settings
→ Wait
→ Observe
→ Verify Settings Main
```

如果后续进入：

- Desktop
- Unknown App
- Unknown Page

最坏情况下应能够：

```text
Current World
→ Recovery Anchor
→ Reconstruct Expected Container
→ Resume
```

---

## 7. Completion Authority

Container Completion 与 Goal Completion 是不同概念。

禁止：

```text
Traversal graph 遍历完了
= Task completed
```

Agent 对最终 Goal Completion 拥有 Authority。

例如：

```text
Goal:
Enable WiFi

Completion Evidence:
Observed WiFi State = ON
```

而不是仅仅：

```text
Node visited
```

### Bounded candidate authorization

SC-P3-CAND-006 的 authorization criterion 由 Goal 注入，并只对 supplied fresh Observation 中的 bounded candidates 产生三值 evidence。Agent 是唯一 semantic authorization authority；Observation 只证明 candidate 被观察到，Traversal 只允许执行已经由 Agent 授权的 candidate。

Task 1.1 提供 immutable evidence value 与 optional Goal criterion。Task 2.1 在 evaluator 存在时由 Agent 对 initial active Container 的同一 fresh Observation 做一次 stable-order classification：`false` / `null` 记录无 Action/ActionId 的 Trace evidence，first `true` 最多生成一个 transient existing Tap step。没有 `true` 时显式 non-completion 且零 candidate dispatch；evaluator 缺席时 fixed-Plan behavior 不变。无论 authorization 结果如何，required work 与最终 Goal completion 仍由 Agent 基于 GoalEvidence 判断。

### Recovery 后的 progress validity

Agent memory 中仍存在 progress，不等于该 progress 在 recovered world 中仍有效。对于 SC-P3-CAND-005 的 one-parent bounded Scenario：

```text
historical completion sequence at/before Trap.Observed
→ retained history only
→ verified Recovery + fresh Observation
→ evaluate approved PlanStep branch-effect criterion
```

- `true`：Agent 可以用 fresh sequence revalidate，并在已经恢复到 suspended parent 时跳过已完成前缀；
- `false`：Agent 排除该 completion；
- `null` / criterion absent：Agent 保留历史 provenance 但不允许其贡献，并显式失败/升级。

这些结果是 Agent 对 fresh evidence 的即时判断，不是新的 validity state、RecoveryState 或 mutable owner。最终完成仍只能来自 satisfied GoalEvidence。

---

## 8. Pause / Resume / Shutdown

Runtime 从第一阶段就应该正确考虑：

- `CancellationToken`
- Pause
- Resume
- Shutdown

Pause 属于 Run Lifecycle。

不要让暂停逻辑渗透到每个 Handler。

Run Controller 负责：

> "现在是否允许继续推进？"

局部组件响应 cancellation，不自行维护全局 Pause 状态。

---

## 9. Concurrency

当前明确假设：

```text
一个 Device = 一个 Active Run
```

不要为了未来假想需求设计：

- Multi-task scheduler
- Multi-agent arbitration
- Concurrent UI actions

真实 Device Action 必须序列化。

允许并发的是非破坏性的后台工作，例如：

- Semantic analysis
- Trace persistence
- Memory enrichment

异步结果必须考虑：

- Observation Version
- Freshness
- Cancellation

旧结果不能覆盖更新的 World State。
