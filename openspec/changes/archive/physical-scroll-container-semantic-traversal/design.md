# Design: Physical Scroll Container Semantic Traversal

## Context

### 毕业基线（multi-level traversal 事实，2026-08-14）

- `RunSemanticGoalAsync`（`Agent/Agent.SemanticRun.cs`，约 405 行）是结构化语义闭环：READ belief → DECIDE → SELECT capability → AUTHORIZE → LOWER → `Traversal.ExecuteLoweredActionAsync` → fresh 观测 → UPDATE → RE-EVALUATE。
- 目标对象未绑定分支（:112-194）**只有导航相位**：`ResolveNavigationPage`（唯一已知非当前页候选）→ `ResolveNavigationAnchor`（唯一锚元素）→ `Tap` → `ProvesNavigationTransition`（fresh 序列推进 ∧ 页面名==期望页 ∧ 页面 CHANGED ∧ `!IsStillMine`）→ `CreateContainer(nextPage)` + `Bind`。**零滚动、零视口探索**。
- 页面信念 Contradicted → `SemanticContradiction` 终止；belief 缺失 → `StateEvidenceRequired` 终止；无导航候选 → `BindingUnresolved` 终止。
- 毕业链（**本 change 在目标绑定后原样复用，零改动**）：binding 存在 ∧ belief ≠ desired → `SelectCapability` 唯一匹配 → `AuthorizeAction` → `SemanticActionLowerer.Lower` → `DeviceAction.SetSwitch(index, desired, bounds)` → `ExecuteLoweredActionAsync` → fresh 观测 → `RefreshContainerEvidence` → 循环 → belief==desired → `GoalEvidence` → `CompleteSemantic`。

### 既有同容器视口机制（仓库真相，已被冻结，非新架构）

| 概念 | 位置 | 现状 |
|---|---|---|
| 无目标滚动动作 | `Model/Actions/DeviceAction.cs:33` | `ScrollForward`：无元素目标、无方向/坐标/距离/时长/progress 语义（SC-P3-003） |
| 滚动物理机制 | `Adapters/Operator/DeviceActionTranslator.cs:76-85` | `TranslateScroll` = `AdbOperation.Swipe(centerX, 70%→30% height)`——adapter 内部机制，**不含**目标语义对象/为何滚动/Goal 成功/路线 |
| 视口探索三值证据 | `Model/ViewportExplorationEvidence.cs` | `{ bool? ContinueExploration; string Reason }`：true=一次进一步移动正面正当化；false=正面耗尽；null=unresolved。**不是**视口身份/分发结果/进度计数/完成判定（SC-P3-CAND-007） |
| 目标注入点 | `Model/Goal.cs:35` | `Goal.ViewportExplorationEvaluator`（`Func<ImmutableArray<Observation>, ViewportExplorationEvidence>?`，可选、确定性、只读） |
| 累积视口证据 | `Container/Container.cs:89,167,282-297` | `ViewportExplorationObservations`（Container 唯一 owner）；`Bind` 重置为 `[observation]`；`TryVerifyViewportContinuity(obs, reconciledPage, foreground)` = `TryVerifyLocalContinuity`（序列严格更新 ∧ 前台兼容 ∧ `IsStillMine` ∧ 相同 reconciled page）∧ 追加观测，**不 Bind**（保留局部进度） |
| 视口升级 | `Container/Container.cs:361` | `CreateViewportContinuityEscalation`（Container-scope Trap；Agent 独占响应） |
| 三值解释 | `Agent/Agent.cs:145-171` | `EvaluateViewportExploration`：`ViewportExplorationEvaluator(container.ViewportExplorationObservations)` → continue/exhausted/unresolved |
| 固定 Plan 视口循环（先例） | `Agent/Agent.PlanRun.cs:141-318` | 完整实现：`IsScrollForwardAction` → 滚动前 `EvaluateViewportExploration`（null→Fail、false→exhausted Fail 或 satisfied Complete、true→dispatch）→ `ScrollForward` 分发 → `TryVerifyViewportContinuity` → 滚动后重评估。**语义环零接线** |
| 逐观测绑定 | `World/BindingAnalysis.cs` + `BindingReconciler.cs` + `StateBeliefReducer.cs` | 观测局部证据 → 绑定 proposal；belief 仅来自绑定内 **恰好一个** toggle 候选（0/≥2 → null UNKNOWN） |
| 页面识别 | `World/PageAnalysis.cs` | TEXT_ANCHOR（部分锚命中即 Supports）/ TEXT_ANCHOR_NEGATIVE / SWITCH_DISTRIBUTION / FOREGROUND 四源；纯函数 |

