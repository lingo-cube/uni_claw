# WI-SIM006-001 — SIM-006 首批 8 场景只读审计报告

- WorkItem：`workitems/WI-SIM006-001.json`（审计执行时 status=in_progress；派发方复核结果后置 done——2026-10-05 修订注记，终态以 WorkItem 文件为准）
- 审计性质：**只读静态审计**。不执行任何构建/测试；一切执行性结论仅引用**既有**证据（TRX 时间戳 2026-10-04 23:00:09，见 §5），**不构成新鲜执行 PASS**。
- 基线校准：`changes/SIM-006/state.md`（Current facts L40–51、baseline matrix L71–82）、`docs/architecture/simulation-baseline-v0.1.md`（C1–C9）、`docs/architecture/simulation-baseline-v0.2-c7-amendment.md`（C7 双面标注）。
- 仓库状态：分支 uni-harness，HEAD `14597766`（docs(sim-006): persist local test engineering spec）；`git status --porcelain` 见文末。

## 0. 共同机制（8 个场景共用，先立锚点）

### 0.1 scenario → 测试 FQN 的映射机制（trait 如何被发现）

- 映射唯一声明点 = 测试代码里的 `[Trait("Scenario", "SCN-…")]`；执法者 `ScenarioCertificationTests`（scenario-coverage.py:10–11 注释指明；工具本身从**二进制**发现 trait）。
- 工具链（只读阅读 `tools/scenario-coverage.py`）：
  - `scenario_test_map`：`dotnet test --list-tests --filter Scenario=<id>` 从二进制枚举 FQN（scenario-coverage.py:109–139）。
  - `parse_trx`：TRX → `{测试 FQN: outcome}`（scenario-coverage.py:96–106）。
  - 两者在工具内按 FQN join，TRX 派生 status 必须等于 JSON 声明 status（scenario-coverage.py:234–255）。
- 本次审计**未执行**该工具（forbidden）；FQN 与 TRX 结果由本报告直接用 Python ElementTree 只读解析既有 TRX 得到（见 §5 命令清单）。

### 0.2 golden-bundle 场景的标准调用链（7/8 场景）

```
测试方法 [Trait("Scenario","SCN-…")]
  → ScenarioLibrary.Load(scenarioId)            tests/…/ScenarioLibrary.cs:58-79
      → ScenarioExpectations.Verify             ScenarioExpectations.cs:45-121
        （expectationsDigest/executionDigest 双自洽校验 :104-118；schemaVersion==2 :99）
      → execution.kind != "golden-bundle" → fail-closed 拒绝   ScenarioLibrary.cs:63-65
      → Carriers[execution.carrier] 工厂        ScenarioLibrary.cs:27-51,68-70
      → ScenarioCertification.TryRunOptions     ScenarioLibrary.cs:71-73
      → Expected = ScenarioExpectations.Load    ScenarioLibrary.cs:77（GoldenScenarioBundles 各工厂内部 :74/:111 等）
  → ScenarioRunner.Run(bundle)                  ScenarioRunner.cs:28-79
      → SimulationHost.Compose                  ScenarioRunner.cs:32 → SimulationHost.cs:156-252
      → host.KernelCore.AdmitContract           ScenarioRunner.cs:36
      → host.Driver.Activate                    ScenarioRunner.cs:42
      → （options.DuplicateActivation 时 re-admit+re-activate，同一 RunId）ScenarioRunner.cs:49-61
      → host.DriveOnce → Driver.Drive           ScenarioRunner.cs:63 → SimulationHost.cs:259-266
      → AgentDiscipline（seam 纪律聚合）        ScenarioRunner.cs:67,107-115
      → 评估：new UniAgent(BuildGoal(bundle.Goal)).Evaluate(result.Outcome)   ScenarioRunner.cs:122-124
      → SemanticDigest + BuildReport + AcceptancePassed       ScenarioRunner.cs:126-170,188-201
  → 测试断言 report / host.Facts
```

- `AcceptancePassed` 六字段判定：status/classification/effects/agentConsultations/unconsumedStimuli/goalSatisfaction 全部对照 certified 投影 `bundle.Expected`（ScenarioRunner.cs:188–201；期望唯一来源 = scenarios/SCN-*.json，ScenarioExpectations.cs:10–27）。

