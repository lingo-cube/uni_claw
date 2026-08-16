# Semantic Agent Runtime — Current State Review

> Generated: 2026-08-10
> Updated: 2026-08-16
> Role: Runtime Architecture Analyst
> Purpose: 建立当前 Runtime 的共同事实基线，作为后续 Semantic Agent Runtime 演进基础。
> Scope: 分析现有代码 + decision docs + archived OpenSpec changes。不提出新实现方案，不修改代码。
> Inputs: 14 Architecture Invariants · decision docs · full production source · current HEAD tests

---

## 0. 2026-08-16 Baseline Update

Current Runtime maturity is `POST_DETERMINISTIC_SEMANTIC_RUNTIME_PROGRESS`.

After the 2026-08-10 review baseline, the following deterministic Runtime capabilities have graduated:

1. `PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED`
   - Record: `docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md`
   - Archive: `openspec/changes/archive/2026-08-16-physical-scroll-container-semantic-traversal/`

2. `SEMANTIC_RUN_POPUP_OBSTRUCTION_HANDLED`
   - Record: `docs/decisions/semantic-run-popup-obstruction-graduation-decision.md`
   - Archive: `openspec/changes/archive/2026-08-16-semantic-run-popup-obstruction-integration/`

3. `PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED`
   - Record: `docs/decisions/perception-actionable-toggle-evidence-reality-repair-graduation-decision.md`
   - Archive: `openspec/changes/archive/2026-08-16-perception-actionable-toggle-evidence-reality-repair/`

4. `SEMANTIC_RUN_UNEXPECTED_NAVIGATION_RECONCILED`
   - Record: `docs/decisions/semantic-run-unexpected-navigation-reconciliation-graduation-decision.md`
   - Archive: `openspec/changes/archive/2026-08-16-semantic-run-unexpected-navigation-reconciliation/`

These entries are graduated deterministic/mechanism capabilities. They do **not** claim universal Android control recognition, open-world traversal completion, DSH cognition, Shadow, or live-device proof.

---

## 1. 当前整体架构地图

```
Business Goal / Intent     PARTIAL — IntentSemanticEnvelope 存在但仅做 plan projection；
                           Runtime 不解析 "开启 Wi-Fi" 这类自然语言目标。
        ↓
Agent                      EXISTS — 闭环 RunAsync + 开环 RunOpenWorldAsync。
                           拥有 RunState、WorldBelief 实例、Container 管理、Recovery 决策。
        ↓
Plan / Execution Contract  EXISTS — Plan = ImmutableArray<PlanStep>。
                           PlanStep 是 "Tap('Wi‑Fi')" / "SetSwitch true" 这类 action token，
                           不是 semantic action。
        ↓
Container                  EXISTS — 语义页面级局部状态域。
                           拥有 _semanticPageName、_observation、_localPageBeliefState。
                           单容器深度 1（Phase 1）；OpenWorld 支持 parent→child 栈。
        ↓
Observation                EXISTS — ImmutableArray<ObservedElement> + ForegroundApplication + SequenceNumber。
                           是 evidence，不是 truth（I-4）。
        ↓
PageAnalysis               EXISTS — 静态纯函数。Observation → SemanticEvidence[]。
                           已接入 Agent 生产主循环（闭环 + 恢复后）。
        ↓
Semantic Evidence          EXISTS — SemanticEvidence { Source, Claim, Stance, Reason? }。
                           多源、可冲突、定性。
        ↓
Semantic Belief            EXISTS — SemanticReconciliation.FuseBelief() → SemanticBeliefState。
                           Container._localPageBeliefState 持有融合结果。
        ↓
Traversal                  EXISTS — Select → Check → Execute → Verify。
                           grounding 仅 Text + SwitchState?；动作仅 Tap / SetSwitch / ScrollForward。
        ↓
DeviceAction               EXISTS — Tap(Index, Bounds?), SetSwitch(Index, State, Bounds?), ScrollForward, LaunchApp。
                           Index 是 observation-local 定位器，不是坐标。Bounds 已购买（可选）。
        ↓
Environment                EXISTS — IEnvironment: ObserveAsync + ExecuteAsync。
                           测试侧 ScriptedEnvironment（确定性的）；生产侧适配器未实现。
```

