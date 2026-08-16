# Design: Physical Settings → WiFi Multi-Level Traversal

## Context

### 毕业基线（Slice 2 事实，2026-08-14）

- `RunSemanticGoalAsync`（`Agent/Agent.SemanticRun.cs`，221 行）是**单容器**闭环：启动锚点落地页面 → `CreateContainer(ExpectedSemanticEntry)` + `Bind(initial)` → 循环 READ belief → DECIDE → SELECT capability → AUTHORIZE → LOWER → `Traversal.ExecuteLoweredActionAsync` → fresh 观测 → `TryVerifyLocalContinuity` → 重新评估。
- 单页约束的**结构性表现**：
  - 目标对象未绑定 → `BindingUnresolved` 终止（:102-105）；
  - 页面信念 Contradicted → `SemanticContradiction` 终止（:89-91）；
  - post-action fresh 观测无法证明同容器连续 → `SemanticContradiction`（:145-149，`TryVerifyLocalContinuity` 要求 reconciled page == 当前容器页）。
  - **没有任何导航步骤**：循环只降低目标能力动作（SetEnabled→SetSwitch），不存在「走向目标所在页面」的语义。
- 毕业链（**本 change 原样复用，零改动**）：binding 存在 ∧ belief false → `SelectCapability` 唯一匹配 → `AuthorizeAction` → `SemanticActionLowerer.Lower`（binding 匹配 / toggle 唯一 / SwitchState 非空 / 已满足→NoOp）→ `DeviceAction.SetSwitch(index, desired, bounds)` → `ExecuteLoweredActionAsync`（fresh 观测 + 序列推进 + Rejected→Failed）→ `Reconcile.FromObservation` → `RefreshContainerEvidence` → 循环 → belief==desired → `GoalEvidence(true, reason, observation.SequenceNumber)` → `CompleteSemantic`。

### 既有跨页机制（仓库真相，非新架构）

| 概念 | 位置 | 现状 |
|---|---|---|
| 多页页面识别证据 | `World/PageAnalysis.cs` | **已支持多页**：`PageAnalysisCriteria.PageAnchors`（每页正锚）、`PageNegativeAnchors`、`PageSwitchStateAnchors`（SwitchState-bearing 锚）；产出 FOREGROUND / TEXT_ANCHOR / TEXT_ANCHOR_NEGATIVE / SWITCH_DISTRIBUTION 四源证据；观测局部、无状态、确定性 |
| 页面信念 reconciliation | `World/Reconcile.cs` | `FromObservation(observation, resolveSemanticPage)`；解析规则由调用侧注入；null → SemanticPage=Unknown（§10 证据不足不得假装确定） |
| 容器创建/绑定/身份 | `Container/Container.cs` | `Bind`（清空绑定与局部进度，重置 `_viewportExplorationObservations`）、`IsStillMine`（注入 identity 规则）、`RefreshSemanticSnapshot`（**逐观测替换** `_objectBindings`——「Index 是 observation-local——跨 observation 必须刷新绑定」:103）、`TryVerifyLocalContinuity`（同页连续性）、`EvaluatePageBelief`（含 TRANSITION 独立证据融合） |
| 跨页容器推进先例 | `Agent/Agent.OpenWorld.cs`（u2/phase3-bounded 先例，Fake 证明） | `RunBoundedCrossPageDiscovery`：BranchInventory 每容器判定 → 授权候选 → `container.ExecuteStep(PlanStep(text,"Tap"))` → fresh 观测 → `Reconcile` 新页面名 ≠ 父页 ∧ `!container.IsStillMine(post)` → `CreateContainer(childPage)` + `Bind(postObservation)`（:399-414）；父返回需精确 fresh 重调和（:121-129） |
| 导航执行/验证协议 | `Traversal/Traversal.cs` + `TargetGrounder.cs` | `ExecuteLoweredActionAsync`（任何 DeviceAction：fresh 观测 + `fresh.SequenceNumber <= observation.SequenceNumber → Failed` + Rejected→Failed）；`TargetGrounder.GroundCriterion`（criterion 双相 grounding + 授权收据，fail-closed） |
| 动作模型 | `Model/Actions/DeviceAction.cs` | `Tap(index, bounds)` / `SetSwitch(index, state, bounds)` / `LaunchApp` / `ScrollForward`——导航即既有 `Tap`，无需新动作类型 |
| 目标对象绑定 | `World/BindingAnalysis.cs` + `BindingReconciler.cs` + `StateBeliefReducer.cs` | 逐观测证据 → binding proposals（observation-scoped）；belief 仅来自 binding 内 toggle 候选 |