### 审计结论（对 CRITICAL ARCHITECTURE QUESTION 的回答）

> 问题：既有 Agent / Container / Traversal / PhysicalEnvironment / Perception / BindingReconciler / StateBeliefReducer 能否支持「同一 Container + 多次视口观测 + 滚动后 fresh 绑定」而不改语义/权威模型？

**能。** 支撑这条闭环的全部概念均已存在且被冻结：无目标 `ScrollForward`（SC-P3-003）、三值探索判据（SC-P3-CAND-007）、Container 累积视口证据 + 同容器连续性、逐观测 fresh 绑定。语义环缺的**只有同容器视口探索相位**——把这套已在固定 Plan 路径证明过的机制接线进 `RunSemanticGoalAsync` 的目标未绑定分支。因此缺口分类：

- **A. IMPLEMENTATION_GAP**（既有架构充分，只需接线）：语义环同容器视口探索分支。无新模型类型、无新状态 owner、无新 authority。
- **B. SEMANTIC_MODEL_GAP**：**无**。`ScrollForward` / `ViewportExplorationEvidence` / `Goal.ViewportExplorationEvaluator` / `Container.ViewportExplorationObservations` 已定义；目标语义对象/状态维度/能力/GoalEvidence 模型不动；「滚动后目标可见即绑定」由既有绑定标准自身发出（`AutomaticSystemUpdates` 只在含其开关的观测绑定），无需新增「视口包含关系」模型。
- **C. AUTHORITY_GAP**：**无**。Agent 仍是唯一语义 authority（探索决策 + 授权一次滚动 + 完成裁决）；Container 仍为页面局部状态 + 累积视口证据唯一 owner；Traversal 仍为执行/验证协议 owner；Environment 传输、Perception 证据、ADB 机制不变。不引入 ScrollManager/ScrollPlanner/ViewportNavigator，不引入新 owner。

## Goals / Non-Goals

**Goals:**

- 选定并形式化最小真实同容器滚动场景：Developer options 页 → 有界 `ScrollForward`（每次 evaluator=true 授权一步）→ `Automatic system updates` 开关 → `AutomaticSystemUpdates.Enabled=true`。
- 让语义环在目标对象未绑定且同容器探索被正面正当化时，执行**证据驱动的同容器视口滚动**：滚动是一次有界动作，路由/次数不预编排。
- 每个滚动动作满足「Action receipt + fresh Observation + 验证同容器连续性」；单独分发永不推进语义进度。
- 视口进度区分「内容改变」vs「未变/耗尽」：fresh 序列推进本身不够，无编造进度、无盲目无限滚动、有界终止/fail closed。
- 逐观测 fresh 绑定结构化：滚动后目标可见 → fresh 绑定（旧视口 ElementIndex ≠ 新视口身份）；Container 保留语义连续性但不保留陈旧 observation-local grounding 为 truth。
- 目标对象绑定后，复用毕业链：SetEnabled→SetSwitch→物理效果→fresh 观测→SwitchState=true→GoalEvidence→Satisfied。
- 八条 falsifier（F1–F8）以确定性 Fake 环境 + 真实 emulator 回放双重证明。
- 完成 `openspec validate` 即停（本 change 不实施）。

**Non-Goals:**

- 不新增 ScrollManager / ScrollPlanner / ViewportNavigator / ScrollCapability authority / workflow engine / 滚动 DSL / 硬编码滚动次数 / 有序视口路线 / 目标专用坐标 / 场景专用滚动状态机 / 预录视口序列 / WorldState / 隐藏 emulator API / 新语义 authority。
- 不引入无限滚动、通用搜索、列表虚拟化、嵌套滚动容器、水平滚动、弹窗处理、Recovery 重设计、跨应用遍历、浏览器滚动、Provider 重设计、感知模型改动、路线规划。
- 不修改毕业 SetEnabled 链、不修改 PhysicalEnvironment / Translator / AdbDispatchTarget / ImageSwitchStateProvider、不修改固定 Plan 视口循环。
- 不把本场景做成固定 Plan 路径的复刻：视口探索相位是**语义环的既有 authority 接线**，不是第二套遍历引擎。

