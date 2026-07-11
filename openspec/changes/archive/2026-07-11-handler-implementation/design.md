## Context

TraversalFSM 定义了 8 个 state 和 20 个合法 transition。当前 5 个 handler 需要实装:
- HandlePreconditionCheck: stub → 实装 (precondition 路由)
- HandleResultVerify: stub → 实装 (retry + popup 检测)
- HandleErrorHandling: stub → 实装 (5-strategy recovery)
- HandlePopupHandling: stub → 实装 (dismiss pipeline)
- HandleFrameComplete: minimal → 增强 (stack pop + teardown)

前置约束: TraversalFSM.cs 已有 HandleNodeSelect (实装), HandleExecute (实装), HandleBranch (实装)。RecoveryExecutor (5 ErrorStrategy hooks) 和 PopupHandler (6-step pipeline) 已存在。

FSM transition matrix (已锁定 → D-3):
```
NodeSelect        → PreconditionCheck, Branch
PreconditionCheck → Execute, ErrorHandling
Execute           → ResultVerify, Branch, ErrorHandling
ResultVerify      → Branch, PopupHandling
Branch            → NodeSelect, PreconditionCheck, FrameComplete, ErrorHandling
FrameComplete     → NodeSelect, ErrorHandling
ErrorHandling     → NodeSelect, Execute, FrameComplete, Branch
PopupHandling     → ResultVerify, ErrorHandling
```

## Goals / Non-Goals

**Goals:**
- 5 handler 从 stub/minimal 升级为实装, FSM 主循环可跑完整遍历
- 每个 handler 有单元测试覆盖正常 + 异常路径
- 保持现有 FSM matrix 不变 (不新增/删除 transition)
- 保持 RecoveryExecutor 和 PopupHandler 的 dispatch-table pattern 不变

**Non-Goals:**
- 不做 D-I/D-V/D-IV 架构重构 (后续 P1-P3)
- 不新增 ITraversalHook 扩展点 (后续 P4)
- 不改 FSM transition matrix 的值数 (→ constitution locked)
- 不做 GlobalFSM 实装 (后续 B2)
- 不做 GlobalState 从 ITraversalContext 移除 (→ D-7/M-14, P2 D-III)

## Decisions

### D1: HandlePreconditionCheck — 简化实现, assume pass with explicit logging

Choice: PreconditionCheck 当前无条件通过, 路由到 Execute。ITraversalNode interface 不暴露 Precondition 属性 (设计文档确认), 无法检查 precondition。

Alternatives rejected: (A) 加 Precondition 属性到 ITraversalNode (breaking interface change, D-6); (B) 从 TraversalNode.Precondition 读取 (需要 Graph 层配合)

Rationale: Phase 2.3 的优先级是 FSM 闭环, 不是接口扩展。PreconditionCheck 保持 "assume pass → Execute" 但加 explicit trace logging (RecordStateTransition + TraceCoordinator.RecordDecision), 使 stub→实装的过渡有 observability。真正 precondition 逻辑等 ITraversalNode 扩展 Precondition 后再做 (Phase 3)。

### D2: HandleResultVerify — 3-round retry + vision correction + popup 检测分流

Choice: ResultVerify 检查页面变化 (PageSnapshotManager.HasChanged), 如果变化未检测到则重新调用 IVisionProvider.GetPageAnalysis()。最多 3 round retry。每次 retry 后重新检查。如果检测到 popup 特征 (PopupDetector regex match), 分流到 PopupHandling。3 round 后仍无变化 → Branch (继续遍历)。

Alternatives rejected: (A) 只做 1 round (太少, 视觉变化可能延迟); (B) 无限 retry until change (anti-loop risk); (C) 不检查 popup (popup 会被当作正常页面变化)

Rationale: Python 实现是 3 round retry with vision re-call。Popup 检测是 ResultVerify 的自然扩展 — popup 出现时验证必然失败, 必须先处理 popup 再重验证。

### D3: HandleErrorHandling — 5-strategy RecoveryExecutor dispatch

Choice: ErrorHandling 不自己实现 recovery 逻辑 — 它委托 RecoveryExecutor (已有 dispatch-table pattern)。RecoveryExecutor.Execute(errorStrategy, context) 返回 ErrorRecoveryResult (Strategy + Outcome + RetryCount)。ErrorHandling 根据 RecoveryResult 映射 FSM transition:
- Retry → Execute (重试当前动作)
- Backtrack → NodeSelect (回退选新节点)
- Skip → Branch (跳过当前节点)
- Continue → NodeSelect (假装错误没发生, 选下一节点)
- Abort → FrameComplete (终止遍历)

ErrorStrategy 选择委托 ErrorClassifier + ErrorStrategySelector (已有)。Consecutive error tracking 用 TraversalRuntimeContext._consecutiveErrors (已有)。

Alternatives rejected: (A) ErrorHandling 自己实现 5-strategy (违反 dispatch-table pattern, 与 RecoveryExecutor 重复); (B) Continue → 保持当前 state (FSM 不允许 "stay in same state" — 必须 transition)

