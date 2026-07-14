# 滚动重构设计:滚动 = 操作 + 对新截图的判断

> 日期: 2026-07-14
> 状态: Draft(待评审)
> 分支: feature/refactor
> 相关代码: `src/UniClaw.Core/Traversal/StepOrchestrator.cs`、`src/UniClaw.Core/StateMachine/TraversalFSM.cs`、`src/UniClaw.Core/StateMachine/Scroll/`、`src/UniClaw.Core/Simulation/Scroll/`
> 相关文档: `docs/system/layers/traversal.md`、`docs/system/decisions/log.md`(D-57、D-66)、`docs/system/layers/simulation-baseline.md`

---

## 1. 背景与问题

当前 engine 的滚动集成存在三层问题(详见会话评估):

1. **运行时路径绕过了 ScrollHandler 7 步管线。** `TraversalFSM.TryHandleScroll` 与 `StepOrchestrator.TryHandleScroll` 都注释"直接执行滚动,不使用 ScrollHandler(简化逻辑)",硬编码 `stepPercent = 0.3`,直接调 `ScrollableMockVisionService.SimulateScroll`。`StateMachine/Scroll/` 下 9 个已建已测的管线类(`ScrollHandler`/`JumpDetector`/`JumpRecoveryHandler`/`AdaptiveStepCalculator`/`ScrollClassifier`/`ScrollDecider`/`ScrollabilityDetector`/`ScrollActionExecutor`/`ScrollStatisticsCollector`)实际不在 engine 路径上,是冷钝代码。

2. **engine 硬耦合 Simulation mock 具体类型。** 两处 `TryHandleScroll` 都做 `is not ScrollableMockVisionService` / `is ScrollableMockActionExecutor` 运行时下转。`IVisionProvider` 已有滚动*感知*接口(`HasScroll()`/`GetScrollProgress()`/`IsEndOfList()`,默认实现),但滚动*执行*(`SimulateScroll`)不在接口上,导致必须下转到 mock。

3. **实现不一致 + 死代码。** 两套 `TryHandleScroll`(FSM 版有 D1/D2/D6 防循环,Orchestrator 版极简)逻辑分叉;`ScrollAwareNodeSelector` 是死代码(ScrollHandler 唯一消费者,但未接入,`GetCurrentPageAnalysis()` 永远返回 null)。

根因:把滚动当成需要专门管线(progress/threshold/jump-detect/verify/recover)的特殊领域概念来处理。

## 2. 核心洞察

**滚动本质上就是一个操作(action),滚动逻辑 = 执行滚动操作 + 对操作后新截图的判断。** 这与 engine 处理任何其它操作(点击、返回)后重新分析页面是同一套机制。无需专门的滚动管线、progress 协议、跳跃检测协议——判断来自重新 `AnalyzeCurrentPageAsync` 后的 `PageAnalysis` 对比。

## 3. 目标与成功标准

- engine 里**真实执行**的滚动逻辑对 mock 与真实服务**代码路径完全相同**,差别仅在 `IActionExecutor` / `IVisionProvider` 的实现。
- engine 生产代码(StateMachine/Traversal/Domain)**零 Simulation 类型引用**(CI guard 强制)。
- 不同 mock(`ScrollBehaviorProfile` / `ScrollDataStore`)表达不同滚动效果:faithful / 稀疏 / 密集 / 窗口跳跃 / 过冲。
- 收敛两处 `TryHandleScroll` 为单站点;删除死代码与冗余管线。
- 真实服务 `IsEndOfList` 不可靠时,到底检测仍鲁棒(经验式:滚一下没出现新元素 = 到底)。
- 既有 LongList/sparse/dense 基线在重标后通过;新增跳跃场景基线验证循环仍能终止。

## 4. 核心设计:滚动 = 操作 + 判断

engine 里真实执行的滚动循环(取代两处 `TryHandleScroll`):

```
DynamicMatch 子节点耗尽(且页面可滚动)
   │
   ▼  ① 操作:ctx.Action.SwipeAsync(垂直 swipe,在滚动区域)
   │     · mock:推进模拟视口
   │     · 真实:执行真实 swipe 手势
   ▼  ② 重新截图:ctx.Vision.AnalyzeCurrentPageAsync() → 新 PageAnalysis
   ▼  ③ 判断(详见 §6 循环契约)
   │     · 滚出未见元素 → 继续 NodeSelect
   │     · 滚动后全是已见元素 → 到底 → FrameComplete(非根则 PressBack)
```