**关键缺失:**
- **SemanticElement**: MISSING — 没有"这个 Wi‑Fi 元素是 StateChangingControl"的 Runtime 内部类型
- **SemanticPage**: PARTIAL — 只有 `string _semanticPageName`，没有 page identity model
- **Business Intent → Semantic Action**: MISSING — "开启 Wi‑Fi" → Tap("Wi‑Fi") 的编译发生在调用侧，Runtime 不解包 intent
- **ElementAnalysis**: MISSING — 类型仅存在于架构文档和决策中，未实现
- **OpenWorld Planning Stack**: 存在但 **test-only** — `IntentSemanticEnvelopeExecution` 和 `Agent.RunOpenWorldAsync` 编译进 Agent，但没有 `src/` 下的生产调用者

---

## 2. 当前 Semantic 能力盘点

### A. 已具备能力

| Capability | Owner | Input | Output | Production-Wired? | Known Limitation |
|---|---|---|---|---|---|
| **PageAnalysis** | Stateless (World/) | Observation + PageAnalysisCriteria | SemanticEvidence[] | **YES** — Agent 主循环 + 恢复后 | 仅 TEXT_ATTRIBUTE 级别（text anchors + foreground app + SwitchState）。无空间/结构信号。 |
| **SemanticEvidence** | Model (不可变) | — | — | **YES** — PageAnalysis 生产 | Contract 是定性三态（Supports/Contradicts/Insufficient），不表达竞争假设 |
| **Belief Fusion** | Stateless (SemanticReconciliation) | SemanticEvidence[] | SemanticBeliefState | **YES** — Container.EvaluatePageBelief | 所有 evidence 视为同一隐式 claim；跨 claim 证据会交叉污染 |
| **Container Local Belief** | Container (I-2) | EvaluatePageBelief 调用 | _localPageBeliefState | **YES** — Agent 主循环设置 | ⚠️ **write-only in production** — Agent 调用 EvaluatePageBelief 但从不读取 LocalPageBeliefState 做控制流决策；仅 observability |
| **Spatial Bounds** | Model (ElementBounds) | — | — | **PARTIAL** — 模型已购买，ScriptedEnvironment 可传递，但真实 perception adapter 未实现 | 仅测试/合成数据填充；真实设备路径不存在 |
| **PerceptionType** | Model (ObservedElement) | — | — | **PARTIAL** — 字段存在，ScriptedEnvironment 可传递，真实路径未实现 | ⚠️ **zero production consumers** — Traversal.Select 只用 Text+SwitchState；TypeLevel 分类器是独立 caller lambda，不读 PerceptionType |
| **Type Directed Dispatch** | Caller-injected (TypeLevelDispatchPolicy) | ObservedElement → TypeLevelElementCategory? | Category→Handling 映射 | **YES** — OpenWorld 路径使用 | 仅 2 个 category（NavigableContainer, StateChangingControl）；分类器是 caller lambda |
| **Grounding** | Traversal | PlanStep + candidates | selected Index | **YES** — 所有 Tap/SetSwitch 步骤 | 仅 Text 匹配 + SwitchState 消歧；空文本 candidate 不可定位；重复文本取首个 |
| **Recovery** | Agent + Recovery 组件 | Trap → RecoveryAnchor → 恢复 recipe | 恢复/失败 | **YES** — drift 触发恢复流程 | 仅合成 fixture 验证；真实设备恢复未验证 |
| **Goal Evidence** | Caller-injected (Goal.EvidenceEvaluator) | Observation | GoalEvidence | **YES** — 每个 post-action observation | I-10 满足：仅 Satisfied=true 触发 Completed |
| **Candidate Authorization** | Caller-injected (Goal.CandidateAuthorizationEvaluator) | Observation + candidate | CandidateAuthorizationEvidence | **YES** — CP12 路径 | 安全策略完全由调用侧定义 |
| **Viewport Exploration** | Agent + Container (TryVerifyViewportContinuity) | postObservation + reconciled page | Continuity verified/failed | **YES** — viewport 步骤后 | 仅合成 fixture；真实 scroll 验证未实现 |
| **Container Continuity** | Container (TryVerifyLocalContinuity) | observation + reconciled page + foreground | bool | **YES** — local handling 步骤后 | 身份规则仍是 caller lambda (identityRule) |
| **Agent Drift Detection** | Agent (IsAgentScopeDrift) | observation + container + belief | bool | **YES** — 每个非 viewport 步骤后 | 仅 foreground + IsStillMine + SemanticPage==null 三信号 |
| **Trap / Escalation** | Container → Agent | TrapKind + TrapScope + evidence | Trap 记录 | **YES** — ContainerMismatch + UnexpectedPage | Container→Agent 单向；Agent→Container 仅 CreateContainer+Bind（discard+rebuild） |