### 审计结论（对 CRITICAL ARCHITECTURE QUESTION 的回答）

语义环缺的**只有导航相位**；支撑它的全部概念（多页页面识别、reconciliation、容器创建/绑定/身份、Traversal 分发+验证、observation-local binding）**均已存在且被 OpenWorld 路径证明过**。因此缺口分类：

- **A. IMPLEMENTATION_GAP**（既有架构充分，只需接线）：语义环导航分支。无新模型类型、无新状态 owner、无新 authority。
- **B. SEMANTIC_MODEL_GAP**：**无**。目标语义对象/状态维度/能力/GoalEvidence 模型不动；页面词汇与锚点是既有 `PageAnalysisCriteria`；「到达即绑定」由绑定标准本身发出（`WifiConnectivity` 只在含 Wi‑Fi 开关的页面绑定），无需新增「可达性/包含关系」模型。
- **C. AUTHORITY_GAP**：**无**。Agent 仍是唯一语义 authority（导航决策 + 授权 + 完成裁决）；Container 仍为页面局部状态唯一 owner；Traversal 仍为执行/验证协议 owner；Environment 传输、Perception 证据、ADB 机制不变。不引入 WorldState、不引入新 owner。

## Goals / Non-Goals

**Goals:**

- 选定并形式化最小真实多页场景：Settings 根页 →（Agent 自主导航）→ Wi‑Fi 开关页 → `WifiConnectivity.Enabled=true`。
- 让毕业语义环在目标对象未绑定时执行**证据驱动的导航**：下一跳 = 当前 fresh 观测中恰好一个已知非自身页面的识别锚点；路由涌现，非预编排。
- 每个导航动作满足「Action receipt + fresh Observation + 验证页面/容器变更」；单独分发永不推进语义进度。
- 跨页 binding 生命周期结构化：逐观测刷新、容器切换即清空（Bind），杜绝旧元素索引跨页复用。
- 目标对象绑定后，复用毕业链：SetEnabled→SetSwitch→物理效果→fresh 观测→SwitchState=true→GoalEvidence→Satisfied。
- 六条 falsifier（F1–F6）以确定性 Fake 多页环境 + 真实 emulator 回放双重证明。
- 完成 `openspec validate` 即停（本 change 不实施）。

**Non-Goals:**

- 不新增 Provider / registry / workflow engine / 导航 DSL / 硬编码屏幕序列 / 坐标脚本 / WiFi 专用 navigator / WorldState / 隐藏 emulator API / 新语义 authority。
- 不证明任意深度 / 任意应用 / 弹窗 / 滚动恢复 / 跨应用 / 通用浏览器导航 / planner 重构。真实 Settings 若自然要求滚动，记录压力并按需裁剪（见 Risks）。
- 不修改毕业 SetEnabled 链、不修改 PhysicalEnvironment / Translator / AdbDispatchTarget / ImageSwitchStateProvider、不修改 OpenWorld 既有路径。
- 不把本场景做成 OpenWorld 路径的复刻：导航相位是**语义环的既有 authority 接线**，不是第二套遍历引擎。

## Decisions

### D1. 语义环导航相位（最小接线）

在 `RunSemanticGoalAsync` 循环内、DECIDE 之后（binding 缺失分支处）增加导航分支：

