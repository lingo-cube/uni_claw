# Action Safety, Cancellation and Concurrency

## 1. Action Safety

真实 UI Action 有副作用。

必须考虑：

- action 是否已经发送；
- action 是否可能执行但响应丢失；
- retry 是否安全；
- action 是否幂等；
- 是否需要重新 Observe 后再决定。

建议概念上区分：

```text
Action Intent
Action Dispatch Record
Action Result
Post-action Observation
```

禁止简单：

```text
catch timeout
→ retry click
```

因为第一次 click 可能已经成功。

高风险或非幂等操作必须优先：

```text
Observe
→ determine actual state
→ decide retry
```

---

## 2. Cancellation / Pause / Shutdown

Runtime 从第一阶段就应考虑：

- CancellationToken
- Pause
- Resume
- Shutdown

Pause 属于 Run lifecycle。

不要让暂停逻辑渗透到每个业务 Handler。

Run Controller 负责：

> "现在是否允许继续推进。"

局部组件响应 cancellation，不自行维护整个 Run 的 Pause 状态。

---

## 3. Concurrency

当前假设：

```text
一个 Device = 一个 Active Run
```

不要为了未来假想需求设计：

- Multi-task scheduler
- Multi-agent arbitration
- Concurrent UI actions

真实 Device Action 保持序列化。

允许并发的是非破坏性的后台工作，例如：

- Semantic analysis
- Trace persistence
- Memory enrichment

异步结果必须携带或关联：

- Observation Version
- Freshness
- Cancellation

禁止旧结果覆盖新状态。