### B. 存在但未使用的模型

| Model | 位置 | 状态 |
|---|---|---|
| `SemanticReconciliation` | Model/SemanticReconciliation.cs | **已使用**（Container.EvaluatePageBelief → FuseBelief） |
| `ElementBounds` | Model/Observation/ElementBounds.cs | **已购买**，字段存在，ScriptedEnvironment 可填充 |
| `BranchProgressEvidence` | Model/ | **已使用**（OpenWorld 路径） |
| `ViewportExplorationEvidence` | Model/ | **已使用**（viewport 步骤） |

---

## 3. 当前 Semantic Boundary

### 3.1 语义层级

```
Level 0: RAW PERCEPTION EVIDENCE
  ObservedElement.Text         — "Wi‑Fi"（OCR 文本）
  ObservedElement.SwitchState? — true / false / null（仅合成数据；真实感知不产生）
  ObservedElement.Bounds?      — normalized [0,1]×[0,1]（仅合成数据）
  ObservedElement.PerceptionType? — "toggle" / "menuItem" / "text"（provider label）
  Observation.ForegroundApplication — "com.android.settings"
  Observation.SequenceNumber   — 单调序号

Level 1: SEMANTIC EVIDENCE
  SemanticEvidence { Source, Claim, Stance, Reason? }
    — FOREGROUND, TEXT_ANCHOR, TEXT_ANCHOR_NEGATIVE, SWITCH_DISTRIBUTION
    — 每个 source 独立产生证据；可冲突

Level 2: SEMANTIC BELIEF
  SemanticBeliefState = SUPPORTED / UNRESOLVED / CONTRADICTED
    — 融合多源证据后的结论
    — Container._localPageBeliefState 持有

Level 3: SEMANTIC INTERPRETATION（MISSING）
  SemanticElement     — 不存在
  SemanticPage        — 不存在（仅 string _semanticPageName）
  ElementCategory     — caller lambda（CategoryClassifier），Runtime 内部无
  InteractionCapability — 不存在

Level 4: SEMANTIC ACTION（MISSING）
  "开启 Wi‑Fi" → Tap("Wi‑Fi") 的编译在调用侧
  Runtime 接收的是 PlanStep("Wi‑Fi", "Tap")，不含语义
```

### 3.2 关键区分

| 概念 | 当前 Runtime 中的存在形式 | 是 Semantic? | 说明 |
|---|---|---|---|
| **Observation** | `Observation` record | **NO** — raw evidence | 不可变快照。I-4: evidence, not truth |
| **ObservedElement** | `ObservedElement` record | **NO** — raw evidence | Text + SwitchState + Bounds + PerceptionType |
| **SemanticElement** | **不存在** | — | 缺失层：有身份、类别、能力的元素 |
| **Container** | `Container` class | **PARTIAL** — local state owner | 持有 page name string + local belief state |
| **Page** | `_semanticPageName` (string) | **NO** — 仅命名 string | 无 page identity model |
| **Action** | `DeviceAction` (Tap/SetSwitch/ScrollForward) | **NO** — execution primitive | 无 semantic action（desired effect, target capability） |
| **Goal** | `Goal` record (caller-injected evaluators) | **NO** — 仅 evaluation predicate | Runtime 不解析 goal 语义 |

### 3.3 Semantic 能力来源

当前 Runtime 的 semantic 能力来源：

| 来源 | 占比 | 示例 |
|---|---|---|
| **Caller-injected lambdas** | ~60% | resolveSemanticPage, identityRule, CategoryClassifier, EvidenceEvaluator, CandidateAuthorizationEvaluator |
| **Runtime-internal stateless** | ~25% | PageAnalysis.Analyze, SemanticReconciliation.FuseBelief, Reconcile.FromObservation |
| **Runtime-internal stateful** | ~15% | Container._localPageBeliefState, Agent._belief, Agent._branchProgress |

