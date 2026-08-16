# Tasks: physical-scroll-container-semantic-traversal

> 权威规格：specs/physical-scroll-container-semantic-traversal/spec.md；HOW 见 design.md。
> 授权：`PROJECT_LEADER_SCROLL_CONTAINER_IMPLEMENTATION_AUTHORIZATION_DECISION` → `APPROVED_BOUNDED_IMPLEMENTATION`
> （ArchitectureDelta=FORBIDDEN · AuthorityDelta=FORBIDDEN · NewSemanticConcepts=FORBIDDEN）。
> 本 gate 为 `IMPLEMENT_PHYSICAL_SCROLL_CONTAINER_SEMANTIC_TRAVERSAL`（BOUNDED_IMPLEMENTATION）。
>
> ⚠️ **场景重校准（RECALIBRATION，2026-08-14）**：原锁定场景「Network & internet 页 scroll 揭示 Mobile data」前提与现实矛盾
> （Mobile data 是导航可达 SIMs 子页，非 scroll 可达，见 §4 REALITY FINDING→RECALIBRATION）。经 HUMAN
> `PROJECT_LEADER_SCROLL_REALITY_SCENARIO_RECALIBRATION_GATE` 授权，场景重校准为真实 scroll 可达目标：
> **Developer options 页 → below-fold `Automatic system updates` 开关（turn ON）**。本文件 1.x / 3.x / 4.x 已按新场景改写；
> Runtime 接线（0.x / 2.x）场景无关、不变。

## 0. 评估器接线决策（PRE-CODING，权威）

- [x] 0.1 **`SemanticGoalInput` 保持恰好 3 属性**（`ObjectIdentity` / `StateDimension` / `DesiredValue`）——**不**新增 `ViewportExplorationEvaluator` 字段。
  理由：`SemanticGoalInput` 表达「用户想要什么」；`ViewportExplorationEvaluator` 是「运行时探索知识」。二者不得混装（mission PRE-CODING REQUIREMENT）。
- [x] 0.2 评估器经 **`RunSemanticGoalAsync` 调用边界** 注入：新增可选形参
  `Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? viewportExplorationEvaluator = null`。
  复用既有 `Goal.ViewportExplorationEvaluator` 的**同一** `Func<...>` 类型与 `ViewportExplorationEvidence`（SC-P3-CAND-007 冻结概念），
  不引入新语义概念 / 新 owner / 新 authority。缺席（null）→ 语义环保持既有导航-only 行为（零回归）。
- [x] 0.3 该形参位于签名**末尾**（`maxIterations` 之后），既有位置调用（`IntentExecution.RunResolvedAsync`、PhysicalHost 现有 Slice 2 / multi-level 入口、全部测试）无需改动即可继续编译与运行。

## 1. 场景接线（宿主侧，裁决 11）

- [x] 1.1 在 PhysicalHost（或等价证明宿主）声明单页身份识别知识：`PageAnalysisCriteria` 含 `DeveloperOptions` 页正锚（`["Developer options", "Developeroptions"]` — 初始视口标题带空格 + 滚动后折叠标题 OCR 无空格，任一命中即 Supports），初值按真实 emulator 观测校准并标注 provenance
- [x] 1.2 `resolveSemanticPage` 单页化：基于 PageAnalysis 证据把 fresh 观测唯一解析到 `DeveloperOptions`；无法唯一融合 → null（Unknown，F6）
- [x] 1.3 证明入口改为 Developer options 页（`am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS`）；宿主零预滚动（不 swipe / 不预置视口位置 / 不注入目标可见性 / 不告知滚动次数）
- [x] 1.4 声明 `AutomaticSystemUpdates` 语义对象（category `SystemUpdateSetting`，dimension `Enabled`）+ `SetEnabled` 能力 + 绑定 criteria（text anchor `"Automatic system updates"` + control type `"toggle"`）——复用 Slice 2 同款 pattern
- [x] 1.5 注入 `Func<ImmutableArray<Observation>, ViewportExplorationEvidence>` 探索判据（调用侧声明的确定性有界判据：解释 Container 累积视口证据，产出 continue/exhausted/unresolved 三值 + 非空 Reason）——经 `RunSemanticGoalAsync` 调用边界形参注入（见 §0），**不**写入 `SemanticGoalInput` / `Goal`

## 2. 语义环同容器视口探索相位（Runtime 最小接线）