**复用现有机制,零新接口:**

| 角色 | 现有组件 |
|------|---------|
| 滚动操作 | `IActionExecutor.SwipeAsync(startX,startY,endX,endY,durationMs)`(mock 与真实都实现) |
| 重新截图 | `IVisionProvider.AnalyzeCurrentPageAsync` |
| 判断是否有新内容 | 累积 seen 元素 id 集合(per-frame,存 `TraversalRuntimeContext`)的差集:本次滚动后出现未见元素 = 有进展 |
| 重新生成子节点 | `DynamicChildManager.Invalidate`(随后 NodeSelect 正常生成/选择子节点) |

**不加新 enum、不加新接口方法。** 滚动就是一次垂直 swipe(swipe 距离 = 滚动步长)。方向:向下 swipe(内容上移)= 向下滚动发现更多;向上 swipe = 回顶场景。v1 滚动坐标用默认中心垂直 swipe(如 (0.5,0.7)→(0.5,0.3)),滚动区域坐标未来可从 PageAnalysis 取。

**真实服务的"等待稳定":** 真实 swipe 与截图间需等待页面动画稳定(已有 `WaitAsync` / page-stability 机制承接);mock 同步推进,无需等待。engine 循环序列(swipe → analyze)两场景相同。

## 5. mock 联动与分层:`SimulatedScreen`(mock-only)

swipe 与 analyze 是 engine 的两次独立接口调用,mock 侧必须作用在**同一个屏幕状态**上。解法:把可变屏幕状态抽成一个共享对象 `SimulatedScreen`,两个 mock 适配器都引用它(构造时注入同一实例)。

```
engine 层 (StateMachine / Traversal)          ← 定义接口, 零 Simulation 引用
  · IVisionProvider      (StateMachine/StepContext.cs)
  · IActionExecutor      (Traversal/IGraphTraversalEngine.cs)
        ▲                        ▲
        │ 实现                   │ 实现
  ScrollableMockVisionService ───┤
  ScrollableMockActionExecutor ──┤   薄适配器, 无互引
        │                        │
        └────► SimulatedScreen ◄─┘   ← 只在 Simulation 层, mock 私有协调
```

**`SimulatedScreen` 只用于 mock,绝不嵌入 engine 流程。** engine 只看到两个接口调用。`SimulatedScreen` 拥有完整模拟设备状态:

- `currentPageId` + 导航历史(承接现有 `SimulateAction`/`NavigateBack`)
- 视口位置(progress / 窗口)
- `ScrollDataStore`(元素数据)
- `ScrollBehaviorProfile`(滚动行为,见 §7)
- 方法:`ApplySwipe(vector)` 按 profile 推进视口;`GetVisibleElements()` 按 profile 可见性模型返回;`GetPageAnalysis()` 构造 `PageAnalysis`;导航方法。

两个 mock 适配器变为无状态薄包装:`ScrollableMockVisionService.AnalyzeCurrentPageAsync → _screen.GetPageAnalysis()`;`ScrollableMockActionExecutor.SwipeAsync → _screen.ApplySwipe(...)`(并记 ActionRecord)。`ScrollableMockActionExecutor` 不再引用 `ScrollableMockVisionService` 具体类型——两者都只依赖 `SimulatedScreen`。

**强制保证:** 新增架构 guard 断言 StateMachine/Traversal/Domain 生产代码不得引用 `UniClaw.Core.Simulation`(无 `using`、无类型引用)。`SimulatedScreen`(及任何 Simulation 类型)物理上无法进入 engine 流程,CI 直接拦截。

## 6. 统一 `TryHandleScroll` 循环契约

单站点,位于 Traversal 层(StepOrchestrator Step 8 与 Step 9 共用)。

