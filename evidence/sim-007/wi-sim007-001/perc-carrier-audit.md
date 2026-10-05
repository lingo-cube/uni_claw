# WI-SIM007-001 — 8 个 execution.kind=none 场景逐载体事实矩阵（只读审计）

- WorkItem: `workitems/WI-SIM007-001.json`（status: in_progress）
- 基线：分支 `uni-harness`，HEAD `b8fe563a`（已核实 `git log --oneline -1`）
- 性质：纯静态只读审计。所有证据为源码 文件:行号 与既有记录；**不含任何新鲜执行 PASS 表述**。
- 唯一可写路径：`evidence/sim-007/wi-sim007-001/**`（本报告）。

---

## 0. 结构性总述（对 8 个场景同构的事实）

1. **8 个场景的专有载体全部在同一个测试类**：`tests/UniClaw.Simulation.Tests/AsyncPerceptionScenarioTests.cs`，经 `[Fact, Trait("Scenario", "SCN-PERC-00N")]` 映射 FQN（行 158/255/313/432/488/548/850/1134）。
2. **certified expectations 消费：8/8 全部为 none**。全仓库 `ScenarioExpectations.Load/Verify` 的调用点只有 `GoldenScenarioBundles.cs`（8 处，行 74/111/138/175/250/302/355/375/467）、`ScenarioLibrary.cs:60`、`ExecutableExpectationBindingTests.cs`（WIFI 场景）。`AsyncPerceptionScenarioTests.cs` / `AsyncPerceptionTracer.cs` / `AsyncPerceptionFixtures.cs` / `AsyncPerceptionRealizationTests.cs` **零调用**（grep 全量核对）。且 `ScenarioLibrary.Load` 对 kind=none 显式 fail-closed 拒绝（`ScenarioLibrary.cs:63-65`）——PERC 场景与 executable expectation projection 保证结构性脱钩，这是设计内行为。
3. **GoalEvaluation 构造：8/8 全部为 none**。PERC harness（`AsyncPerceptionHost`，`AsyncPerceptionTracer.cs:527-611`）不构造 `UniAgent`、不产生 `GoalEvaluation`；grep 该 4 个文件仅命中 `ScriptedUniAgent` 构造（`AsyncPerceptionTracer.cs:579`）。`GoalEvaluation` 类型在产品面（`src/UniClaw.Agent/Evaluation/GoalEvaluation.cs:11`），测试面消费点仅在 `ScenarioRunner.cs:159/189`（golden-bundle 链路）。
4. **digest 校验：8/8 全部为 none**。PERC 测试文件不读取 `certification` 块、不引用 expectationsDigest。
5. **decision 面运行时对应物（8/8 同构，部分成立）**：`AsyncPerceptionHost` 构造 `ScriptedUniAgent(scenario.AgentScript)` 并经 `ConsultationJournal` 注入 `KernelRunDriver.RunDriverInputs.ConsultAgent`（`AsyncPerceptionTracer.cs:579-581, 591`）。缝的产品定义为 `src/UniClaw.Kernel/Runtime/RunDriverInputs.cs:66`，消费点 `src/UniClaw.Kernel/Runtime/KernelRunDriver.cs:1021`。decision 面确有运行时对应物（double：ScriptedUniAgent）；但无任何测试断言 consultation **计数**（`agentConsultations=2/1` 期望无计数断言对应，grep `Calls.Count` 在 AsyncPerceptionScenarioTests.cs 零命中）。
6. **realization 标注的执法测试存在但为弱执法**：`ScenarioRealizationAnnotationTests.cs:25-26` 的构成锚点是 **ScenarioRunner/SimulationHost** 的构成（`ActualDecisionRealization="double"`、`ActualEvaluationRealization="real"`），测试遍历全部 `scenarios/SCN-*.json` 比对 JSON 值（行 56-92）。对 kind=none 场景：它只检查 JSON 字段存在且等于 ScenarioRunner 构成常量，**不检查专有 harness 是否真的走 UniAgent/GoalEvaluation**——故 `goalEvaluationRealization=real` 标注对 PERC 场景是悬空声明（无对应物），`agentDecisionRealization=double` 有对应物（ScriptedUniAgent，见第 5 条）。
7. **旧 TRX（既有记录，非本轮执行证据）**：`tests/UniClaw.Simulation.Tests/TestResults/coverage.trx`（= `sim006-full.trx`，同字节数 273668）含 15 个 `AsyncPerceptionScenarioTests.*` 方法记录；TRX 内不携带 `SCN-PERC-*` 字面 trait 字符串，trait→FQN 对应只由源码 `[Trait]` 建立并按状态链引用，本轮不据此下 PASS 结论。
8. **配置与资产来源（8/8 同构）**：`AsyncPerceptionTruth.Load`（`AsyncPerceptionFixtures.cs:43`）读 `platforms/perception/evaluation/reports/fsv001/screenvlm-probe/vlm-compare/type-truth.json` 与 `…/dual/type-dual-local.json`（`AsyncPerceptionFixtures.cs:51-53`）；其余输入为测试内构造的 typed elements / scheduled results（如 `AsyncPerceptionScenarioTests.cs:172-190`）。无 golden bundle、无 manifest、无 profile/testset 绑定；`execution = {"kind":"none"}`，certifiedByChange=CAP-001（2026-10-04）。

