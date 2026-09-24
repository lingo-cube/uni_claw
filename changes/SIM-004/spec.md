# SIM-004 — Simulation Agent Seam De-Concreting（spec）

> 前置审计：Abstraction Boundary Audit（2026-09-24 会话，ABS-001/002/003 findings）。
> 指令权威：owner continuation 指令（SIM-004 — Continue From Current HEAD，Step 0-17）。
> 上游协议权威：RUN-005 FROZEN v0.3.1（Slice A/B/C 已落地）· AGT-001 FROZEN v0.2。
> 形态：**continuation change**——RUN-005 Slice C（490a8776）已交付 phase-aware
> ScriptedUniAgent 决策核与 PhaseScript bundle 字段，本 change 收敛其残留。

## Step-0 Reconciliation 矩阵（current HEAD bc051175，已逐项引用代码）

| Finding | 状态 | 证据 |
|---|---|---|
| ABS-001 agent 缝旋钮 concrete 收窄 | **NOT DONE** | `SeamOverrides.Agent : ScriptedUniAgent?`（SimContract.cs:534）；`SimulationHost.ScriptedAgent : ScriptedUniAgent`（SimulationHost.cs:47）；`AsyncPerceptionHost.ScriptedAgent/EffectDriver : DeterministicEffectDriver`（AsyncPerceptionTracer.cs:508-509）；`SemanticDigest.Of(..., ScriptedUniAgent, ...)`（SemanticDigest.cs:17） |
| ABS-002 probe 绕过产品 facts | **NOT DONE** | `EffectDriver is DeterministicEffectDriver d ? d.DeliveryCount : -1`（SimulationHost.cs:51-52，全仓唯一 concrete is-cast）；`AsyncPerceptionHost` 17 处 `host.EffectDriver.DeliveryCount` 读取 |
| ABS-003 脚本词表镜像 taxonomy | **PARTIAL** | 已有（RUN-005 Slice C，保留）：turn 表机制（`ScriptedUniAgent.ConsultPhaseAware` 相位匹配 fail-closed、PolicyInvalidated 合法相位、Policy/Defer 全覆盖、`PhaseScript` digest 渲染）。残留（本 change 收敛）：`ScriptDecisionKind` 五成员枚举 ≈ AgentDecision 四变体 + null 的镜像（SimContract.cs:185-192）与 legacy `AgentScriptKind/ScriptActionStep/AgentScriptStep`（:154-177）**并存成两个镜像**；legacy 隐藏策略（StepVerified→自动 NoAction、second-call→duplicate-call，ScriptedUniAgent.cs:135-139）；phase-aware NoAction 无 CompletionEvidence 载荷、Defer Subject 不可 authoring |

## Intent（WHAT/WHY）

关闭 ABS-001/002/003：Simulation 的 agent 注入面回到 Product 冻结缝
`Func<AgentDecisionContext, AgentDecision?>`（RUN-004 D6 / AGT-001 §1）；测试观测
（calls/violations/late-call/count）移居 seam 消费侧 sim-only probe，产品 facts
（EffectReceipts）优先；脚本「返回什么」的语义类型直接来自 Product
（AgentActionStep/ObserveSpec/CompletionEvidence/PolicyProposal），double 只保留
「第几轮、期待什么相位、什么行为」（Respond/NoResponse 为 double behavior
taxonomy，非 Product semantic taxonomy）。

## Scope（IN）

- ABS-001：`SeamOverrides.Agent` → Product Func 类型；`SimulationHost`/`AsyncPerceptionHost`
  的 agent 观测改经 `ConsultationJournal`（seam 装饰）+ `IScriptedAgentProbe`
  （sim-only probe 接口）；`ScriptedUniAgent` 保持 concrete（AGT-001 §10.2 指定的
  deterministic double），但只作为组合根自选 realization 存在。
- ABS-002：`EffectDeliveryCount`/tracer 计数 ← `KernelCore.EffectReceipts`（产品
  owner facts）；删除 is-cast 与 -1 sentinel（含 AsyncPerceptionHost 面）。
