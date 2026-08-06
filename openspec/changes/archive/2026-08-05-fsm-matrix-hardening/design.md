## Context

TraversalFSM 的转移矩阵当前承担两个未分离的职责——Handler 门（handler 返值合法性）和异常路由门（StepAsync catch 的 ErrorHandling 路由）——导致矩阵含 3 条死边、错误计数路径间不一致、LastError 跨恢复残留、异常路由对 ErrorHandling 自身不安全。fsm-analyzer 双轨分析（静态矩阵审计 + E2E run 诊断）确认全部缺陷。详细设计见 `docs/refactor/2026-08-05-fsm-matrix-hardening-design.md`，本文档为 OpenSpec 格式的决策摘要。

## Goals / Non-Goals

**Goals:**
- 矩阵职责单一化：只做 Handler 门，22 边 → 19 边，每条边均有至少一个 handler 显式生产
- 异常路由安全化：StepAsync catch 从无条件 ErrorHandling 改为 CanTransitionTo 守卫 + 按状态降级链
- 错误生命周期规范化：ConsecutiveErrors 语义修正为"恢复尝试次数"（单点递增），LastError 处置完毕清零（3 返回点全覆盖）
- PopupHandling 失败补全错误上下文

**Non-Goals:**
- DynamicMatch 容器 FrameComplete（可观测性缺陷，非功能性，见 §3.1）
- Vision 层指纹稳定性 / OCR 变体去重
- SafeActionExecutor deny.default 优化
- 拦截层 NextState 矩阵校验

## Decisions

### D1: 矩阵瘦身 — 移除 3 条死边

按 D-1 先例（PreconditionCheck→Branch 已因"handler 从不返回"移除），移除：
- Execute→Branch（HandleExecuteAsync 只返回 ResultVerify/ErrorHandling；拦截 Step 8 互锁）
- Branch→PreconditionCheck（HandleBranchAsync 只返回 NodeSelect/FrameComplete；拦截层 NextState ∈ {Branch, NodeSelect, FrameComplete}）
- FrameComplete→ErrorHandling（HandleFrameCompleteAsync 为纯 Task.FromResult，无法抛异常）

矩阵：22 → 19 边。

### D2: 异常路由安全化 — CanTransitionTo 守卫 + 降级链

StepAsync catch 不再硬编码 ErrorHandling。守卫 + 按状态降级：
- CanTransitionTo(ErrorHandling)=true → ErrorHandling（5 个状态）
- CanTransitionTo(ErrorHandling)=false → 降级：NodeSelect→Branch, FrameComplete→NodeSelect, ErrorHandling→FrameComplete

降级目标均在 19 边矩阵内合法。降级后可能有步数燃烧（FrameComplete handler 不弹栈 → 循环在帧内直至 max_steps），但优于崩溃。

**备选方案已拒绝**：矩阵补自环（B 方案）——自环不解决"HandleErrorHandlingAsync 自己崩了怎么办"，只会把 DomainValidationException 换成无限重试。

### D3: 递增收敛 — 单一递增点

移除 StepAsync catch（line 130）、HandlePreconditionCheckAsync（line 181）、HandleExecuteAsync catch（line 238）的 IncrementConsecutiveErrors。保留 HandleErrorHandlingAsync（line 592）为唯一递增点。语义：计数器 = 恢复尝试次数，非出错次数。所有路径（异常路由 / handler 显式返回 / PopupHandling 失败）一致 +1/周期。

### D4: LastError 生命周期 — 处置完毕清零

在 HandleErrorHandlingAsync 全部 3 条返回路径前加 `ctx.SetLastError(null)`：主返回（line 630）、page-item 门限（line 608）、consecutive 门限（line 621）。HandleErrorHandlingAsync 是唯一读 LastError 的 handler，读完即清。

### D5: PopupHandling 失败 — 补全错误上下文

弹窗 dismiss 失败时构建 `InvalidOperationException("Popup dismiss failed: dismiss_action=...")`。消息不含枚举名——ErrorClassifier 是大小写不敏感 substring 匹配，`"Permission"` / `"Timeout"` 会误分类。

## Risks / Trade-offs

- **步数燃烧**：降级后 ErrorHandling→FrameComplete→NodeSelect 不弹栈。若 ErrorHandling 反复崩在同一帧，会烧步数直到 max_steps 终止。优于崩溃，但不是最优点。后续可在降级路径加显式 Pop（需在拦截层统一处理，见跨层附录 §3.2）。
- **LastError 消费者不止 HandleErrorHandlingAsync**：引擎 OnErrorAsync 钩子（TraversalEngine:345-348）和 PopupHandler preserve/restore 也读 LastError。清零对二者安全（钩子在入口步触发早于清零；popup restore 在清零后 preserve 捕获 null）。
- **Popup 失败消息不含枚举名**：ErrorClassifier 丢失弹窗类型信息，统一归类为 Unknown。后续若需精确分类，应在 ErrorClassifier 中加 `"popup dismiss failed"` 模式匹配。
