# SIM-006 首批基线矩阵（A1 交付物）

> DocumentType: `SIM_BASELINE_INVENTORY`
> Status: `COMPLETE / EVIDENCE-BACKED`
> Authority: `NONE`
> 调查基线: 2026-10-05 WI-SIM006-001 只读审计（`evidence/sim-006/wi-sim006-001/scenario-audit.md`，
> Leader 抽查复核）+ `scenarios/SCN-*.json` + 既有 TRX。分析结论要产生约束力须经 change 落入
> 对应权威目录（docs/README.md §3）。

> 范围：SIM-006 首批 8 个闭环场景 + 组件契约轴的双轴基线。
> 本矩阵是**事实编目**：不产生新鲜执行 PASS 声明；TRX 引用为 2026-10-04 旧执行记录；
> 新鲜执行证据见 changes/SIM-006/state.md verification 与 evidence/sim-006/runs/。

## 轴一：闭环场景（8 项）

| 场景 | 能力 / components | realization（decision / evaluation） | execution / carrier | 测试载体（FQN） | 主要断言（外部行为） | 配置/资产来源 | 已知缺口 |
|---|---|---|---|---|---|---|---|
| SCN-SMOKE-001 | host-integration / kernel, world-model, assurance, effect-boundary, control-loop, run-model | double / real | golden-bundle / wifi-off-to-on | `UniClaw.Simulation.Tests.SimulationHostSmokeTests.S1_HappyPath_EndToEnd_Accepts` | AcceptancePassed 六字段；零 AgentViolations；ModelCalls=N/A（double 不冒充 live model） | SCN JSON + golden-run-v1 | 与 WIFI-001 三个 certification digest 逐字节相同、断言为其严格子集——非独立新能力 |
| SCN-WIFI-001 | 同上 | double / real | golden-bundle / wifi-off-to-on | `UniClaw.Simulation.Tests.DeterministicScenarioTests.S1_OffToOn_OneAuthorizedEffect_VerifiedTerminalOutcome` | 超集：+decision-id 协议格式、stimulus 消费顺序、GoalEvaluation 与 Outcome 同 RunId、driver 自驱（run terminal+单 receipt） | SCN JSON + golden-run-v1 | driver/decision 为 double，不证明真实设备切换 |
| SCN-WIFI-002 | 同上 | double / real | golden-bundle / wifi-already-on | `UniClaw.Simulation.Tests.DeterministicScenarioTests.S2_AlreadyOn_ZeroEffect_TerminalCompletion` | effects=0/consultations=1/Completed/Satisfied（零 dispatch 为六字段之一）；host 侧计数与 report 一致 | SCN JSON + golden-run-v1（case-a-before 帧） | 无 |
| SCN-PERC-001 | async-perception | double / **real（标注未被载体建立）** | **none** / 无 | `UniClaw.Simulation.Tests.AsyncPerceptionScenarioTests.S1_FastMissesTarget_TargetedSlowRecovers_OneEffectOnRealTarget` | 测试字面量：虚拟时间轴合法等待；slow 到达后覆盖并集→act→post→Completed；omission≠absence；恰 1 effect 接地真实行；零 producer 泄漏 | type-truth.json + type-dual-local.json（fsv001 探针资产） | **certified expectations 六字段与 evaluation=real 均无运行时消费者（悬空声明）**；不经 ScenarioRunner；realization 执法锚点不覆盖 kind=none 专有载体；agentConsultations=2 期望无对应断言 |
| SCN-BARRIER-001 | effect-barrier | double / real | golden-bundle / wifi-two-step-toggle-menu | `UniClaw.Simulation.Tests.TwoStepBarrierTests.TwoStep_EvidenceBetweenSteps_SecondEffectAllowed_TerminalCompletion` | effects=2 各一 receipt；消费顺序 obs-2-post 在 step2 前（不变量 43 可观察面）；零 violations | SCN JSON + golden-run-v1 | 无（负向面 SCN-BARRIER-002/003 同文件并存，证明力条件成立） |
| SCN-POLICY-006 | policy-guard | double / real | golden-bundle / policy-guard-unknown | `UniClaw.Simulation.Tests.PolicyScenarioTests.P6_ConflictedClaim_GuardUnknown_FailClosed` | TerminalNotProven；FailureReason=policy:guard-unknown；EffectDeliveries=0（Unknown 不降级） | SCN JSON + golden-run-v1 | 无 |
| SCN-POLICY-007 | policy-verification | double / real | golden-bundle / policy-verification-failure | `UniClaw.Simulation.Tests.PolicyScenarioTests.P7_VerificationFailureMidway_VoidsPolicy_ExistingTransition` | Phase=VerificationFailed；post-action-desired-state-not-satisfied；ApplicationsUsed=0；EffectDeliveries=1 | SCN JSON + golden-run-v1 | 无 |
| SCN-POLICY-009 | policy-effect-class | double / real | golden-bundle / policy-forbidden-effect | `UniClaw.Simulation.Tests.PolicyScenarioTests.P9_ForbiddenEffectClass_RejectedAtConsultation` | AgentDecisionFailed；Reason=policy:effect-class-not-allowed（V6d 咨询期契约拒绝）；零 effect；单咨询 fail-closed | SCN JSON + golden-run-v1（hvac/temp 合成 claim，无 Settings 菜单语义） | 不证明未知 Settings 菜单识别（覆盖范围如实表达） |