```
若 container.ObjectBindings 不含目标对象：
  1. pageEvidence = PageAnalysis.Analyze(observation, _pageAnalysisCriteria)   # 既有纯函数
  2. nextPage = 唯一已知且非当前页的候选页：
       候选页 Q 满足：Q ≠ container.SemanticPageName
                      ∧ Q 的正锚在 observation 中全部/部分出现
                      ∧ Q 的 negative 锚不出现
                      ∧ (Q 的 SwitchState 锚无 SwitchState-bearing 候选时仍按文本锚计)
     恰好 1 个 → 继续；0 个 → FailSemantic(BindingUnresolved("当前页面无已知导航目标"；零导航分发))
     多个 → FailSemantic(BindingUnresolved("导航目标歧义"))    # fail closed，不猜
  3. 从 observation.Elements 定位 Q 的锚元素（唯一文本匹配；多匹配 → fail closed）
  4. Agent 授权导航 Tap（决策 authority 在 Agent；无 CandidateAuthorizationEvaluator 也成立——
     导航目标唯一性即授权条件，见 D3）
  5. lowerResult = Traversal.LowerAction 之外的直接路径：
     构造 DeviceAction.Tap(anchor.Index, anchor.Bounds)，
     step = await _traversal.ExecuteLoweredActionAsync(tap, observation)   # 既有协议：fresh+seq+Rejected
  6. freshObs = journal.PostActionObservation
     freshBelief = Reconcile.FromObservation(freshObs, _resolveSemanticPage)
     验证：freshObs.SequenceNumber > observation.SequenceNumber
           ∧ freshBelief.SemanticPage == nextPage
           ∧ freshBelief.SemanticPage != container.SemanticPageName
           ∧ !container.IsStillMine(freshObs)
     任一不满足 → FailSemantic(ExecutionFailed("导航未证明页面/容器变更；不盲目重发"))
  7. observation = freshObs; _belief = freshBelief
     _activeContainer = CreateContainer(nextPage); _activeContainer.Bind(freshObs)
     RefreshContainerEvidence(_activeContainer, freshObs)
     continue   # 重新评估 belief
```

- 页面信念 Contradicted 分支（:89-91）原样前置；`resolveSemanticPage` 多页化后，Unknown 页面（resolver 返回 null）→ belief.SemanticPage null → 导航分支 fail closed（F4）。
- 目标对象绑定后走毕业路径：**导航分支不触碰** SELECT/AUTHORIZE/LOWER/GoalEvidence 任何一行。

### D2. 页面/容器转换语义

- **当前页面信念** = `WorldBelief.SemanticPage`（`Reconcile.FromObservation`，宿主注入多页解析）。
- **期望语义对象** = `SemanticGoalInput`（不动）。
- **可达下一容器** = D1 唯一候选页（由当前 fresh 观测 + 声明锚点涌现，非表驱动）。
- 容器转换 = `CreateContainer(nextPage)` + `Bind(freshObs)`：Bind 清空绑定与局部进度——**新页面的 binding 从 fresh 观测重新计算**（`RefreshContainerEvidence`），旧页元素索引结构性失效（F5 结构性满足：`Container.Bind` 清空 `_objectBindings`、`RefreshSemanticSnapshot` 逐观测替换；实现不得跨容器缓存 binding）。
- 身份规则：每个容器由宿主注入 `identityRule`（页面名/锚点匹配），`IsStillMine` 判断 fresh 观测是否仍属当前页——页面变更的**独立反证**。
- 与 OpenWorld 先例同语义：`childPage != parentPage ∧ !container.IsStillMine(postObservation)`（OpenWorld :399-409）；本设计在语义环内等价实现，不复制其 BranchInventory 机制。

### D3. 导航动作授权（不新增 authority）

- 导航目标 = D1 唯一解析（页面 + 锚元素都唯一）——这是 Agent 的既有语义裁决：**唯一性即授权充分条件**，无歧义即无风险分发（fail closed 覆盖 0/多候选）。
- 导航动作 = `DeviceAction.Tap`（既有动作模型），经 `Traversal.ExecuteLoweredActionAsync`（既有执行/验证协议）。**不**引入导航语义动作类型、不**不**经 Traversal 直发 ADB。
- Agent 仍为唯一语义 authority：导航决策（去哪页、点哪行）、目标动作授权、完成裁决全部在 Agent；Container/Traversal/Environment/Perception 职责不变（C: 无）。

