# RFS-001 — Runtime Closed Loop, Phase 1 Architecture Lockdown & Deterministic Simulation Tracer

lifecycle_state: closed · disposition: none  # Human closure authorized 2026-09-13 after remediation D18–D24 verification · depth: decision-heavy · base: 05d398ce81dd4fff7bb752c23e57e9e445c641a7 · pin: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 (2026-09-13 resume)

> History: 本 Change 最初是 design-only（runtime 闭环/Simulation 核心模型候选文档，
> 见 `docs/design/runtime-flow-simulation-core-model-v0.1.md` 与下方 2026-09-12
> 三条 status log）。2026-09-13 Human 重新授权扩大为两段串行任务：(1) 锁定
> Phase 1 依赖的最小架构语义并过 Architecture Gate；(2) Gate 通过后实现
> Phase 1 deterministic tracer。原候选文档降级为问题线索，不构成本 Change 的
> 架构事实。

## Intent（WHAT/WHY）

**WHAT（段 1 — 架构锁定）**：做 repository-grounded architecture review，把
Phase 1 及后续 Phase 依赖的最小架构语义（Owner / Authority / Lifecycle / 状态
转移 / 安全不变量 / Host 隔离）正式落入 canonical 文档，逐条区分
`FROZEN_INHERITED / HUMAN_SELECTED / TRACER_HYPOTHESIS / OPEN_GATE`；不锁
类名、字段、namespace、transport、storage 或完整 Interface。通过 Architecture
Gate 后把 roadmap H2 改为 ACCEPTED AS CHANGE INPUT。

**WHAT（段 2 — Phase 1 tracer）**：以 TDD 实现独立 Simulation Host
（composition root，装配真实 Product Runtime modules）、Minimal Scenario
Bundle v0、ScenarioStimulus、ScriptedUniAgent（走真实外部 UniAgent seam 的
deterministic double）与 Kernel internal run driver 的最小 legal activation /
self-drive，覆盖五个确定性场景，验证 Runtime self-drive、UniAgent external
seam 调用纪律、现实 Effect 串行验证屏障、Trace 三臂等价与 seal/import 门、
  同一 Bundle 两遍 semantic digest 一致、CURRENT Product assemblies 无 Simulation
  编译依赖或已知功能引用。完整 Product Host dependency closure 留待 Product Host
  实际存在后验证。

**WHY**：ADR-0019 已裁决 self-drive 但 legal activation、internal run driver
与外部事件等待均未落地（协议 P18 Status 自注）；当前组合测试仍由测试代码逐
cycle 调用 Kernel 操作面；UniAgent 侧只有 terminal Goal Evaluation，无 semantic
decision seam。不先锁语义就实现，容易让 Simulator/Trace/UniAgent 取得第二份
产品 authority；不先用 tracer 证明就冻结 Interface，会把实现假设泄漏进产品契约。

## Scope

- 修订 `docs/architecture/product-architecture-baseline-l0-l3.md`：新增 narrow
  amendment（legal activation、internal run driver、Agent decision boundary、
  现实 Effect 串行验证屏障、恢复/UnknownOutcome、Agent Continuation、Grant 与
  Contract generation 分离、untrusted evidence 与 Human preemption、Host 隔离、
  Trace 异步与 seal/import 门；延续 §20 不变量编号）
- 修订 `docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`：
  narrow amendment（通则 12/13、新边 P24 Legal Run Activation / P25 Agent
  Decision Consultation / P26 Sealed Trace→ScenarioStimulus、Deferred ⑯/⑰
  状态注记）
- `CONTEXT.md` 增补已稳定术语（Legal Activation、ScenarioStimulus、
  Simulation Host、Product Host、ScriptedUniAgent）
- roadmap（`docs/design/runtime-perception-world-simulation-replay-roadmap-v0.1.md`）
  H2 在 Architecture Gate 通过后改为 ACCEPTED AS CHANGE INPUT（保持 Authority:
  NONE；不冻结 Interface/实现）
- 新增产品代码 `src/UniClaw.Kernel/Runtime/`：Kernel internal run driver 最小
  concrete seam（legal activation + self-drive + 串行验证屏障 + Agent decision
  boundary 消费）
- 新增 `tests/UniClaw.Simulation.Tests/`：独立 Simulation Host composition
  root、Minimal Scenario Bundle v0、ScenarioStimulus、ScriptedUniAgent、
  Scenario Importer（sealed-only）、五个确定性场景、两遍 digest、Trace 三臂、
  闭包/治理测试
- 新增 Product Host 依赖闭包测试（product 侧可执行执法）
- 更新本 state、status log、residual risks、verification 证据

## Out of Scope

- 冻结任何新公共 Interface（`Ixxx`）；Interface 形状、字段、namespace、
  transport、storage、timeout/retry policy 全部留待真实 buyer 证据
- 完整 R1（activation identity/correlation/并发/跨 Session 恢复）、Deferred ⑯
  完整 cancel/pause/resume/escalation vocabulary（Phase 1 cancel 经 contract
  声明的 SafeStop obligation + evidence-backed proof 表达）
- async Fast/Slow、页面 composer、完整 baseline governance、兼容矩阵、自动
  migration、tombstone、retention/redaction、在线 registry
- 非终态 supervision（Awaiting Supervision）与真实模型评测（Model Evaluation
  Replay）