**Runtime 尚未拥有独立的 semantic interpretation 能力。** 除了 PageAnalysis（caller 提供 criteria → Runtime 产生 evidence）和 Belief Fusion（纯函数），所有"这个元素是什么""这个页面是什么""目标是否达成"的判断都由调用侧注入的 lambda 完成。

---

## 4. Business Semantic Gap 分析

### 4.1 当前入口

```
Business Goal: "确保 WiFi 已开启"
        ↓（调用侧编译）
IntentSemanticEnvelope.Project(intent, goal, ClosedWorldConcrete(plan))
        ↓
Plan = [
    PlanStep("Network&internet", "Tap"),
    PlanStep("Internet", "Tap"),
    PlanStep("Wi‑Fi", "Tap"),
    PlanStep("Wi‑Fi", "SetSwitch true"),
]
        ↓
Agent.RunAsync(goal, plan) → 逐步骤执行
```

**Runtime 不知道 "确保 WiFi 已开启" 的含义。** 它只知道执行 4 个步骤：
1. Tap "Network&internet"
2. Tap "Internet"
3. Tap "Wi‑Fi"
4. SetSwitch "Wi‑Fi" true

Goal evidence 是 `Wi‑Fi element with SwitchState==true` 的存在性检查——这也是 caller lambda。

### 4.2 缺失的语义层

```
当前:
  Goal → Tap("Wi‑Fi")

缺少:
  Goal → Capability("ToggleWiFi") → SemanticObject("Wi‑Fi Switch") → Action(SetDesiredState, ON)
```

### 4.3 具体 Gap

| 业务目标 | 当前能否表达？ | 缺什么？ |
|---|---|---|
| "开启 Wi‑Fi" | **INDIRECTLY** — 通过 caller 编译为 Plan | Runtime 不理解意图；caller 必须预知 exact plan steps |
| "关闭蓝牙" | **INDIRECTLY** — 同上 | 同上 |
| "切换语言" | **NO** — 无对应 fixture | 无页面知识、无元素知识、无 navigation graph |
| "登录账号" | **NO** — 涉及输入文本、多步表单 | 无 input action、无 form semantics、无 credential model |

**根本 Gap: Runtime 没有 Business Intent → Semantic Action 的编译能力。** 所有编译发生在调用侧。Runtime 是 plan executor，不是 intent interpreter。

---

## 5. Container / Agent 权责分析

### 5.1 按 I-1 ~ I-14 验证

| Invariant | 状态 | 证据 |
|---|---|---|
| **I-1** Agent→Container→Traversal→Environment | **RESPECTED** | 依赖方向正确；Container 不引用 Agent，Traversal 不引用 Container |
| **I-2** 一个 mutable state 一个 owner | **RESPECTED** | Container 拥有 _observation/_localPageBeliefState；Agent 拥有 _belief/_state/_trace |
| **I-3** 一个 decision 一个 authority | **RESPECTED** | Completion=Agent(I-10)；Grounding=Traversal；Local Continuity=Container |
| **I-4** Observation 是 evidence | **RESPECTED** | Observation doc 明确标注；所有 semantic 路径通过 evidence 层 |
| **I-5** Plan 是 hypothesis | **PARTIALLY** | Plan 可 fail；无"偏离 plan 自行探索"的机制 |
| **I-6** Fingerprint 是 evidence | **RESPECTED** | Observation 无 Fingerprint 字段（显式 defer） |
| **I-7** FSM 不做 intelligence | **RESPECTED** | 无 FSM；Agent 用 RunState enum 做简单状态迁移 |
| **I-8** Lower scope escalate up | **RESPECTED** | Container→Agent: Trap；不反向偷取 authority |
| **I-9** Recovery 是闭环 | **RESPECTED** | act→observe→verify→reconcile；非单个 PressBack |
| **I-10** Completion 由 Goal Evidence | **RESPECTED** | 仅 Satisfied=true 触发 Completed |
| **I-11** 不继承旧控制结构 | **RESPECTED** | Guard 1/2 机械保证；零旧 namespace 引用 |
| **I-12** YAGNI | **RESPECTED** | 无提前抽象；ElementAnalysis/Memory 未实现 |
| **I-13** 无 God Context | **RESPECTED** | Observation/WorldBelief/RuntimeState/Memory 分离 |
| **I-14** AI 可插拔 | **RESPECTED** | 无 VLM/LLM 依赖；确定性 core 独立运行 |