```
TryHandleScroll(ctx, frame) → { Continue, Stop }
  (seenIds = ctx.Context.GetSeenElementIds(frame.NodeId)  // per-frame 累积集合)
 1. ctx.Action.SwipeAsync(垂直 swipe)              // ① 操作
 2. after = ctx.Vision.AnalyzeCurrentPageAsync()    // ② 重新截图
 3. ctx.Context.SetCurrentPageAnalysis(after)
 4. ctx.ChildMgr.Invalidate(frame.NodeId)
 5. afterIds = after.Items 的元素 id 集合
 6. newIds = afterIds − seenIds; seenIds ∪= afterIds
 7. 若 newIds 非空: return Continue     // 滚出未见元素 → 有进展
 8. 否则: return Stop                   // 全是已见元素 = 到底/循环
```

- **终止条件 = 一次滚动后没有出现任何未见元素。** 经验式到底检测,对真实服务鲁棒(`IsEndOfList` 不可靠时仍成立:滚一下全是已见内容 = 到底)。
- **循环防护** = seen 集合差分本身,无需 `_visitedScrollRanges` 进度范围去重(随 FSM 版删除)、无需重试阈值。
- seenIds 为 per-frame(per node-visit)累积集合,存 `TraversalRuntimeContext`,具体生命周期(帧 pop 时清理)为实施细节。
- 可配置:swipe 距离/duration(滚动步长)。
- 返回 Continue → orchestrator 继续 NodeSelect(由 NodeSelect 正常生成/选择新子节点);返回 Stop → 根节点 FrameComplete,非根节点 PressBack + Pop。
- **方向:** 本循环针对**前向(向下)发现**滚动。向上/回顶场景(`wifi-list-scroll-back-to-top` 类)终止条件不同(到达顶部),作为专门场景处理,不套用本通用循环(见 §13 延后)。

## 7. Mock 多形态:`ScrollBehaviorProfile`

`ScrollableMockVisionService` 当前是**累积模型**(threshold≤progress 的元素全可见),无法表达跳跃(元素不会滚出视野)。新增视口模型,`ScrollBehaviorProfile` 替代被删的 `ScrollHandlerConfig` 在 mock 中的角色:

| Profile | 行为 | 用途 |
|---------|------|------|
| Cumulative(默认) | 累积可见,step 线性推进 | 复现当前基线,faithful scroll |
| Windowed | 视口窗口内可见,元素可滚出 | 真实滚动语义 |
| Windowed + Jump | 视口推进过冲/跳段 | 验证循环在跳跃下仍终止 |

`ScrollBehaviorProfile` 字段:`VisibilityMode {Cumulative, Windowed}`、`ViewportSize`(窗口模式用)、`SwipeToAdvanceMap`(swipe 距离→视口推进;线性默认)、可选 `JumpProfile {None, Overshoot(factor), Skip(segmentCount)}`、`ProgressEpsilon`(从 `ScrollHandlerConfig` 迁入)。

不同 profile + 不同 `ScrollDataStore` = 稀疏/密集/跳跃/过冲。**engine 循环对所有 profile 完全相同**,只看新 PageAnalysis。

## 8. 删除清单

**删除(9 管线类 + 依附类型):**
`ScrollHandler`、`ScrollabilityDetector`、`ScrollClassifier`、`ScrollDecider`、`ScrollActionExecutor`、`JumpDetector`、`JumpRecoveryHandler`、`AdaptiveStepCalculator`、`ScrollStatisticsCollector`、`ScrollActionResult`、`ScrollVerifyResult`、`JumpRecoveryResult`、`ScrollContext`、`ScrollAction`、`ScrollActionType`。

(实施时编译器确认无其它引用;若有零散引用随管线一并清理。)

**保留并改造:**
- `ScrollableMockVisionService` → 薄适配器,委托 `SimulatedScreen`。
- `ScrollableMockActionExecutor` → 薄适配器,委托 `SimulatedScreen`;删除 `ScrollDown`/`ScrollUp`/`ScrollHistory`/`GetScrollCount`/`GetScrollUpCount`(滚动改走 `SwipeAsync`,指标改从 ActionHistory 取)。
- `ScrollDataStore`、`ScrollState`、`ScrollSegment`、`ScrollSegmentBuilder` → mock 数据原语,保留。
- `ScrollHandlerConfig` → 仅 `ProgressEpsilon` 迁入 `ScrollBehaviorProfile`,其余随管线删。