- ABS-003：删除全部镜像类型（AgentScriptKind/ScriptActionStep/AgentScriptStep/
  ScriptDecisionKind/ScriptPredicateSpec/ScriptGuardSpec/ScriptPolicySpec）；
  `ScriptedTurn` 载荷改 Product 类型 + `ScriptedDoubleBehavior{Respond,NoResponse}`；
  删除 legacy 决策核与隐藏策略；`MinimalScenarioBundle.PhaseScript` 成为唯一脚本
  字段（required）；digest 以 Product 载荷渲染；`ScenarioBuilder.AgentDecides` 迁移；
  18 legacy golden 场景 + P1-P11 + UAP tracer 场景全部迁移到显式 turn authoring。
- Alignment tests A1-A8（double 级；A4/A5 场景级由 PolicyScenarioTests 既有承载）。
- Architecture tripwire：反射取 AgentDecision 变体名集合，禁止 sim 枚举镜像；
  ScriptedTurn 载荷槽类型必须是 Product 程序集类型（不写死四种决策）。
- 构成锚点同步（ScenarioRealizationAnnotationTests）与受影响测试迁移。

## Out of Scope

ABS-004 reference wall 孪生 · ABS-005 ambient UtcNow · ABS-006
FrameOccurrenceStrategy dead realization · 任何 src/ 产品语义修改 · AGT-001 架构 ·
RUN-005 protocol · DSH 接入 · Policy runtime 重设计 · 新增大批 scenario（A1-A8 为
double 级测试，不新增 SCN 条目）· Conditional/Adversarial scheduler ·
`IAgentDecisionProvider`（AGT-001 §1 冻结禁止）。

## 关键裁决（Decisions）

1. **turn 响应载体 = Product 类型**：`ScriptedTurn(ExpectedPhase, Behavior,
   Act?:IReadOnlyList<AgentActionStep>, Justification?, Completion?:
   CompletionEvidence, Defer?: ObserveSpec, Policy?: PolicyProposal)`——无 kind
   枚举；Respond 由载荷存在性判别（全空 = NoAction），DecisionId 以 ctx 回带
   （D2 防串话——预铸 decision 无法携带 Kernel 铸造的 DecisionId，故必须有
   单点 presence 包装，这是数据序列化的最小桥，同 ScenarioExpectations 的
   枚举名字符串桥性质）。
2. **观测三层**：`ConsultationJournal`（seam 级：Calls transcript/late-call/
   missing-run-correlation/count——realization 无关，未来 DSH conformance 同律）
   + `IScriptedAgentProbe`（double 级：phase-mismatch/script-exhausted/
   unconsumed-turns）+ 产品 facts（EffectReceipts）。
3. **P11 rogue 注入位**：`RogueScriptPredicate : PolicyPredicate`（sim-side 派生
   记录）直接进 `PolicyProposal.Match`——外来 AST 经正式 seam 入场被 V6a 拒绝
   （保留 Slice C 语义，删除 ScriptPredicateSpec 字符串映射层）。
4. **行为保持**：legacy 显式迁移为等价 turn 序列（如 WIFI-001 →
   ActAt(InitialPlanning)+NoActionAt(StepVerified)）；BARRIER-003 的 legacy 伪
   `duplicate-call` 违规改为显式 `NoResponseAt(VerificationFailed)` turn——
   certified 六字段不变，acceptance 由伪违规必假翻真（测试原不断言
   AcceptancePassed，零断言 churn）。
5. **Certification**：期望/execution 绑定/runtimeSourceHash（Kernel+Agent src）
   三者均不变 → **NO RECERTIFICATION**（29 条 SCN json 零触碰）；bundle digest
   为构造时自洽重算，非认证钉扎面。

## Acceptance

1. `dotnet test` 全 solution 绿（Kernel/Simulation/Host/Agent/Core/FSRealization）；
2. `tools/scenario_certify.py --check` 29/29 PASS（零重认证）；`scenario-coverage.py` 29/29；
3. A1-A8 全绿；tripwire 全绿且对镜像枚举注入呈 RED（负向自证）；
4. G1-G7 Narrow Grill 全部通过（含 G7 产品零修改）；
5. 既有 29 场景六字段语义零回归（SemanticDigest 双跑相等性测试维持）。