- Agent Decision Context / Bounded Contingent Decision Package 的载荷与字段
  （TRACER_HYPOTHESIS，Phase 6）
- 提交 commit；修改/回退并行 dirty 文件（UAR-002 文档族、FSV-001 感知资产、
  `show-me-perception-arch-diff.html`、`docs/design/README.md`、UAR-002 对
  CONTEXT.md 的增补段）
- 把 ScriptedUniAgent 计作第三个 UniAgent realization 或以 deterministic
  replay 宣称 Agent 智力/模型质量/production readiness

## Decisions

分类词汇：`FROZEN_INHERITED`＝上位文档已锁；`HUMAN_SELECTED`＝本次 Human
授权锁定（落 canonical 文档）；`TRACER_HYPOTHESIS`＝需 tracer 证明；
`OPEN_GATE`＝留待后续裁决。

| # | 语义 | 分类 | 落点 |
|---|---|---|---|
| D1 | Runtime 可独立合法激活；完整 UniAgent realization 是产品级主要目标理解与发起对象 | FROZEN_INHERITED（ADR-0019/0022）+ HUMAN_SELECTED（独立激活语义写入 baseline §24.1） | baseline amendment |
| D2 | accepted Contract ≠ activation；激活后 Kernel internal driver self-drive；Host/测试/UniAgent 不逐 cycle 驱动 | FROZEN_INHERITED（ADR-0019、协议 0.1.11、P1 Non-Activation）；幂等/at-most-one 不变量 44 新增入 amendment | baseline §24.1/24.2、协议 P24 |
| D3 | UniAgent 拥有 Goal interpretation/global strategy/Contract authoring/Plan Hypothesis/Goal Evaluation；Control 独占 Tactical Hypothesis 与 Control Intent；Kernel 只 orchestration/lifecycle | FROZEN_INHERITED（baseline §3.1/§6/§14、ADR-0019）+ Plan Hypothesis 归属 HUMAN_SELECTED 写入 | baseline §24.2 |
| D4 | Kernel 只在语义 decision boundary 请求 UniAgent；UniAgent 返回 advisory proposal，不直接写 Run State/WorldBelief/Assurance/Binding/receipt/Effect/Outcome | HUMAN_SELECTED（外部语义）；载荷 TRACER_HYPOTHESIS（H14） | baseline §24.2、协议 P25 |
| D5 | 现实 Effect 严格串行：dispatch 后须 post-action accepted Evidence（P2/PostActionEffectFlow）+ reconciliation + verification 清偿才允许下一次现实 Effect；Observation 可 cheap/scoped/native 但必经 P2；receipt 不是现实证明 | receipt≠proof FROZEN_INHERITED（不变量 33/34、P15、ING-006 MaterialEffect 门）；串行屏障 HUMAN_SELECTED＝不变量 43 | baseline §24.3、协议通则 13 |
| D6 | Evidence Admission / Belief Relevance / Reconciliation / WorldBelief revision / 按需 Slice 派生分离；accepted Evidence 不保证 revision，revision 不自动 Slice | FROZEN_INHERITED（baseline §3.6/§11/§12、不变量 9/10/19、P4）；无需新写 | （不新增；复核通过） |
| D7 | Trace capture/persistence 异步、非权威、不入 critical path/恢复裁决；仅 drain/seal/integrity 完成的 artifact 可交 Importer；Simulation 只消费派生 ScenarioStimulus，永不把 Trace Event 当 command | 非权威 FROZEN_INHERITED（ADR-0013）；seal/import 门与 Stimulus-only HUMAN_SELECTED | baseline §24.9、协议 P26 |
| D8 | 恢复由 canonical owner records + 可靠 Checkpoint + Attempt/Obligation/Evidence refs + fresh observation 决定；UnknownOutcome 是恢复屏障，禁 blind redispatch | blind-redispatch 禁令 FROZEN_INHERITED（P15/DSE-001）；恢复裁决来源 HUMAN_SELECTED；Checkpoint 细节 OPEN_GATE | baseline §24.4 |
| D9 | Agent Continuation 只保存 cognition summary + canonical refs，不复制 Run/Control/Assurance/Effect state，不依赖 Host transcript/隐藏思维链/Trace replay | HUMAN_SELECTED（外部语义）；载荷 OPEN_GATE（Phase 6） | baseline §24.5 |
| D10 | Contract 预授权低风险 task-scoped capability；高影响 semantic Commit Point 用细粒度一次性 Grant；Human/Policy Authority 签发 Grant；UniAgent 基于 Grant author 新 Contract Proposal；Run Model admission 产生新 immutable Contract View generation；Grant 本身不直接授权动作 | HUMAN_SELECTED（细化协议 Deferred ② 方向，不与 baseline §17 冲突）；载荷/撤销/过期细节 OPEN_GATE（Phase 6/H15 后续裁决） | baseline §24.6 |
| D11 | 页面/app/消息/网页内容都是 untrusted Evidence，不得改变 Goal/Contract/Offer/Grant/系统约束；Human manual input/app switch 优先抢占，失效 execution lease/package，停新 Effect | HUMAN_SELECTED；检测机制 OPEN_GATE（Phase 6/7） | baseline §24.7 |
| D12 | Product Host 与 Simulation Host 是不同 composition root，装配同一 Product Runtime artifact；Product Host 依赖闭包不含 ScenarioStimulus consumer/Replay/Oracle/Importer | HUMAN_SELECTED；executable Product Host 本身尚不存在——Phase 1 以产品程序集闭包可执行执法为最小 realization | baseline §24.8、closure test |
| D13 | Bounded Contingent Decision Package 外部语义（immutable advisory proposal；有限 DAG + typed tri-state Guard；非 Effect batch/Driver macro/第二 Runtime；单 active + 预算内 speculative；绑定 Goal/Contract generation/scope/assumptions/Offers/risk/max-effects/duration 而非 revision number；每动作仍 fresh Grounding/Assurance；Guard Unknown/失效/Human preemption fail closed 回 Agent boundary；Kernel 只入口校验；Control 只记 ref；Run Model 不存内容） | HUMAN_SELECTED（仅外部语义，不锁数据结构）；完整语义 TRACER_HYPOTHESIS（Phase 6）；Phase 1 不实现 Package | baseline §24.2 注记 |
| D14 | Phase 1 cancel：经 contract 声明的 SafeStop obligation + evidence-backed SafeStop proof 表达，不新增 terminal 分类、不新增协议词汇 | HUMAN_SELECTED（tracer realization 选择）；正式 lifecycle command protocol 仍 Deferred ⑯ | state（本行）+ 协议注记 |
| D15 | Phase 1 agent decision boundary：主/兄弟场景各恰一次 consult（initial observation 后、首个 Effect 前）；no-response/invalid/correlation mismatch fail closed | TRACER_HYPOTHESIS（由 tracer 证明后记录） | state + tracer tests |
| D16 | 观察输入：golden-run response JSON 经真实 FastPerception+LiveVisionStrategy（recorded adapter）；reviewed manifest state 以独立 claim + lineage 入 P2 | TRACER_HYPOTHESIS（tracer realization；不重跑模型） | state + tracer tests |
| D17 | 新 ADR：不新增。理由：self-drive/activation 属 ADR-0019 已决；其余按 task 分区落 baseline/protocol；Grant 链与 Package 的 ADR 资格留待 Phase 5/6 tracer 证据后按 archive skeleton §4 晋升门评估 | HUMAN_SELECTED（本 Change 记录理由） | state（本行） |
| D24 | 本轮 Human remediation：P25 Phase 1 只锁 schema、Decision/Run correlation、非空 target 与 AllowedEffects；capability/risk/budget、完整 Contract scope 校验因缺 owning model 延后 Phase 6。真实 async Trace persistence 延后 Phase 3；Phase 1 只证明 enabled/disabled/failing recorder 行为等价。完整 Product Host dependency closure 延后至 Product Host 存在；当前只证明 Product assemblies 的 CURRENT-truth 闭包。P26 integrity、single active driver、verification barrier、digest/registry/importer 代码修复已完成并待最终 Human 复审。 | HUMAN_SELECTED（本轮明确授权） | baseline/protocol/roadmap + 本 state |