## Decisions

### D1. 语义环同容器视口探索相位（最小接线）

在 `RunSemanticGoalAsync` 循环内、目标未绑定分支处（:112-194），在**导航决策之前**增加同容器视口探索决策（同一容器内先试滚动，再考虑跨页导航——因为目标是同页 below-fold 而非另一页）：

```
若 container.ObjectBindings 不含目标对象：
  1. 若 goal.ViewportExplorationEvaluator is not null
       且 EvaluateViewportExploration(goal, container) 返回 ContinueExploration == true：
       a. Agent 授权 ONE ScrollForward（决策 authority 在 Agent）
       b. step = await _traversal.ExecuteLoweredActionAsync(new DeviceAction.ScrollForward(), observation)
          # 既有协议：fresh 观测 + 序列严格推进 + Rejected→Failed
       c. freshObs = journal.PostActionObservation（非空，否则 ExecutionFailed）
       d. 同容器连续性验证（D2/D5）：
          container.TryVerifyViewportContinuity(freshObs, Reconcile.FromObservation(freshObs).SemanticPage, foreground)
          成功 → 追加进 ViewportExplorationObservations，不 Bind（同容器，保留局部进度）
          失败（fresh 证明另一页/Unknown/前台不兼容/序列未推进）
            → 若 fresh 证明页面真的变了：按既有 multi-level 遍历规则 reconcile（外部世界权威）
            → 否则容器级 escalation，Agent 独占响应，fail closed
       e. observation = freshObs; 重新 RefreshContainerEvidence(container, freshObs)
       f. continue  # 重新评估 SAME goal：若目标现已绑定 → 毕业链；否则再次进入本相位或导航
  2. 否则（evaluator 缺席 / null / false / 无目标未绑定场景）→ 回到既有导航分支（D1 multi-level），
     行为与 multi-level traversal 完全一致（零回归）。
```

- `EvaluateViewportExploration` 返回 `null`（unresolved）→ 不 dispatch 下一次滚动，fail closed（复用 SC-P3-CAND-007 语义）。
- `EvaluateViewportExploration` 返回 `false`（exhausted）→ 停止滚动；若 GoalEvidence 已满足 → Complete，否则 Fail（复用 SC-P3-CAND-007 语义）。
- 目标对象绑定后走毕业路径：**视口探索相位不触碰** SELECT/AUTHORIZE/LOWER/GoalEvidence 任何一行。
- `evaluator` 缺席时（`goal.ViewportExplorationEvaluator is null`）：完全跳过本相位，行为与当前语义环一致——固定行为不变（SC-P3-CAND-007 兼容性保证）。

### D2. 同容器连续性语义（CRITICAL：滚动 ≠ 容器转场）

- **滚动不是容器转场**：`ScrollForward` 后的 fresh 观测若证明**同一语义页**（`IsStillMine==true` ∧ reconciled 页面名 == 当前容器页 ∧ 前台兼容 ∧ 序列严格推进），则 Agent **继续使用同一 Container**，只经 `TryVerifyViewportContinuity` 追加视口证据（不 `Bind`、不 `CreateContainer`）。
- **外部世界权威**：若 fresh 观测证明**另一页**（`!IsStillMine` 或 reconciled 页面名 ≠ 当前页），则滚动导致了意外页面/容器变更（F5），按既有 multi-level 遍历规则 reconcile——**绝不因为「上一步是 Scroll」就强制同容器续跑**。
- 语义模型（禁止的解读）：
  ```
  Observation N → Container A → 目标不可见
    → 视口探索 → Scroll → Observation N+1 → STILL Container A → 刷新视口 → 目标可见
  ```
  **禁止**：`Scroll → CreateContainer(B)`。
- `TryVerifyViewportContinuity` 是既有连续性验证（`Container.cs:282`）：成功不调用 `Bind`（保留既有 local progress），只追加观测；失败产生 Container-scope escalation（`CreateViewportContinuityEscalation`），Agent 独占 rebind/recovery/fail 响应。

### D3. 滚动动作授权（SCRITICAL scroll authority，不新增 authority）