---

## 1. 逐场景事实矩阵

### SCN-PERC-001

| 项 | 事实 | 证据 |
|---|---|---|
| (a) JSON 声明 | kind=none；decision=double；evaluation=real；expectations 六字段：status=Completed, classification=Completion, effects=1, agentConsultations=2, unconsumedStimuli=0, goalSatisfaction=Satisfied；expectationsDigest `7de541f2…` | scenarios/SCN-PERC-001.json |
| (b) 载体 | `AsyncPerceptionScenarioTests.S1_FastMissesTarget_TargetedSlowRecovers_OneEffectOnRealTarget`；Trait("Scenario","SCN-PERC-001") | AsyncPerceptionScenarioTests.cs:158-159 |
| (c) 消费 certified expectations | **none**（不调用 ScenarioExpectations.Verify/Load，无 digest 校验） | 全文件 grep；结构性总述第 2 条 |
| (d) 构造真实 GoalEvaluation | **none**（不构造 UniAgent/GoalEvaluation；只断言 TerminalClassification.Completion :243） | AsyncPerceptionScenarioTests.cs:192-248 |
| (e) decision 面运行时对应物 | 有：ScriptedUniAgent 经 ConsultationJournal → RunDriverInputs.ConsultAgent | AsyncPerceptionTracer.cs:579-581,591；RunDriverInputs.cs:66 |
| (f) realization 标注对应物 | decision=double：**有**；evaluation=real：**无**（harness 不产生 GoalEvaluation；标注测试只比对 ScenarioRunner 构成常量） | AsyncPerceptionTracer.cs:559-611；ScenarioRealizationAnnotationTests.cs:25-26,78-86 |
| (g) 配置与资产来源 | fsv001 type-truth.json + type-dual-local.json + 测试内构造；无 manifest | AsyncPerceptionFixtures.cs:51-53 |
| (h) 结论 | 与 SIM-006 §3 结论**逐条一致**（见 §2 对照表） | — |

### SCN-PERC-002

| 项 | 事实 |
|---|---|
| (a) JSON | kind=none；decision=double；evaluation=real；六字段与 001 完全相同（status=Completed/classification=Completion/effects=1/agentConsultations=2/unconsumedStimuli=0/goalSatisfaction=Satisfied）；expectationsDigest `7de541f2…`（与 001 相同） |
| (b) 载体 | `S2a_FastMisjudgesState_SlowCorrectsBeforeDecision`，AsyncPerceptionScenarioTests.cs:255-256；Trait("Scenario","SCN-PERC-002") :255 |
| (c) expectations 消费 | **none**（同结构性总述第 2 条） |
| (d) GoalEvaluation | **none**（AsyncScenario → Activate → AsyncPerceptionHost，:288-294；断言 Consultations.Calls 内容 :303-304、EffectDeliveries :306、ClaimEvolutionLog :308-309、Completion :310） |
| (e) decision 对应物 | 有：同构 ScriptedUniAgent→ConsultAgent 缝（tracer:579-591）；且本测试断言 consultation **内容**（"disabled"，:303-304），但不断言计数=2 |
| (f) 标注对应物 | decision=double **有**；evaluation=real **无** |
| (g) 资产 | 同构 fsv001 truth fixtures |
| (h) 结论 | 载体真实存在且断言行为面；certified expectations 与 goalEvaluationRealization=real 声明无运行时消费——悬空，与 001 同构 |