## Assumptions

- ADR-0013/0019/0022、UniAgent realization baseline、perception provider
  baseline、UWorld L4 继续是上游权威；本 Change 只做 narrow amendment。
- baseline §23 的 L0–L3 横向结构不被重开：amendment 只新增 ADR-0019 明确留给
  R1 的最小 lifecycle/authority/不变量与 Phase 1 依赖项。
- 当前 cardinality `1 Session / 1 Goal / 1 Run` 不变。
- 并行 dirty 文件（UAR-002、FSV-001、perception 资产）不属于本任务；CONTEXT.md
  只追加新术语段，不触碰 UAR-002 增补块。
- golden-run-v1 资产（case-a-before / case-b-off / case-b-on + manifest）作为
  Minimal Scenario Bundle v0 的内容寻址输入。

## Alternatives

1. **把全部新语义写进新 ADR**：拒绝——违反分区规则且在 tracer 证据前预支
   ADR 资格（archive skeleton §4 晋升门要求先有 tracer）。
2. **让测试继续逐 cycle 编排 Kernel 并只在测试里模拟 self-drive**：拒绝——
   无法证明 ADR-0019 self-drive 可实现，Simulator 会成为第二 Runtime（G2/G11）。
3. **为 cancel 新增 terminal 分类或 DispatchResult 第四态**：拒绝——P15/P16
   词汇已锁；SafeStop obligation + evidence-backed proof 足以表达。
4. **Scenario 直接消费 Trace Event 或运行中 Trace**：拒绝——违反 ADR-0013 与
   H7；只有 sealed artifact 经 Importer 派生 Stimulus。
5. **在产品侧新建 IUniAgentDecisionService 等公共 Interface**：拒绝——单
   adapter + 无完整失败语义证据；用最小 concrete delegate seam，Interface
   冻结留待 Phase 6。
6. **重跑感知模型推断 off/on**：拒绝——Phase 1 明确不重跑模型（锚定 response
   JSON + reviewed state claim）。

## Owner-Authority impact

- 不新增 Product Authority class；六个 L2 Owner 不变。
- Kernel 只新增 activation latch 与 run-driver 编排（composition/lifecycle
  coordination，协议通则 10 允许域）；canonical Run State 仍只经 Run Model
  typed transition。
- UniAgent 不因 ScriptedUniAgent 取得任何新 authority；double 只验证调用纪律。
- Simulation Host / ScenarioStimulus / Importer 全部非权威、test-side。

## ADR refs