### 5.2 权责分配

| 职责 | Container | Agent |
|---|---|---|
| **Local semantic state** | **OWNS** (_localPageBeliefState, _observation) | 不拥有 |
| **Page belief** | **OWNS** (EvaluatePageBelief → fusion → store) | **CONSUMES** (adjudication 时读取) |
| **Element relationship** | **不拥有** — 无 element model | **不拥有** |
| **Global reasoning** | 不拥有 | **OWNS** (drift, recovery, completion, container switching) |
| **Arbitration** | 不拥有 | **OWNS** (Trap 接收, CreateContainer/Bind, rebind) |
| **Recovery** | 不拥有 | **OWNS** (RecoverFromDriftAsync, recovery decision) |
| **Ambiguity decision** | **ESCALATES** (Trap when UNRESOLVED/CONTRADICTED) | **ADJUDICATES** |
| **Run-level state** | 不拥有 | **OWNS** (_belief, _state, _trace, _branchProgress) |

### 5.3 权责冲突点

| 冲突 | 严重度 | 说明 |
|---|---|---|
| **Container name vs belief** | MEDIUM | Container._semanticPageName 是构造时不可变 string（来自 old resolver）。当 PageAnalysis evidence 与该 name 冲突时，Container 名为 "WifiSub" 但 belief 为 CONTRADICTED。Agent 必须 CreateContainer+Bind（discard+rebuild）来修正——无 in-place revision。 |
| **identityRule 仍是 caller lambda** | LOW | Container.IsStillMine 仍依赖注入的 identityRule lambda。LOCAL_IDENTITY evidence 的来源是 caller，不是 Runtime。 |
| **WorldBelief 重叠** | LOW | Agent._belief.SemanticPage（来自 old resolver）与 Container._semanticPageName 是重叠的 page-identity string，由两个 owner 持有。目前通过比较相等性来协调，不是真正的单一 owner。 |

---

## 6. 当前数据模型地图