**共用机制**（全 8 项）：7 项走 `ScenarioLibrary.Load → ScenarioRunner（AdmitContract→Activate→Drive）→ SimulationHost` 标准链，期望唯一真值 = `scenarios/SCN-*.json`（Load 时 expectationsDigest/executionDigest fail-closed 校验）；golden 资产唯一来源 `platforms/perception/evaluation/assets/captures/golden-run-v1/`（manifest contentHash 双核对）。scenario→FQN 映射唯一声明点 = 测试代码 `[Trait("Scenario", …)]`，由工具从二进制枚举发现后与 TRX 按 FQN join。PERC-001 为唯一例外（专有 AsyncPerceptionHost）。

## 轴二：组件契约（引用既有断言，不复制 golden）

| 契约面 | 既有载体 | 约束 |
|---|---|---|
| 组件隔离 double 保真（C1） | `SeamOverrideTests` | 相邻 double 必须完整履行 typed seam 契约 |
| realization 标注执法（C7 v0.2） | `ScenarioRealizationAnnotationTests` | 标注存在/合法/与构成一致；**锚点只覆盖 ScenarioRunner 构成**（PERC 缺口由此漏出） |
| 期望绑定完整性 | `ExecutableExpectationBindingTests`、`ScenarioCertificationTests` | 认证 digest 自洽、expectations 与执行绑定 |
| golden bundle 完整性 | `BundleIntegrityTests` | 资产 contentHash、manifest 一致 |
| 零纪律/协议对齐/fail-closed | `PerCycleZeroDisciplineTests`、`ProtocolAlignmentTests`、`FailClosedScenarioTests` | 每周期纪律、协议对齐、fail-closed 行为 |
| 确定性（C5） | 套件内 digest 执法 + `CanonicalEnumerationRegressionTests` | 两次运行同 digest、枚举稳定 |

## 证据等级声明

- 仿真（本矩阵）：deterministic double 闭环——不证明 live model 语义质量、真实设备行为或性能。
- Product Host 本地组、live device：独立等级，本矩阵不含其 PASS 声明。
- TRX 引用（coverage.trx，2026-10-04T23:00:09+08:00，188 结果）为旧执行记录；新鲜执行证据见 SIM-006 verification。

## 遗留缺口（不在 SIM-006 内修）

1. SCN-PERC-001 realization 标注语义（词汇表或专有载体期望消费面）——涉及 C7 v0.2 执法面扩展，需致因 Change + 适当 authority。
2. SCN-SMOKE-001 与 SCN-WIFI-001 载体重复——是否合并/差异化属场景库演化决策，非本 Change。