| 权威 | owner | 职责（本 change 不变） |
|---|---|---|
| **探索决策 + 授权 + 再决策** | **Agent**（唯一语义 authority） | 判定「Goal 本地不可满足 + 决定探索」→ 授权 **ONE** 滚动 → fresh obs 后**再决定**（继续/停止/导航/fail） |
| **页面局部信念 + 累积视口证据** | **Container**（唯一 state owner） | `_objectBindings` / `_objectStateBeliefs` / `_viewportExplorationObservations` / `IsStillMine`；不裁决、不偷取 Agent authority |
| **执行一次滚动 + fresh obs + 协议/新鲜度验证** | **Traversal** | `ExecuteLoweredActionAsync`：一次滚动 + fresh 观测 + 序列推进 + Rejected→Failed |
| **物理传输** | **PhysicalEnvironment**（Environment） | `ExecuteAsync`：ScrollForward → AdbOperation → ADB；ObserveAsync：截图 → 感知 → Observation |
| **感知证据** | **Perception**（LocalVisionPerceptionSource + ImageSwitchStateProvider） | 候选 + 开关状态（ON/OFF/UNKNOWN）；仅证据，不裁决 |

- 滚动动作 = 既有 `DeviceAction.ScrollForward`（无目标、无方向/坐标/距离）。Provider（`DeviceActionTranslator.TranslateScroll`）只知道物理机制（Swipe 70%→30%），**不知道**目标语义对象、为何滚动、Goal 成功、路线。
- **禁止**：ScrollManager / ScrollPlanner / ViewportNavigator / ScrollCapability authority / workflow engine（任何替代 Agent 决策或 Container 状态的组件）。
- Agent 仍为唯一语义 authority；Container/Traversal/Environment/Perception 职责不变（C: 无）。

### D4. 绑定生命周期（逐观测 fresh 绑定）

- **ElementIndex 是 observation-local**：旧视口 `ElementIndex` ≠ 新视口身份。滚动后目标可见 → 必须从 fresh 观测重新 `BindingAnalysis.Analyze` → `BindingReconciler.Reconcile` → `RefreshSemanticSnapshot`（逐观测替换 `_objectBindings`）。
- **Container 保留语义连续性但不保留陈旧 grounding 为 truth**：同一 Container 跨视口保留语义页身份与累积视口证据，但 `_objectBindings` / `_objectStateBeliefs` 每次 fresh 观测刷新；旧视口的元素索引/边界不得复用于新视口动作目标（F4 结构性满足）。
- **禁止**：跨视口缓存 binding / 复用旧视口元素索引 / 将视口 N 的元素身份当作视口 N+1 的身份。结构上由 `RefreshSemanticSnapshot` 逐观测替换保证，约束测试再加静态断言（见 tasks 2.x）。

### D5. 视口进度验证（fresh 序列推进 ≠ 内容推进）

- 每个滚动动作后必须获得 fresh 观测：`journal.PostActionObservation` 非空 ∧ 序列严格推进（`ExecuteLoweredActionAsync` 内置）。
- **dispatch 成功 ≠ 视口进度**（F2）：Traversal `Succeeded` 仅证明「动作已执行 + fresh 观测产生」；视口内容是否改变必须由「同容器连续性 + 三值探索判据」独立证明。
- **内容改变 vs 未变/耗尽**（复用既有 SC-P3-CAND-007 耗尽机制，**审计后不新造**）：
  - 滚动后 fresh 观测**内容改变**且判据返回 `true` → 一次进一步移动被正面正当化（可再授权一次，见 D1）；
  - 滚动后 fresh 观测**未变**或判据返回 `false`（正面耗尽）→ 无编造进度、无盲目无限滚动、有界终止（fail closed 或 satisfied）；
  - 判据返回 `null`（unresolved）→ 无进一步移动、不假装完成、fail closed。
- **Fingerprint 可作证据但不得作身份**：视口指纹/截图差异可以是「内容是否改变」的证据线索，但**不得**成为 Container 身份裁决（身份 = `IsStillMine` + reconciled 页面名）。


### D5.5. LATENCY_DRIVEN_BOUNDED_EXECUTION_POLICY（后滚动语义协调策略）

**背景**：完整语义页面解析（Reconcile.FromObservation + PageAnalysis）可能涉及昂贵的感知操作。
在连续滚动场景中，每次滚动后都执行完整语义协调可能导致不必要的延迟。

**策略**：支持两种模式：

- **STRICT（默认）**：每次 ScrollForward 后执行完整语义协调（`TryVerifyViewportContinuity`）。
  这是既有行为，保持向后兼容。零回归。

