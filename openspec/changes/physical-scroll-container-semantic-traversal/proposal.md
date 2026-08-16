# Proposal: Physical Scroll Container Semantic Traversal（Agent 自主同容器视口滚动语义闭环）

| 属性 | 内容 |
|------|------|
| Change ID | `physical-scroll-container-semantic-traversal` |
| 状态 | Proposed（**本 change 只产出 proposal / design / specs / tasks，不实施**） |
| 类型 | Vertical Slice（毕业语义环之上的第一条 **Agent 自主同容器视口滚动** 真实切片） |
| 日期 | 2026-08-14 |
| 分支 | `uni-agent` |
| 上游 | `docs/decisions/physical-settings-to-wifi-multi-level-graduation-decision.md`（RealityLevel=EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP） |
| 前置事实 | 语义环 `RunSemanticGoalAsync` 无任何视口滚动：目标未绑定 → 只有导航分支（`ResolveNavigationPage`→`Tap`→`CreateContainer`）；同容器视口探索机制（`ScrollForward` / `ViewportExplorationEvidence` / `Container.ViewportExplorationObservations`）已在 `Agent.PlanRun.cs` 固定 Plan 路径冻结（SC-P3-003 / SC-P3-CAND-007），但语义环**零接线** |
| Authority | `PROJECT_LEADER_SELECT_NEXT_REAL_AGENT_SCENARIO_SCROLL_CONTAINER_TRAVERSAL` |

> **验收约束**：本 change 完成后运行 `openspec validate physical-scroll-container-semantic-traversal` 通过即停（STOP after OpenSpec validation）。任何实现都需另行授权。MODE: `SCENARIO_SELECTION_AND_OPENSPEC_ONLY`，IMPLEMENTATION: `FORBIDDEN`。

## Why

毕业语义环（multi-level traversal）已把「目标对象未绑定」的唯一出路接成**跨页导航**：目标不在当前容器 → 解析唯一已知页锚点 → `Tap` → 证明页面/容器**变更** → 创建新容器。但真实 Android 页面的目标对象常常**就在当前页面、只是位于初始视口之下**（below-fold）——例如 Settings 的 Developer options 页，`Automatic system updates` 开关在初始视口里根本不可见，向下滚动一段才出现。此时正确答案不是「跳到另一页」，而是**在同一个容器内做有界视口滚动**，用 fresh 观测重新 reconcile、重新绑定、继续同一条语义闭环。

这正是本 change 要钉住的语义边界，也是它与此前 multi-level traversal 的**关键语义区分**：

```
Observation N → Container A → 目标不可见
  → Agent 判定「本容器内进一步探索被正面正当化」→ 授权 ONE ScrollForward
  → Observation N+1 → STILL Container A（IsStillMine==true，同一语义页）
  → 刷新视口 → 目标可见 → fresh 绑定 → SetEnabled → Goal 满足
```

**禁止的解读**：Scroll → 创建 Container B。滚动不是容器转场；滚动是同一容器内的视口推进。若 fresh 观测证明页面真的变了，则按既有 multi-level 遍历规则 reconcile，绝不因为「上一步是 Scroll」就强制同容器续跑。

仓库真相（已审计）：支撑这条闭环的**全部语义/权威/机制模型都已存在且已被冻结**——`DeviceAction.ScrollForward`（无目标、无方向/坐标/距离）、`ViewportExplorationEvidence`（continue/exhausted/unresolved 三值）、`Goal.ViewportExplorationEvaluator`（有界同容器探索判据）、`Container.ViewportExplorationObservations` + `TryVerifyViewportContinuity`（同容器连续性 + 累积视口证据）。缺口只有一个：**把这套已在 `Agent.PlanRun.cs` 固定 Plan 路径证明过的机制，接线进 `Agent.SemanticRun.cs` 语义环的目标未绑定分支**。因此架构缺口分类 = **A. IMPLEMENTATION_GAP only**（详见 design.md §审计结论）。

## What Changes

- **语义环视口探索相位（最小 Runtime 接线，非新架构）**：`Agent.SemanticRun.cs` 的目标未绑定分支，在「导航」之前/并列增加「同容器视口探索」决策——当目标对象未绑定且当前 Container 的 `ViewportExplorationEvaluator` 对累积视口证据返回 `continue`（一次进一步移动被正面正当化）时：
  1. Agent 授权 **ONE** `DeviceAction.ScrollForward`（既有目标无关动作），经既有 `Traversal.ExecuteLoweredActionAsync` 分发（fresh 观测 + 序列推进 + Rejected→Failed 协议不变）；
  2. fresh 观测需证明**同一容器连续性**（`Container.TryVerifyViewportContinuity`：序列严格推进 ∧ 前台兼容 ∧ `IsStillMine` ∧ reconciled 页面名 == 当前容器页）——证明成功 → 追加进 `_viewportExplorationObservations`，**不 Bind**（保留局部进度，不创建新容器）；证明失败 → 容器级 escalation，Agent 独占后续响应；
  3. Agent 用 fresh 观测重新 `RefreshContainerEvidence`（`BindingAnalysis`/`BindingReconciler`/`StateBeliefReducer` 逐观测刷新）→ **继续同一条语义闭环**（同一 `SemanticGoal` 原样存活）→ 若目标现已绑定 → 走毕业 SetEnabled→SetSwitch 链；
  4. 若 fresh 观测证明页面/容器**真的变了**（`!IsStillMine` 或 reconciled 页面名 ≠ 当前页）→ 按既有 multi-level 遍历规则 reconcile，外部世界权威（绝不强制同容器续跑）；
  5. `continue` 为 null（unresolved）/ false（exhausted）→ fail closed / 停止滚动，无盲目重发、无编造进度（复用 SC-P3-CAND-007 既有耗尽语义）。