### SCN-PERC-003

- (a) kind=none；decision=double；evaluation=real；六字段：status=**AgentDecisionFailed**, classification=null, effects=0, agentConsultations=**1**, unconsumedStimuli=0, goalSatisfaction=null；expectationsDigest `400b1755…`
- (b) `S2b_FastOnlyContrast_UncorrectedMisjudge_FailsClosed_NoCompletion`，AsyncPerceptionScenarioTests.cs:313-314
- (c) **none**；(d) **none**（:331-336 构造 AsyncScenario 走专有 host；断言 fail-closed 行为 :338 起）
- (e) 有（同构缝）；(f) decision **有** / evaluation **无**
- (g) 同构 fsv001 fixtures
- (h) 同构悬空：expectations（AgentDecisionFailed / consultations=1）无投影消费、无计数断言；goalEvaluationRealization=real 无对应物。

### SCN-PERC-004

- (a) kind=none；decision=double；evaluation=real；六字段同 001；expectationsDigest `7de541f2…`
- (b) `S3a_IrrelevantAndExactDuplicateEvidence_DoNotGrowRevision`，AsyncPerceptionScenarioTests.cs:432-433
- (c) **none**；(d) **none**（AsyncScenario 构造 :457）
- (e) 有；(f) decision **有** / evaluation **无**
- (g) 同构；(h) 同构悬空（001 模式）。

### SCN-PERC-005

- (a) kind=none；decision=double；evaluation=real；六字段同 001；expectationsDigest `7de541f2…`
- (b) `S3b_BasisConflictUncertaintyChange_GrowsRevision`，AsyncPerceptionScenarioTests.cs:488-489
- (c) **none**；(d) **none**（:514）
- (e) 有；(f) decision **有** / evaluation **无**
- (g) 同构；(h) 同构悬空（001 模式）。

### SCN-PERC-006

- (a) kind=none；decision=double；evaluation=real；六字段同 001；expectationsDigest `7de541f2…`
- (b) `S4_StaleSlowAfterPageAdvance_Quarantined_ZeroBeliefPollution`，AsyncPerceptionScenarioTests.cs:548-549
- (c) **none**；(d) **none**（:586；另 :619 构造 controlHost 对照）
- (e) 有；(f) decision **有** / evaluation **无**
- (g) 同构；(h) 同构悬空（001 模式）。

### SCN-PERC-007

- (a) kind=none；decision=double；evaluation=real；六字段同 001；expectationsDigest `7de541f2…`
- (b) `S6_FourTerminalCompletionStates_StrictlyDistinct`，AsyncPerceptionScenarioTests.cs:850-851
- (c) **none**；(d) **none**（多 host 构造 :986/:1073 等）
- (e) 有；(f) decision **有** / evaluation **无**
- (g) 同构；(h) 同构悬空（001 模式）。

### SCN-PERC-008

- (a) kind=none；decision=double；evaluation=real；六字段：status=**TerminalNotProven**, classification=null, effects=0, agentConsultations=2, unconsumedStimuli=0, goalSatisfaction=null；expectationsDigest `4b272fd7…`
- (b) `S8b_StillWrongOrConflicting_SlowNotAutoTruth_AmbiguityZeroClicks`，AsyncPerceptionScenarioTests.cs:1134-1135
- (c) **none**；(d) **none**（多 host 构造 :1153/:1186/:1257）
- (e) 有；(f) decision **有** / evaluation **无**
- (g) 同构；(h) 同构悬空；期望 status=TerminalNotProven / goalSatisfaction=null 无投影消费。