### 0.3 两个 realization 面的组合锚点（C7 v0.2）

- decision=double 锚点：`SimulationHost.Compose` 默认构造 `ScriptedUniAgent(bundle.PhaseScript)` 并经 `ConsultationJournal` 进 driver ConsultAgent 缝（SimulationHost.cs:221–232）；PERC 专有 host 同样构造 ScriptedUniAgent（AsyncPerceptionTracer.cs:579–581）。
- evaluation=real 锚点：`ScenarioRunner.BuildReport` 经 `new UniAgent(BuildGoal(bundle.Goal)).Evaluate(...)`（ScenarioRunner.cs:122–124）；`UniAgent` 为真实产品类型 `src/UniClaw.Agent/UniAgent.cs:16`（`public sealed class UniAgent`，`Evaluate` :38）。
- 执法：`ScenarioRealizationAnnotationTests`（ScenarioRealizationAnnotationTests.cs:41–91）——构成锚点常量 :19–20，逐场景 JSON 比对 :52–91。**注意其锚点只针对 SimulationHost/ScenarioRunner 构成**（:47–49 用 WifiToggleOffToOn bundle 验证），不覆盖 PERC 专有 host（见 §3 PERC 缺口）。

### 0.4 配置与资产来源

- golden bundle 资产：运行时读取 `platforms/perception/evaluation/assets/captures/golden-run-v1/scenario-manifest.json`（GoldenScenarioBundles.cs:47–50；BundleRoot 常量 SimContract.cs:24），asset contentHash 重算双核对（GoldenScenarioBundles.cs:58–78）；device profile = 同目录 `device-profile.json`（:121–122）。
- 期望 truth：`scenarios/SCN-*.json`（ScenarioExpectations.cs:47–49）。
- `testsets/android-settings/manifest.json`：**未被这 8 个场景的任何测试代码消费**（tests/UniClaw.Simulation.Tests/*.cs 中无 "testsets" 引用；grep 证据见 §5）。它是 Host Catalog 就绪前的声明式任务来源（state.md L50、L113 定位一致）。
- 认证 digest：`runtimeSourceHash` 只覆盖 `src/UniClaw.Kernel` + `src/UniClaw.Agent` 源（scenario_certify.py:16,90–107）；8 个场景 JSON 的该字段全部相同（`50797535…`），即同一 Kernel/Agent 源哈希。Host/工具/profile/testset 不在哈希覆盖内（与 state.md L168 自述一致）。

---

## 1. SCN-SMOKE-001 与 SCN-WIFI-001（共用 carrier 的专有核对项）

**(a) 真实载体**
- SMOKE：`SimulationHostSmokeTests.S1_HappyPath_EndToEnd_Accepts`，tests/UniClaw.Simulation.Tests/SimulationHostSmokeTests.cs:13–27。
- WIFI-001：`DeterministicScenarioTests.S1_OffToOn_OneAuthorizedEffect_VerifiedTerminalOutcome`，tests/UniClaw.Simulation.Tests/DeterministicScenarioTests.cs:29–66。

**(b) 调用链**：均为 §0.2 标准链（SMOKE 入口 SimulationHostSmokeTests.cs:16–17；WIFI-001 入口 DeterministicScenarioTests.cs:32,36）。

**(c) 主要断言差异（共用 carrier 专有核对项）**
- 共用点：同一 carrier `wifi-off-to-on` → `GoldenScenarioBundles.WifiToggleOffToOn`（ScenarioLibrary.cs:30 → GoldenScenarioBundles.cs:42–85）；两 JSON 的 expectations 与全部三个 certification digest 逐字节相同（SCN-SMOKE-001.json / SCN-WIFI-001.json 的 expectationsDigest `7de541f2…`、executionDigest `981ff6da…`、runtimeSourceHash `50797535…`）。
- SMOKE 断言（Type-B 3 条）：`AcceptancePassed`、`AgentViolations` 空、`Metrics.ModelCalls` 以 "N/A" 开头（SimulationHostSmokeTests.cs:20–26；scripted double 无 live model 显式 N/A 而非伪造 0）。
- WIFI-001 断言（超集，跑两次独立执行）：AcceptancePassed（:40–41）；decision id 协议格式 `decision-{RunId 末12}-1`（:45–49）；stimulus 消费顺序 `obs-1-initial, obs-2-post`（:52）；GoalEvaluation 与 Outcome 同 RunId（:57–58）；driver 自驱证明 `host.Facts.IsRunTerminal` + 单 receipt（:62–64）。
- 结论（事实）：**SMOKE 是 WIFI-001 的严格弱化重复**——期望绑定相同，仅断言面不同；不构成独立新能力（与 state.md L75 表述一致）。

**(d) realization**：JSON 均为 decision=double / evaluation=real；锚点见 §0.3。

**(e) execution**：kind=golden-bundle，carrier=wifi-off-to-on；资产物理来源 `platforms/perception/evaluation/assets/captures/golden-run-v1/`（manifest + frames + perception captures，§0.4）。

**(f) FQN/TRX**
- SMOKE：`UniClaw.Simulation.Tests.SimulationHostSmokeTests.S1_HappyPath_EndToEnd_Accepts`
- WIFI-001：`UniClaw.Simulation.Tests.DeterministicScenarioTests.S1_OffToOn_OneAuthorizedEffect_VerifiedTerminalOutcome`
- 两者均在 TestResults/coverage.trx（2026-10-04 23:00:09，188 个结果）中，outcome=Passed。**这是既有 TRX，不是本次执行证据**；按 coverage 工具自身的 freshness 规则（scenario-coverage.py:153–166），其后任何源码/JSON 修改都会使其陈旧。

**(g) 配置来源**：scenarios/SCN-SMOKE-001.json + SCN-WIFI-001.json（certified 期望投影）；golden-run-v1 manifest 资产；无 fixture/profile/testset 参与。RunOptions 默认（无 options 块）。

**(h) 第一处缺口**：none（绑定链完整：digest 自洽由 ScenarioExpectations.cs:104–118 在每次 Load 时强制；断言由 ScenarioRunner.cs:188–201 强制）。证据：两 JSON 的 executionDigest 与 `cert-exec-v1` canonical 渲染一致性由 fail-closed 校验承载——若 carrier/期望被改动未重认证，测试入口在 Load 即抛异常（ScenarioExpectations.cs:106–118）。唯一保留事项：TRX 为 2026-10-04 的旧执行，非新鲜证据（本审计不做 PASS 声明）。

---

## 2. SCN-WIFI-002

**(a) 载体**：`DeterministicScenarioTests.S2_AlreadyOn_ZeroEffect_TerminalCompletion`，DeterministicScenarioTests.cs:72–86。
**(b) 调用链**：§0.2 标准链（入口 :75–76）。carrier `wifi-already-on` → `GoldenScenarioBundles.AlreadyOnZeroEffect`（ScenarioLibrary.cs:31 → GoldenScenarioBundles.cs:89–122），单初始帧 + NoAction turn（:107–110）。
**(c) 主要断言**：expectations `effects=0, agentConsultations=1, status=Completed, classification=Completion, goalSatisfaction=Satisfied`（SCN-WIFI-002.json）经 AcceptancePassed 承载（DeterministicScenarioTests.cs:80–81）；Type-B：host 侧计数与 report 一致 + run terminal（:84–85）。零 dispatch 是**期望六字段之一**，非仅靠零动作计数单独判成功（与 state.md L77 边界一致）。
**(d)** decision=double / evaluation=real；锚点 §0.3。
**(e)** kind=golden-bundle，carrier=wifi-already-on；同 golden-run-v1 资产。
**(f)** FQN `UniClaw.Simulation.Tests.DeterministicScenarioTests.S2_AlreadyOn_ZeroEffect_TerminalCompletion`；coverage.trx 中 Passed（2026-10-04，旧证据）。
**(g)** scenarios/SCN-WIFI-002.json certified 期望（expectationsDigest `e9f7b914…`，独立于 SMOKE/WIFI-001）+ golden-run-v1 case-a-before 帧（GoldenScenarioBundles.cs:102–104）。
**(h) 第一处缺口**：none（同 §1(h) 绑定证据）。

---

## 3. SCN-PERC-001（execution.kind=none 专有核对项）

**(a) 载体**：`AsyncPerceptionScenarioTests.S1_FastMissesTarget_TargetedSlowRecovers_OneEffectOnRealTarget`，tests/UniClaw.Simulation.Tests/AsyncPerceptionScenarioTests.cs:158–248。
**(b) 专有 harness 链路（不经 ScenarioRunner/ScenarioLibrary）**：
```
测试方法（AsyncPerceptionScenarioTests.cs:159）
  → AsyncPerceptionTruth.Load（type-truth.json + type-dual-local.json）  AsyncPerceptionFixtures.cs:50-53,66-77
  → 构造 ScriptedOperation 序列 + AsyncScenario（:178-198）
  → Activate(scenario) → new AsyncPerceptionHost(scenario)              :89-95 → AsyncPerceptionTracer.cs:559-593
      → AdmitContract（AsyncPerceptionScenarioTests.cs:92）→ Driver.Activate（:93）
  → 多次 host.Clock.AdvanceTo + host.DriveOnce（:201-213）
      → Driver.Drive（AsyncPerceptionTracer.cs:596-603）
  → 断言读 host.Feed.Operations / WorldCore / KernelCore / EffectBoundaryCore / LedgerCore
```
- 该 host 组合（AsyncPerceptionTracer.cs:527–611）：同一批 L2 真件（UniKernel/KernelRunDriver/EvidenceLedger/WorldModel/ControlLoop/RuntimeAssurance/EffectBoundary），缝为 AsyncObservationFeed + ScriptedUniAgent + DeterministicEffectDriver + SeedingAssociationStrategy + MenuFrameObservationStrategy + SatisfyingFreshness；Trace=DisabledRunTrace（:584）。
- `ScenarioLibrary.Load("SCN-PERC-001")` 会 fail-closed 抛异常（kind=none 无 bundle carrier，ScenarioLibrary.cs:63–65）——专有载体与 executable expectation projection 保证**结构性脱钩**，这是设计内行为。

**(c) 主要断言**（期望的外部行为，全部是测试代码字面量，非 certified 投影）：
- 虚拟时间轴上合法等待（T0、T0+2 均 WaitingForInput，:201–207）；targeted slow 到达后覆盖并集完成 → act → 等 post（:209–210）；post 到达 → Completed（:212–214）；operation DeliveredResultIds = fast+slow（:216–218）。
- omission ≠ absence：belief 无「目标不存在」负声明；slow 找回后 UniqueCandidate 且 locator 中心 = 真值 cy（:222–228）。
- 恰 1 Effect 且接地到真实行（`host.EffectDeliveries == 1`，binding locator 断言，:234–237）；零 fast/slow producer 泄漏（:240–241）；`TerminalClassification.Completion`（:243）。
- **与 JSON expectations 的对应：无对应**。SCN-PERC-001.json 的 expectations（status=Completed/effects=1/agentConsultations=2/unconsumedStimuli=0/goalSatisfaction=Satisfied）没有被该测试的任何断言逐项消费——测试不经 ScenarioRunner，无 AcceptancePassed 六字段核对。

**(d) realization 与标注的一致性**：
- JSON：decision=double / evaluation=real。
- decision=double **成立**：AsyncPerceptionHost 构造 ScriptedUniAgent 经 ConsultationJournal 进缝（AsyncPerceptionTracer.cs:579–581）。
- evaluation=real **未由该载体建立**：PERC harness 全程不构造 `UniAgent`、不产生 GoalEvaluation（AsyncPerceptionTracer.cs:559–611 无任何评估构造；测试只断言 TerminalClassification.Completion :243）。`goalEvaluationRealization=real` 的执法测试（ScenarioRealizationAnnotationTests.cs:78–86）只比对 JSON 值与「ScenarioRunner 构成」常量，不检查 kind=none 场景的专有 harness 是否真的走 UniAgent。

**(e) execution**：kind=none，无 carrier；资产物理来源 = `platforms/perception/evaluation/reports/fsv001/screenvlm-probe/vlm-compare/type-truth.json` 与 `…/dual/type-dual-local.json`（AsyncPerceptionFixtures.cs:50–53），加上测试内构造的 typed elements/scheduled results（AsyncPerceptionScenarioTests.cs:172–190）。不消费 golden-run-v1 manifest。

**(f)** FQN `UniClaw.Simulation.Tests.AsyncPerceptionScenarioTests.S1_FastMissesTarget_TargetedSlowRecovers_OneEffectOnRealTarget`；coverage.trx 中 Passed（2026-10-04，旧证据）。
**(g)** 配置来源：type-truth.json（Human-reviewed ground truth）+ type-dual-local.json（历史预测）；无 bundle、无 manifest、无 profile/testset。
**(h) 第一处缺口**：**certified expectations 与 goalEvaluationRealization=real 两项声明保证在专有载体入口处即未建立**——`AsyncPerceptionScenarioTests.S1` 从不调用 `ScenarioExpectations.Verify/Load("SCN-PERC-001")`，也不构造 GoalEvaluation；SCN-PERC-001.json 的 expectationsDigest `7de541f2…` 与 WIFI-001 相同（同六字段），但对 PERC 无任何运行时绑定消费（对比：golden-bundle 场景在 ScenarioLibrary.Load → ScenarioExpectations.Verify 强制）。次要：agentConsultations=2 的期望无对应断言（测试断言的是 consultation 内容而非计数）。

---

## 4. SCN-BARRIER-001

**(a) 载体**：`TwoStepBarrierTests.TwoStep_EvidenceBetweenSteps_SecondEffectAllowed_TerminalCompletion`，tests/UniClaw.Simulation.Tests/TwoStepBarrierTests.cs:25–45。
**(b) 调用链**：§0.2 标准链（入口 :28–29）。carrier `wifi-two-step-toggle-menu` → `GoldenScenarioBundles.TwoStepToggleThenMenuItem`（ScenarioLibrary.cs:34 → GoldenScenarioBundles.cs:212）。
**(c) 主要断言**：expectations `effects=2, agentConsultations=2, status=Completed, classification=Completion, goalSatisfaction=Satisfied`（SCN-BARRIER-001.json）经 AcceptancePassed（TwoStepBarrierTests.cs:33）；Type-B：两 effect 各一 receipt、run terminal、零 violations（:37–40）；消费顺序 `obs-1-initial, obs-2-post, obs-3-post`——即 obs-2-post 在 step2 之前被消费（不变量 43 的可观察面，:44）。
- state.md L79 要求「同时关联既有负向契约/场景」：负向面在**同文件**既有——SCN-BARRIER-002（缺中间证据→第二次 effect 被阻，:53–85）与 SCN-BARRIER-003（post 与 DesiredState 矛盾→fail closed，:99–122）。映射存在（事实）；是否足以证明阻断有效由 Leader 裁决，本报告不代判。
**(d)** decision=double / evaluation=real；锚点 §0.3。
**(e)** kind=golden-bundle，carrier=wifi-two-step-toggle-menu；golden-run-v1 资产（case-b-on / case-c frames 复用，GoldenScenarioBundles.cs:208–235 区域）。
**(f)** FQN `UniClaw.Simulation.Tests.TwoStepBarrierTests.TwoStep_EvidenceBetweenSteps_SecondEffectAllowed_TerminalCompletion`；coverage.trx Passed（旧证据）。
**(g)** scenarios/SCN-BARRIER-001.json certified 期望（expectationsDigest `e70845d5…`）+ golden-run-v1 manifest 资产。
**(h) 第一处缺口**：none（绑定链证据同 §1(h)）。

---

## 5. SCN-POLICY-006 / SCN-POLICY-007 / SCN-POLICY-009

共同载体类：`PolicyScenarioTests`（tests/UniClaw.Simulation.Tests/PolicyScenarioTests.cs）；共同入口 `Run(scenarioId)` = §0.2 标准链（PolicyScenarioTests.cs:19–20）。类注释声明零仿真专用执行路径（:8–15，经 ScriptedUniAgent → AgentDecision.Policy → KernelRunDriver policy expansion）。

### SCN-POLICY-006
- (a) `P6_ConflictedClaim_GuardUnknown_FailClosed`，PolicyScenarioTests.cs:140–150。
- (b) 标准 chain；carrier `policy-guard-unknown` → `PolicyGuardUnknown`（ScenarioLibrary.cs:45 → GoldenScenarioBundles.cs:588–604）。conflict 注入方式 = 同一固定 scope `hvac.mode` 跨帧 cool→heat（GoldenScenarioBundles.cs:594–597），guard `ObservationUnchanged("hvac.mode",1)`（:601）。
- (c) 断言：AcceptancePassed + 零 violations（:145）；`calls[1].FailureReason == "policy:guard-unknown"`（:148）；`EffectDeliveries == 0`（:149，注释「Unknown 不降级——零 dispatch」）。与 JSON expectations（TerminalNotProven/classification=null/effects=0/consultations=2/goalSatisfaction=null）经 AcceptancePassed 对应。
- (d) double / real；锚点 §0.3。
- (e) golden-bundle / policy-guard-unknown；golden-run-v1 资产（policy frames 复用 case-b-off perception +  authored claim scopes，见 JSON security.reviewNote）。
- (f) FQN `UniClaw.Simulation.Tests.PolicyScenarioTests.P6_ConflictedClaim_GuardUnknown_FailClosed`；coverage.trx Passed（旧证据）。
- (g) scenarios/SCN-POLICY-006.json（expectationsDigest `4b272fd7…`）+ golden-run-v1。
- (h) 第一处缺口：none（绑定链证据同 §1(h)）。

### SCN-POLICY-007
- (a) `P7_VerificationFailureMidway_VoidsPolicy_ExistingTransition`，PolicyScenarioTests.cs:154–168。
- (b) 标准链；carrier `policy-verification-failure` → `PolicyVerificationFailureMidway`（ScenarioLibrary.cs:46 → GoldenScenarioBundles.cs:606–623）。post-action 帧仍 off（desired true，:611–613）。
- (c) 断言：AcceptancePassed（:159）；`calls[1].Phase == VerificationFailed` 且 FailureReason `post-action-desired-state-not-satisfied`（:162–163）——既有转移 + policy 作废；`ApplicationsUsed == 0`（:166，「已送达不等于已完成」的 policy 侧表达）；`EffectDeliveries == 1`（:167）。对应 JSON expectations（TerminalNotProven/effects=1/consultations=2）。
- (d) double / real；锚点 §0.3。
- (e) golden-bundle / policy-verification-failure；golden-run-v1 资产。
- (f) FQN `UniClaw.Simulation.Tests.PolicyScenarioTests.P7_VerificationFailureMidway_VoidsPolicy_ExistingTransition`；coverage.trx Passed（旧证据）。
- (g) scenarios/SCN-POLICY-007.json（expectationsDigest `5fc11824…`）+ golden-run-v1。
- (h) 第一处缺口：none（绑定链证据同 §1(h)）。

### SCN-POLICY-009
- (a) `P9_ForbiddenEffectClass_RejectedAtConsultation`，PolicyScenarioTests.cs:208–219。
- (b) 标准链；carrier `policy-forbidden-effect` → `PolicyForbiddenEffect`（ScenarioLibrary.cs:48 → GoldenScenarioBundles.cs:646–657）。脚本携带 `templateEffectClass: "swipe"`（:653）——超出 contract 授权 effect class（`tap` 等）。
- (c) **专有核对项**：本场景断言的是 **effect class 契约拒绝（V6d at consultation）**，不是未知 Android 菜单的语义风险识别——证据：期望 status=`AgentDecisionFailed`（JSON expectations），断言 `report.Reason == "policy:effect-class-not-allowed"`（:216，协议字符串即 V6d 契约拒绝出口）、零 effect（:217）、单咨询即 fail closed（:218）。测试与 bundle 中不含任何 Settings 菜单语义内容（policy frames 复用 hvac/temp 合成 claim，GoldenScenarioBundles.cs:648–653）。与 state.md L82/D9 表述一致。
- (d) double / real；锚点 §0.3。
- (e) golden-bundle / policy-forbidden-effect；golden-run-v1 资产。
- (f) FQN `UniClaw.Simulation.Tests.PolicyScenarioTests.P9_ForbiddenEffectClass_RejectedAtConsultation`；coverage.trx Passed（旧证据）。
- (g) scenarios/SCN-POLICY-009.json（expectationsDigest `400b1755…`）+ golden-run-v1。
- (h) 第一处缺口：none（绑定链证据同 §1(h)）。

---

## 6. 汇总表

| 场景 | 载体（类.方法） | FQN 所在 TRX | decision/eval | kind / carrier | 配置来源 | 第一处缺口 |
|---|---|---|---|---|---|---|
| SCN-SMOKE-001 | SimulationHostSmokeTests.S1_HappyPath_EndToEnd_Accepts | Passed (2026-10-04) | double / real | golden-bundle / wifi-off-to-on | SCN JSON + golden-run-v1 | none |
| SCN-WIFI-001 | DeterministicScenarioTests.S1_OffToOn_… | Passed (2026-10-04) | double / real | golden-bundle / wifi-off-to-on | SCN JSON + golden-run-v1 | none |
| SCN-WIFI-002 | DeterministicScenarioTests.S2_AlreadyOn_… | Passed (2026-10-04) | double / real | golden-bundle / wifi-already-on | SCN JSON + golden-run-v1 | none |
| SCN-PERC-001 | AsyncPerceptionScenarioTests.S1_FastMissesTarget_… | Passed (2026-10-04) | double / **real 标注未被载体建立** | none / 无 | type-truth.json + type-dual-local.json | expectations 与 evaluation=real 未被专有 harness 消费（§3h） |
| SCN-BARRIER-001 | TwoStepBarrierTests.TwoStep_EvidenceBetweenSteps_… | Passed (2026-10-04) | double / real | golden-bundle / wifi-two-step-toggle-menu | SCN JSON + golden-run-v1 | none |
| SCN-POLICY-006 | PolicyScenarioTests.P6_ConflictedClaim_… | Passed (2026-10-04) | double / real | golden-bundle / policy-guard-unknown | SCN JSON + golden-run-v1 | none |
| SCN-POLICY-007 | PolicyScenarioTests.P7_VerificationFailureMidway_… | Passed (2026-10-04) | double / real | golden-bundle / policy-verification-failure | SCN JSON + golden-run-v1 | none |
| SCN-POLICY-009 | PolicyScenarioTests.P9_ForbiddenEffectClass_… | Passed (2026-10-04) | double / real | golden-bundle / policy-forbidden-effect | SCN JSON + golden-run-v1 | none |

注：TRX 列表示既有 coverage.trx（tests/UniClaw.Simulation.Tests/TestResults/coverage.trx，Times creation=2026-10-04T23:00:09+08:00，188 个 UnitTestResult）中含该 FQN 且 outcome=Passed——**旧执行记录，非本次审计的执行证据**。

## 7. 与 changes/SIM-006/state.md 的出入（逐条，不裁决）

1. **state.md L44**：「Goal Evaluation 经真实 UniClaw.Agent.UniAgent 执行」——对 ScenarioRunner 路径（7/8 场景）成立（ScenarioRunner.cs:122–124）；但 SCN-PERC-001 走专有 AsyncPerceptionHost，**全程不执行 UniAgent.Evaluate**，而其 JSON 标注 goalEvaluationRealization=real（SCN-PERC-001.json）。state.md 该句字面仅描述 ScenarioRunner，不构成矛盾；但 realization 标注执法（ScenarioRealizationAnnotationTests.cs:78–86）对 kind=none 场景的专有 harness 无覆盖，state.md Current facts 未记录这一点。实际观察见 §3(d)。
2. **state.md L46**：「SCN-PERC-001 execution.kind=none，使用专有测试载体」——一致；补充事实：其 certified expectations 六字段（与 WIFI-001 同 digest `7de541f2…`）同样未被专有载体消费（§3(c)），即 state.md「不能宣称它已有与 golden-bundle 相同的期望绑定保证」在源码层面得到证实，且缺口范围比「期望绑定」更宽，还包括 evaluation=real 标注。
3. **state.md L75**（SMOKE 不是零动作烟测、与 WIFI-001 共用 carrier）——一致；补充精确事实：两场景 certification 三个 digest 逐字节相同，SMOKE 断言是 WIFI-001 断言的严格子集（§1(c)）。
4. **state.md L79**（BARRIER-001「需同时关联既有负向契约/场景」）——事实补充：负向场景 SCN-BARRIER-002/003 已存在于同一测试文件（TwoStepBarrierTests.cs:53,99），映射非悬空；是否满足「关联」要求由 Leader 裁决。
5. **state.md L50**（testsets 为声明式来源）——一致且补充：本批 8 场景的测试代码零消费 testsets/（grep 无引用）；`testsets/android-settings/manifest.json` 与本批场景当前无执行级关联。
6. 其余核对项（scenario-coverage 二进制 trait 机制 L45、runtimeSourceHash 只覆盖 Kernel/Agent L168、TRX/JSON status 非新鲜证据 L167）均与实际观察一致，无出入。

## 8. 候选（非事实，仅供 Leader 参考）

- 候选：为 kind=none 场景的专有 harness 增加期望消费面（例如专有 host 亦消费 ScenarioExpectations 投影）与 evaluation 面的实际构成校验——现有 ScenarioRealizationAnnotationTests 构成锚点只覆盖 ScenarioRunner 构成。
- 候选：PERC-001 的 expectations 六字段与专有载体断言的显式映射（当前 JSON expectations 对该场景是无消费者的悬空声明）。

---

## 附录 A：本次运行过的只读命令清单

1. `pwd && git log --oneline -1 && git branch --show-current && git status --porcelain | head`
2. `ls tests/UniClaw.Simulation.Tests/ && wc -l tests/UniClaw.Simulation.Tests/*.cs tools/scenario-coverage.py tools/scenario_certify.py docs/architecture/simulation-baseline-v0.1.md docs/architecture/simulation-baseline-v0.2-c7-amendment.md`
3. `cd scenarios && for f in SCN-SMOKE-001 SCN-WIFI-001 SCN-WIFI-002 SCN-PERC-001 SCN-BARRIER-001 SCN-POLICY-006 SCN-POLICY-007 SCN-POLICY-009; do echo "=== $f ==="; cat $f.json; done`
4. `grep -rn "SCN-PERC-001\|SCN-BARRIER-001\|SCN-WIFI-002\|SCN-POLICY-006\|SCN-POLICY-007\|SCN-POLICY-009" tests/UniClaw.Simulation.Tests --include="*.cs" | grep -i "trait\|Scenario\""`（附 `ls tests/UniClaw.Simulation.Tests/TestResults/`）
5. `grep -n "class AsyncPerceptionHost\|AsyncPerceptionHost(" …`；`grep -n "WifiToggleOffToOn\b|AlreadyOnZeroEffect|TwoStepToggleThenMenuItem|PolicyGuardUnknown|PolicyVerificationFailureMidway|PolicyForbiddenEffect" tests/UniClaw.Simulation.Tests/GoldenScenarioBundles.cs`
6. `grep -rn "BundleRoot" …`；`ls platforms/perception/evaluation/assets/captures/golden-run-v1/`；`python3 -c "…print scenario-manifest.json 前段"`
7. `python3`（heredoc，xml.etree 只读解析 coverage.trx，匹配 8 个 FQN 的 outcome；两次调用，第二次为模糊匹配定位）
8. `grep -c testId …/coverage.trx`；`head -5 …/coverage.trx`；`stat -f '%m %Sm %N' …/coverage.trx`
9. `grep -n "Load\b|path|Path|json" tests/UniClaw.Simulation.Tests/AsyncPerceptionFixtures.cs`；`grep -rn "class AsyncPerceptionTruth" …`；`grep -n "Load|type-truth|…|Path.Combine" AsyncPerceptionTracer.cs`
10. `grep -n "LoadAssets|LoadTargetUiSystem|…|manifest" GoldenScenarioBundles.cs`；`sed -n '660,816p' … | grep …`
11. `sed -n '580,700p' GoldenScenarioBundles.cs`
12. `sed -n '1,120p' docs/architecture/simulation-baseline-v0.1.md`；`sed -n '1,62p' docs/architecture/simulation-baseline-v0.2-c7-amendment.md`
13. `grep -rln "testsets" tests/UniClaw.Simulation.Tests/*.cs`；`python3 -c "…print testsets/android-settings/manifest.json 前段"`；`grep -n "runtime_source_hash|def verify_entry" tools/scenario_certify.py`；`sed -n '/def runtime_source_hash/,/^def /p' tools/scenario_certify.py`
14. `mkdir -p evidence/sim-006/wi-sim006-001`（唯一写操作，本报告所在目录）

未运行：dotnet build/test/run、scenario_certify.py、scenario-coverage.py、verify-change、verify-live（符合 forbidden）。

## 附录 B：git status --porcelain 实际输出（审计结束时）

```
?? evidence/sim-006/wi-sim006-001/
?? workitems/WI-SIM006-001.json
```

- `workitems/WI-SIM006-001.json` 为派发方预先落盘的 WorkItem（审计开始前已存在，见首次 git status）。
- 除唯一可写路径 `evidence/sim-006/wi-sim006-001/` 外零工作树改动。