- ADR-0013（Trace 非权威）、ADR-0019（self-drive）、ADR-0022（双 realization）
  继续有效；本 Change 不新增/supersede ADR（D17）。

## Acceptance

1. 架构锁定：baseline/protocol narrow amendment + CONTEXT 术语落位；每项新语义
   有唯一 Owner、明确 lifecycle、fail-closed 行为；与现有基线/CONTEXT/协议/ADR
   无语义冲突（DocsMetadataTests + 复核）。
2. Architecture Gate 通过后 roadmap H2 改为 ACCEPTED AS CHANGE INPUT（保持
   Authority NONE、Status 无 FROZEN/CLOSED 字样）。
3. tracer：测试 runner 对 Runtime 的 per-cycle 调用为零（只有 AdmitContract 一次、
   Activate 一次、Drive 一次）；无 parallel Runtime FSM；无 expected owner state
   注入 actual path。
4. 五个确定性场景全部 GREEN：
   S1 off→一次授权 Effect→post-action on→verified terminal（exactly one Effect）；
   S2 already-on→terminal（zero Effect）；
   S3 duplicate activation→只有一个 Primary Run（RunId 不变、effect/outcome 不重复）；
   S4 missing post-action Stimulus→合法 waiting 或 fail closed（非 terminal
   success、无第二次 Effect）；
   S5 waiting→cancel→late Stimulus→取消后 zero late Effect、late input 不恢复 Run。
5. 主场景全链真实：accepted Contract→legal activation→initial Stimulus→真实
   Evidence Ledger/World Model/Control/Grounding/Assurance→ScriptedUniAgent
   semantic decision→一次 deterministic EffectDriver delivery→post-action
   Stimulus→真实 Evidence/reconciliation/verification→Outcome Proof→Runtime
   Outcome→Goal Evaluation（真实 UniAgent.Evaluate）。
6. ScriptedUniAgent 走与真实 UniAgent 相同的外部 seam；验证调用边界/顺序/次数/
   correlation；no-response fail closed；不调用 live model。
7. missing/unexpected/duplicate/unconsumed Stimulus fail closed；receipt 不触发
   成功（MaterialEffect 只认 PostActionEffectFlow Observation）。
8. Phase 1 仅要求 Trace enabled/disabled/recorder-failure 三臂 canonical output
   等价；真正后台 async persistence 延后 Phase 3，不得宣称本阶段已实现 async。
   未 sealed 或 integrity unknown 的 artifact 不能导入（Importer fail closed）。
9. 同一 Minimal Bundle 连续运行至少两次 semantic digest 完全一致。
10. 在 Product Host 尚不存在期间，只证明 CURRENT product assemblies 无
    Simulation/Replay/Oracle/Importer 的编译依赖或已知功能引用；完整 Product
    Host dependency closure 延后至 Product Host 存在后验证，不以当前测试冒充。
11. 记录 critical-path latency、ModelCalls、Observations、Reconciliations、
    Regrounds、VerificationMode、InputTokens；ScriptedUniAgent 不适用项显式记
    N/A。
12. 不冻结新公共 Interface；core-model archive 保持 New core Interface
    entries: NONE；不宣称两真实 adapter / Agent 智力 / production readiness。
13. 完整 solution regression GREEN；git diff --check 干净；精确路径 git status
    只含授权改动。

## Constraints

- TDD（RED→最小实现→GREEN→Refactor）；先架构 Gate 后 tracer 实现。
- 保留并行 dirty 工作；使用精确路径；无 destructive git 操作；不提交 commit。
- 产品代码（src/）不得引用/包含 Simulation 类型；Simulation 只在
  tests/UniClaw.Simulation.Tests。

## Residual risks

- **P25 seam 单 adapter**：UniAgent decision seam 仍只有 ScriptedUniAgent 一个
  真实消费者；Codex/DSH realization 未接入——不足以冻结 Interface。
- **P25 capability/risk/budget 与完整 Contract scope 入口校验显式 deferred**：无
  owning 模型（Capability Plane/Grant 语义 = Phase 6）；Phase 1 只锁 schema、
  Decision/Run correlation、非空 target 与 AllowedEffects，已以注记+state 记录，
  不伪造检查。
- **Agent decision boundary 仍只有 InitialPlanning 单边界**；re-plan/多轮
  consultation/预算语义未证。Steps 是有界有序列表，不是 Decision Package
  （DAG/Guard/lease = Phase 6）。
- **Trace async 延期（本轮 Human 已授权）**：Phase 1 只证明 enabled/disabled/
  failing recorder 行为三臂等价 + 故障全吸收 + sealed-only import；真正后台
  异步持久化 writer 延后 Phase 3。
- **cancel 经 SafeStop obligation 表达**仍是 Phase 1 realization（D14）；
  正式 lifecycle command protocol（Deferred ⑯）出现后应替换。
- **sim World realization choices**：occurrence carry-over、relevance scope =
  contract scope、reviewed manifest 作为 state 语义真值（双源证据已在代码
  doc 与本 state 如实表述）——Phase 4 真实 perception 语义接入时重估。
- **importer 派生的 context/virtual time 取自 source bundle 声明**：trace
  贡献 sealedness/correlation/顺序/presence；无 canonical clock（Deferred ⑪）
  下 time 不可能来自 trace 本身——Phase 3 bundle 治理再演进。