- **DEFERRED_BOUNDED（可选）**：通过 `RunSemanticGoalAsync` 的 `enableDeferredReconciliation=true`
  参数启用。首次滚动执行 STRICT 协调，后续滚动（当 evaluator 继续返回 true 时）进入延迟模式。

**延迟模式规则**：

1. **新鲜观测**：每次 ScrollForward 后必须获得新鲜观测（MANDATORY，Traversal 强制执行）。
2. **陈旧 grounding**：每次 ScrollForward 后，旧的 ElementIndex / Bounds / target geometry
   立即失效（不得用于后续动作授权）。
3. **语义协调延迟**：在延迟模式下，完整语义协调（`TryVerifyViewportContinuity`）被推迟。
   仅执行廉价漂移检查（`PerformCheapDriftCheck`），使用已获得的 Fresh Observation。
4. **廉价漂移检查**：
   - 前台应用是否改变
   - 弹窗/系统窗口是否出现（前台兼容但页面未知）
   - 当前 Container 身份是否被强烈矛盾（`IsStillMine` 失败且页面已知）
   - 无需额外截图、无需 LLM、无需重复感知
5. **安全预算**：`MaxDeferredScrolls = 5`（安全/延迟预算，非场景知识）。
6. **强制检查点（强制协调）**：在以下情况必须执行完整语义协调：
   - Goal 目标候选可见时
   - Runtime 即将执行非滚动语义动作时（SetSwitch / Tap / completion）
   - 延迟滚动安全预算耗尽时
   - 明显世界漂移被检测到时
   - 视口探索耗尽/模糊时
   - 前台应用实质性变化时
   - 弹窗/系统窗口中断出现时
7. **检查点协调逻辑**：
   - CASE A（同容器）：`TryVerifyViewportContinuity` 成功 → 保留当前 Container，继续同 Goal
   - CASE B（不同已知页）：`TryVerifyViewportContinuity` 失败但页面解析为另一个已知语义页 →
     使用既有 multi-level 协调（`CreateContainer` + `Bind` + `RefreshContainerEvidence`），继续同 Goal
   - CASE C（未知）：页面无法解析 → fail closed（`SemanticContradiction`）
   - CASE D（同页但连续性无法证明）：fail closed（`SemanticContradiction`）
8. **F5 修复**：`ReconcilePostScrollContinuityFailure` 方法处理 STRICT 模式下 `TryVerifyViewportContinuity`
   失败的情况。如果新鲜观测解析为另一个已知语义页，执行 multi-level 协调（CASE B）。
   如果未知 → fail closed（CASE C/D）。

**真理规则**：在延迟窗口内，不声明每个中间观测已被语义确认为属于原始 Container。
正确解释：
- 观测 #N：新鲜世界证据，语义连续性尚未重新验证
- 观测 #N+1：新鲜世界证据，语义连续性仍未经核实
- 检查点观测：执行语义协调

**架构影响**：RuntimePolicyDelta = YES（后滚动协调时机）。
ArchitectureDelta = NONE（无新架构组件）。
AuthorityDelta = NONE（Agent 仍然唯一决策权威）。
SemanticModelDelta = NONE（无新语义模型）。
### D6. 场景事实（宿主声明，裁决 11）