Rationale: RecoveryExecutor 已有完整 dispatch + fallback chain (→ patterns/dispatch-table.md)。ErrorHandling 只做 ErrorClassifier → ErrorStrategySelector → RecoveryExecutor.Execute → FSM transition 映射。这是 handler-pipeline pattern 的 "detect-classify-decide-execute" 流程。

### D4: HandlePopupHandling — PopupHandler 6-step pipeline delegation

Choice: PopupHandling 不自己实现 dismiss 逻辑 — 它委托 PopupHandler (已有 6-step pipeline + PopupActionExecutor dispatch-table)。PopupHandler.HandlePopup() 返回 PopupHandlingResult (Success + Action + Description)。PopupHandling 根据 Result 映射 FSM transition:
- Success → ResultVerify (回到验证)
- Failure → ErrorHandling (dismiss 失败, 需要错误处理)

Popup 检测委托 PopupDetector (regex-based) + PopupClassifier (PopupType + DismissStrategy + UrgencyLevel)。这些都已实装。

Alternatives rejected: (A) PopupHandling 自己实现 dismiss (违反 handler-pipeline pattern, 与 PopupHandler 重复); (B) Failure → NodeSelect (比 ErrorHandling 更危险, 失败后直接继续遍历)

Rationale: PopupHandler 已有完整 pipeline (detect → classify → preserve → dispatch → restore → validate → trace)。PopupHandling 只做 PopupDetector → PopupHandler.HandlePopup → FSM transition 映射。

### D5: HandleFrameComplete — stack pop 由 StepOrchestrator 而非 FSM handler 负责

Choice: HandleFrameComplete handler 只决定 FSM transition (→ NodeSelect 或 ErrorHandling)。Stack pop + frame teardown + visited bookkeeping 由 StepOrchestrator 的 Step 10 (FRAME_COMPLETE override) 负责。Handler 保持 minimal — 只 return TraversalState。

Alternatives rejected: (A) Handler 内做 stack pop (FSM handler 不应操作 stack — stack 是 orchestrator 的职责); (B) 不做任何增强 (当前 return NodeSelect 已正确, stack pop 已在 orchestrator step 10)

Rationale: 当前架构中 FSM handler 只决定 next state, 不操作 context 数据结构。Stack pop 在 StepOrchestrator.Step10 已有逻辑 (pop stack + update context)。HandleFrameComplete 的 "minimal" 实现其实是正确的 — 它不需要增强。FSM handler 的职责边界是: 决定 transition, 不操作 stack/cache/context。

**修正**: HandleFrameComplete 不需要增强 — 从 P0 scope 中移除。当前 return NodeSelect 是正确行为, stack pop 在 StepOrchestrator 已实现。575 测试已验证此路径。

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|-----------|
| PreconditionCheck remain "assume pass" | 不做真正 precondition 检查 | 加 trace logging 使 stub→实装过渡可观测; Phase 3 扩展 ITraversalNode.Precondition |
| ResultVerify 3-round retry 可能 insufficient | 视觉变化延迟超 3 round | 3 round 与 Python 对齐; 超限走 Branch (继续遍历, 不阻塞) |
| Continue→NodeSelect mapping | "假装没发生" 继续选新节点而非重试 | 与 RecoveryExecutor.ContinueOutcome 一致; Python 也做此映射 |
| PopupHandler.HandlePopup 可能抛异常 | dispatch-table fallback 返回 back_fallback | PopupHandling 检查 Success=false → ErrorHandling |
| HandleFrameComplete 从 P0 scope 移除 | 5 handler 减为 4 | stack pop 已在 orchestrator; handler minimal 实现正确 |
| PopupDetector substring false positive | "ad" 匹配 "Headphones Pro" 等正常文本 | HandleResultVerify 使用 PageAnalysis.IsPopup 作为权威检测，PopupDetector 仅做已知弹窗的分类 (不做初始扫描) |

### D6: HandleResultVerify popup 检测用 IsPopup 而非 PopupDetector substring scan

Choice: HandleResultVerify 使用 `PageAnalysis.IsPopup`（vision/AI 层权威判定）作为弹窗检测，不使用 PopupDetector regex substring matching 做初始扫描。

Alternatives rejected: (A) 用 PopupDetector substring scan（false positive 风险高——"ad" 匹配 "Headphones Pro"、"ok" 匹配含 ok 的正常按钮文本）; (B) PopupDetector 加 word boundary（增加 regex 复杂度，仍不能完全避免 false positive）

Rationale: PopupDetector 设计用于弹窗分类（已知 IsPopup=true 后判断 PopupType），不适合做"当前页面是否有弹窗"的初始检测。vision/AI 层的 IsPopup 标志是权威判定。这需要在 spec 中更新 PopupDetector 的使用边界。

## Open Questions

1. **ResultVerify retry 间隔**: Python 有 wait_between_retries, C# 当前无。是否加 DelayPerStepMs 或固定 100ms?
2. **ErrorClassifier 输入**: ErrorClassifier 需要 errorType + consecutiveErrors。ErrorHandling 如何构造 ErrorClassifierContext — 从 TraversalRuntimeContext 取还是新创建?
3. **PopupDetector 调用时机**: PopupDetector 在 ResultVerify (retry 后检测) 还是 PopupHandling (进入时检测)? Python 在两处都检测。