---

## 2. SCN-PERC-001 与 SIM-006 既有审计（§3）显式对照

| SIM-006 §3 结论 | 本轮重核 | 判定 |
|---|---|---|
| 载体 = S1 方法，:158–248 | 一致（:158-248；Trait :158） | **一致** |
| 不经 ScenarioRunner/ScenarioLibrary；ScenarioLibrary.Load 对 kind=none fail-closed（ScenarioLibrary.cs:63-65） | 一致（同行号仍成立） | **一致** |
| (c) 不调用 ScenarioExpectations.Verify/Load，无六字段核对 | 一致（全文件 grep 零命中） | **一致** |
| (d) harness 全程不构造 UniAgent/GoalEvaluation；只断言 TerminalClassification.Completion（:243） | 一致 | **一致** |
| decision=double 成立（ScriptedUniAgent 经 ConsultationJournal，tracer:579-581） | 一致 | **一致** |
| evaluation=real 未由载体建立；标注测试只比对 JSON 值与 ScenarioRunner 构成常量 | 一致（ScenarioRealizationAnnotationTests.cs:25-26,78-86） | **一致** |
| (e) 资产来源 fsv001 type-truth.json / type-dual-local.json（fixtures:50-53） | 一致（现为 :51-53，行号微移，内容一致） | **一致**（行号微差：50-53 → 51-53） |
| expectationsDigest 与 WIFI-001 相同（7de541f2…） | 一致（002/004-007 同 digest） | **一致** |
| agentConsultations=2 无计数断言 | 一致（全文件无 Calls.Count / consultation 计数断言） | **一致** |
| 总判定：certified expectations 与 goalEvaluationRealization=real 均悬空 | 重核后不变 | **一致** |

结论：本轮对 SCN-PERC-001 的重核与 `evidence/sim-006/wi-sim006-001/scenario-audit.md` §3 **全部条目一致，无不一致项**。

---

## 3. 对 SCENARIO_CAVEATS 推导规则的含义

工具规则现状：`tools/verify-change:105-120`（`scenario_caveats`）按 `execution.kind == "none"` 全称机械推导，对每个命中场景输出 `tools/verify-change:402` 的 caveat 文案（"certified expectations and realization annotations are NOT consumed by the carrier"）。本轮事实矩阵对该规则逐场景的判定（**只陈述事实，不裁决收窄或维持**）：

| 场景 | 规则声称 | 本轮事实 | 规则对该场景 |
|---|---|---|---|
| SCN-PERC-001 | expectations 与 realization 标注不被载体消费 | expectations 无消费（none）；evaluation=real 无对应物；decision=double **有**对应物 | 成立（expectations/evaluation 半边严格成立；decision 半边按字面是"not consumed by the carrier"，标注消费执法在标注测试但仅对 ScenarioRunner 构成——规则按"载体消费"口径成立） |
| SCN-PERC-002 | 同上 | 同构：expectations none、evaluation 无对应物、decision 有对应物 | 成立（同上口径） |
| SCN-PERC-003 | 同上 | 同构 | 成立（同上口径） |
| SCN-PERC-004 | 同上 | 同构 | 成立（同上口径） |
| SCN-PERC-005 | 同上 | 同构 | 成立（同上口径） |
| SCN-PERC-006 | 同上 | 同构 | 成立（同上口径） |
| SCN-PERC-007 | 同上 | 同构 | 成立（同上口径） |
| SCN-PERC-008 | 同上 | 同构 | 成立（同上口径） |

事实要点（供后续裁决引用，非本轮裁决）：

