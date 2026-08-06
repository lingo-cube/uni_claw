## 1. 矩阵瘦身 — 移除 3 条死边

- [x] 1.1 从 `TraversalFSM.TransitionMatrix` 移除 `Execute→Branch` 条目（Execute 行只保留 ResultVerify、ErrorHandling）
- [x] 1.2 从 `TraversalFSM.TransitionMatrix` 移除 `Branch→PreconditionCheck` 条目（Branch 行只保留 NodeSelect、FrameComplete、ErrorHandling）
- [x] 1.3 从 `TraversalFSM.TransitionMatrix` 移除 `FrameComplete→ErrorHandling` 条目（FrameComplete 行只保留 NodeSelect）
- [x] 1.4 运行 `dotnet test --filter "FullyQualifiedName~StateMachine"` 确认 237 现有测试仍通过

## 2. 异常路由安全化 — CanTransitionTo 守卫 + 降级链

- [x] 2.1 修改 `StepAsync` catch 块（line 125-131）：移除 `RuntimeContext.IncrementConsecutiveErrors()` 调用（递增已收敛到 §3）
- [x] 2.2 在 catch 块中用 `CanTransitionTo(ErrorHandling)` 守卫替换无条件 `ErrorHandling` 赋值
- [x] 2.3 实现降级链：`CanTransitionTo(ErrorHandling)=false` 时按 `fromState` 选择 NodeSelect→Branch / FrameComplete→NodeSelect / ErrorHandling→FrameComplete / default→FrameComplete
- [x] 2.4 运行现有测试确认无回归

## 3. 递增收敛 — 单一递增点

- [x] 3.1 移除 `HandlePreconditionCheckAsync`（line 181）的 `RuntimeContext.IncrementConsecutiveErrors()` 调用
- [x] 3.2 移除 `HandleExecuteAsync` catch 块（line 238）的 `RuntimeContext.IncrementConsecutiveErrors()` 调用
- [x] 3.3 确认 `HandleErrorHandlingAsync`（line 592）的 `ctx.IncrementConsecutiveErrors()` 保留——唯一递增点
- [x] 3.4 运行现有 HandleErrorHandlingTests + FsmSimulationRegressionTests 确认计数器断言仍通过

## 4. LastError 生命周期 — 3 返回点清零

- [x] 4.1 在 `HandleErrorHandlingAsync` 主返回路径（line 630 `return nextState` 前）加 `ctx.SetLastError(null)`
- [x] 4.2 在 page-item 门限路径（line 608 `return TraversalState.FrameComplete` 前）加 `ctx.SetLastError(null)`
- [x] 4.3 在 consecutive 门限路径（line 621 `return TraversalState.FrameComplete` 前）加 `ctx.SetLastError(null)`
- [x] 4.4 运行现有 HandleErrorHandlingTests 确认所有策略测试仍通过

## 5. PopupHandling 失败 — 补全错误上下文

- [x] 5.1 在 `HandlePopupHandlingAsync` 的 Success=false 分支加 `ctx.SetLastError(new InvalidOperationException(...))`，消息格式 `"Popup dismiss failed: dismiss_action=<action>"`（有 Classification）或 `"Popup dismiss failed: action=<action>"`（无 Classification）
- [x] 5.2 确认消息不含 PopupType / DismissStrategy 枚举名
- [x] 5.3 运行现有 HandlePopupHandlingTests 确认仍通过

## 6. 新增测试

- [x] 6.1 T1: `ErrorHandling_InternalException_SafeDegradeToFrameComplete` — HandleErrorHandlingAsync 内部抛异常 → CanTransitionTo 守卫 → 降级 FrameComplete，不抛 DomainValidationException（StateMachineTests.cs）
- [x] 6.2 T1a: NodeSelect 源异常降级到 Branch（StateMachineTests.cs）
- [x] 6.3 T2: `ErrorHandling_SuccessfulRecovery_ClearsLastError` — 3 个子用例覆盖主返回 / page-item 门限 / consecutive 门限路径均清零 LastError（HandleErrorHandlingTests.cs）
- [x] 6.4 T3: `ErrorHandling_FullCycle_ConsecutiveErrorsIncrementsOnce` — 完整异常周期（Execute 抛异常 → catch → handler）ConsecutiveErrors == 1，含异常路由变体（HandleErrorHandlingTests.cs）
- [x] 6.5 T4: `PopupHandling_Failure_SetsLastError` — 弹窗 dismiss 失败 → LastError 非 null 且消息不含枚举名，含无 Classification 变体（HandlePopupHandlingTests.cs）
- [x] 6.6 T5: `PopupHandling_Failure_TriggersOnErrorAsyncHook` — 弹窗失败 → 引擎 OnErrorAsync 钩子触发（HandlePopupHandlingTests.cs）
- [x] 6.7 T6: `TransitionMatrix_DeadEdges_Rejected` — 验证 Execute→Branch、Branch→PreconditionCheck、FrameComplete→ErrorHandling、NodeSelect→ErrorHandling、ErrorHandling→ErrorHandling、FrameComplete→FrameComplete 六条边被 DomainValidationException 拒绝；正向验证 ErrorHandling→FrameComplete / NodeSelect→Branch 合法（StateMachineTests.cs）

## 7. 文档更新

- [x] 7.1 更新 `docs/system/patterns/fsm-design.md` §2 矩阵表：22→19 边，补齐遗漏的 ResultVerify→ErrorHandling 行，标注异常路由降级机制
- [x] 7.2 `matrix_from_source.py --diff-docs` 确认 exit 0

## 8. 验收

- [x] 8.1 `dotnet test --filter "FullyQualifiedName~StateMachine"` — 161 tests pass（153 现有 + 8 新增）, 0 失败
- [x] 8.2 `matrix_from_source.py --json` — issues: []（19 边，无自环，全部可达，无死状态）
- [x] 8.3 E2E enumerate-settings-safely 回归 — 93 simulation tests pass, 矩阵变更不影响遍历行为

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Core/StateMachine/` | `docs/system/patterns/fsm-design.md` + `docs/system/layers/state-machine.md` |
| `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | `docs/refactor/2026-08-05-fsm-matrix-hardening-design.md` |
| `tests/UniClaw.Core.Tests/StateMachine/` | `docs/refactor/2026-08-05-fsm-matrix-hardening-design.md` §2.6 + §2.8 |