### D4. 绑定生命周期（跨页）

- **逐观测局部化**：`BindingAnalysis.Analyze(observation, criteria)` 产出观测局部证据；`BindingReconciler.Reconcile` 聚合；`Container.RefreshSemanticSnapshot` 逐观测替换。容器切换经 `Bind` 清空。
- **禁止**：跨容器缓存 binding / 复用旧页元素索引 / 将页面 N 的元素身份当作页面 N+1 的身份（F3/F5 的反面）。结构上由 D2 保证，约束测试再加一道静态断言（见 tasks 2.x）。
- 到达页面上若目标对象仍未绑定（F3：页面到了但子对象缺席）→ 导航分支继续解析下一候选（若存在）或 fail closed（无候选）——**绝不回退用旧页绑定冒充**。

### D5. 每跳 fresh 观测要求

- 每个导航动作（含返回/重试，若真实路线要求）执行后必须获得 fresh 观测：`journal.PostActionObservation` 非空 ∧ 序列严格推进（`ExecuteLoweredActionAsync` 内置）∧ 页面信念 == 期望目标页 ∧ `!IsStillMine`。
- dispatch 成功 ≠ 页面变更（F2）：Traversal `Succeeded` 仅证明「动作已执行 + fresh 观测产生」；页面/容器变更必须由 reconciliation + identity 独立证明。未证明变更 → 零推进、终止（不重发不编造）。

### D6. 场景事实（宿主声明，裁决 11）

- 初始世界：Settings 应用**根页**（`am start -a android.settings.SETTINGS`）。宿主**只**允许：emulator ready + Settings 应用启动；**禁止**导航/点按/定位到 Wi‑Fi 开关。
- 期望语义路线（**预期**，非脚本）：SettingsRoot → NetworkAndInternet → WifiInternet → SetEnabled(true)。实际路线由 D1 每跳从观测涌现；若真实页面结构不同（如多了分区），按「记录压力、fail closed、不扩界」处理。
- `resolveSemanticPage`（宿主注入）：基于 PageAnalysis 四源证据融合出唯一页面名；无法唯一融合 → null（Unknown，F4 路径）。

#### D6a. 双词汇决策（有意区分，非 drift）

导航识别与容器身份是**两套有意不同的词汇**，回答两个不同问题：

| 词汇 | 构成 | 回答的问题 | 使用位置 |
|---|---|---|---|
| **Navigation Recognition Criteria（导航识别）** | **仅正锚**（positive page anchors）+ SwitchState 锚 | "当前可见页面/候选是什么？"（下一跳识别） | Agent `ResolveNavigationPage`（`Agent.SemanticRun.cs:298`），宿主注入 `navigationPageCriteria` |
| **Container Identity Criteria（容器身份）** | **正锚 + negative 锚**（消歧共享标题文本） | "这个观测是否仍属于本容器？"（页面/容器身份消歧） | `resolveSemanticPage`（宿主注入 `identityCriteria`）→ `Reconcile.FromObservation` → `IsStillMine` |

**为何分开**：negative 锚属身份消歧——同一共享标题文本（如 "Internet" 同时出现在 N&I 页与 WifiInternet 页）必须在身份层被 negative 锚排除，才能唯一解析当前容器页。但若把 negative 锚放进**导航** criteria，会误杀合法下一跳（例如：目标页的 negative 锚文本可能恰是当前页的正锚文本）。故导航只用正锚识别"候选"，身份用正负锚消歧"归属"。

**两者都不编码** "应执行什么有序路线"——它们分别是 recognition knowledge（识别知识）与 identity knowledge（身份知识），不是 route（路线）。路由仍由 D1 每跳从当前 fresh 观测唯一候选涌现。