- **初始世界**：Settings 应用 **Developer options 页**（`am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS`；现场验证 `mCurrentFocus=com.android.settings.Settings$DevelopmentSettingsActivity`，2026-08-14 emulator-5554）。宿主**只**允许：emulator ready + 该页启动；**禁止**预滚动 / 预定位 / 注入视口进度 / 注入目标可见性 / 告知 Agent 滚动次数。
- **初始视口（未滚动）**：Developer options（标题）、Memory / Bug report / Desktop backup password 等开发者项，无 `Automatic system updates` 候选 → 目标在初始视口不可见（genuinely below-fold，约 3–5 次滚动后出现，现场校准 3–5 次取决于 swipe settle）。
- **目标语义对象**：`AutomaticSystemUpdates`（category `SystemUpdateSetting`，dimension `Enabled`），能力 `SetEnabled`（复用 Slice 2 同款 pattern），绑定 text anchor `"Automatic system updates"` + control type `"toggle"`。
- **滚动后（约 3–5 次 `input swipe 540 1344 → 540 576`）**：`Automatic system updates` 行（menu_item 标签 + switch，归一化 x≈0.833–0.961 y≈0.649–0.694 视具体滚动落点）出现，目标开关被感知为 `switch` → 归一化 `toggle`。注意：该目标是**导航可达**页面（Developer options 直启）上的 **scroll 可达**目标，与锁定场景前提一致。
- **目标当前状态**：`Automatic system updates` 开关实际写回 key = `ota_disable_automatic_update`（global 命名空间，**INVERTED**：0 ↔ 开关 ON，1 ↔ 开关 OFF；AOSP 默认 0 → 开关默认 ON）。现场校准：该开关 tap 只写 `ota_disable_automatic_update`，**不写** `automatic_system_updates`（后者在该 build 是无效 key）。因此 Goal = `AutomaticSystemUpdates.Enabled=true`（OFF→ON 一次 SetSwitch 翻转 ON），OFF 基线由宿主 run 外 `settings put global ota_disable_automatic_update 1` + force-stop + 冷启动重渲染 OFF（现场已验证）。
- **同容器连续性事实**：页面标题 `Developer options` 在滚动前（text_block y≈0.196，带空格）与滚动后（app bar 折叠 OCR 合并为 `Developeroptions`，menu_item y≈0.0625，sticky）**均存在** → 页面身份 criteria 以 `["Developer options", "Developeroptions"]` 为跨视口持久正锚（PageAnalysis 部分锚命中即 Supports），`resolveSemanticPage` 在滚动前后均唯一解析到 `DeveloperOptions`，`IsStillMine==true`。
- **绑定现实压力（记录，不扩界）**：`Automatic system updates` 文本在滚动后出现两次（开关行 label + 副标题 "Apply updates when device restarts" 被 OCR 误读为同一文本）；既有 `BindingReconciler` 把两者聚合进同一 binding，`StateBeliefReducer` 只计「恰好一个 PerceptionType==toggle 且 SwitchState 非空」的元素 → 副标题（menu_item）不是 toggle，不污染状态读取。这是**现实压力**，由既有管道确定性处理，不改模型。
- **为何滚动真的必要**：目标 `AutomaticSystemUpdates` 及其开关在初始视口**完全缺席**（初始视口无该行），只在滚动后出现；无 popup、无跨应用、无隐藏 API、无嵌套滚动。

### D7. 证明宿主（PhysicalHost 扩展）

- 入口：`am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS`（Developer options 页），然后 **Agent 拥有全部后续视口探索/动作权**。
- 证明输出（结构同 Slice 2/multi-level，视口化）：滚动 journal（`ScrollForward` Succeeded + fresh 序列）、同容器连续性已证、最终 `SetEnabled`/`SetSwitch` 恰一次、`GoalEvidence.SourceObservationSequence == fresh 观测序列`、感知 SwitchState=true（目标行，排除 sticky 顶部 "Use developer options" 主开关）；`settings get global ota_disable_automatic_update` 读回仅佐证（**非成功条件**，ON 时读回 `0`）。
- `proofScrollContainer = satisfied ∧ sameContainerContinuityProved ∧ viewportStepCount ≥ 1（真实同容器滚动）∧ exactlyOneSetSwitch ∧ sourcePointsAtFresh ∧ perceptionSwitchOn`。
- F2 live 变体：滚动分发成功但视口未变 → 非 SATISFIED 终止（宿主提供独立读回/页面名佐证）。
- Reality level 目标：**EMULATOR_REALITY_SCROLL_CONTAINER_SEMANTIC_LOOP**（§33 emulator-only）。

### D8. Falsifier 映射（specs §Falsifiers）

| # | 场景 | 预期 | 既有语义 |
|---|---|---|---|
| F1 | 目标初始不可见（本场景常态） | 不猜坐标、仅授权 ScrollForward | 视口探索相位；无目标绑定即无 SetSwitch |
| F2 | 滚动分发成功但视口未变 | 无进度、有界停止、非 SATISFIED | SC-P3-CAND-007 耗尽/未变 → 停止滚动 |
| F3 | fresh 视口但目标仍缺席 | 从 fresh 世界 reconcile；可再有界一步（若正当），无预计算次数 | 视口探索相位继续 or fail closed |
| F4 | 目标滚动后出现 | 仅 fresh 绑定，不复用旧索引/边界 | `RefreshSemanticSnapshot` 逐观测替换 |
| F5 | 滚动致意外页面/容器变更 | 外部世界权威，按 multi-level 规则 reconcile | `!IsStillMine`/页面名变更 → 走遍历规则 |
| F6 | 滚动后观测 UNKNOWN | fail closed | 页面名 null → StateEvidenceRequired/SemanticContradiction 终止 |
| F7 | 目标初始已可见 | **零滚动分发** | 目标绑定即走毕业链，无滚动 |
| F8 | 目标可见但歧义 | 不猜动作 | 绑定 0/≥2 toggle → StateEvidenceRequired（UNKNOWN） |