- **closure 测试为 CURRENT-truth 口径**：证明产品程序集当前无 Simulation
  功能/标识/编译依赖（InternalsVisibleTo 恰为两个测试程序集）；不是对未来
  Product Host 完整依赖闭包的证明（无 Product Host 存在，D23）。
- **Freshness 用 scripted-sufficient double**；真实 freshness policy（Deferred
  ④）未接入，VerificationMode 如实标注。
- **Runtime artifact hash 以当前加载 DLL 计算**；跨发布管道 reproducible-build
  绑定是 Phase 7 事项。AppBuild 为显式 sentinel "not-recorded"（资产不含）。
- R1 完整语义（activation identity/并发/跨 Session 恢复）与 Deferred ⑯⑰
  保持 OPEN_GATE，未因 tracer 绿灯而关闭。

## Verification

```yaml
verification:
  level: CONTRACT + DETERMINISTIC + SCENARIO
  method: >
    dotnet test 全三测试项目（产品回归 + tracer 场景）；DocsMetadataTests；
    ProductHostClosureTests；git diff --check；精确路径 git status；
    PerCycleZeroDisciplineTests 源码扫描
  expected: >
    Acceptance 1–13 全满足（见上）：架构锁定无冲突、H2 改 ACCEPTED、
    五场景各自语义不变量、两遍 digest 一致、Trace 三臂等价、
    sealed-only import、Product Host closure、metrics N/A 显式、
    零新公共 Interface、全绿且无未授权改动
  actual: >
    （remediation 后 2026-09-13 二轮）Kernel.Tests 382/382（DocsMetadata 4、
    Closure 3 升级为 InternalsVisibleTo 边界执法、RunDriver 14：gate 共享/
    steps 校验×4/DecisionId 确定性/waiting-resume）；Agent.Tests 17/17；
    Simulation.Tests 50/50（S1–S5 含 S5 真·两段式 waiting→cancel→late、
    single active driver、Assurance post-action checks/contradictory desired-state、
    两步屏障×2 证伪面、importer 真 derive→re-drive 闭环 + Quarantined/
    cross-scenario 负向、Trace IntegritySha256 tamper fail-closed、repeated
    artifact occurrence 保留、instance BundleAssetRegistry、EntityScope/
    TargetFramework digest、Steps max16、late-call、duplicate-Submit；同程序集
    只读投影与源码纪律门）；共 449；git diff --check 干净；
    精确路径 status 仅含授权改动清单
  evidence: >
    可复现命令：
    dotnet test tests/UniClaw.Kernel.Tests
    dotnet test tests/UniClaw.Agent.Tests
    dotnet test tests/UniClaw.Simulation.Tests
    git diff --check
    git status --short -- src/ tests/ docs/architecture CONTEXT.md changes/RFS-001 UniClaw.Kernel.slnx
    逐项四元组（method · expected · actual · evidence ref）：
    · 架构 Gate | DocsMetadataTests 4/4 + 人工复核无语义冲突 | 4/4 PASS，baseline §24/protocol §7/CONTEXT 五术语/H2 落位 | dotnet test --filter DocsMetadataTests
    · activation 幂等/fail-closed | KernelRunDriverTests 7 断言组 | 7/7 PASS | tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs
    · S1 off→effect→on | exactly one Effect + verified terminal + goal Satisfied | PASS（两遍）| DeterministicScenarioTests.S1_*
    · S2 already-on | zero Effect + Completion | PASS | DeterministicScenarioTests.S2_*
    · S3 duplicate activation | 单一 Primary Run/RunId 不变/outcome 恰一次 | PASS | DeterministicScenarioTests.S3_*
    · S4 missing post-action | WaitingForInput + 非终态 + 无第二次 Effect + receipt 不成 proof（OutcomeProofLog 空）| PASS | DeterministicScenarioTests.S4_*
    · S5 cancel→late | SafeStop + 零 late Effect + late 不复活（AlreadyTerminal）| PASS | DeterministicScenarioTests.S5_*
    · digest 两遍一致 | 同 bundle 两 run digest 相等；异 bundle 不同 | PASS | TraceArmsAndDigestTests
    · Trace 三臂 | enabled/disabled/failing canonical 等价 | PASS | TraceArmsAndDigestTests
    · sealed-only import | Quarantined 拒绝 / Finalized 接受 | PASS | TraceArmsAndDigestTests
    · no-response / ctx-mismatch | AgentDecisionFailed/UnexpectedInput + 零 Effect + 非终态 | PASS | FailClosedScenarioTests
    · metrics N/A | ModelCalls/InputTokens 显式 N/A 非 0 | PASS | FailClosedScenarioTests
    · per-cycle 零 | 测试与 runner 源码无 Process/SelectIntent/Act/EvaluateTerminal 调用；runner 恰一次 Drive | PASS | PerCycleZeroDisciplineTests
    · Product Host closure（CURRENT-truth） | 当前产品程序集零 Simulation 引用/标识 | PASS | ProductHostClosureTests 3/3
```

## Status log