- 页面词汇与锚点（实现阶段以真实 emulator 观测校准）：
  - `SettingsRoot`：正锚如 ["Network & internet", "Connected devices", ...]（根页行文本）；身份 negative 锚用于排除子页特有文本。
  - `NetworkAndInternet`：正锚 ["Internet"] 等（本页特有行）；身份 negative 锚（排除共享标题/其他页特有文本）。
  - `WifiInternet`：正锚 ["Wi-Fi"（SwitchState-bearing，SWITCH_DISTRIBUTION 强信号）, "Add network"]；身份 negative 锚 ["Internet"]。
  - 锚点精确值以现场观测校准并记录于校准资产（本 change 只规定方法与证据要求）。

### D7. 证明宿主（PhysicalHost 扩展）

- 入口：`am start -a android.settings.SETTINGS`（根页），然后 **Agent 拥有全部后续导航权**。
- 证明输出（结构同 Slice 2，多页化）：逐跳 journal（导航 Tap 每跳 Succeeded + fresh 页面名序列）、最终 `SetEnabled`/`SetSwitch` 恰一次（初始 OFF）、`GoalEvidence.SourceObservationSequence == fresh 观测序列`、`SwitchState=true`、`wifi_on` 读回佐证（**非成功条件**）。
- `proofMultiLevel = satisfied ∧ hopSequence.Length ≥ 2（真实多页） ∧ eachHopFreshVerified ∧ exactlyOneSetSwitch ∧ sourcePointsAtFresh ∧ perceptionSwitchOn`。
- F2 live 变体：导航 Tap 分发成功但页面未变 → 非 SATISFIED 终止（宿主提供独立读回/页面名佐证）。
- Reality level 目标：**EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP**（§33 emulator-only）。

### D8. Falsifier 映射（specs §Falsifiers）

| # | 场景 | 预期 | 既有语义 |
|---|---|---|---|
| F1 | 根页无任何已知页锚点 | 零导航分发、无坐标猜测、无编造进度 | BindingUnresolved/StateEvidenceRequired 终止 |
| F2 | 导航 Tap 分发成功但页面未变 | 当前容器信念仍权威，零推进，非 SATISFIED | ExecutionFailed（未证明页面/容器变更） |
| F3 | 新页面出现但目标子对象缺席 | 从 fresh 世界重新 reconcile，不复用旧 binding | 继续导航或 fail closed；Bind 清空 + 逐观测刷新结构性禁止复用 |
| F4 | 遍历中观测 Unknown（页面信念 null） | fail closed / 依既有 Agent authority 有界恢复 | StateEvidenceRequired/SemanticContradiction 终止，零导航分发 |
| F5 | 页面转换后复用旧观测元素索引 | 拒绝/无法解析 | 结构性：Bind 清空 + 索引 observation-local；约束测试 + Fake 断言 |
| F6 | 到达最终页时 Wi‑Fi 已 ON | Satisfied 且 **零 SetSwitch** 变更 | 毕业幂等（S2E6 复用） |

## Risks / Trade-offs

1. **真实页面共享文本导致导航歧义**（如根页 "Network & internet" 行与子页标题同文；根页 summary 含 "Wi‑Fi" 文本 vs WifiInternet 锚）：→ 用 negative 锚 / SwitchState-bearing 锚（仅开关页有 toggle）/ 现场校准消歧；歧义未消 → fail closed（F1 覆盖），不猜。**记录为真实压力**，实现阶段以现场观测定锚点，不扩界。
2. **语义环导航分支的复杂度**：新分支必须与毕业路径正交（绑定即走毕业路径，不混流）。约束测试（架构级）防「导航进入 SetSwitch 语义链」或「毕业路径引入导航」。
3. **Fake 多页环境与真实 Settings 的保真差**：Fake 用于 falsifier 确定性；真实证明以 emulator 现场回放为准（同 Slice 2 双层策略）。Fake 页面模型须反映真实锚点校准结果。
4. **路由涌现 ≠ 遍历保证**：本设计不承诺任意深度/任意分支完备性（BFS/回溯不在范围）。真实 Settings 若需滚动/返回，记录压力并按需裁剪（Non-Goals）。
5. **证明可重复性**：导航依赖真实 UI 状态（Wi‑Fi OFF 基线准备如 Slice 2：宿主在 run 外准备 OFF 基线，允许——同毕业切片先例，不进语义路径、不计入 ActionHistory）。