| Object | Key Fields | Purpose | Semantic? | Mutable? | Owner |
|---|---|---|---|---|---|
| **Observation** | Elements, ForegroundApplication, SequenceNumber | 外部世界证据快照 | NO — raw evidence | NO (record) | 无（不可变） |
| **ObservedElement** | Text, SwitchState?, Index, Bounds?, PerceptionType? | 单个 UI 元素证据 | NO — raw evidence | NO (record) | 无（不可变） |
| **ElementBounds** | X1, Y1, X2, Y2, CenterX, CenterY, Width, Height, IsValid | 归一化空间边界 | NO — spatial evidence | NO (record) | 无（不可变） |
| **SemanticEvidence** | Source, Claim, Stance, Reason? | 一个 source 对 claim 的立场 | **YES** — 定性语义证据 | NO (record) | 无（不可变） |
| **SemanticBeliefState** | Supported / Unresolved / Contradicted | 融合后的信念状态 | **YES** — 语义结论 | NO (enum) | 无（不可变） |
| **PageAnalysisCriteria** | ExpectedForegroundApplication, PageAnchors, PageNegativeAnchors?, PageSwitchStateAnchors? | Caller 提供的识别知识 | **YES** — 语义知识（不是 verdict） | NO (record) | Caller |
| **Container** | _semanticPageName, _observation, _executedSteps, _viewportExplorationObservations, _isLocalComplete, _localPageBeliefState | 页面级局部状态 | **PARTIAL** — state owner | **YES** | Container (I-2) |
| **WorldBelief** | SemanticPage, Confidence, Evidence, SourceObservationSequence | Agent 对世界的当前判断 | **YES** — run-level belief | NO (record) | Agent (I-2) |
| **DeviceAction.Tap** | TargetElementIndex, TargetBounds? | 点击动作 | NO — execution primitive | NO (record) | 无（不可变） |
| **DeviceAction.SetSwitch** | TargetElementIndex, TargetState, TargetBounds? | 开关动作 | NO — execution primitive | NO (record) | 无（不可变） |
| **Goal** | EvidenceEvaluator, CandidateAuthorizationEvaluator?, ViewportExplorationEvaluator?, BranchInventoryEvaluator?, CategoryClassifier? | 目标定义（全部 caller lambda） | **YES** — 但 caller 定义 | NO (record) | Caller |
| **Plan / PlanStep** | TargetDescription, ActionDescription, TargetGroundingCriterion?, BranchEffectEvidenceEvaluator? | 执行计划（caller 编译产物） | NO — execution contract | NO (record) | Caller |
| **TraversalJournalEntry** | StepId, SelectedElementIndex, DispatchedAction, PostActionObservation, Result, RetryCount | 单步执行记录 | NO — trace evidence | NO (record) | Traversal |
| **Trap** | TrapKind, TrapScope, ExpectedSequence, ObservedSequence, Source, Evidence, RelevantAction | Container→Agent 升级信号 | **YES** — 语义升级 | NO (record) | Container (创建) / Agent (消费) |
| **IntentSemanticEnvelope** | Intent, Goal, Representation (ClosedWorldConcrete / OpenWorldTypeLevel) | Caller 侧 intent→plan 编译结果 | **YES** — 但 caller 侧 | NO (record) | Caller |
| **TypeLevelDispatchPolicy** | CategoryHandling (ImmutableDictionary) | Category→Handling 映射 | **YES** — 调度策略 | NO (record) | Caller |
| **RecoveryAnchor** | ExpectedSemanticEntry, ApplicationIdentity, RestoreRecipe?, VerificationCriteria?, Depth? | 恢复锚点 | **YES** — 恢复语义 | NO (record) | Agent |
| **RunState** | Idle / Initializing / Ready / Running / Completed / Failed / Recovery | Agent 生命周期状态 | NO — execution state | **YES** | Agent (I-2) |

---

## 7. 当前真实能力验证链

### 7.1 Settings → Network → Internet → Wi‑Fi 案例

| 步骤 | 当前能做到？ | Reality Level | 说明 |
|---|---|---|---|
| **Launch Settings app** | **YES** | REALITY-SEEDED | ForegroundApplication 检查；ScriptedEnvironment 模拟 launch |
| **Observe SettingsRoot** (16 elements) | **YES** | REALITY (A3 EP-04 data) | 元素数据来自真实录制；duplicate labels + empty text + subtitle phantom 均保留 |
| **Tap "Network&internet"** | **YES** | REALITY-SEEDED | Text 匹配成功（有 duplicate，取首个）；transition 转场到 NetworkInternet |
| **Verify NetworkInternet reached** | **PARTIAL** | REALITY-SEEDED | resolveSemanticPage 检查 anchor text；无 transition evidence 验证 |
| **Tap "Internet"** | **YES** | REALITY-SEEDED | Text 匹配成功（有 duplicate，取首个） |
| **Verify InternetPage reached** | **PARTIAL** | REALITY-SEEDED | 同上；无 post-action spatial verification |
| **Tap "Wi‑Fi" to enter WifiPage** | **YES** (synthetic WifiPage) | **SYNTHETIC** | 真实录制无 WifiPage；合成 fixture 构建 |
| **SetSwitch "Wi‑Fi" ON** | **YES** (synthetic) | **SYNTHETIC** | SwitchState 合成；无真实设备 OFF→ON pair |
| **Verify Goal (Wi‑Fi ON)** | **YES** | REALITY-SEEDED | Goal.EvidenceEvaluator 检查 SwitchState==true |

### 7.2 假设清单

| 假设 | 真实性 |
|---|---|
| WifiPage 存在且与 InternetPage 不同 | **SYNTHETIC** — 真实录制无此页 |
| Wi‑Fi switch 文本 == "Wi‑Fi" | **SYNTHETIC** — B1 真实设备为 "WLAN"；switch 文本为空 |
| SwitchState 从感知获得 | **SYNTHETIC** — 真实感知不产生 SwitchState；测试 fixture 硬编码 |
| 页面 transition 由 text-anchor 验证 | **PARTIAL** — 无独立 transition evidence channel |
| "Network&internet" 仅一个可交互候选 | **ASSUMPTION** — 真实数据有 duplicate；取首个（可能错） |
| 元素 Index 稳定跨观测 | **ASSUMPTION** — 动态列表 / 异步加载会改变顺序 |

