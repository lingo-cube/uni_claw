## Why

Engine 滚动集成当前是被绕过的:两处 `TryHandleScroll` 都注释"不使用 ScrollHandler(简化逻辑)",硬编码 `stepPercent=0.3` 直接调 mock,导致 `StateMachine/Scroll/` 下 9 个已建已测的管线类(ScrollHandler/JumpDetector/Recovery/Adaptive…)成为冷钝代码;且 engine 通过 `is ScrollableMockVisionService` 运行时下转**硬耦合 Simulation mock**,真实服务无法接入。根因是把滚动当成需要专门 progress/threshold/jump 管线的特殊概念。本次把它回归本质:**滚动 = 一次操作(swipe)+ 对新截图的判断**,与 engine 处理任何操作后重新分析页面同一套机制。

## What Changes

- **重构 engine 滚动模型**:统一 `TryHandleScroll`(Traversal 层)= `SwipeAsync` → `AnalyzeCurrentPageAsync` → 累积 seen 元素集合差分判断终止(滚出未见元素→Continue,全是已见→Stop)。Step 8/9 共用此单站点。
- **BREAKING — 删除 ScrollHandler 7 步管线**:`ScrollHandler`/`ScrollabilityDetector`/`ScrollClassifier`/`ScrollDecider`/`ScrollActionExecutor`/`JumpDetector`/`JumpRecoveryHandler`/`AdaptiveStepCalculator`/`ScrollStatisticsCollector` + 依附类型(`ScrollActionResult`/`ScrollVerifyResult`/`JumpRecoveryResult`/`ScrollContext`/`ScrollAction`)+ `ScrollActionType` enum。跳跃检测/恢复/自适应步长不再作为 engine 概念。
- **删 `TraversalFSM.TryHandleScroll` + `_visitedScrollRanges`**:滚动决策全归 engine,FSM 保持无滚动职责。`HandleBranch` 对 DynamicMatch 耗尽返回 `NodeSelect`。
- **删死代码 `ScrollAwareNodeSelector`**。
- **新增 mock 共享状态 `SimulatedScreen`(mock-only)**:`ScrollableMockVisionService`/`ScrollableMockActionExecutor` 改为它上面的薄适配器;消除两者具体互引。**BREAKING — 删 `ScrollableMockActionExecutor.ScrollDown/ScrollUp/ScrollHistory/GetScrollCount/GetScrollUpCount`**(滚动走 `SwipeAsync`)。
- **新增动态分页内容源 `IScrollContentSource` + `PagedItemGenerator`**:按页码确定性按需生成,配置驱动复用多场景(密集/稀疏/跳跃),取代每场景预构静态 `ScrollDataStore`。`ScrollDataStore`/`ScrollSegment`/`ScrollSegmentBuilder` 迁移为生成器配置后删除。
- **新增 `ScrollBehaviorProfile`**(sealed record,无新 enum):`Cumulative`/windowed/`ScrollJump` 控制滚动效果。
- **指标收口 ActionHistory**:`ScrollCount`/`ScrollUpCount`/`FinalProgress` 从 `IActionExecutor.GetHistory()` 取。**BREAKING(C-11 宪法级)— 移除 `numericAnchor` 的 `jumpDetected`/`jumpRecovered`/`adaptiveStepIncreases`**(管线已删无数据源)。
- **新增架构 guard**:StateMachine/Traversal/Domain 生产代码零 `UniClaw.Core.Simulation` 引用(强化 C-5,消除 engine→Simulation 下转)。
- **显式 supersede D-32~D-48**(decision log append-only)。

## Capabilities

### New Capabilities
- `scroll-mock-content`: 可复用的动态分页滚动 mock 内容源(`IScrollContentSource`/`PagedItemGenerator`)+ 共享模拟屏幕状态(`SimulatedScreen`)+ 滚动行为 profile(`ScrollBehaviorProfile`),配置驱动模拟密集/稀疏/跳跃场景,无需重建 fixture。

### Modified Capabilities
- `scroll-aware-traversal`: 滚动机制从 ScrollHandler 管线 + progress/元素计数循环防护,改为"swipe 操作 + AnalyzeCurrentPageAsync + seen 集合差分终止";DynamicMatch 子节点耗尽触发该循环;FSM 不再持有滚动决策。
- `baseline-scroll-metrics`: 滚动指标来源从 `ScrollableMockActionExecutor.ScrollHistory` 改为 `IActionExecutor.GetHistory()`(ActionHistory);移除 jump 类指标。
- `scroll-metrics-extraction`: 移除 `ScrollableMockActionExecutor.GetScrollCount/GetScrollUpCount` 及 `ScrollHistory`(改由 ActionHistory 统计)。
- `expected-behavior`: **C-11 宪法级** — `NumericAnchor` 移除 `jumpDetected`/`jumpRecovered`/`adaptiveStepIncreases` 三字段,保留 `scrollCount`/`scrollDistance`/`scrollUpCount`/`finalProgress`。
- `phase22-guard-tests`: 新增架构 guard 断言 StateMachine/Traversal/Domain 生产代码零 `UniClaw.Core.Simulation` 引用(强化 C-5 依赖方向)。

## Impact

- **代码**:`src/UniClaw.Core/StateMachine/Scroll/`(整目录删除)、`src/UniClaw.Core/Simulation/Scroll/`(重构:SimulatedScreen + 适配器瘦身 + PagedItemGenerator,删 ScrollDataStore/Segment)、`src/UniClaw.Core/Traversal/StepOrchestrator.cs`(统一 TryHandleScroll)、`src/UniClaw.Core/Traversal/ScrollAwareNodeSelector.cs`(删除)、`src/UniClaw.Core/StateMachine/TraversalFSM.cs`(删 TryHandleScroll/_visitedScrollRanges)、`Simulation/ExpectedBehavior/NumericAnchor`(C-11 schema)。
- **测试**:`ArchitectureGuardTests`(新 guard)、基线测试 + JSON fixture(重标为 PagedItemGenerator 配置)、Scroll 相关单测/集成测试(重写)。
- **文档**:`docs/system/layers/traversal.md`、`docs/system/decisions/log.md`(supersede D-32~D-48)、`docs/system/layers/simulation-baseline.md`。
- **依赖**:消除 engine→Simulation 具体类型耦合;mock 与真实服务代码路径统一。
- **详细设计**:见 `docs/refactor/2026-07-14-scroll-as-action-refactor-design.md`。