- 2026-09-12 · enter→understanding · 汇总用户提出的快慢感知、容器内操作记录、异常裁决、Simulation、资产回放、Trace 与独立核心文档要求
- 2026-09-12 · understanding→resolving · 形成候选核心模型与接口；D1–D5 等待 Human 审阅，不进入实现或架构 adoption
- 2026-09-12 · resolving · 文档 metadata/whitespace/路径范围检查通过；Human Gate 未裁决，保持 resolving
- 2026-09-13 · resume·reauthorizing · Entry/Resume 复核：HEAD 40f190c（base 后两个 FSV-001 docs commit，无冲突）；RFS-001 原 design-only 授权与本任务不一致，Human 重新授权为「架构锁定 + Phase 1 tracer」两段串行任务；保留原历史，修订 Intent/Scope/Out of Scope/Decisions/Acceptance；识别并行 dirty（UAR-002 文档族、FSV-001 资产）为不可触碰
- 2026-09-13 · resolving·architecture-review · repository-grounded review 完成：17 项语义逐条分类（D1–D17）；确认无必须由 Human 决定的真实冲突（cancel 经 SafeStop obligation 表达；Grant 链细化 Deferred ② 不冲突）；zero-new-ADR 决策（D17）
- 2026-09-13 · resolved·architecture-gate · 落位：baseline §24 narrow amendment（不变量 43–47）、protocol §7（通则 12/13、P24/P25/P26、Deferred ⑯⑰②注记）、CONTEXT 五术语、roadmap H2 → ACCEPTED AS CHANGE INPUT；DocsMetadataTests 4/4、git diff --check 干净、路径复核仅含授权改动；Gate 通过，进入 PLAN/IMPLEMENT（tracer）
- 2026-09-13 · planning→implementing · tracer 垂直切片：产品侧 src/UniClaw.Kernel/Runtime/（KernelRunDriver 最小 concrete seam：activation latch + self-drive + 串行屏障 + P25 agent seam 消费 + AgentPlanPolicy）；sim 侧 tests/UniClaw.Simulation.Tests/（Bundle v0 + ScenarioStimulus + StimulusFeed + ScriptedUniAgent + RecordedVisionAdapter + DeterministicEffectDriver + ScenarioRunner + SemanticDigest + ScenarioImporter + 五场景/两遍 digest/Trace 三臂/闭包测试）；TDD RED→GREEN
- 2026-09-13 · implementing·checkpoint·PAUSED · Human 要求暂停。已完成：Slice 1（产品侧 run driver）GREEN——`tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs` 7/7（activation fail-closed/幂等同 RunId、Drive before activation、no-response/correlation/effect-class fail-closed、cancel 无 SafeStop obligation fail-closed）；实现 `src/UniClaw.Kernel/Runtime/{AgentDecision,RunDriverInputs,AgentPlanPolicy,KernelRunDriver}.cs` + UniKernel 只读观察面（RunView/RunId/IsRunTerminal/RunState/EffectReceipts）。回归：Kernel.Tests 371/371、Agent.Tests 17/17（Simulation.Tests 未入此轮——in-flight）。In-flight：sim 基础设施首文件 `tests/UniClaw.Simulation.Tests/ScenarioStimulus.cs` 已写（ScenarioStimulus/Feed/ScenarioPerceptionAdapter），引用尚未存在的 BundleAssets 等类型——Simulation.Tests 当前不可编译（预期中 RED 中间态）。已完成骨架：csproj（引用同一 Kernel+Agent artifact）+ slnx 注册。Resume 顺序：(1) BundleAssets/MinimalScenarioBundle（golden-run 资产加载+内容寻址+integrity fail-closed）；(2) ScriptedUniAgent + DeterministicEffectDriver + sim World strategies（seed association + live.frame occurrence w/ state）；(3) SimulationHost/ScenarioRunner（一次性 Admit+Activate+Drive+真实 UniAgent.Evaluate）；(4) SemanticDigest；(5) S1 主场景 RED→GREEN；(6) S2–S5；(7) 两遍 digest/Trace 三臂/importer/闭包/metrics；(8) Verify 收尾。禁改并行 dirty（UAR-002/FSV-001）仍有效
- 2026-09-13 · resumed·delegating · Human 指示采用 Leader 决策/设计 + SubAgent 编码分工。Leader 已完成 seam 锁定：`ScenarioStimulus.cs`（join 规则最终化：reviewed elements 恒产 occurrence，perception 检测供 locator 证据；center-containment 匹配）+ `SimContract.cs`（MinimalScenarioBundle v0 全字段、BundleAssets 内容寻址注册表、RunOptions/TraceArm、ScenarioReport/ScenarioMetricsSnapshot、ScenarioBundleDigest canonical 帧）——Simulation.Tests 骨架编译 GREEN。委派进行中：Agent-1（sim 基础设施：GoldenScenarioBundles 四工厂 + ScriptedUniAgent + DeterministicEffectDriver + WorldStrategies + SimulationHost + ScenarioRunner + SemanticDigest + ScenarioImporter + 单测 + S1 smoke）；Agent-1b（Kernel.Tests/ProductHostClosureTests 三测）。后续 Agent-2（五场景/两遍 digest/Trace 三臂/per-cycle 零扫描/metrics N/A 断言）待 Agent-1 完成后串行委派；REVIEW/Verify 由 Leader 执行
- 2026-09-13 · review·agent-1+1b · Agent-1b 完成：ProductHostClosureTests 3/3（Leader 第一手复核通过；Kernel.Tests 374/374）。Agent-1 完成：Simulation.Tests 18/18（Leader 第一手复核通过；含 S1 端到端 smoke——真实 L2 + 一次性提交 + self-drive 全链 GREEN）。Leader 裁决两项 deviation：(1) S5 contract Scope 增补 `run.cancel-requested`——语义必然（relevance scope 来自 contract scope，cancel claim 必须在 scope 内才能进入 WorldState 支撑 SafeStop proof），批准；(2) ReplayFrameObservationStrategy 对非 live.frame record 携带 previous occurrence 景观重提议——IUiObservationStrategy 是 owner-internal 确定性 seam，previous 是合法输入，不违反 UWM-009 §35「替换不继承」模型语义（模型仍替换；owner 侧策略选择重申），批准并记录为 sim realization choice。Agent-2 已委派（DeterministicScenarioTests S1–S5 + TraceArmsAndDigestTests + FailClosedScenarioTests + PerCycleZeroDisciplineTests）
- 2026-09-13 · verify·CLOSED · Leader VERIFY 全绿：Kernel.Tests 374/374（DocsMetadata 4 + Closure 3 + RunDriver 7）、Agent.Tests 17/17、Simulation.Tests 31/31（S1–S5 + digest 两遍 + Trace 三臂 + sealed-only import + no-response/ctx-mismatch fail-closed + metrics N/A + per-cycle 零扫描）；git diff --check 干净；精确路径 status 仅授权改动（CONTEXT/baseline/protocol/UniKernel/slnx M；RFS-001、roadmap、src/Runtime、Kernel.Tests 新测试、Simulation.Tests untracked）。Agent-2 三项 adaptation 复核批准（TerminalClassification namespace、NoResponse unconsumed=1——初始观察先于 consult 消费、ContextMismatch unconsumed=2——mismatch 不出队）。Acceptance 1–13 全满足；residual risks 与四元组已落档。交付：架构锁定（D1–D17）+ Phase 1 tracer（五场景）+ seam 证据；未冻结新公共 Interface；未提交 commit；并行 dirty 未触碰
- 2026-09-13 · review·REJECTED→resuming · Human Review 不通过（Standards 4×P1+1×P2；Spec 5×P1+3×P2），接受全部结论，退回 RESOLVE。核实项：P26 importer 只做 sealed 门未派生 Stimulus；bundle digest 遗漏 Stimulus 内容/Contract/Goal/expectation/producer/TargetUiSystem 全量；P25 入口校验只有 correlation+EffectClass；activation latch 是 per-driver 实例态非 Run/Kernel 级；closure 测试只能证明「当前无已知标识」却声称完整闭包；PerCycleZero 源码扫描可绕过；BundleAssets 全局可变注册表；Runtime 类型全 public 事实形成公共 Interface surface（违背 Acceptance 12 精神）；Trace async 未实现却以完成口径交付；S5 一次 Drive 直接消费 cancel、从未真实进入 Waiting；roadmap H2 三处陈旧矛盾（§16 自审/Phase 0 Gate/§17 摘要仍称 REQUEST_CHANGES）；主场景 state claim 来源表述失真；串行屏障无第二 dispatch 路径不可证伪；ScriptedUniAgent 未覆盖 late/cancel 且 runner 未调 AssertDiscipline。测试数量属实但证明力不足，CLOSED 撤回。
- 2026-09-13 · resolving·rescope · 范围裁决（D18–D23）：D18 IN—P26 真 importer：sealed trace（经 FastPerception trace 注入携带 artifact refs）+ source bundle → 派生 versioned ScenarioStimulus → 重新喂入新 SimulationHost run 复现同一语义结果（import→re-drive 闭环）；sealed/integrity/run-correlation 全 fail closed。D19 IN—bundle digest 全字段 canonical（stimuli 完整内容/context/claims/virtual time、contract scope/effects/obligations、goal criteria、完整 expectation、producer identities、TargetUiSystem 全量、runtime artifact），逐字段 tamper 负向测试。D20 IN—activation gate 上移 UniKernel（composition/lifecycle coordination state，协议 0.1.10 允许域；KernelRunDriver 委托 kernel gate）；双 driver 单 kernel 负向测试。D21 IN—S5 改真两段式：Drive→WaitingForInput→feed.Submit(cancel+late)→再 Drive→SafeStop；late 保持 unconsumed；feed 增 Submit（dup id 拒绝）；PerCycleZero 放行 Driver.Drive/Activate（原文只禁 Process/SelectIntent/Act/EvaluateTerminal 四个 kernel 面）。D22 IN—串行屏障可证伪：AgentActionProposal 扩为有界有序 steps（非 DAG；Package 仍留 Phase 6），driver 逐步执行（每步 act→post-action evidence→下一步），新增两动作场景（toggle→menuItem）证明屏障放行已验证序列、且缺中间 evidence 时零第二 Effect。D23 IN—Runtime seam internal 化 + InternalsVisibleTo 仅 UniClaw.Simulation.Tests（显式 test seam 边界，closure 测试执法「恰好一次且只指向 sim test 项目」）；P25 校验扩展（phase/run-correlation/target 非空/decision kind 已知；capability/risk/budget 无 owner 模型，显式 deferred 记录理由）；runner 调 AssertDiscipline + double 记 late-call；读面改同程序集优选只读投影，源码纪律门保留，真正编译隔离需拆 assembly；closure 测试改称「当前无已知 Simulation 标识」诚实口径；roadmap H2 三处陈旧矛盾修复；主场景 state claim 来源如实表述（response-JSON occurrences + reviewed manifest claims 双源）。DEFERRED（本 RESOLVE 授权）：true async trace writer（Phase 3；行为三臂等价保留为 Phase 1 证据）；完整 dependency-closure 证明（无 Product Host 存在，测试只证当前真）
- 2026-09-13 · implementing·remediation · Agent-3 完成（Leader REVIEW 通过，一项否决）：产品侧四缺陷修复——UniKernel internal ActivateGate/IsActivated（gate 上移，双 driver 共享，测试 TwoDrivers_OverOneKernel_ShareActivationGate）；P25 校验扩展（DecisionId=decision-{RunId[^12..]}-1 run-correlated + empty-steps/missing-target-role/missing-effect-class/unknown-decision-kind 拒绝；capability/risk/budget 显式 deferred 注记）；AgentActionProposal→有界 Steps + Drive 可 resume 相位机（NeedInitialObservation/NeedDecision/StepAct/StepVerify/TerminalEvaluation，StepVerify=不变量 43 屏障且可证伪）；Runtime 全类型 internal 化 + InternalsVisibleTo 双测试程序集。Kernel.Tests 381/381。Leader 否决其「MSBuild 属性拼接程序集名绕过 closure 扫描」手法（属 review 点名的字符串拆分绕过气味），csproj 已改回显式 `<InternalsVisibleTo Include="UniClaw.Simulation.Tests" />`——closure 测试将由 Agent-4 升级为解析 InternalsVisibleTo 条目并执法集合恰为两个测试程序集。roadmap H2 三处陈旧矛盾已由 Leader 修复（REQUEST_CHANGES 残留 0）。Agent-4 已委派：D18 真 importer（trace 注入→sealed artifact refs→派生 stimuli→re-drive 闭环）、D19 digest 全字段+逐项 tamper、D21 S5 两段式+Feed.Submit+Host.DriveOnce/SubmitStimulus、D22 两步屏障场景（含缺中间 evidence 臂）、KernelFacts 编译级 facade、runner AssertDiscipline+late-call、closure 升级、TargetUiSystem 增 AppBuild 显式 sentinel、双源证据表述
- 2026-09-13 · review·remediation·agent-4 · Agent-4 完成（Leader REVIEW 通过）：Simulation.Tests 46/46。关键真实性抽查：TwoStepBarrierTests 双臂成立——happy path 2 effects 证明屏障放行已验证序列（排除「结构性无第二 effect」解释），missing-middle 臂证明 step2 就绪仍被阻塞（1 effect + Waiting）后 cancel→SafeStop 仍 1 effect；ScenarioImporter.Derive 真派生（Finalized 门 + sim:ScenarioId correlation 门 + trace 序 artifact refs + 16-hex 前缀 fail-closed 映射 + re-versioned stimuli + resealed bundle）并经 re-drive 复现 Completion/1 effect；digest 全字段 canonical + 20 mutation 全部改变 digest；S5 真·两段式（PhasedPending → SubmitStimulus(cancel+late) → DriveOnce → SafeStop，late 保持 unconsumed）；KernelFacts 编译级读面替代字符串扫描主执法；closure 测试升级为 InternalsVisibleTo 集合执法（恰两个测试程序集）+ CURRENT-truth 诚实口径；AppBuild sentinel not-recorded；双源证据表述落入 doc comments。deviation 采纳：TwoStepMissingMiddle 的 Expected 钉等待中状态、终态逐项断言（设计如实）；CheckDiscipline 非抛出聚合 + AssertDiscipline 抛出包装分层
- 2026-09-13 · verify·awaiting-human-review · 二轮 Leader VERIFY 全绿：Kernel.Tests 381/381 + Agent.Tests 17/17 + Simulation.Tests 46/46（共 444）；git diff --check 干净；精确路径 status 授权清单内（uniagent-realization-baseline 为 UAR-002 并行 untracked，非本 change）。review 13 项发现闭环：11 项修复（P26 真 importer、digest 全字段、P25 校验、kernel 级 gate、closure 诚实口径、internal seam、S5 两段式、H2 三处、state claim 双源表述、屏障证伪、discipline 补全）+ 2 项经 RESOLVE 授权延期（trace async writer→Phase 3；完整 closure 证明→无 Product Host 存在，CURRENT-truth 口径）。lifecycle 置 verifying/awaiting-human-review，不自封 CLOSED
- 2026-09-13 · verified·awaiting-human-closure · 本轮复核证据更新：Kernel.Tests 382/382、Agent.Tests 17/17、Simulation.Tests 50/50，共 449；新增 single active driver、Assurance post-action checks/contradictory desired-state、Trace IntegritySha256 tamper fail-closed、repeated artifact occurrence、instance BundleAssetRegistry、EntityScope/TargetFramework digest、Steps max16 证据。KernelFacts 编译级 facade 表述删除，改为同程序集优选只读投影 + 源码纪律门；真正编译隔离仍需拆 assembly。Product Host 口径统一为 CURRENT product assemblies。代码修复项已完成，等待 Human closure；本日志不构成 CLOSED。
- 2026-09-13 · closed·human-closure · Human 授权关闭 RFS-001。基于本 Change 已记录的 382/382、17/17、50/50（共 449）验证结果及 D18–D24 remediation 证据，状态更新为 `closed · disposition: none`。保留 Phase 3 async Trace persistence、Phase 6 owning-model authorization checks、未来 Product Host 完整 dependency closure 等明确延期项；本 Change 不宣称这些延期项已完成。