### 7.3 Reality Classification Summary

| 测试类别 | 数量（估计） | 示例 |
|---|---|---|
| **REALITY** | **0%** — 无纯 REALITY 测试（无完整 trace replay、无真实设备端到端运行） | — |
| **REALITY-SEEDED** | ~15% (5 files) | RealitySeededWifiScenarioTests, PageAnalysisObservationDerivedTests, MinimumSpatialObservationContractTests, PerceptionEvidencePreservationTests, EnvironmentSpatialActionMappingTests |
| **REALITY-SEEDED** | ~40% | RealitySeededWifiScenarioTests, PageAnalysisObservationDerivedTests（使用真实元素数据但合成页面配置） |
| **SYNTHETIC** | ~45% | ScriptedEnvironmentVariants, OpenWorldTypeDirectedScenarioTests（完全合成） |

---

## 8. 当前最大的架构风险排序

### P0: NO BUSINESS INTENT INTERPRETATION

**风险:** Runtime 只能执行预编译的 Plan，不能理解 "开启 Wi‑Fi" 这类业务目标。所有语义编译（intent → plan）在调用侧完成。如果继续在现有模型上叠加功能而不引入 intent interpretation 层，Runtime 将始终是一个 administrative shell，永远无法自主决策"达到目标需要什么步骤"。

**如果继续开发而不解决:** 每个新业务目标都需要调用侧编写完整的 Plan + 注入 lambda。Runtime 智能度不随功能增加而增长。

### P1: NO SEMANTIC ELEMENT MODEL

**风险:** 当前 grounding 仅靠 Text 匹配。空文本 toggle、subtitle phantom、duplicate labels 都无法正确处理。"Wi‑Fi" entry 和 "Wi‑Fi" switch 在 Runtime 看来是同一个 Text 的两个 Index——没有类型区分、没有交互能力区分、没有空间关系。

**如果继续开发而不解决:** SetSwitch 只能作用于有 SwitchState 且有匹配 Text 的合成元素。真实设备的 switch 无法定位（空文本 + 右侧位置 + 无类型标签）。TypeLevelDispatch 将始终依赖 caller CategoryClassifier lambda。

### P2: PAGE IDENTITY 仍是 STRING + CALLER LAMBDA

**风险:** `Container._semanticPageName` 是构造时不可变 string，来自 old resolver lambda。当 PageAnalysis evidence 与该 name 冲突时，Container 无法 in-place 修正身份。必须整个 Container 放弃重建（CreateContainer+Bind）。`identityRule` 仍是 caller lambda——LOCAL_IDENTITY 证据的来源是 caller，不是 Runtime 内部感知。

**如果继续开发而不解决:** Page identity 永远不会成为 evidence-backed Runtime capability。所有 page 判断仍依赖 caller 提供的 lambda。OpenWorld 子 container 创建完全依赖 old resolver 的 string verdict。

### P3: PERCEPTION ADAPTER + EVIDENCE CONSUMPTION 双重缺失

**风险 A — 真实设备 perception adapter 不存在:** 所有测试通过 ScriptedEnvironment（确定性 fake）。真实设备的 observation 构造、Bounds 填充、PerceptionType 传递、SwitchState 检测——这些生产路径全部不存在。

**风险 B — 已购买 evidence 无生产消费者:** `Container.LocalPageBeliefState` 是 **write-only**（Agent 调用 EvaluatePageBelief 但从不在控制流中读取）。`PerceptionType` 有 **zero production consumers**（Traversal.Select 只用 Text+SwitchState）。已购买的能力在模型层存在但在决策层未被使用。

**如果继续开发而不解决:** 所有 spatial + type evidence 能力停留在模型层和 observability。真实设备上 Bounds=null, PerceptionType=null, SwitchState=null → 回到最原始的 Text-only grounding。即使真实 adapter 到位，PerceptionType 和 LocalPageBeliefState 仍然不会影响任何决策。

---