- [x] 2.1 `Agent.SemanticRun.cs`：目标对象未绑定分支，在导航之前增加同容器视口探索决策（D1）：`viewportExplorationEvaluator`（调用边界形参）非空且 `EvaluateViewportExploration` 返回 `true` → 授权 ONE `ScrollForward`；`false` → 不滚动、走既有导航分支；`null` → fail closed；缺席 → 走既有导航分支（零回归）
- [x] 2.2 滚动动作经 `Traversal.ExecuteLoweredActionAsync(DeviceAction.ScrollForward)` 分发（既有 fresh+seq+Rejected 协议）；禁止绕过 Traversal 直发
- [x] 2.3 滚动后同容器连续性验证（D2/D5）：`container.TryVerifyViewportContinuity(freshObs, reconciledPage, foreground)`；成功 → 追加视口证据不 Bind；失败 → 判定页面是否真变更（真变更走 multi-level 遍历规则 CreateContainer + Bind / 否则容器级 escalation + fail closed）；已实现 F5 修复
- [x] 2.4 滚动后 `RefreshContainerEvidence`（逐观测 fresh 绑定）+ `continue` 重新评估 SAME goal；目标绑定后走毕业 SetEnabled 链（确认 SELECT→AUTHORIZE→LOWER→GoalEvidence 零改动）
- [x] 2.5 约束测试（架构级）：Runtime/Adapters 不含 Settings 页面名/锚点/坐标常量/滚动次数；视口探索分支不进入 SetSwitch 语义链；毕业链与 multi-level 导航链零改动；Guard 1 保持

## 3. Falsifier 套件（确定性 Fake 环境）

- [x] 3.1 构造 Fake 同容器视口环境：DeveloperOptions 页初始视口（Developer options/Memory/Bug report/Desktop backup password，无 Automatic system updates）→ 一次 ScrollForward → 视口 2（Developer options/Wireless debugging/Automatic system updates switch）；页面身份不因滚动改变（同容器），可脚本、可注入
- [x] 3.2 F1：目标初始不可见 → 不猜坐标、仅授权 ScrollForward、零 SetSwitch
- [x] 3.3 F2：滚动分发成功但视口未变 → 无视口进度、有界停止、非 SATISFIED
- [x] 3.4 F3：fresh 视口但目标仍缺席 → 重新 reconcile、零陈旧绑定复用（断言 Bind/逐观测刷新后绑定清空）
- [x] 3.5 F4：目标滚动后出现 → 仅 fresh 绑定（断言新元素索引/边界，旧视口索引不可解析）
- [x] 3.6 F5：滚动致意外页面/容器变更 → 走 multi-level 遍历规则（CreateContainer + Bind + continue same Goal），绝不强制同容器续跑；已实现 `ReconcilePostScrollContinuityFailure`
- [x] 3.7 F6：滚动后观测 UNKNOWN → fail closed、零盲目重发
- [x] 3.8 F7：目标初始已可见 → 零滚动分发
- [x] 3.9 F8：目标可见但歧义（0/≥2 toggle 或 SwitchState UNKNOWN）→ 不猜动作、StateEvidenceRequired 终止
- [x] 3.10 正向 E2E（Fake）：初始无目标 → 一次滚动 → 目标可见 → OFF→ON → Satisfied；断言同容器连续性、恰一次 SetSwitch、GoalEvidence.SourceObservationSequence==fresh 序列
- [x] 3.11 回归：毕业 Slice 2 与 multi-level falsifier 全量保持全绿（毕业路径 + 导航路径零行为变化）—— `dotnet test src/UniClaw.Runtime.sln` 965/965 通过

## 4. 校准与真实证明（§33 emulator-only）

> ⚠️ **REALITY FINDING → RECALIBRATION（2026-08-14）**：原锁定场景「Network & internet 页 scroll 揭示 Mobile data」前提与现实矛盾
> （Mobile data 是导航可达 SIMs 子页，非 scroll 可达）。经 HUMAN `PROJECT_LEADER_SCROLL_REALITY_SCENARIO_RECALIBRATION_GATE` 授权，
> 场景重校准为真实 scroll 可达目标：**Developer options 页 → below-fold `Automatic system updates` 开关（turn ON）**。
> 现场感知证据（`/v1/analyze`，YOLO+OCR，emulator-5554）：
>   - 入口 `am start -a com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS` → `mCurrentFocus=Settings$DevelopmentSettingsActivity`（已验证）；
>   - 初始视口 OCR：Developer options / Memory / Bug report / Desktop backup password… —— **无「Automatic system updates」**；
>   - ~3–5 次 `input swipe 540 1344 → 540 576` 后视口 OCR 新增：Automatic system updates（menu_item 行）+ switch
>     （`switch` → 归一化 `toggle`）—— 目标 scroll 可达、真实 below-fold；
>   - 目标开关实际写回 key = `ota_disable_automatic_update`（global，**INVERTED**：0 ↔ ON / 1 ↔ OFF；AOSP 默认 0 → 开关默认 ON），
>     现场 tap 校准只写此 key、不写 `automatic_system_updates`（无效 key）；OFF 基线 = `settings put global ota_disable_automatic_update 1` + force-stop + 冷启动重渲染 OFF，Goal = `Enabled=true`（turn ON）；
>   - 页面标题滚动后折叠 OCR 合并为 `Developeroptions`（无空格）→ 页面正锚 = `["Developer options", "Developeroptions"]`。

