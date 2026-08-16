# physical-scroll-container-semantic-traversal Specification

## Purpose
TBD - created by archiving change physical-scroll-container-semantic-traversal. Update Purpose after archive.

## Requirements

### Requirement: 初始世界与宿主设置边界

系统 SHALL 在 Settings 应用 **Developer options 页**启动证明（宿主仅执行 emulator ready + `am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS`）。宿主 SHALL NOT 预滚动、预定位 UI 于目标开关、注入视口进度、注入目标可见性、或以任何方式告知 Agent 滚动次数；该页启动后，一切后续视口探索与动作 SHALL 由 Agent 自主执行。

#### Scenario: 宿主只建立 Developer options 页起点

- **WHEN** 证明开始且 Settings 应用 Developer options 页已启动
- **THEN** 初始观测 reconciled 到 `DeveloperOptions` 页面，且初始容器绑定该页面；此前宿主未分发任何 ScrollForward / 未预置视口位置

#### Scenario: 初始观测与期望入口不符

- **WHEN** 初始观测不能 reconciled 到宿主声明的 Developer options 页
- **THEN** 运行以 `SemanticContradiction` 终止且零设备动作分发（既有语义）

### Requirement: 目标未绑定时的同容器视口探索

当目标对象 `AutomaticSystemUpdates` 在当前容器未绑定且 `Goal.ViewportExplorationEvaluator` 对累积视口证据返回 `continue` 时，Agent SHALL 授权 **ONE** `DeviceAction.ScrollForward` 并在同一容器内继续。滚动 SHALL 由每次 fresh 观测重新决策，SHALL NOT 来自预编排的滚动次数、有序视口路线或坐标脚本。多次滚动 = 每次 evaluator=true 授权一步、fresh 观测后重评估的涌现结果。

#### Scenario: 探索被正面正当化时授权一次滚动（F1 常态）

- **WHEN** 当前容器累积视口证据正面正当化一次进一步移动（判据返回 `true`），且目标对象未绑定
- **THEN** Agent 授权并分发恰好一个 `DeviceAction.ScrollForward`，随后必须获得序列严格推进的 fresh 观测；不猜测目标坐标、不分发 SetSwitch

#### Scenario: 判据未解决（unresolved）

- **WHEN** 累积视口证据既不能证明继续也不能证明耗尽（判据返回 `null`）
- **THEN** Agent 不 dispatch 下一次滚动，零编造进度，运行 fail closed（复用 SC-P3-CAND-007 语义）

#### Scenario: 判据正面耗尽（exhausted）

- **WHEN** 累积视口证据正面证明无进一步 Goal-relevant 前向探索（判据返回 `false`）
- **THEN** Agent 停止请求滚动；若 GoalEvidence 已独立满足 → Complete，否则 Fail；不盲目滚动、不编造完成

#### Scenario: 目标初始已可见（F7）

- **WHEN** 初始观测已含 `AutomaticSystemUpdates` 绑定（目标在初始视口可见）
- **THEN** 零滚动分发，直接走毕业 SetEnabled→SetSwitch 语义链

### Requirement: LATENCY_DRIVEN_BOUNDED_EXECUTION_POLICY（后滚动语义协调策略）

系统 SHALL 支持两种后滚动语义协调策略：

**STRICT 策略（默认）**：每次 ScrollForward 后，系统 SHALL 执行完整语义协调（`TryVerifyViewportContinuity`）。
如果协调失败，系统 SHALL 按 F5 规则处理（见下文）。

**DEFERRED_BOUNDED 策略（可选）**：通过 `enableDeferredReconciliation=true` 参数启用。
系统 SHALL NOT 在每次 ScrollForward 后执行完整语义协调。系统 SHALL 在以下情况下执行强制检查点协调：