## 9. 给未来演进留下的关键问题

1. **Business Intent 如何进入 Runtime？** 当前 intent→plan 编译在调用侧。Runtime 需要什么 interface 才能接收 "开启 Wi‑Fi" 并自主推导 execution strategy？

2. **SemanticElement 是否需要独立模型？** 还是 ObservedElement + PerceptionType + Bounds 已经足够作为 ElementAnalysis 的输入？ElementAnalysis 是应该产生新的 SemanticElement 类型，还是在现有 evidence 上叠加 interpretation？

3. **Page 是否只是 Container State？** 当前 Page identity = `string _semanticPageName`。是否需要独立的 SemanticPage 模型（page identity + page context + page continuity rules），还是 Container 本身就足够？

4. **Container 是否应该持有 Semantic World Model？** 当前 Container 持有 observation + belief + steps。如果未来有 semantic element 和 page transition graph，这些应该属于 Container、Agent、还是新的 World Model owner？

5. **Action 是否应该从 primitive 升级为 semantic action？** 当前 `DeviceAction.Tap(Index)` 是 execution primitive。"Tap Wi‑Fi entry to navigate to WifiPage" 的语义全部在调用侧。Runtime 是否需要理解 action semantics（desired effect, target capability, expected transition）？

6. **Evidence Fusion 是否应该 claim-aware？** 当前 `FuseBelief` 将所有 evidence 视为同一隐式 claim。PageAnalysis 产生多 claim evidence（"page is InternetPage" vs "page is WifiPage"），跨 claim 的 Contradicts 会污染不相关 claim 的 fusion。是否需要 claim-aware fusion？

7. **Container identity revision 是否需要 in-place 路径？** 当前修正 Container page identity 的唯一方式是 CreateContainer+Bind（discard+rebuild），丢失所有 local progress。是否需要 "Agent adjudication → Container applies revision" 的 in-place 路径？

8. **真实设备 perception adapter 何时购买？** 所有 spatial/type evidence 能力在真实设备路径上无法填充。生产 perception→Observation 的 adapter 是实现这些能力的前提。优先级如何？

9. **OpenWorld page discovery 如何与 PageAnalysis 集成？** 当前 OpenWorld 子 container 创建依赖 old resolver string。PageAnalysis 已购买但未接入 OpenWorld 路径。子页面发现应该使用 PageAnalysis evidence 还是 old resolver？

10. **SwitchState detection 是否必须在 ElementAnalysis 之前解决？** 当前 SwitchState 在真实设备上不存在。如果先做 ElementAnalysis（association, grouping, type interpretation）但没有 SwitchState，SetSwitch 仍然无法在真实设备上工作。顺序是否应该反过来？

---

## Summary

**当前 Runtime 是一个具有 evidence-backed belief 的 plan executor，但还不是一个 semantic agent。**

已具备:
- ✅ 完整的 execution pipeline（Agent→Container→Traversal→Environment）
- ✅ Multi-source qualitative evidence（SemanticEvidence + Belief Fusion）
- ✅ Observation-scoped page analysis（PageAnalysis, TEXT_ATTRIBUTE level）
- ✅ Spatial evidence contract（ElementBounds 归一化 [0,1]×[0,1]）
- ✅ Raw perception type preservation（PerceptionType）
- ✅ Container-scoped local belief state
- ✅ Agent adjudication authority（Trap + CreateContainer/Bind）
- ✅ Type-directed dispatch（caller-injected, 2 categories）
- ✅ Recovery（drift→trap→recover 闭环）
- ✅ 14/14 Architecture Invariants respected

仍然缺失:
- ❌ Business intent interpretation（intent→semantic action 编译）
- ❌ Semantic element model（identity, category, capability, interaction surface）
- ❌ Semantic page model（page identity beyond string name）
- ❌ Real device perception adapter（Bounds/PerceptionType/SwitchState 无法在生产路径填充）
- ❌ Claim-aware evidence fusion
- ❌ In-place Container identity revision
- ❌ ElementAnalysis（association, grouping, duplicate disambiguation）
- ❌ SwitchState detection from real perception

**574 tests, 0 failures, 14/14 invariants respected.**

---

*No production changes. No new abstractions. No implementation plan. This is a common fact baseline.*

STOP.