**清理:**
- 删除 `ScrollAwareNodeSelector.cs`(死代码)。
- 删除 `TraversalFSM.TryHandleScroll` + `_visitedScrollRanges`;`HandleBranch` 对 DynamicMatch 耗尽直接返回 `NodeSelect`(滚动决策归 engine)。
- `StepOrchestrator` 两处 `TryHandleScroll` 收敛为 §6 单循环。

## 9. 指标(收口到 ActionHistory)

- `ScrollCount` / `ScrollUpCount` = 数 `IActionExecutor.GetHistory()` 中向下 / 向上 swipe(ActionRecord 方向由 swipe 坐标或参数判定)。
- `FinalProgress` / `ScrollDistance`:mock 可从 `SimulatedScreen` 视口位置算(可选);真实无则 N/A。
- `JumpDetected` / `JumpRecovered` / `AdaptiveStepIncreases`:**移除**(管线已删)。`openspec/specs/baseline-scroll-metrics` 同步更新。

## 10. 测试

- **`TryHandleScroll` 单测**(fake vision+action):有新元素→Continue / 指纹未变→Stop / 稀疏 / 窗口跳跃下仍终止。
- **`ScrollBehaviorProfile` 单测**:Cumulative vs Windowed+Jump 产出不同 PageAnalysis;`SimulatedScreen` 两适配器联动一致(ApplySwipe 后 Analyze 反映新视口)。
- **架构 guard 测试**:StateMachine/Traversal/Domain 生产代码无 `UniClaw.Core.Simulation` 引用。
- **集成**:LongList/sparse/dense 基线重标后通过;**新增 Windowed+Jump 场景基线**(验证循环在跳跃下仍能终止、不漏终止条件)。

## 11. 迁移分期(每期测试绿)

1. 引入统一 `TryHandleScroll`(§6 action+judge),Step 8/9 接它;删除 FSM 版 + `ScrollAwareNodeSelector`。
2. 抽 `SimulatedScreen`,两个 mock 适配器改造委托它;删除 `ScrollableMockActionExecutor.ScrollDown/Up/History`。
3. 删除 9 管线类 + 依附类型;`ScrollHandlerConfig.ProgressEpsilon` 迁入 `ScrollBehaviorProfile`。
4. mock 增 `ScrollBehaviorProfile`(Cumulative/Windowed/Jump)。
5. 指标改 ActionHistory;基线重标 + 新增 Jump 基线。
6. 文档更新 + 架构 guard 上线。

## 12. 文档更新

- `docs/system/layers/traversal.md` §2:滚动改为"操作+判断"模型;删除 ScrollHandler 集成描述;更新 D-57/D-66 指向。
- `docs/system/decisions/log.md`:新增决策(滚动 = 操作 + 判断;删 9 管线;`SimulatedScreen` mock-only + Simulation 引用 guard),修正此前"接入 ScrollHandler"方向。
- `docs/system/layers/simulation-baseline.md`:移除 jump 类指标字段;更新标定流程。
- 无新增 enum(滚动走 `SwipeAsync`)→ 不触发 `constitution/locked-enums.md`。

## 13. 范围外 / 延后

- 真实(非 mock)`VisionService`/`ActionExecutor` 实现 —— 本次只保证接缝可用,不构建真实实现。
- 滚动区域坐标从 PageAnalysis 精确推导 —— v1 用默认中心垂直 swipe。
- 自适应步长(swipe 距离随历史动态调整)—— 本次步长固定可配;若需要,未来在循环里按"上次新元素数"微调,无需恢复被删的 `AdaptiveStepCalculator`。

## 14. 已解决的决策日志

| # | 决策 | 选择 |
|---|------|------|
| Q1 | "真实测试场景"含义 | 两者都要:多形态 mock 为主验证 + 接口让真实服务未来可插入 |
| 方案 | 重构方向 | 方案 1 → 演化为"操作+判断"极简模型(用户纠偏后) |
| §A' | 滚动模型 | 操作(swipe)+ 操作后截图判断;非专门管线 |
| 管线 | 9 个 Scroll 类处置 | 删除(冷钝代码) |
| 联动 | swipe 与 analyze 合并/拆开 | 拆开(两次接口调用);mock 侧共享 `SimulatedScreen` |
| 分层 | `SimulatedScreen` 归属 | mock-only,不进 engine;架构 guard 强制无 Simulation 引用 |