1. 当 Goal 目标候选首次在视口中可见时
2. 当 Runtime 即将执行任何非滚动语义动作时（SetSwitch / Tap / completion）
3. 当延迟滚动安全预算（MaxDeferredScrolls = 5）耗尽时
4. 当廉价漂移检查检测到明显世界变化时
5. 当视口探索判据返回 exhausted 或 unresolved 时

在延迟模式下，系统 SHALL 在每次 ScrollForward 后执行廉价漂移检查（仅使用已获得的 Fresh Observation，
无需额外截图或感知）。廉价漂移检查 SHALL 检测：
- 前台应用是否改变
- 弹窗/系统窗口是否出现
- 当前 Container 身份是否被强烈矛盾

#### Scenario: 延迟模式检查点协调（CASE A - 同容器）

- **WHEN** 延迟滚动后执行强制检查点协调，且 `TryVerifyViewportContinuity` 成功确认当前语义页
- **THEN** 系统 SHALL 保留当前 Container，追加视口证据，刷新语义绑定，继续同一 SemanticGoal

#### Scenario: 延迟模式检查点协调（CASE B - 不同已知页）

- **WHEN** 延迟滚动后执行强制检查点协调，且 `TryVerifyViewportContinuity` 失败但新鲜观测解析为另一个已知语义页
- **THEN** 系统 SHALL 使用既有 multi-level 遍历协调：创建新 Container，从新鲜观测绑定，刷新证据，继续同一 SemanticGoal

#### Scenario: 延迟模式检查点协调（CASE C - 未知页）

- **WHEN** 延迟滚动后执行强制检查点协调，且语义页面无法解析（未知）
- **THEN** 系统 SHALL 以 `SemanticContradiction` 终止（fail closed）

#### Scenario: 延迟模式检查点协调（CASE D - 同页但连续性无法证明）

- **WHEN** 延迟滚动后执行强制检查点协调，且 `TryVerifyViewportContinuity` 失败但页面名相同
- **THEN** 系统 SHALL 以 `SemanticContradiction` 终止（fail closed）

### Requirement: F5 - 滚动导致的意外页面/容器变更

系统 SHALL NOT 在滚动导致语义页面变更时强制保留同一容器连续性。
当一次 ScrollForward 后，`TryVerifyViewportContinuity` 失败（STRICT 模式）或检查点协调失败（DEFERRED 模式），
且新鲜观测解析为另一个已知语义页时，系统 SHALL 使用既有 multi-level 遍历协调机制：

- 从新鲜观测解析目标语义页
- 创建新 Container
- 从新鲜观测 Bind
- 刷新语义证据
- 继续同一 SemanticGoal

该行为被称为"外部世界权威"（external world wins）。

#### Scenario: 滚动导致意外页面变更

- **WHEN** 一次 ScrollForward 后，新鲜观测解析为另一个已知语义页（与当前 Container 不同）
- **THEN** 系统 SHALL 执行 multi-level 协调：创建新 Container，从新鲜观测 Bind，刷新证据，继续同一 Goal
- **AND** 系统 SHALL NOT 因"上一步是 Scroll"而强制同容器续跑

### Requirement: 每次滚动动作验证与同容器连续性

每个滚动动作 SHALL 满足「Action receipt + fresh Observation + 验证同容器连续性」：分发返回后必须获得 fresh 观测，且该观测的序列严格推进、前台兼容、`IsStillMine` 为真、reconciled 页面名等于当前容器页；任一不满足 SHALL NOT 被当作同容器视口推进。单独分发成功 SHALL NOT 推进语义进度。

#### Scenario: 滚动分发成功且同容器连续性成立

- **WHEN** 一次 `ScrollForward` 分发成功，fresh 观测序列严格推进且 `TryVerifyViewportContinuity` 接受（前台兼容 ∧ `IsStillMine` ∧ 相同 reconciled 页面）
- **THEN** 同一 Container 追加该 fresh 观测进 `ViewportExplorationObservations`（不 Bind、不创建新容器），保留既有局部进度，继续同一语义闭环