### D9. 所有权表（本 change 零迁移）

| 概念 | 唯一 owner | 跨 owner 只传 |
|---|---|---|
| 探索决策 / 一次滚动授权 / 完成裁决 | Agent | 不可变 GoalEvidence / 决策结果 |
| 页面局部信念 + 绑定 + 状态信念 + 累积视口证据 | Container | 不可变快照（`ViewportExplorationObservations` 等） |
| 一次滚动执行 + fresh 观测 + 新鲜度/协议验证 | Traversal | 不可变 `TraversalStepResult` / journal 快照 |
| 物理动作传输 + 观测获取 | PhysicalEnvironment（Environment） | `ActionResult` / `Observation` |
| 候选 + 开关状态感知 | Perception（LocalVisionPerceptionSource + ImageSwitchStateProvider） | `ObservedElement` / `SwitchState` |
| 绑定/状态/页面证据聚合 | BindingAnalysis / BindingReconciler / StateBeliefReducer / PageAnalysis（无状态纯函数） | 不可变 proposal |
| 滚动物理机制（Swipe 70%→30%） | DeviceActionTranslator（adapter 内部） | `AdbOperation` |

### D10. 禁止机制（Forbidden）

- **不引入**：ScrollManager / ScrollPlanner / ViewportNavigator / ScrollCapability authority / workflow engine / 滚动 DSL。
- **不脚本化**：滚动恰好 N 次、固定滚动次数、有序视口路线、目标专用 swipe 坐标、场景专用滚动状态机、预录视口序列。
- **不注入**：WorldState / 视口进度 / 目标可见性 / 滚动次数。
- **不把滚动当容器转场**：`Scroll → CreateContainer(B)` 是语义错误解读。
- **不把 Fingerprint 当身份**；**不把 dispatch 收据当世界效果**；**不把陈旧 grounding 当 truth**。
- 允许的语义闭环：fresh Obs + 当前 Container 信念 + 目标未绑定 + 证据证明可进一步探索 → Agent 授权 ONE 滚动 → fresh obs → reconcile → 再决定。

## Risks / Trade-offs

1. **滚动后目标状态/可见性不稳定**（真实 UI 滚动后 `Automatic system updates` 开关可能随滚动位置/动画瞬时变化）：→ 复用多页遍历的「有界 settle + 仅重观测（零重发、零新 journal 条目、零动作）」语义；耗尽仍未证明 → 原原因 fail closed。**记录为真实压力**，实现阶段以现场观测定界，不扩界。
2. **语义环视口分支与导航/毕业分支正交性**：新分支必须与毕业路径正交（绑定即走毕业路径，不混流）；约束测试（架构级）防「视口滚动进入 SetSwitch 语义链」或「毕业路径引入滚动」。
3. **`Automatic system updates` 文本双重出现（开关行 label + 副标题 OCR 误读）**：既有 `BindingReconciler`/`StateBeliefReducer` 已确定性处理（仅 toggle 带状态）；若现场校准发现歧义（如副标题被误判为 toggle），记录压力、以 text anchor + control type + same-row 组合消歧，歧义未消 → fail closed（F8）。
4. **Fake 与真实 Settings 的保真差**：Fake 用于 falsifier 确定性；真实证明以 emulator 现场回放为准（同 Slice 2 双层策略）。Fake 视口模型须反映「初始无目标、一次滚动后目标出现、同容器身份不变」的现场校准结果。
5. **同容器连续性 vs 页面变更的边界**：滚动导致页面真正变更时（如误触行/边缘手势）→ F5 外部世界权威；实现不得因「上一步是 Scroll」强制同容器续跑。约束测试断言 `!IsStillMine` 时走遍历规则而非同容器追加。
6. **证明可重复性**：滚动依赖真实 UI 状态；宿主在 run 外准备初始状态（若需复位 `Automatic system updates` 基线到 OFF，允许——同毕业切片先例，不进语义路径、不计入 ActionHistory）。