1. 规则的核心断言（专有载体不消费 certified expectations）对 8/8 场景**事实成立**——此前对 002-008 是待证假设，本轮升级为已核实事实。
2. 规则文案的"realization annotations are NOT consumed by the carrier"对 8/8 场景**部分成立**：goalEvaluationRealization=real 确无载体消费；agentDecisionRealization=double 在 8/8 载体上**有**运行时对应物（ScriptedUniAgent → ConsultAgent 缝）。规则口径若按"载体不经 ScenarioRunner 投影消费"理解则全称成立，若按"标注完全无运行时对应物"理解则对 decision 半边不成立——两种读法的事实基础均已列出，证据不足以判定工具文案意图取哪一种。
3. 8 个场景中 2 个（003、008）的 expectations 含 null 字段（classification/goalSatisfaction），status 为 AgentDecisionFailed / TerminalNotProven；其余 6 个与 WIFI-001 共享同一 expectationsDigest（`7de541f2…`）。该 digest 重叠事实对规则本身无影响，仅为 Human Gate 材料补充。

---

## 4. 候选（仅标注，非裁决）

- 候选（非本审计裁决）：caveat 文案可按 decision/evaluation 两面拆分表述（decision 面有对应物、evaluation 面无），使工具输出与事实矩阵逐面对齐。
- 候选：8 个场景共享 digest `7de541f2…` 的事实可在词汇表/证据中显式记录，避免误读为绑定错误。

---

## 5. 只读命令清单（本轮实际执行）

```
pwd
git log --oneline -1
git status --porcelain
git status --porcelain | head -20
sed -n 1,200p changes/SIM-007/state.md
ls scenarios/SCN-PERC-00*.json tests/UniClaw.Simulation.Tests/
python3 -c "…dump 8 个 PERC JSON 关键字段…"
wc -l tests/UniClaw.Simulation.Tests/AsyncPerception{ScenarioTests,Tracer,Fixtures,RealizationTests}.cs ScenarioExpectations.cs
grep -rn 'ScenarioExpectations' tests/UniClaw.Simulation.Tests/ --include=*.cs
grep -n 'SCN-PERC' tests/UniClaw.Simulation.Tests/*.cs
grep -rn 'ConsultAgent' src/ tests/ --include=*.cs
grep -n 'Consultations|ConsultAgent|…' tests/UniClaw.Simulation.Tests/AsyncPerceptionTracer.cs
sed -n 520,620p tests/UniClaw.Simulation.Tests/AsyncPerceptionTracer.cs
sed -n 1,90p tests/UniClaw.Simulation.Tests/ScenarioLibrary.cs
sed -n 1,143p tests/UniClaw.Simulation.Tests/ScenarioExpectations.cs（read 工具）
grep -rn 'GoalEvaluation' src/ tests/ --include=*.cs
grep -n 'def scenario_caveats' -A 15 tools/verify-change
sed -n 360,420p tools/verify-change
sed -n 1,140p tests/UniClaw.Simulation.Tests/ScenarioRealizationAnnotationTests.cs
python3 -c "…8 个 JSON 的 certifiedByChange/execution…"
grep -o 'SCN-PERC-[0-9]*' tests/UniClaw.Simulation.Tests/TestResults/*.trx
grep -o 'AsyncPerceptionScenarioTests[^"]*' tests/UniClaw.Simulation.Tests/TestResults/coverage.trx
grep -n 'UniAgent|certif|Digest' tests/UniClaw.Simulation.Tests/AsyncPerception*.cs
sed -n '/## 3/,/## 4/p' evidence/sim-006/wi-sim006-001/scenario-audit.md
grep -n 'Activate(new AsyncScenario|Calls.Count' tests/UniClaw.Simulation.Tests/AsyncPerceptionScenarioTests.cs
mkdir -p evidence/sim-007/wi-sim007-001
```

未执行任何 dotnet build/test/run；未执行 scenario_certify.py / scenario-coverage.py / verify-change；未执行任何写模式 git 命令。

## 6. git status --porcelain 实际输出（审计收尾时刻）

```
?? evidence/sim-007/
?? plans/2026-10-05-sim-008-carrier-duplication-options.md
?? workitems/WI-SIM007-001.json
```

（本审计创建的仅 `evidence/sim-007/wi-sim007-001/perc-carrier-audit.md`；`workitems/WI-SIM007-001.json` 为派发时已存在的未跟踪文件；`plans/2026-10-05-sim-008-carrier-duplication-options.md` 非本轮创建，系会话期间并行出现的未跟踪文件，本轮未读写其内容。审计全程零 M/D 工作树改动。）