#### Scenario: 滚动分发成功但视口未变（F2）

- **WHEN** 滚动分发成功且获得 fresh 观测，但 fresh 观测内容未引入 Goal-relevant 证据且判据未返回 `true`（未变或耗尽）
- **THEN** 无视口进度、有界停止、非 SATISFIED 终止；绝不将 Traversal `Succeeded` 视为视口进度证明

#### Scenario: 滚动分发被拒

- **WHEN** Traversal 以 Rejected 结果拒绝滚动动作
- **THEN** 运行以 `ExecutionFailed("Semantic action rejected: …")` 终止（既有协议，无自动恢复尝试）

### Requirement: 滚动不是容器转场

一次滚动 SHALL NOT 创建新 Container。滚动后的 fresh 观测若证明**同一语义页**，Agent SHALL 继续使用同一 Container；若证明**另一页**（`!IsStillMine` 或 reconciled 页面名 ≠ 当前页），系统 SHALL 按既有 multi-level 遍历规则 reconcile，绝不因「上一步是 Scroll」强制同容器续跑。

#### Scenario: 同容器滚动（核心语义）

- **WHEN** Observation N → Container A → 目标不可见 → 一次滚动 → Observation N+1 仍 reconciled 到 Container A 的语义页（`IsStillMine==true`）
- **THEN** Container 保持 A，视口刷新，目标在 A 内可见并被 fresh 绑定；不创建 Container B

#### Scenario: 滚动导致意外页面/容器变更（F5）

- **WHEN** 滚动后 fresh 观测证明另一语义页（`!IsStillMine` 或 reconciled 页面名 ≠ 当前页）
- **THEN** 外部世界权威：按既有 multi-level 遍历规则 reconcile；绝不强制同容器续跑、绝不把页面变更当作视口推进

### Requirement: 滚动后 fresh 绑定生命周期

对象绑定 SHALL 为观测局部：滚动后目标可见 → 必须从 fresh 观测经 `BindingAnalysis`/`BindingReconciler` 重新计算并经 `RefreshSemanticSnapshot` 逐观测替换。视口 N 的元素索引/边界 SHALL NOT 被当作视口 N+1 的元素身份复用；Container 保留语义连续性但不保留陈旧 observation-local grounding 为 truth。

#### Scenario: 目标滚动后出现 → fresh 绑定（F4）

- **WHEN** 滚动后 fresh 观测含 `Automatic system updates` 开关（此前不可见）
- **THEN** 绑定从 fresh 观测重新计算（新元素索引/边界），不使用旧视口的索引/边界；状态信念来自 fresh 观测的开关状态

#### Scenario: 目标滚动后仍缺席（F3）

- **WHEN** 滚动后 fresh 观测仍不含 `AutomaticSystemUpdates` 绑定
- **THEN** 从 fresh 世界重新 reconcile；不复用旧视口绑定；若判据再有界正当化可再授权一步，否则 fail closed——无预计算滚动次数

#### Scenario: 目标可见但歧义（F8）

- **WHEN** fresh 观测含 `Automatic system updates` 相关文本但绑定 0 个或多个 toggle 候选（或 SwitchState UNKNOWN）
- **THEN** 状态信念为 UNKNOWN，不猜测动作，运行以 `StateEvidenceRequired` 终止（fail closed）

### Requirement: 同一条 Goal 存活于滚动

同一 `SemanticGoal`（`AutomaticSystemUpdates.Enabled=true`）SHALL 在滚动前后原样存活。滚动 SHALL NOT 重建 Goal、不引入场景专用继续状态、不改变 Goal 语义对象/状态维度/期望值。

#### Scenario: 滚动不重建 Goal

- **WHEN** 一次滚动发生后，语义环继续评估
- **THEN** 评估的仍是同一个 `SemanticGoalInput`（同 objectIdentity/stateDimension/desiredValue）；无滚动→重建 Goal、无滚动→场景专用状态