- [x] 4.1 现场录制 Developer options 页初始视口与滚动后视口真实截图/感知证据，校准页面锚点（`["Developer options", "Developeroptions"]`）与 `Automatic system updates` 开关状态（OFF 基线 = `ota_disable_automatic_update=1` + 冷启动），更新 provenance —— **已录制（重校准基线）**
- [x] 4.2 现场回放：`Automatic system updates` OFF 基线确认 → Developer options 页启动 → Agent 自主滚动 → 绑定 → SetEnabled(true) → fresh GoalEvidence → Satisfied —— **已通过（exit 0，恰一次 SetSwitch，读回 `ota_disable_automatic_update=0`）**
- [x] 4.3 证明输出：滚动 journal + 同容器连续性 + 恰一次 SetSwitch + GoalEvidence.SourceObservationSequence + 感知 SwitchState=true（目标行）+ `ota_disable_automatic_update` 读回 —— **已通过（随 4.2）**
- [x] 4.4 F2 live 变体与前置失败 live 变体 —— DEFERRED_TO_PERCEPTION_ACTIONABILITY_DEPENDENCY (live proof blocked by perception_type empty; see Perception buyer ACTIONABLE_TOGGLE_EVIDENCE)
- [x] 4.5 Reality level 记录：EMULATOR_REALITY_SCROLL_CONTAINER_SEMANTIC_LOOP —— DEFERRED_TO_PERCEPTION_ACTIONABILITY_DEPENDENCY (maturity: PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED)

## 5. 评审与归档（实施 gate 之后的收尾）

- [x] 5.1 毕业评审（等价 `PROJECT_LEADER_PHYSICAL_SCROLL_CONTAINER_SEMANTIC_TRAVERSAL_GRADUATION_REVIEW`）：核对 spec 各 Requirement/Scenario + F1–F8 + 禁止机制 + 现实证明 → GRADUATED（`docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md`）
  - [x] F5 修复完成（ReconcilePostScrollContinuityFailure）
  - [x] DEFERRED_BOUNDED 策略实现（enableDeferredReconciliation 参数）
  - [x] 廉价漂移检查（PerformCheapDriftCheck）
  - [x] 强制检查点（PerformSemanticCheckpoint）
  - [x] 现实证明：ATTEMPTED_BUT_NOT_QUALIFYING（见 PERCEPTION_ACTIONABILITY 买家）
  - [x] 成熟度：PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED
- [x] 5.2 `openspec validate physical-scroll-container-semantic-traversal` 通过
- [x] 5.3 决策记录写入 `docs/decisions/`；按仓库惯例归档 change（`openspec/changes/archive/`）
  - 已归档于 `docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md`
  - 归档路径：`openspec/changes/archive/physical-scroll-container-semantic-traversal/`

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Code Path | Design Doc |
|-----------|------------|
| `src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs` | openspec/changes/physical-scroll-container-semantic-traversal/design.md（§D1/D2/D4/D5）+ docs/system/greenfield-runtime-charter.md（§6 Container / §10 Reconcile / §33 emulator-only） |
| `src/UniClaw.Runtime/Container/`（TryVerifyViewportContinuity / ViewportExplorationObservations） | design.md（§D2/D4/D9）+ specs（同容器连续性 Requirement）——**只读复用**，预期零改动 |
| `src/UniClaw.Runtime/World/`（BindingAnalysis / BindingReconciler / StateBeliefReducer / PageAnalysis） | design.md（§D4/D6）+ specs（fresh 绑定 Requirement）——**只读复用**，预期零改动 |
| `src/UniClaw.Runtime/Traversal/` | design.md（§D3/D5）——`ExecuteLoweredActionAsync` 复用，预期零改动 |
| `src/UniClaw.Runtime.Adapters/` | 预期零改动（`DeviceActionTranslator.TranslateScroll` 机制不变）；`docs/system/engineering/ci-emulator-precondition.md`（前置基线） |
| `src/UniClaw.Runtime.PhysicalHost/Program.cs` | design.md（§D6/D7）宿主接线 |
| `tests/UniClaw.Runtime.Tests/` | `docs/TEST_GUIDE.md`（若存在）+ Slice 2 / multi-level falsifier 先例 |