- **证明宿主（PhysicalHost）**：入口 = Developer options 页（`am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS`，现场验证 `mCurrentFocus=com.android.settings.Settings$DevelopmentSettingsActivity`），**不得**预滚动/预定位/注入视口进度/注入目标可见性/告知滚动次数。目标 = `AutomaticSystemUpdates` 开关（初始视口不可见，below-fold，约 3–5 次滚动后出现），Goal = `AutomaticSystemUpdates.Enabled=true`。成功条件 = `Satisfied` ∧ 同容器连续性已证 ∧ 恰一次 SetSwitch ∧ `GoalEvidence.SourceObservationSequence==fresh` ∧ 感知 SwitchState=true。滚动次数是证据涌现结果（每次 evaluator=true 授权一步、fresh 观测后重评估），非硬编码计数。
- **Falsifier F1–F8**（见 tasks/specs）与 **现实证明要求**（emulator-5554 现场回放，§33 emulator-only）。

## Capabilities

### New Capabilities

- `physical-scroll-container-semantic-traversal`: Agent 自主同容器视口滚动语义闭环（Developer options 页 → 有界 ScrollForward（每次 evaluator=true 授权一步）→ `Automatic system updates` 开关 → SetEnabled），含同容器连续性验证、逐观测 fresh 绑定、滚动失败/耗尽 fail-closed、同一 Goal 原样存活与八条 falsifier。

### Modified Capabilities

无（本仓库无 `openspec/specs/` 主规格；`physical-settings-to-wifi-multi-level-traversal` 已归档，其规格**不改动**——同容器视口滚动是新能力，不是对多页遍历或单页闭环的修改；毕业 SetEnabled 链在目标绑定后原样复用）。

## Impact

- `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs`：语义环目标未绑定分支增加同容器视口探索决策（接线既有 `ScrollForward` / `TryVerifyViewportContinuity` / `ViewportExplorationObservations` / `ViewportExplorationEvaluator`；Agent 仍为唯一语义 authority——裁决不迁移）。
- `src/UniClaw.Runtime.Adapters/`：**零改动**（PhysicalEnvironment / DeviceActionTranslator（`TranslateScroll` 已存在，swipe 70%→30%）/ AdbDispatchTarget / ImageSwitchStateProvider 机制不变；滚动即既有 `ScrollForward`→`Swipe` 机制）。
- `src/UniClaw.Runtime.PhysicalHost/Program.cs`：单页身份识别知识 + Developer options 页启动 + `AutomaticSystemUpdates` 语义对象/能力/绑定 criteria + `ViewportExplorationEvaluator` 注入 + 证明输出（宿主侧接线，裁决 11）。
- `tests/UniClaw.Runtime.Tests/`：Fake falsifier 套件（F1–F8）+ 约束测试 + 校准资产扩展 + 真实环境集成 tier。
- 文档：毕业决策记录（RealityLevel=EMULATOR_REALITY_SCROLL_CONTAINER_SEMANTIC_LOOP）；`docs/decisions/`。
- 依赖：无新增外部依赖；emulator/adb 为环境前置（显式失败，非静默跳过）。

## 非目标（Forbidden / Deferred，本 change 明确不做）

- **不做**：ScrollManager / ScrollPlanner / ViewportNavigator / ScrollCapability authority / workflow engine / 滚动 DSL / 硬编码滚动次数 / 有序视口路线 / 目标专用 swipe 坐标 / 场景专用滚动状态机 / 预录视口序列 / WorldState 注入 / 隐藏 emulator API / 新语义 authority。
- **不引入**：无限滚动、通用搜索、列表虚拟化、嵌套滚动容器、水平滚动、弹窗处理、Recovery 重设计、跨应用遍历、浏览器滚动、Provider 重设计、感知模型改动、路线规划。
- 滚动语义权威边界**不动**：Agent 唯一决定「探索与否 + 授权一次滚动 + fresh obs 后再决定」；Container 唯一拥有页面局部信念 + 累积视口证据；Traversal 唯一执行一次滚动 + fresh obs + 协议/新鲜度验证；Environment 仅传输；Perception 仅证据。
- 同容器滚动的**证明层级目标**：EMULATOR_REALITY_SCROLL_CONTAINER_SEMANTIC_LOOP（emulator-only，§33；不称 REAL_DEVICE_PROVEN）。