### Requirement: 最终状态动作与完成

一旦 `AutomaticSystemUpdates` 在当前容器绑定，系统 SHALL 复用毕业语义链：capability 选择 → 授权 → `SetEnabled` → `SetSwitch` → 物理效果 → fresh 观测 → 感知 `SwitchState=true` → `GoalEvidence` → `Satisfied`。完成 SHALL 仅由 fresh post-dispatch 观测的 GoalEvidence 证明；ADB 分发收据或 `settings get global ota_disable_automatic_update` 读回 SHALL NOT 作为成功 authority（仅佐证）。

#### Scenario: 滚动后完整同容器闭环（OFF→ON）

- **WHEN** 初始 `Automatic system updates` OFF 且不可见，Agent 在 Developer options 页自主滚动至其可见并执行 SetEnabled(true)
- **THEN** 运行以 `Satisfied` 终止；`GoalEvidence.SourceObservationSequence` 等于含 `SwitchState=true` 的 fresh post-dispatch 观测序列；恰一次 SetSwitch；同容器连续性已证且视口滚动步数 ≥ 1

#### Scenario: 目标状态无法证明

- **WHEN** 分发后 fresh 观测 SwitchState 仍非目标值或为 UNKNOWN
- **THEN** 非 SATISFIED 终止（StateEvidenceRequired/ExecutionFailed），不得以分发收据完成

### Requirement: 禁止机制

系统 SHALL NOT 引入：ScrollManager / ScrollPlanner / ViewportNavigator / ScrollCapability authority / workflow engine / 滚动 DSL / 硬编码滚动次数 / 有序视口路线 / 目标专用坐标 / 场景专用滚动状态机 / 预录视口序列 / WorldState 注入 / 隐藏 emulator API / 新语义 authority。滚动 SHALL 复用既有 Agent 裁决、Container 状态、Traversal 协议、`DeviceAction.ScrollForward` 与毕业 SetEnabled 链。

#### Scenario: 无场景专用滚动代码进入 Runtime

- **WHEN** 构建 `src/UniClaw.Runtime` 与 `src/UniClaw.Runtime.Adapters`
- **THEN** 不包含 Settings 页面名/锚点/坐标常量或滚动次数（场景知识仅存在于宿主注入的 criteria / identity 规则 / ViewportExplorationEvaluator）；Guard 1（零 ProjectReference）保持；ADB Swipe 机制不变

#### Scenario: 毕业链零改动回归

- **WHEN** 实施本 change 后运行既有毕业回归（含 Slice 2 与 multi-level falsifier）
- **THEN** 全部通过，且 `Agent.SemanticRun` 的毕业路径（SELECT→AUTHORIZE→LOWER→ExecuteLoweredActionAsync→GoalEvidence）与 multi-level 导航路径行为不变

### Requirement: 现实证明

证明 SHALL 在 §33 emulator-only 边界内进行（emulator-5554 等既有前置），以真实 Settings 应用现场回放为现实证据；页面锚点与目标状态 SHALL 以现场观测校准并记录 provenance。成功判定 SHALL NOT 依赖 `REAL_DEVICE_PROVEN`。

#### Scenario: 现场同容器滚动回放

- **WHEN** emulator 前置满足且 `Automatic system updates` OFF 基线已确认（宿主 run 外读回佐证）
- **THEN** 证明输出显示至少一步 fresh 验证的 `ScrollForward`、同容器连续性已证、恰一次 SetSwitch、fresh GoalEvidence 与感知 SwitchState=true；`ota_disable_automatic_update` 读回仅打印佐证

#### Scenario: 前置失败显式终止

- **WHEN** emulator 不可达 / 截图探针失败 / Developer options 页启动失败
- **THEN** 以显式原因终止（exit 2 类），零滚动分发、零遍历
