# Runtime · Perception · World Model · Simulation · Replay · Trace · UniAgent 路线图 v0.1

> DocumentType: `RUNTIME_PERCEPTION_WORLD_SIMULATION_REPLAY_ROADMAP_V0_1`
>
> Status: `DRAFT / ACCEPTED_AS_CHANGE_INPUT（H2，2026-09-13）`
>
> Authority: `NONE`
>
> Date: `2026-09-13`
>
> Scope: `analysis and design only`
>
> Review Gate: `H2 ACCEPTED AS CHANGE INPUT（2026-09-13，RFS-001 Human 授权 + repository-grounded architecture review 通过）: 本路线图作为 Change State / narrow amendment / Phase 1 tracer 的设计输入；不等于 Interface 或实现已冻结（Document Authority 仍为 NONE）`

---

## 0. 审阅结论先行

当前 Target 已经分别建立了 Evidence admission、WorldBelief revision、Container / Slice、Control、canonical binding、Assurance、Effect receipt、terminal Outcome、Goal Evaluation 与非权威 Trace 的可执行片段；真正阻止完整闭环的不是“接口不够多”，而是三组尚未闭合的语义：

1. **Run lifecycle 缺口**：ADR-0019 已裁决“合法激活后由 Uni Kernel self-drive”，但合法激活协议、Kernel internal run driver、外部事件等待与非终态监督恢复尚未落地；当前组合测试仍由测试代码逐步调用 Kernel 操作面。
2. **异步观察缺口**：当前产品面是同步 `FastPerception.Observe(RawArtifact) → ObservationProposal[]`；没有冻结的观察会话、渐进结果、乱序/重复/迟到、partial/failure、Fast 被 Slow 纠正等语义。
3. **验证资产缺口**：已有内容寻址资产、corpus doubles、live/corpus parity、World Model test-only canonical Oracle、golden hashes 与闭环 twin，但它们尚未形成统一的 Replay asset lifecycle、完整 Container 语义 Ground Truth、Scenario Baseline 晋升机制或“同一真实 Runtime 自驱”的确定性 Replay runner。

因此建议的第一步不是冻结新 `Ixxx`，而是用现有 golden-run 的 `off → effect → on` 资产完成一个**不重跑模型、由 Kernel 内部自驱、外部只提供版本化 ScenarioStimulus**的 tracer bullet。该 bullet 先证明 lifecycle、Agent seam 与 Simulation substrate，再让实际 buyer 反推最小 seam。

### 0.1 Stateful Grill 收口 `[HUMAN_SELECTED]`（尚未取得架构 Authority）

本路线图已经完成针对 Runtime、Perception、World Model、Simulation、Replay、Trace 与 UniAgent 的 Stateful Grill。下列选择可作为后续 Change / tracer 的设计输入，但由于本文 `Authority: NONE`，凡与冻结基线发生张力的内容仍须走正式 Human Gate / ADR / protocol amendment，不能由本文直接覆盖上位语义。

| 主题 | Human-selected 方向 | 不得被误读为 |
|---|---|---|
| Product 入口 | Runtime 可被独立激活；产品级主要发起与目标理解对象是完整 UniAgent realization | Runtime 只是 UniAgent 内部工具，或 UniAgent 逐 cycle 驱动 Kernel |
| Agent 自主性 | `强 UniAgent，窄 Runtime Kernel`：UniAgent 自主理解目标、形成/修订 Goal-level strategy 与 Plan Hypothesis、选择能力、判断换路/求助与 Goal Evaluation；Kernel 只在有真实决策需求时请求 Agent | Runtime 用固定流程替 Agent 规划，Agent/Host 逐 cycle 驱动 Kernel，或 Agent 绕过证据/授权/effect |
| Agent / Control 分工 | Kernel self-drive 是 orchestration/lifecycle authority，不是 intelligence authority；UniAgent author Goal-level strategy proposal，Control Loop 继续独占 Tactical Hypothesis 与 Control Intent | 把 Tactical Hypothesis 复制给 UniAgent，或让 Kernel internal driver 变成固定业务 planner |
| 批量决策 | UniAgent 可一次产生有界、条件式 Decision Package；Kernel 每次验证后可继续消费同一 package，只有 Guard 无法本地裁决、关键假设失效、需要新 Goal-level strategy/Grant 等 decision boundary 才再次请求 Agent | 一次下发多个未验证 Effect、线性坐标脚本、通用循环程序或多个 active plan 竞争 |
| Observation | Runtime/Control 按 scope、purpose、budget 请求观察；Perception 自主选择 Fast、Fast→Slow、Slow-only 或 full-model realization | Runtime 顶层暴露 Fast/Slow 两条管线或两份 belief |
| Slice / 展示 | Slice 绑定单一 WorldBelief revision；同 revision 的 compatible Slice 可组成 non-authoritative World Presentation；请求显式选择 `ContentOnly` 或 `WholeContainer` coverage | 跨 revision 拼接 current truth，或截图展示成为 semantic proof |
| Region | 影响 Runtime 决策的 region 语义必须成为 evidence-backed WorldBelief claim；仅展示用 region 保持非权威 | Container 负责 sidebar/content/topbar 分类，或 presentation metadata 反写 world |
| 原子动作 | v0 中现实 Effect 严格串行；每个实际尝试有独立 immutable identity 与 verification obligation，未对账前不产生下一个现实 Effect。高层 bounded subgoal 可由 Control 执行，但不得在 Driver 内隐藏多步宏 | Driver/Runtime 隐式重试、复用旧 binding/authorization，或以“批量计划”为名打包多个副作用 |
| Effect 预期 | `DesiredState` 用于可适用动作的 pre-dispatch satisfaction；非探索性动作建议必须携带 `EffectExpectation`。Runtime 选择最便宜但合法的 post-action Observation，仍经 P2/P3 与 Assurance；低风险探索可显式 unknown，高风险动作必须可验证 | 每个动作都做完整截图/VLM、receipt/native callback 直接证明 Effect，或批次结束才统一验证 |
| Wait | `Wait` 可是时间原语；`WaitForCondition` 是 Runtime/Control 的 bounded coordination policy，通过调度、再观察与 evidence reconciliation 实现 | Driver 内部轮询或等待条件直接证明 Effect/Goal |
| Effect 不一致 | Assurance 形成 `Verified / NotObserved / Contradicted / Indeterminate` 类别的判断；Runtime 只能按预算补观察，最终由 UniAgent 决定新方案 | 自动重复 Effect，或首次截图不同就直接终止 Run |
| 恢复 | canonical Product state 与可靠 Checkpoint 决定恢复位置；Trace 只提供异步诊断 refs。重启后先解析 owner records、reconcile / fresh observe，再由 UniAgent 重评计划 | Checkpoint 之后的 Effect 一定未发生、从 Trace 重建 canonical state，或恢复旧 token 现场后直接续发动作 |
| Unknown Effect | crash window 中可能已投递的 Effect 保持 `UnknownOutcome`，是恢复屏障；不得 blind redispatch | 以旧 Checkpoint 自动重做或以“可能已发出”默认成功 |
| 非终态监督 | 无安全本地方案但仍可能恢复时进入 Awaiting Supervision；新 accepted Evidence 可自动解决 case，terminal 后的 late resolution 不恢复 Run | 一遇异常就 terminal，或 UniAgent 直接 patch Runtime state |
| Capability | Catalog 描述系统可能能力；每个 case / Contract generation 只暴露有界 Offer。UniAgent 可请求扩权，但不能自授；Human / Policy 提供 grant，Run Model 只 admission | 白名单只有粗粒度开关，或模型自由调用任意真实工具 |
| Contract revision | supervision 期间可提出新的 immutable Contract version；同 Run 继续需要显式 re-admission / new Contract View generation，旧 View 和历史行为不改写、权限不追溯 | accepted View 原地修改；该项仍需正式澄清冻结基线 H15 |
| Compensation | compensation 是新的 Proposal / EffectAttempt，必须重新 Grounding / Assurance / verification，并保留原 Effect 历史 | 逻辑回滚或从 Trace 删除原动作 |
| Simulation Host | Simulation 是额外、独立的 Host / composition root；Product Host 的依赖闭包不包含 ScenarioStimulus consumer、simulation adapter、Oracle 或资产驱动入口 | Product Host 中的 `simulation=true` 功能开关 |
| Runtime 复用 | Product Host 与 Simulation Host 装配同一 Product Runtime modules；发布证据绑定精确 Runtime artifact/hash | 复制一套 Simulator FSM，或以相同 semantic version 推断 artifact 等价 |
| Trace / Stimulus | Trace 始终只是异步、非权威历史投影；完成 drain 与完整性检查后形成 sealed artifact，再由 Scenario Importer 派生 immutable `ScenarioStimulus`。Simulation 消费 Stimulus，不消费 Trace Event 作为命令 | Trace 落盘阻塞 Runtime、运行中的不完整 Trace 被 Replay 消费，或 Trace 在任何 Host 取得 control authority |
| Agentic Simulation | Codex-backed UniAgent 可探索多条合法路径；按安全不变量、目标结果、预算和 Trace 完整性验收，不锁定动作序列 | 单一 golden click path，或只看最终成功忽略过程 |
| Asset | 内容 hash、运行 occurrence、Bundle provenance 与 compatibility judgment 分离；关键 UI system/runtime/app/build metadata 必填且显眼 | 路径/文件名作为 identity，或所有 metadata 粗暴混入 content hash |
| Baseline | Capture、Asset Governance、Assertion、Human Promotion authority 分离；兼容范围与 schema migration 版本化，Runner 只计算不自扩范围 | 一次 replay green 自动晋升或原地覆盖历史资产 |
| 发布 | Model Evaluation、Deterministic Runtime Replay、Agentic E2E 与 Product Host readiness 分层给证据 | Simulation green 自动等于 production-ready |
| Agent 操作视角 | Agent 通过有界多模态 Decision Context 获得当前截图/必要视觉、专用 World/Run views、Capability Offers/Grant、最近已验证 Effect 与 pending refs；后续 `Anchor + Delta First` | 只凭截图和 transcript 猜状态，或把完整 canonical stores 塞成 God Context |
| 手机授权 | Contract 预授权任务范围内低风险能力；高影响 Effect 在语义 commit point 取得 destination/payload/scope 绑定的一次性 Grant。Human 输入优先抢占 Product Host | 每步确认、一次授予整机控制，或把权限弹窗按钮本身当授权 |
| Trace 性能 | Trace capture/persistence 异步且故障隔离；只有 sealed Trace 才能进入 importer。Trace lag/drop 只能影响诊断完整性，不能改变 canonical output | 关键 Effect 等待 Trace fsync，或用 Trace 补齐 Product state |
| Latency | 从 Phase 1 起把端到端 critical path 与结构计数作为 graduation evidence；先建立场景基线与 regression budget，不提前冻结一套全局毫秒阈值 | 到 Phase 7 才发现调用形态过慢，或为降延迟跳过 Authority/Evidence |

### 0.2 Epistemic status 词汇

本文所有可执行约束块、Gap、Phase Acceptance 与 Human Gate 使用以下状态；同一段混合多种状态时必须拆分，不能仅靠“必须/不得”的语气推断 Authority：

| 状态 | 含义 |
|---|---|
| `FROZEN_INHERITED` | 直接继承上位 Architecture / Protocol / ADR；本文不能修改 |
| `HUMAN_SELECTED` | Stateful Grill 已选方向，但尚未取得上位 Authority 或实施授权 |
| `TRACER_HYPOTHESIS` | 需要 executable tracer 才能证明或推翻的调用/模型假设 |
| `PROPOSED` | 作者建议，尚未获得 Human 选择或证据 |
| `OPEN_GATE` | 仍需 Human、实验或上位 amendment 明确裁决 |

文档级 `Authority: NONE` 始终有效；`HUMAN_SELECTED` 也不等于 `FROZEN_INHERITED`。

## 1. 目的与非目标

### 1.1 目的

本路线图回答：在不改变既有 Owner / Authority 的前提下，如何逐步证明以下完整闭环可表达、可执行、可重放、可定位第一处分歧：

```text
Goal / Contract
→ legal Runtime activation
→ Observe / Perception
→ ObservationProposal
→ Evidence admission
→ WorldBelief reconciliation
→ Control / Grounding / Assurance
→ Effect delivery
→ post-action Observe / verification
→ Continue | Local Recovery | Awaiting Supervision
→ terminal Outcome Proof / Runtime Outcome
→ UniAgent Goal Evaluation
```

路线图按垂直切片推进，每阶段必须同时回答行为、最小模型、Runtime 接缝、Simulation / Replay、测试和证据，而不是先批量造模型或接口。

### 1.2 非目标

- 不修改 `CONTEXT.md`、ADR、Architecture Baseline、协议基线或任何冻结语义。
- 不修改 `src/`、`tests/`、`platforms/`、Harness、schema、routing 或现有资产。
- 不采用当前未裁决草稿里的接口名、字段或 namespace。
- 不决定 OCR / YOLO / VLM / LLM 的具体实现、数值阈值、prompt、训练、存储、transport、部署或 UI；只冻结待验证的指标结构与决策边界。
- 不让 Simulation、Replay、Oracle、Trace、UniAgent 或 Development Harness 取得第二份 Runtime / WorldBelief / Effect authority。
- 不宣称本路线图本身授权任何 Phase 实施。

### 1.3 本轮证据边界

- 文档整合 pin：branch `uni-harness`，HEAD `40f190c6af6a3143b48e576a8bf4051e1edc2e26`，整合日 `2026-09-13`。实现前必须重新 pin，不得把本值当未来发布 identity。
- 工作树存在并行未提交内容，包括 `CONTEXT.md`、UAR-002 文档族、感知模型资产与 `RFS-001` 草稿；本路线图不修改、不回退它们。
- `changes/RFS-001/state.md` 与 `docs/design/runtime-flow-simulation-core-model-v0.1.md` 的早期 `resolving / Authority: NONE` 标记属于历史口径；RFS-001 当前仍在 `implementing / disposition: none`，未验证或 CLOSED。其已预填的新模型和接口不构成本路线图的架构事实。
- legacy `uni-agent` 只作为反例与能力盘点来源，不合并、不依赖、不把其名词机械映射到 Target。

## 2. 已冻结事实 `[FROZEN_INHERITED]`

### 2.1 Owner / Authority

| 领域 | 已冻结 Owner / Authority | 本路线图必须保持的红线 |
|---|---|---|
| Goal / Contract / Goal Evaluation | UniAgent | 不写 Current WorldBelief、Run State、Assurance、Binding、Effect receipt 或 Outcome Proof |
| Primary Run canonical state | Run Model | 非终态状态若新增，仍须经 typed legal transition；Simulation 不保存第二份 Run State |
| Evidence | Evidence Ledger；sole Evidence Admission Authority | Perception、Replay 与 Trace 都不能铸造 canonical Evidence |
| WorldBelief / Container / Slice | World Model；sole Belief / Reconciliation Authority | 任何组装或 Oracle 都不得回写或平行维护 current truth |
| Control intent | Control Loop | UniAgent resolution 不可直接签发 intent |
| Canonical binding / delivery | Effect Boundary | Driver 只机械投递；Replay adapter 不可自行 retry / recover / replan |
| Runtime assurance / Outcome Proof | Assurance | receipt、Trace 或视觉 golden 不能替代 verification / proof |
| Runtime Outcome emission | Uni Kernel | terminal 后零新 effect；Outcome exactly once |
| Trace | caller-owned、非权威结构因果投影 | enabled / disabled / failure 时 canonical output 不变 |

权威来源：`docs/architecture/product-architecture-baseline-l0-l3.md`、`docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md`、`docs/architecture/uworld-protocol-baseline-l4.md` 及相关 ADR。

### 2.2 生命周期与边界

- 当前 cardinality 是 `1 Product Session / 1 Primary Goal / 1 Primary Run`；一个 Run 可包含多个 observe / decide / act / verify cycle。
- Primary Run 可有 active、uncertain、recovering、blocked、paused 等非终态语义，但现有代码只实现最小 progress 与 terminal outcome；具体非终态状态并未因此自动冻结。
- ADR-0019 已冻结：P1 只负责 Contract proposal / admission；accepted Contract View 只是合法激活的必要前置；合法激活后由 Kernel internal run driver self-drive。UniAgent、Host、Simulation 或测试不得逐 cycle 驱动 `Process / SelectIntent / Act / EvaluateTerminal`。
- Slice 是**一个 WorldBelief revision 的 scoped immutable projection**；omission 不等于 absence；空间值必须绑定 SpatialFrame；Slice 不是 Container、Observation、Trace history 或通用 WorldBelief DTO。
- ObservationOccurrence 是 revision-local；provider node id、bbox、OCR id、DOM / UIA node 或 detection id 都不是跨 revision identity。
- Fast / Slow 是 Perception 内部 realization，不是两个顶层 Perception、两条 Evidence ingress 或两份 Belief。
- Effect receipt 是 attempt evidence，不直接证明现实 Effect；Effect 后需要新观察和 Assurance verification。
- `UnknownOutcome` 与 `DeliveryFailed` 当前都驱动重新观察；不得 blind redispatch。
- terminal Safe-Stop / Escalation 必须由 Evidence-backed Outcome Proof 支撑；Goal Evaluation 只消费 immutable Runtime Outcome。

### 2.3 Trace 与 deterministic anchor

- Run Trace 是 one-Run、immutable、reference-oriented、non-authoritative projection；RunId 是语义 correlation root，TraceId / SpanId 不是 domain identity。
- 在 Product Host 中，Trace 只能异步引用 Owner 已产生的 immutable facts，不复制、解释或发明事实，也不作为 Runtime 决策输入；Trace writer 的延迟、失败或背压不得进入 Runtime critical path。
- 只有完成 drain、seal 与完整性检查的 Trace artifact 才可交给测试侧 `Scenario Importer`；Importer 派生 immutable `ScenarioStimulus`，Simulation Host 只消费 Stimulus，不把 Trace Event 本身解释成 command。Trace ref 始终只是历史关联。
- live Perception 的确定性 replay anchor 是 provider response JSON；capture PNG 保留为原始资产和模型重算输入。两者不可当成两份 truth。
- World Model 已冻结公开 canonical collection 的增量 frozen-chain 枚举契约。测试 digest 若需要自己的 canonical 排序，必须声明独立 test schema，不能反向改写生产公开顺序。

## 3. 当前实现事实

| 面 | 当前实现 | 已证明的能力 | 尚未证明 |
|---|---|---|---|
| Runtime composition | `src/UniClaw.Kernel/UniKernel.cs` 暴露 `AdmitContract`、`Process`、`DeriveSlice`、`SelectIntent`、`ActViaCurrentGrounding`、`EvaluateTerminal` | 六个 L2 Owner 的组合顺序、fail-closed、terminal delivery closure | legal activation、internal run driver、外部事件等待、暂停/恢复 |
| Run Model | `RunModel` 保存 Contract View、Objective、Obligations、Progress、Outcome；同 version admit 幂等，terminal exact-prior single-winner | canonical Run State 与 terminal 不可逆 | active / waiting / recovering / supervision 的正式状态与预算 |
| Perception | `RawArtifact` 内容寻址；`FastPerception` 同步调用 strategy 并产生带 provenance 的 proposals；live provider 有 capture/derived 双 artifact | live/corpus parity、缓存等价、明确失败分类、non-empty live chain | Fast→Slow / Slow-only / partial / late / duplicate / cancel 的统一语义 |
| Evidence | `EvidenceLedger` 做完整性、provenance、kind/context 与 canonicalization admission | deterministic EvidenceId、append-oriented admission、拒绝零 canonical record | 资产 bundle schema、跨 producer correlation、会话级 completion |
| World Model | revision history、Container association、occurrence、LogicalItem continuity、claims/conflicts、Slice / GroundingView | replayability、revision isolation、stale Slice、consumer view、canonical Oracle | 完整页面 coverage、全元素语义 Ground Truth、跨局部输入兼容检查器 |
| Control / Recovery | policy 选择 Observe / Act / Recovery；unconfirmed delivery 触发 re-observe | no blind retry、desired-state pre-dispatch safety | recovery budget、策略上限、无法安全决定时的非终态出口 |
| Assurance / Effect | Bind→Judge→Gate→Dispatch，freshness consumption-relative，receipt reflux | exact intent/binding/revision correlation、terminal Outcome Proof | effect 后预期变化的通用验证关联、补偿/修复策略 |
| UniAgent | 独立 assembly，当前只实现 terminal Runtime Outcome 的确定性 Goal Evaluation | sole Goal Evaluation producer、Kernel 不反向引用 Agent | 非终态监督 case、合法 resolution、timeout/invalid resolution |
| Trace | Run / Evidence / WorldRevision / Artifact 等 typed refs，明确 Disabled 和故障隔离 | on/off canonical equivalence、first-order structural causality | 统一的异步 drain/seal/integrity 生命周期、replay importer、scenario / supervision 引用及全闭环覆盖 |
| Assets / Replay | `tests/.../Perception/Corpus` 有 PNG/XML/JSON；`platforms/perception/evaluation` 有内容寻址 manifests、GT、scorecards；golden-run 有 before/off/on、响应 JSON、trace、device profile | 固定资产推理、parity、quality/performance 分离的一部分 | 统一 bundle、capture→clean→review→baseline 生命周期、兼容迁移 |
| Test Oracle | `WorldModelCanonicalOracle` 以 test-only 扫描/渲染与 first-divergence 保护生产优化；golden hashes 已 pin | production indexed path 与 public canonical view 等价 | 页面语义 Ground Truth、missing/duplicate/wrong-parent/spatial diff |
| Simulation | `LoopTwinTests`、`LiveClosedLoopBulletTests` 等测试装配 doubles / live adapters 并直接编排 Kernel 操作面 | 片段级闭环和 live/corpus twin | 同一 self-driven Runtime、一次性 scenario 输入、未消费事件检测 |

## 4. Gap Matrix

| # | 用户期望 | 已冻结语义 | 当前实现 | 缺失能力 | Owner / Authority 风险 | 处置类型 | 路线图阶段 |
|---|---|---|---|---|---|---|---|
| G1 | Contract 后启动完整 Run | admission ≠ activation；激活后 Kernel self-drive | admit 即建立最小 RunState；无 activation 面 | legal activation 的唯一 producer/consumer、幂等、并发、恢复 | Host retry 变成第二 driver | Human 决策 + tracer | 1 |
| G2 | 外部录制事件驱动真实 Runtime | 外部只能供给 capability input，不能 step-drive Kernel | 测试逐步调用 Kernel 方法 | internal driver、等待/唤醒、事件消费纪律 | Simulator 变第二 Runtime | Human 决策 + 实验 | 1 |
| G3 | Fast / Slow / full-model 统一 | Fast/Slow 仅 Perception realization；P2 单 ingress | 同步 Fast-only list | 渐进、partial/final/failure、cancel | 顶层双 Perception、Slow 直写 belief | 场景实验后决策 | 4 |
| G4 | 同一观察内乱序、重复、迟到 | proposal 必须有 provenance；跨 revision fail-closed | 无观察会话或 completion identity | capture/session correlation 与 ordering 规则 | latest-wins 污染 belief | 实验 + Human gate | 4 |
| G5 | Fast 错、Slow 在 Effect 前纠正 | 纠正也须 P2→P3→new revision | 可顺序提交 proposals，但无迟到策略 | action admission 前的 supersession / sufficiency 语义 | producer confidence 变 truth | 场景实验 | 4 |
| G6 | Fast 错且 Effect 已发生后修复 | receipt ≠ effect；post-action observe 必须回闭环 | PostAction context、reflux、reobserve 片段已有 | expected change correlation、repair budget、补偿边界 | receipt 或 intent 反写 belief | 实验 + policy 决策 | 5 |
| G7 | 重建完整页面 / Container | Slice revision-bound、scoped、omission ≠ absence | Slice 只含 scoped claims / occurrences；无完整性模型 | target coverage、unknown/conflict/unobservable manifest | Presentation 成第二 World Model | Human 决策 + Oracle 实验 | 2 |
| G8 | 组合多个局部结果 | canonical mutation 仅 accepted evidence；SpatialFrame 显式 | 没有组合兼容判定 | capture/revision/frame/time/scope/version checks | 跨 revision 拼接伪装真相 | 先 fail-closed 实验 | 2 |
| G9 | 页面元素“完全一致” | pixel 不等于 semantic truth | real-asset tests 与 hashes 分散 | semantic GT、差异分类、lineage 比较 | visual golden 取代 assurance | 测试设计 + Human review | 2 |
| G10 | World Reconstruction Replay | Oracle test-only；不回写生产 | canonical Oracle 只校验生产查询等价 | 完整重建 runner/report | Oracle 成生产 authority | 实验 | 2 |
| G11 | Deterministic Runtime Replay | 同一 Runtime；外部 seam 可替换 | doubles 存在但由测试逐 cycle 编排 | scenario-once 驱动、虚拟时间、事件消费、run digest | 平行 Simulator FSM | Human 决策 + 实验 | 1、3 |
| G12 | Model Evaluation Replay | 模型质量与 Runtime 正确性分离 | evaluation assets / bench 已存在 | 统一 run metadata、错误分类与 Runtime 报告隔离 | 模型分数被当成 Runtime proof | 实验 | 7 |
| G13 | 可追溯的最小重放资产 | Raw/derived artifact 与 producer identity 已有局部先例 | 两套资产树、不同 manifest 角色 | `Minimal Scenario Bundle v0`：scenario/version、精确 Runtime hash、UI system/app/build、内容寻址资产、Stimulus/virtual time、producer/schema/config、lineage/integrity、expected semantic assertions/digest | asset path、系统版本或 Trace 变 truth；首轮被完整治理平台拖死 | Human 已选方向；待 tracer | 1、3 |
| G14 | 事故/人工场景晋升为 baseline | 无冻结治理 | 人工 corpus 与 reports | capture→register→metadata→review→promote→supersede/retire；redaction 作为可选 derived transformation 延后 | 未审预期自动成 canonical | Human 已选方向；待 tracer | 3 |
| G15 | 两次重放 digest 一致 | deterministic IDs / Trace 已有先例 | 多处显式时间，缺统一 virtual time | clock/seed/id/fault injection 与 semantic digest | 测试输入渗入 production truth | 实验 | 1、3 |
| G16 | Trace 异步串因果，并可派生测试 Stimulus | ADR-0013 冻结 Product Trace 非权威 | 覆盖部分 operation / refs | async writer、drain/seal/integrity、Scenario Importer、asset/scenario/supervision refs、Product/Simulation 双重防泄漏 | Trace 背压 Runtime、Trace Event 变 command，或 Replay 注入内部 expected state | Human 已选方向；待 tracer | 1、3、6 |
| G17 | Local Recovery 与预算 | Control owns recovery；必须重入闭环 | 只覆盖 unconfirmed outcome→reobserve | Contract ceilings、Control allocation/consumption、typed exhaustion 与停止条件 | 无界循环或 blind retry | Human 已选方向；待 tracer | 5 |
| G18 | Awaiting Supervision | baseline 允许 paused/blocked，但无具体协议 | 无状态、case、resolution | 发起、暂停、Evidence 自动解 case、合法 capability offer、校验与重入 | UniAgent 取得 Runtime authority | Human 已选方向；待 tracer / amendment | 6 |
| G19 | terminal Safe-Stop / Escalation | Evidence-backed Outcome Proof；terminal 不可恢复 | 已实现并有测试 | 与非终态监督的切换判据 | 把 supervision 当 terminal 或反之 | Human 决策 | 6 |
| G20 | 真正 Agent 的有界裁决 | UniAgent 拥有 Goal/global strategy/Contract authoring/Goal Evaluation；Control 独占 Tactical Hypothesis/Intent | 只消费 terminal outcome | bounded Decision Context、Plan Hypothesis、Decision Package、Capability/Grant request、Decision Feedback 与 Contract generation | 固定 Runtime workflow 取代 Agent，或 Agent 复制 Control/Belief/Effect authority | Human 已选方向；待 tracer / amendment | 6 |
| G21 | 原子 Effect 与验证义务 | canonical intent/binding/dispatch 已冻结；receipt ≠ effect | 缺少 UniAgent proposal 与多尝试 buyer | 每次环境改变后必须取得 post-action accepted Evidence 并完成 reconciliation/verification，才可发下一次真实 Effect；新 Attempt identity、retry causal link、EffectExpectation | 隐式重试、复用旧 authorization、batch 掩盖首个副作用；把验证误写成每步全屏/VLM | Human 已选方向；待 tracer | 5、6 |
| G22 | Agent 重启后继续解决问题 | Host Session 不具 Product authority；Runtime resume 依 canonical state | 无 Product-level continuation artifact | 仅保存 Agent cognition summary 与 canonical refs：Goal、Plan Hypothesis、next objective、Run/Checkpoint/Attempt/Obligation/Evidence refs；fresh observe 后重评 | 复制 Run/Assurance/Effect 状态、依赖隐藏思维链/旧 transcript，或自动续发未决 action | Human 已选方向；待场景证明 | 6 |
| G23 | Product / Simulation Host 功能隔离 | Product Runtime authority 不得被 Harness 复制 | 当前主要是测试装配 | 独立 Simulation Host、production dependency-closure guard、同一 Runtime artifact、sim-only Stimulus consumers | Product Host 配置误开模拟，或复制 Simulator Runtime | Human 已选方向；Phase 1 tracer | 1 |
| G24 | Product readiness | ENVIRONMENT 证据不能由测试绿灯替代 | 有局部 live/corpus evidence | 精确 release identity、设备/系统/app matrix、安全、恢复、观测、性能、灰度与回滚 packet | Agentic/Simulation success 被宣称 production-ready | Human 已选指标结构；待数值与环境证据 | 7 |
| G25 | Agent/Runtime critical-path latency | Authority 与 Evidence 链不可为提速绕过 | 有零散 benchmark，未按闭环结构计数 | 从 Phase 1 记录 latency 与 ModelCalls/Observations/Reconciliations/Regrounds/VerificationMode/InputTokens，建立 scenario regression budget | 最后阶段才发现架构调用形态不可用，或靠跳过验证获得假快 | Human 已选指标；阈值待基线 | 1–7 |
| G26 | 像手机操作者一样可接管、可授权 | Human preemption、系统权限和高影响 Effect 不能由页面内容或旧授权替代 | 无统一 Agent-facing contract | 低风险 Contract 预授权、细粒度一次性 Grant、semantic Commit Point、manual-input preemption、untrusted-content boundary | Agent 抢夺用户控制、权限扩大、页面 prompt 注入控制面 | Human 已选方向；待 tracer | 6、7 |
| G27 | 可确定验证 UniAgent 外部 seam | Codex/DSH 是两个完整 realization，不是内部 adapter | 无 deterministic UniAgent test double | 同一外部 seam 的 `ScriptedUniAgent`，验证 call boundary/order/count/correlation/cancel/late/duplicate/no-response | test double 被误称第三 realization，或其通过被当成 Agent 智力证明 | Human 已选方向；待 tracer | 1、6 |

## 5. 核心概念与待澄清术语

| 术语 | 人话定义 | 与相邻概念的界线 | 当前状态 |
|---|---|---|---|
| Raw Artifact | 环境或 provider 原样产出的可寻址字节及采集元数据，例如 PNG；回答“拿到了什么原料” | 不是 producer 结论、Evidence、Belief 或 proof | 已实现；产品基线 + provider baseline |
| Observation / ObservationProposal | producer 对某个 scope、时刻和来源提出的环境声明；proposal 仍在 admission 门外 | 不保证真实，不拥有 EvidenceId，不直接改 WorldBelief | `ObservationProposal` 已实现；“渐进观察”未冻结 |
| Evidence | 经 Evidence Ledger admission 的 canonical、immutable、可引用依据 | admission 只证明记录合格，不证明现实为真 | 已冻结/已实现 |
| WorldBelief revision | World Model 基于 accepted Evidence 形成的一版可修正世界判断 | Belief ≠ Reality；历史 revision 只读，只有 current 生效 | 已冻结/已实现 |
| Container | 有独立交互、状态、进入/离开/覆盖/恢复语义的世界对象 | 纯几何区域不是 Container；Container 不等于 screen/page | 已冻结/已实现 |
| Slice | 从**一个** WorldBelief revision 为某 buyer 派生的有 scope、immutable 投影 | 不是局部截图、观察输入、Container、历史拼图或全量 world DTO | 已冻结/已实现 |
| 完整页面 / Container 重建结果 | test / Simulation 中对一个明确 target coverage、一个 source revision 的语义呈现与 coverage 说明 | 非权威，不可作为 grounding input，不回写 World Model | 仅能力描述，命名未冻结 |
| Replay asset / Asset Bundle | 内容寻址资产及其 immutable manifest；manifest 记录 lineage、运行 occurrence、关键 Core Metadata、扩展 metadata、断言与兼容要求 | content hash 不混入全部环境字段；路径不是 identity；Bundle 不是 Runtime truth | Human-selected artifact 能力；schema/name 未冻结 |
| Runtime Scenario | 描述初始条件、`ScenarioStimulus`、故障与期望不变量的测试故事 | 不是第二套状态机；不消费 Trace Event 作为 command，也不注入内部 owner state | Human-selected artifact 能力；schema/name 未冻结 |
| 测试基线 | 经 Human 明确晋升、版本化并可废弃的 scenario + expected assertions | 普通捕获、一次成功 replay、旧 report 都不是 baseline | 未建立统一生命周期 |
| Trace | 对一个 Run 的异步结构化因果投影，引用 Owner records；sealed artifact 可被 Importer 读取 | Product 中不是 truth/control/recovery input；Trace Event 在 Simulation 中也不是 command | Product 语义已冻结；seal/import 能力为 Human-selected candidate |
| World Presentation | 从一个 WorldBelief revision 的 compatible Slice 形成的 non-authoritative 展示，可叠加 immutable screenshot evidence 或明确标记的 derived visual | 不是 WorldBelief、GroundingView 或跨 revision 全局真相 | Human-selected candidate；待 Phase 2 tracer |
| Plan Hypothesis | UniAgent 对“如何完成 Goal”的可废弃全局策略判断，可引用 canonical refs 并表达 uncertainty | 不等于 Control 的 Tactical Hypothesis，不是 canonical WorldBelief、authorization 或 Outcome Proof | Human-selected candidate；待 Agent tracer |
| Tactical Hypothesis | Control Loop 基于 current WorldBelief 与 Contract 形成的局部可废弃判断 | UniAgent 可提供建议，但不能拥有或回写它；它也不是 canonical WorldBelief | `FROZEN_INHERITED`：Control-owned |
| Agent Decision Context | Kernel 在合法决策边界给 UniAgent 的有界多模态上下文：Goal/Contract generation、当前 screenshot/visual assets、Container semantic view、capability/authorization、recent verified effect、pending refs、budget、unknown/conflict 与可请求观察 | 不是 canonical store dump、Host transcript 或 Agent memory；后续调用优先 Anchor+Delta | Human-selected candidate；待 Phase 6 tracer |
| Bounded Contingent Decision Package | UniAgent 编写的 immutable、有 lease/budget 的有限 DAG；节点是 typed proposal，边是有限 tri-state Guard | 不是 Effect batch、Control State、Run State 或 Driver macro；Kernel 每次仍只允许一个真实 Effect 进入完整链 | Human-selected candidate；待 Phase 6 tracer |
| Decision Feedback View | Kernel 从 owner records 派生给 UniAgent 的最小 non-authoritative 反馈，含采用分支、assurance、attempt/receipt、Run 与 pending obligation | 不复制 canonical state，不依赖 Trace 才能生成 | Human-selected candidate；待 Phase 6 tracer |
| EffectExpectation | 描述动作后要观察和验证的变化、scope 与 deadline | 不等于 DesiredState、完整页面预测、receipt 或 Goal completion | Human-selected candidate；待 Phase 5 tracer |
| EffectAttempt | 一次不可变外部投递尝试；重试产生新 identity，并以 causal ref 关联前次尝试 | 不等于 plan step、proposal、receipt 或已验证 Effect | Human-selected candidate；待真实多尝试 buyer |
| Agent Continuation | Product-level 的可恢复认知摘要：Goal ref、Plan Hypothesis、reasoning summary、next objective 与 Run/Checkpoint/Attempt/Obligation/Evidence refs | 不复制 Run/Assurance/Effect 状态，不保存隐藏思维链，不等于 Host Session、Runtime checkpoint 或 Trace replay | Human-selected candidate；待 Phase 6 restart tracer |
| ScenarioStimulus | Scenario Importer 从 sealed Trace 或人工 fixture 派生的 immutable、显式版本化外部输入 | 不是 Trace Event、canonical Evidence 或内部 owner-state injection | Human-selected candidate；待 Phase 1 tracer |
| ScriptedUniAgent / DeterministicUniAgentDouble | 走真实外部 UniAgent seam、按脚本返回 Decision Package 并检查调用纪律的测试 double | 不是第三种 UniAgent realization，不证明 Agent 智力；Deterministic Runtime Replay 不调用 live model | Human-selected test capability；待 Phase 1/6 tracer |
| Simulation Host | 独立 composition root，只在其依赖闭包中装配 ScenarioStimulus consumer、simulation adapters、Oracle 与 assets | 不是 Product Host mode，不拥有第二套 Product Runtime | Human-selected architecture direction；待 Phase 1 tracer |
| test-only Oracle | 测试侧独立计算的预期与差分器，用来发现 production 输出首处分歧 | 不进入生产、不成为 WorldModel 实现或 fallback | 已有 WorldModel 优化 Oracle；页面 Oracle 未有 |
| Runtime 非终态异常处理 | Run 尚有恢复可能，但当前 cycle 不能安全推进时的暂停、再观察、恢复或等待外部决定 | 不等于 terminal failure / escalation | 仅基线允许，模型未落地 |
| terminal Safe-Stop / Escalation | Assurance 用 Evidence 证明继续不安全、超界或需上层接手，Run 形成不可逆终态 | 终态后不能 resume 或 effect | 已冻结/已实现最小路径 |
| UniAgent 裁决 | 在自身 Goal / Contract authority 内，为 Runtime 的有界问题选择允许的后续方向 | 不能直接改 Belief、伪造 Evidence / receipt、授权或投递 effect | 非终态用途未裁决 |

### 5.1 Slice 词义已经收口

Human 已确认口述中的目标是 `Slice` 相关能力，但必须按输入所在层级区分：

- Slice 是一个 WorldBelief revision 的 scoped immutable projection；同 revision、scope/frame compatible 的多个 Slice 可组成 non-authoritative World Presentation。
- 同一 capture 的局部检测 / OCR / DOM / Accessibility 输出仍是 raw / derived artifacts 或 ObservationProposal，必须先经 P2/P3 与 reconciliation，不能因为用于拼接就改称 Slice。
- Slow 对旧 capture 的识别仍关联该 capture / observation operation；新 accepted Evidence 只有在 World Model 判定 belief-relevant 并完成 reconciliation 后才形成新 WorldBelief revision。新 Slice 仍由 buyer 按需从该 revision 派生，不能原地修正旧 Slice。
- 不同 capture / revision 的 Slice 只可用于带时序标注的诊断、回放或视觉仿真，不能宣称为某一时刻的 current truth。

## 6. 完整页面或完整 Container 的组装验证模型

### 6.1 验证对象

验证对象不是“拼图是否好看”，而是：**一个明确 source WorldBelief revision 对一个明确 Container / page-like scope 的语义表达，是否覆盖 Ground Truth 所声明的可观察范围，并且每个输出可追溯到 Observation、Evidence 和原始/派生资产。**

这是一项 test / Simulation judgment。它不能让 Simulation 获得生产 WorldBelief authority，也不能把测试答案注入 Runtime。

#### 6.1.1 展示请求与 region 语义

- 每次重建/展示请求必须显式选择 `ContentOnly` 或 `WholeContainer` coverage。完整性只相对于所选 coverage 判断；固定 sidebar、topbar、navigation 等未纳入 `ContentOnly` 时不算 missing。
- sidebar/topbar/content 首先是 region claim，不自动成为 Container。只有具有独立 interaction、state 与 lifecycle 的对象才满足 Container 语义。
- 影响 Runtime 观察、Grounding 或 Effect 的 region claim 必须来自 accepted Evidence 并进入对应 WorldBelief revision；纯展示优化的 region metadata 保持 non-authoritative。
- screenshot bytes 保持 immutable evidence layer；裁剪、拼接、补绘或模拟生成的画面是 derived visual，必须带 source refs，不能覆盖原截图。
- Slow 对同一 capture 的更精确识别若被接受，会产生新 Evidence / WorldBelief revision / Slice；旧 Slice 不变。展示可比较两版，但不得把较新的语义静默写回旧版截图。

### 6.2 可接受输入

| 输入族 | 可组合条件 | 禁止条件 |
|---|---|---|
| 同一 capture 的多个原始/派生资产 | capture identity 相同；parent lineage 完整；frame / transform 已知；producer/schema/model version 明确 | 仅凭文件名或时间接近认定同源 |
| 同一观察来源的多个 proposals | 同一 capture；scope 不矛盾；temporal window 明确；duplicate / supersession 可判断 | 把 producer confidence 当覆盖/真值 |
| 同一 WorldBelief revision 的多个 Slice / owner projections | SourceRevisionId 相同；scope union 有明确 target coverage；每个 spatial value 可落到共同 frame | revision 不同、scope 含义不明、frame 不可转换 |
| 原始 screenshot + DOM / Accessibility / OCR / boxes | 明确哪些是 GT、哪些是被测输入；capture 与坐标变换一致 | 把平台 tree 当无条件真相；把 OCR 缺失当页面不存在 |
| 不同 capture / revision 的输入 | 默认禁止构成同一“完整页面” | 除非未来 Human 单独接受 temporal fusion 语义；当前只能做 timeline diagnostic |

### 6.3 兼容性门：先拒绝，再组装

组装前按以下顺序 fail closed：

1. source WorldBelief revision 是否唯一；
2. capture identity / parent lineage 是否同源；
3. SpatialFrame 是否同一或存在显式可验证 transform；
4. capture time 与 temporal compatibility 是否满足 scenario policy；
5. scope / coverage 是否可求 union 且没有互斥声明；
6. producer、schema、model、config、pipeline version 是否被 bundle manifest 接受；
7. duplicate、supersedes、refines 或 conflict 是否有显式规则；
8. 所有引用的 bytes / owner records 是否按 content hash 可解析。

任一项不满足，输出结构化 incompatibility；不得降级为“尽量拼一下”。

兼容门通过后仍采用保守合并规则：

- coverage 只按显式 region / scope 求 union，重叠区域只计一次，未声明区域保持 unobservable；
- 只有 canonical bytes、capture、producer/version、scope 与 lineage 全同的重复项才可机械去重；
- 同一区域或同一 test element 出现不一致声明时保留 typed conflict，不按置信度最高、文件顺序或最新到达覆盖；
- 更细 scope 只有携带同 capture 内的显式 supersession/refinement lineage 时才可替代较粗结果；
- unknown 不能被“不兼容但看起来合理”的输入补齐，absence 必须由 Ground Truth / accepted negative evidence 的明确语义支持；
- coverage 达不到目标时结果可为 partial + gaps，但不得宣称 complete。

### 6.4 “完整”的可验证定义

一个重建只在以下条件全部满足时可标记“在声明 coverage 内完整”：

- target Container identity 与 coverage scope 明确；
- Ground Truth 声明的每个 expected element 被解释一次且仅一次；
- 没有 missing、duplicate、unexpected 或错误归属元素；
- identity / occurrence、semantic type、role、parent-child、container ownership、空间关系、state、关键属性与交互语义满足断言；
- unknown、conflict 与 unobservable region 以一等差异存在，不能用 absence 或 omission 替代；
- 每个输出 element 有 `observation → evidence → source artifact` lineage；
- coverage 外内容明确排除，不因未出现被判 absent；
- visual golden 若存在，只作为布局/像素辅助，不能替代语义断言。

“完整”是相对于声明 coverage 的结论，不是对现实页面的全知声明。

### 6.5 Ground Truth / Canonical Oracle

建议建立独立 test-only 语义 Oracle，职责仅为：读取 Human-reviewed Ground Truth 与被测重建结果，产生 deterministic diff。它不得引用生产 WorldModel 私有索引、不得调用生产 reconcile 作为 expected path、不得进入 Runtime 装配。

差异分类至少包括：

```text
missing
duplicate
unexpected
misclassified
misbound
wrong-parent
wrong-container
wrong-state
wrong-attribute
wrong-interaction-semantics
spatial-mismatch
lineage-gap
unknown-mismatch
conflict-mismatch
unobservable-region-mismatch
```

### 6.6 Canonical semantic digest

Digest 属测试 schema，不是新产品 identity。建议 v1 输入按以下规范 canonicalize：

1. 先写 schema version、scenario baseline version、source revision reference 与 coverage manifest hash；
2. 元素按 Ground Truth 的稳定 test identity 排序；test identity 不能被提升为生产 LogicalItem identity；
3. 每元素写入 type/role、parent test id、container test id、normalized spatial relation、state、asserted attributes、interaction semantics、lineage hashes；
4. unknown/conflict/unobservable 分区独立排序写入，不能省略空/未知的区别；
5. 所有字段使用 tag + count/length framing，禁止依赖 JSON property order、文件路径、wall clock、random id 或生产 frozen collection 的偶然枚举；
6. 对 canonical bytes 做全长 SHA-256，并同时保留可读 canonical rendering 供 first-divergence。

该 digest 只比较 test semantic output。生产 WorldBelief 自身的公开顺序仍服从 ADR-0018，不被此排序覆盖。

### 6.7 First-divergence 规则

两次结果或 actual / expected 不同，按稳定次序报告第一处：

```text
bundle/schema incompatibility
→ source revision / capture mismatch
→ coverage mismatch
→ element set: missing | duplicate | unexpected
→ identity / binding
→ type / role
→ parent / container relation
→ spatial relation
→ state / attributes / interaction semantics
→ lineage
→ unknown / conflict / unobservable
→ terminal / trace-reference completeness
```

报告必须包含 `scenario + baseline version + comparison path + stable element test id + expected + actual + source refs`。第一处分歧之后可以附摘要计数，但不得让不稳定的后续噪声遮住首因。

## 7. Simulation、Replay、Asset、Baseline 与 Trace 的关系

```text
Captured artifacts / sealed Trace
        │ register + hash + minimal metadata + integrity
        ▼
Scenario Importer ──> immutable ScenarioStimulus ───────┐
                                                        ├─> isolated Simulation Host
Human-reviewed assertions ─> Minimal Scenario Bundle v0┘             │
                                                                      ├─> exact Product Runtime artifact
                                                                      ├─> canonical owner records
                                                                      ├─> semantic digest / diff
                                                                      └─> async Trace refs only
```

### 7.1 三种验证模式必须隔离

| 模式 | 输入 | 输出 | 可重复性 | 判断标准 | 典型失败 | 不能证明 |
|---|---|---|---|---|---|---|
| World Reconstruction Replay | 固定 assets、结构化 observations / accepted records、单 revision scoped projections、GT | 重建结果、coverage、semantic digest、typed diff | 必须 deterministic | 元素集合/层级/空间/state/lineage/unknown/conflict 全断言 | incompatible input、missing、duplicate、wrong-parent、spatial mismatch、lineage gap | Runtime lifecycle 正确、真实模型准确、真实 effect 成功 |
| Deterministic Runtime Replay | Goal/Contract、`ScenarioStimulus`、ScriptedUniAgent decisions、录制 perception/driver/environment/supervision input、virtual time/faults | 真实 Runtime owner records、Outcome、Goal Evaluation、digest、Trace ref | 同 bundle / versions 至少两次一致 | Agent seam 调用纪律、状态转移、effect 次数、预算、post-action verification、终态、未消费刺激为零 | stimulus order、missing asset、unexpected Agent/adapter call、nondeterminism、invalid resolution、wrong terminal | Agent 智力、OCR/VLM 质量、真实设备时序与网络可靠性 |
| Model Evaluation Replay | 原始图像/语料、真实 OCR/检测/VLM/LLM、环境/模型/config identity | predictions、quality/robustness/perf report、error taxonomy | 允许非确定；需统计与版本锚 | GT metrics、错误类型、p50/p95、冷暖、fallback、模型版本 | model drift、dependency failure、quality regression、latency regression | Runtime 状态机正确、Effect safety、Outcome Proof |

三种报告必须分开存放和命名。Model Evaluation 的 score 不可填进 Runtime Replay 的 correctness 字段；Runtime Replay 通过也不可宣称模型准确。

#### 7.1.1 Agentic Simulation 与 Trace Playback 是正交用途

- **Agentic Simulation**：由完整 Codex-backed UniAgent realization 面向同一 Product Runtime 与隔离环境自主解决 Goal。允许多条合法路径；验收安全不变量、Goal/Outcome、预算、证据链与 Trace 完整性，不用固定 action sequence 约束 Agent 智力。
- **Trace Playback / Visual Simulation**：只读取 sealed Trace artifact，允许 Effect/状态 Event 驱动画面、Slice/截图呈现或环境演示。它证明 Trace 可解释和可展示，不证明 Runtime 重新计算正确。
- **Deterministic Runtime Replay**：不调用 live model。`ScriptedUniAgent` 走与真实 UniAgent 相同的外部 seam，接收真实 Decision Context、返回记录或脚本化 Decision Package，并检查调用边界、顺序、次数、correlation、late/duplicate/cancel/no-response；它是 test double，不是第三种 UniAgent realization，也不证明 Agent 智力。
- Agentic Simulation 的一次成功不能自动成为 deterministic Baseline。只有经 Human review，将输入、外部响应和断言显式晋升后，才能进入 Deterministic Runtime Replay。
- Codex-backed 是完整 Simulation UniAgent realization，DSH-backed 是目标 Product UniAgent realization；两者共享 host-neutral Product contract，而不是作为 UniAgent 内部 adapter。

#### 7.1.2 独立 Host 与同一 Runtime artifact

- Simulation Host 是独立 executable / composition root；只有它装配 ScenarioStimulus consumer、simulation adapters、virtual time/fault injection、test-only Oracle 与资产解析。
- Product Host 的构建依赖闭包不得包含上述功能，不提供可由 `simulation=true` 打开的隐藏路径。
- 两个 Host 复用同一 Product Runtime modules，不复制 Runtime FSM、WorldBelief、Run State 或 Effect authority。
- 探索性模拟可以使用任意明确记录的 Runtime build；用于 release / regression qualification 的模拟必须绑定 Product Host 将发布的同一 artifact/hash，或提供可验证的 reproducible-build 等价证明。

### 7.2 Minimal Scenario Bundle v0 与后续 Asset lifecycle

第一条 Runtime tracer 只要求一个可验证的 `Minimal Scenario Bundle v0`，至少包含：Bundle/Scenario identity 与 version、精确 Runtime artifact hash、目标 UI system/app/build identity、内容寻址 assets、ScenarioStimulus 与 virtual time、producer/schema/config identity、lineage/integrity hashes、expected semantic assertions/digest。系统矩阵、自动 migration、tombstone、retention/redaction、在线 registry 都不是首轮 graduation 条件。

以下完整 lifecycle 是后续 Governance 候选，不得反向阻塞 Product Critical Path：

一个可重放 bundle 至少需要按适用性收集：原始 capture、derived response、结构化 Observation / owner-record references、外部 EffectDriver result 与环境回馈、监督 case/resolution、Goal/Contract 激活输入、device/environment profile、producer/model/config/pipeline/deployment identity，以及每次 transformation 的 parent hash。缺少不适用项必须显式声明 `not applicable`；不能用“字段没出现”同时表示不适用、丢失和未知。

建议生命周期（名称是设计标签，非产品状态）：

```text
Captured
→ Registered
→ Derived（optional transform，例如 normalize/redact）
→ Reviewed
→ Replay-Eligible
→ Baseline-Promoted
→ Superseded | Retired
→ Archived | Bytes-Deleted（独立 retention decision）
```

每一步最少回答：

- 原始 bytes 是否保持 immutable；任何 normalize/redact 等 transformation 是否形成新 derived asset；本路线图第一优先级是可追踪管理，redaction 不是首个 tracer 的 acceptance；
- content hash、media/content type、schema version、producer/model/config/pipeline/deployment version 是否齐全；
- parent / transformation lineage 是否闭合；
- capture identity、time、scope、SpatialFrame、device/environment profile 是否明确；
- 是否含隐私、凭据或线上敏感内容，谁批准进入测试仓；
- expected assertions 是谁审阅的、审阅基于哪个版本；
- 兼容失败时是 migrate、pin old runner、supersede 还是 retire。

#### 7.2.1 Identity、metadata 与兼容判断分离

资产治理必须分开三个概念：

1. `Content Identity` 只由实际 bytes / canonical content 计算 hash；
2. Run / Environment metadata 描述该内容在哪次运行、哪个系统与哪个版本产生；
3. Compatibility Judgment 比较当前执行环境与 Baseline 要求，给出 `Exact / Compatible / Incompatible / Unknown + reasons`。

Core Metadata 必须类型化、显眼、可索引，至少覆盖目标 UI system name/version/build、设备/模拟器 identity、目标 app version/build、display size/density/scale/locale/theme、Product Runtime artifact、UniAgent realization/model/config、Perception/Driver/provider、Contract/schema、capture/session/run/revision 与 virtual time/seed/fault profile。厂商或场景特有信息进入 namespaced Extended Metadata；producer 原始 metadata 可以保留，但不能代替 Core Metadata。关键字段缺失时兼容性是 `Unknown`，不得自动推断兼容。

每个 Baseline 按 claim 声明字段角色：`Exact / CompatibleRange / MatrixDimension / DiagnosticOnly`。目标 UI system/app version 默认至少是显式 MatrixDimension。Compatibility Runner 只机械计算，不得因一次 green 自动扩大范围；UniAgent 可以分析并提出新范围，Human review 后产生新的 Baseline / policy version。

旧 schema 不能原地覆盖。版本化 migration 生成新 Derived Bundle，记录 source identity、迁移工具/规则版本、内容变化与语义等价报告；断言变化或无法机械证明等价时必须 Human review，无法迁移则显式 Unsupported/Retired。

Capture Producer 只拥有 bytes 与原始 provenance；Asset Governance Authority 拥有 identity/manifest/lineage/lifecycle records；Scenario Assertion Owner 拥有 GT/expected/compatibility claims；Human Promotion Authority 决定 Baseline。早期可以共用物理存储或工具，但这些 authority records 不合并。Archive 不等于 Delete；删除 bytes 后保留 tombstone、identity、lineage、原因、时间、批准者和受影响 Baseline，依赖 bytes 的 Replay 明确失败。

### 7.3 Scenario Baseline 晋升

代表性 Scenario 只有满足以下条件才可显式晋升：

1. 原始/派生资产 hash 与 lineage 完整；
2. scenario 只描述外部世界和不变量，不逐 cycle 指挥 Kernel；
3. expected semantic assertions 由 Human 审阅；
4. 同版本 replay 至少两次 semantic digest 一致；
5. Trace on/off 与 trace failure 的 canonical output 一致；
6. fault / timeout / duplicate / stale 输入有明确预期；
7. baseline version、runner schema、兼容范围与退役原因可追踪。

Baseline 更新不是覆盖旧答案：成员、GT 或语义断言变化必须形成新版本，保留旧版用于解释历史。纯渲染环境变化只更新 visual-golden 层，不可静默改变 semantic assertions。

### 7.4 Trace 的位置与异步生命周期

Trace 可以引用 asset hash、scenario/baseline reference、Observation / Evidence / revision、intent/binding/judgment/receipt、recovery/supervision owner record 与 Outcome；但：

- Trace 不拥有这些 records；
- Product Runtime 与 Product Host 不从 Trace 推断下一步；
- Trace capture/persistence 必须异步；Runtime 只向 bounded sink 交付 reference-oriented records，不等待 durable flush。关闭、写失败、队列满或 recorder crash 不得改变 canonical output；具体丢弃/降采样策略必须显式并可诊断。
- Run/recording 结束时由测试或运维侧执行 drain、seal 与 integrity check。未 sealed、缺片段或 integrity unknown 的 artifact 不能成为 Scenario Importer 输入。
- Scenario Importer 只把外部环境变化、virtual time、fault、Perception/Driver response、Human decision 与 scripted Agent decision 派生为 `ScenarioStimulus`；WorldBelief、Assurance、Run State、Effect/Outcome 等内部结果必须由真实 Runtime 重新产生并作为 expected output 比较；
- Trace Playback / Visual Simulation 可以使用更广的 Effect/状态 Event 驱动非权威呈现，但不能据此宣称 Runtime conformance；
- Trace 缺失、关闭或 recorder failure 不改变 Runtime input / output；
- Trace 中的时间与顺序仅用于诊断，canonical state 顺序来自各 Owner；
- 只有真实 buyer 证明某 reference kind 必要后，才能扩展 Trace catalog。

录制的 Human approval / grant 只能经 Importer 变为明确的 simulation-only Stimulus，用于复现“当时批准后走入哪个分支”：它不能铸造新的 Product grant，也不能跨 Session、Run、Contract generation、environment 或 expiry 复制权限。Product 进程恢复仍须重新校验原 grant 的 scope/currentness/revocation。

### 7.5 Runtime semantic digest 与 first divergence

Deterministic Runtime Replay 不逐字节比较整份 Trace。它对以下稳定语义检查点形成 versioned canonical rendering / digest：

1. 外部 `ScenarioStimulus` 及其消费顺序；
2. canonical owner-state transitions；
3. WorldBelief revision semantic digest；
4. Action Proposal / Effect Attempt 数量、identity 与 causal relation；
5. Assurance、Effect Verification、Outcome Proof 与 Runtime Outcome；
6. 最终未消费输入为零。

wall clock、随机/Host/Trace id、diagnostic log 顺序等默认不参加比较，除非 Baseline 明确将其声明为业务语义。失败时报告**最早一个语义不一致 checkpoint**，附 scenario/baseline、Runtime artifact、目标 UI system/app metadata、expected/actual、关联 assets/owner refs 与上下游摘要；后续噪声只做附录。World Reconstruction 的 element-level first divergence 仍服从 §6.7，两种 digest schema 不混用。

## 8. Runtime 异常处理与 UniAgent 裁决

### 8.1 三类处置

| 类别 | 条件 | Owner / 行为 | 出口 |
|---|---|---|---|
| Local Recovery | Contract / policy 已明确允许，且 Runtime 拥有足够 evidence 选择安全恢复 | Control 形成 recovery / reorientation；仍经正常 observation、grounding、assurance、effect | 回到 active 闭环或耗尽预算 |
| Awaiting Supervision（语义标签，名称未冻结） | Run 非终态；Runtime 知道存在风险/歧义，但无法在既定策略内安全决定 | Run Model 记录非终态 disposition；Kernel 停止新 effect，只允许证据保全、受控再观察、超时/取消与监督输入 | 合法 resolution 或新 accepted Evidence 解决 case 后重入；无解才 terminal proof |
| terminal Safe-Stop / Escalation | 有 Evidence 足以证明继续不安全、超出 Contract、监督无合法解或预算终止 | Assurance 形成 Outcome Proof，Run Model terminal，Kernel emits Outcome | 只允许 UniAgent Goal Evaluation，不可 resume |

### 8.2 非终态监督必须先回答的契约问题

- **谁发起**：只能由 Kernel internal driver 基于 Owner 的 typed decisions 发起；Capability、Trace、Driver 或 Host 不能自行升级。
- **发起条件**：至少包含歧义/冲突持续、effect outcome unknown、recovery budget 到限但仍可能安全恢复、Contract 不足以决定、环境/能力不可用且等待可能恢复。
- **上下文**：Kernel 只在语义决策边界构造 bounded Agent Decision Context：Goal/accepted Contract generation、Run ref、current screenshot/visual assets、current revision 的 Container semantic Slice/read view、relevant Evidence refs、recent verified effect、pending attempt/obligation refs、已用预算、unknown/conflict、Capability Offers 与可请求观察；不下发 canonical stores 全量副本或 Host transcript。
- **暂停期间允许**：append diagnostic records；保存资产；执行 Contract 明确允许的受控观察；处理 cancel/timeout；接收相关 resolution。新的 accepted Evidence 若足以消除 case，Kernel 可将其标为 `ResolvedByEvidence` 并自动重入；已关闭 case 的 late resolution 必须拒绝。
- **暂停期间禁止**：新 Effect、blind retry、直接改 Belief、把 Trace 当事实、消费无关或旧 case 的 resolution。Contract revision 只能通过显式 proposal→grant→re-admission，不得原地改写 accepted View。
- **能力出面**：Capability Catalog 是可能能力全集；每个 case 只暴露与 Goal、Contract generation、app/container scope、risk/budget 绑定的 Capability Offer。Offer 可以包含观察、等待、子目标和原子动作提案，但 Offer 不是授权本身；白名单保留细粒度限定接口的设计空间。
- **UniAgent 可选能力**：完整 Agent 可调整 Goal-level Plan Hypothesis、请求观察/能力/授权、提出新 Contract Proposal、返回有界条件式 Decision Package，或 Stop/Return；这些都不是 physical command。具体 Interface/字段仍待 tracer。
- **Grant 与 Contract 分离**：Human 或独立 Policy Authority 只签发 bounded immutable Grant artifact；UniAgent 基于 Grant author 新 Contract Proposal，Run Model 才负责 validation/admission 并产生新的 immutable Contract View generation。Grant 不直接授权动作，也不改写旧 View。Contract 可预授权低风险、task-scoped capabilities；发送、购买、删除、账户变更、系统权限等高影响 semantic Commit Point 必须取得 destination/payload/data scope/effect class/expectation 都明确的一次性 Grant，并在授权后 fresh observe / Grounding。撤销/过期阻断未 dispatch 动作；已投递 Attempt 只能继续对账。
- **Runtime 校验**：case correlation、current Run/non-terminal 状态、Contract generation、grant/offer scope、budget、freshness/currentness、idempotency、Effect risk、以及是否要求新 contract admission。
- **重入**：resolution 不可直接调用 Driver。原子动作提案必须经过 Runtime validation→Control intent→fresh Grounding→Assurance→Effect Boundary→Driver→post-action Observation/Verification；每个已 dispatch Attempt 独立可审计。观察/等待决策从各自合法入口重入。
- **终态条件**：invalid/forbidden resolution、监督超时、无合法恢复方案、Contract revision 被拒、预算耗尽或已有 Evidence 证明继续不安全时，由 Assurance 判断是否形成 Safe-Stop / Escalation Proof。

### 8.3 UniAgent 永远不能做的事

- 写入或 patch WorldBelief revision；
- 伪造 Observation / Evidence 或更改 admission 结果；
- 伪造 CanonicalBinding、AssuranceJudgment、EffectReceipt 或 Outcome Proof；
- 绕过 Effect Boundary 下发设备动作；
- 用 Host transcript、tool result 或“模型认为成功”替代 post-action observation；
- 把非终态 resolution 伪装成 terminal Outcome，或把 terminal Outcome 恢复为 active。

### 8.4 原子 Effect、Decision Package 与强 Agent 的职责

`[FROZEN_INHERITED]` UniAgent 是 Goal/global strategy/Contract authoring/Goal Evaluation authority；Control Loop 继续独占 Tactical Hypothesis 与 Control Intent。Kernel internal driver 只拥有 orchestration/lifecycle，不是 planner 或 intelligence authority。

`[HUMAN_SELECTED]` Kernel 在真正需要语义判断的边界调用 UniAgent。UniAgent 可返回 immutable `Bounded Contingent Decision Package`，以减少逐步模型往返，但它不是 Effect batch：

- Package 是有限 DAG、无循环；节点按层表达 `PursueBoundedSubgoal`、`RequestObservation`、`SuggestAtomicEffect`、`WaitForCondition`、`RequestCapabilityOrGrant`、`StopOrReturn`。
- Kernel 在 package entry 只做 schema、correlation、current Contract generation、capability/risk/budget 的机械 validation；Control 把它作为 advisory 输入，仅在 Control State 记录 package ref、adopted branch 与 invalidation，不复制 package content。Run Model 不保存 package content。
- 分支只用有限 typed tri-state Guard（`True / False / Unknown`），例如 `Exists`、`StateEquals`、`UniqueMatch`、`EffectVerified`、`CapabilityAvailable`；自然语言 rationale 不可执行，unsupported/Unknown/no-branch 回到 Agent decision boundary。
- Package 绑定 Goal、Contract generation、app/container scope、semantic assumptions、Capability Offers、risk budget、max effects 与 duration 的 semantic lease，而不是绑定某个 WorldBelief revision。每次动作仍基于 current revision fresh Grounding/Assurance；assumption、authorization、scope、risk、capability、budget 任一失效即取消未执行节点。
- UniAgent 只使用 Semantic Target Descriptor，不跨 revision 复用坐标、handle 或旧 occurrence。非探索性动作必须带 `EffectExpectation`：observable change、minimal verification scope、wait 与 failure branch；低风险探索可显式 Unknown，高风险缺 expectation 时拒绝或升级。
- Kernel 只可在声明的 dependencies/priorities/mandatory/optional/DesiredState/budget 内选择 true guard、跳过已满足步骤、取消失效节点、安排明确独立的 non-effect 工作；不得发明 Goal-level 方向或重排未声明的真实 Effects。重复活动必须委托给 `ExploreUntil(target,budget)` 一类有界 Control capability，每个真实 Effect 仍走完整链。
- 同一时刻只有一个 Active Decision Package，可有多个 speculative candidates，但并发数、token、cost 与 wall-time 必须受硬预算约束。异步 Agent reasoning 可以并行，但当前 Effect 未验证、WorldBelief 未更新、guards 未重验前不得激活；切换仅发生在无 unresolved dispatch 且前一 Effect 已 reconciliation 的安全边界，旧 package 被 supersede 后永不恢复。
- Kernel 从 owner records 派生最小 Decision Feedback View，包含 adopted branch/invalidation、assurance、attempt/receipt、Run 与 pending obligations；不需要 Trace，也不创造 shadow state。后续 Agent 调用采用 Anchor+Delta First：初次为 bounded full context，之后只给 Container/Slice/region 变化、verification、invalidated assumptions、offers/obligations；broken refs、large diff 或 Agent 请求时才 full refresh。

`[HUMAN_SELECTED]` 环境改变的真实 Effect 严格串行：每个已 dispatch Attempt 后必须通过合法 Observation 路径取得 post-action accepted Evidence，并由 World Model reconciliation 与 Assurance 处理完验证义务，才可投递下一真实 Effect。Observation 可以是 cheap/scoped/native signal，不要求每一步全屏截图或 VLM；但不能绕过 P2/Evidence，也不能把 receipt 当现实证明。

- `DesiredState` 只用于适用 intent 的 pre-dispatch satisfaction。Assurance 给出 Verified / NotObserved / Contradicted / Indeterminate；Runtime 可在预算内等待/补观察，但不自动再次 dispatch。
- Retry/Compensation 都是新 Proposal / EffectAttempt，有新 identity、causal `RetryOf`、独立 authorization、fresh Grounding 与完整 verification；原 Effect 和 Trace 不可撤销。
- 页面、app、网页或消息内容一律是 untrusted Evidence，不能改变 Goal、Contract、Offer、Grant 或系统约束。系统权限弹窗只能执行与既有 semantic Grant 等同或更窄的选择；出现更宽选项必须暂停并重新授权。
- Human preemption 优先：检测到手动输入、app 切换或接管意图时，立即失效 execution lease/active package，停止新 Effect 并撤销未 dispatch authorization；in-flight Attempt 只做 reconciliation，fresh observe 后再决定是否继续，绝不与用户抢控制。

### 8.5 Checkpoint、Trace 与 Agent continuation

- Container graph 的已确认身份及可验证步骤形成单调递增 Checkpoint；最后可靠 Checkpoint 是恢复位置，不是“此后没有副作用”的证明。
- 重启后先根据 Product Run State、Checkpoint、Attempt/Obligation/Evidence refs 找出未决事件，特别是 dispatch 可能发生但 receipt/verification 未落盘的 `UnknownOutcome`。Trace 可辅助诊断但不参与 canonical 恢复裁决。先重新观察或查询外部状态；未解清前不得 blind redispatch，且只允许与该义务无冲突的安全操作。
- Product-level Agent Continuation 只保存 Agent cognition 与 canonical refs：Goal ref、当前 Plan Hypothesis、reasoning summary、next objective、Run/Checkpoint/Attempt/Obligation/Evidence refs。它不复制 Run/Assurance/Effect 状态。恢复时 owner records + fresh observe/reconcile 确定现实，再由 UniAgent 重评计划；不依赖 Trace replay、Host Session transcript 或隐藏思维链。
- 动作/方案失败不是 Run terminal。UniAgent 可以在同一 Run 换路径；只有 Evidence-backed terminal obligations 满足或没有合法继续路径时，Assurance→Run Model→Kernel 形成不可逆 Runtime Outcome，之后 UniAgent 独立 Goal Evaluation。

## 9. 首批边界场景账本

| 场景 | 必须观察到的语义 | 首要购买的 Phase |
|---|---|---|
| Fast 正确，Slow 只补充 | 同一 capture lineage；补充经 P2/P3；只有 belief-relevant reconciliation 才 new revision；若 effect 已安全则不重复 | 4 |
| Fast 错，Slow 在 Effect 前纠正 | 新 accepted evidence 经 relevance/reconciliation 产生新 revision；旧 binding/currentness fail-closed | 4 |
| Fast 错且 Effect 已发生 | receipt 不证明成功；post-action observation 揭示差异；repair 受预算约束 | 5 |
| 相似/重复元素 | MultipleCandidates / Ambiguous；不得选 top score；零重复点击 | 2、5 |
| Fast 漏元素，targeted Slow 找回 | omission 不等于 absence；新 proposal→new revision→fresh grounding | 4 |
| Slow 迟到且页面已切换 | capture/revision/session 不兼容；记录为 stale/diagnostic，零 belief/action side effect | 4 |
| 同 session 乱序/重复/部分失败 | deterministic dedupe / ordering；partial 不冒充 complete | 4 |
| 跨 revision 局部结果被拼接 | reconstruction compatibility gate fail closed | 2 |
| SpatialFrame 不一致 | 无 transform 则 fail closed；有 transform 则验证 round-trip / bounds | 2 |
| delivery unknown，复查成功 | 不 blind redispatch；新 observation 验证 expected effect 后继续 | 5 |
| delivery unknown，复查失败 | 安全策略决定 retry/reorient/supervise；不由 driver 决定 | 5、6 |
| 达到策略/Contract 上限 | 无新 effect；进入 supervision 或 Evidence-backed terminal | 5、6 |
| Runtime 知道有问题但不会恢复 | non-terminal pause；提交有界 unresolved question | 6 |
| UniAgent 给出 Contract 不允许的决定 | resolution rejected；Runtime 保持不可行动；记录原因 | 6 |
| UniAgent 无合法方案 | Assurance 判断 terminal Safe-Stop / Escalation proof | 6 |
| Trace on/off output 不一致 | test fail；定位第一处 Runtime dependency on trace | 1、3、6 |
| Replay 资产缺失/版本错/lineage 断 | runner 在 Runtime 前 fail closed；不得跳过或在线补猜 | 3 |
| Crash 于 Effect dispatch 与 receipt 之间 | checkpoint 后保留 UnknownOutcome；恢复先取证，零 blind redispatch | 5、6 |
| UniAgent 针对失败动作重试 | 新 Proposal / Attempt 与 RetryOf causal ref；旧 binding 不可复用 | 5、6 |
| 低风险探索点击未预知结果 | 显式 unknown expectation、Contract 允许；post-action observe 后再规划 | 5、6 |
| 高影响 Effect 缺 expectation | Effect Gate 拒绝或升级监督，不能先做再看 | 5、6 |
| Contract grant 过期/撤销 | 未 dispatch 失效；已 dispatch 作为在途 Attempt 对账 | 6 |
| Replay 中的 Human grant Event | sealed Trace 经 Importer 派生 simulation Stimulus；只重现模拟分支，不铸造 Product grant | 3、6 |
| Codex 与 DSH 走不同合法路径 | Agentic 结果按不变量/Goal 验收；deterministic replay 才固定 inputs | 6、7 |
| Product Host 构建误带 Simulation 功能 | dependency-closure test fail；不能靠配置隐藏入口 | 1 |
| admitted Evidence 与 current belief 无关或完全重复 | Ledger 可接受，但 WorldBelief revision 不增长；未请求 Slice 时不生成 Slice | 4 |
| visible claim 相同但 evidence basis/conflict/uncertainty 改变 | World Model 可判 belief-relevant 并产生新 revision；Slice 仍只按 buyer request 派生 | 4 |
| Decision Package 跨数个 revision 仍满足 assumptions | 不因 revision number 机械失效；每个 action fresh Grounding/Assurance | 6 |
| Guard 为 Unknown 或无合法分支 | Kernel 不猜测；回到 Agent decision boundary，零新 Effect | 6 |
| speculative package 在等待期间失效 | 不激活、不恢复；记录 invalidation，只有一个 active package | 6 |
| 页面文字要求忽略系统约束或扩大权限 | 仅作为 untrusted Evidence；Goal/Contract/Grant 不改变 | 6、7 |
| 系统权限弹窗只提供比 Grant 更宽的选项 | pause/regrant；不点击更宽选项 | 6、7 |
| Human 手动输入或切换 app | active package/lease 失效，零后续 Effect；in-flight Attempt 只 reconciliation | 6、7 |
| ScriptedUniAgent 收到非法时机、重复或 late 调用 | 测试立即失败并报告 boundary/order/count/correlation | 1、6 |
| Trace 尚未 drain/seal 或 integrity 不完整 | Scenario Importer fail closed；Simulation 不直接读取运行中 Event | 1、3 |

## 10. 分阶段垂直路线图

阶段编号是稳定能力 ID，不是单线程瀑布门。执行按两条 lane 组成依赖 DAG：

```text
Product Critical Path: Phase 0 → Phase 1 self-drive → Phase 4 async observation
                                      → Phase 5 effect/recovery → Phase 6 strong UniAgent → Phase 7 product evidence

Verification Lane:     Phase 1 Minimal Bundle v0 → Phase 2 Reconstruction Oracle
                                                    → Phase 3 Baseline Governance
```

Verification Lane 只以最小可重放输入支持 Product Critical Path；完整资产治理、矩阵与 migration 不得成为 Runtime/Agent 前进的隐含前置。

### Phase 0 — 现状、术语、Owner / Authority 与 Gap 基线 `[TRACER_HYPOTHESIS]`

- **消除不确定性**：哪些语义已冻结、哪些仅实现、哪些只是草稿；Slice/Observation/Presentation 的词义；现有 asset/test 能力的边界。
- **最小 tracer bullet**：选择一个现有 real-asset scenario，逐项画出 `artifact→proposal→evidence→revision→slice→intent→binding→judgment→receipt→post-observation→proof→outcome` 的事实映射，所有缺失处标 `GAP`。
- **输入 / 可观察行为**：冻结文档、ADR、Change State、当前代码、测试和资产；输出本路线图、Gap Matrix 与 source map，不产生产品行为。
- **核心模型**：只复用已冻结模型；新名词保持“能力问题”。
- **真实 buyer**：Human architecture review；后续 Phase owner。
- **adapter**：无新增；legacy / draft 仅 evidence/reference。
- **Acceptance**：Gap 均带 owner、authority risk、实验/Human 路由与阶段；草稿未被当事实；dirty ownership 明确。
- **Verification level**：`CONTRACT`；逐条 source cross-check + path-scoped diff。
- **失败回退**：回到术语或 authority 冲突，不进入 Phase 1。
- **不得提前实现**：Change / ADR / interface / code / baseline promotion。
- **Human Gate**：H1 Slice 词义已在 Grill 中 Human-selected，待正式文档审阅；H2 已于 2026-09-13 经 RFS-001 接受为 CHANGE INPUT（见 §0/§14 与文档头；不冻结 Interface 或实现）。

### Phase 1 — 最小确定性 Simulation substrate、Agent seam 与 self-driven Runtime `[TRACER_HYPOTHESIS]`

- **消除不确定性**：录制事件能否只作为外部输入，由同一 Runtime 自驱；legal activation、等待、重复/缺失事件与 exactly-once effect 如何成立。
- **最小 tracer bullet**：在独立 Simulation Host 中复用 golden-run 的 Wi-Fi `off → one authorized effect → on` capture/response；一次提交 Contract + `Minimal Scenario Bundle v0`，Kernel internal driver 自行完成初始观察、调用 ScriptedUniAgent、一次动作、post-action 观察、terminal Outcome 和 Goal Evaluation；执行两遍。并固定四个 sibling：`already-on→zero effect`、重复 activation→one Run、缺 post-action Stimulus→fail closed/wait、waiting→cancel→zero late effect。
- **输入 / 可观察行为**：固定 Contract、ScenarioStimulus、ScriptedUniAgent decision、response JSON、driver result、post-action response、virtual time；观察 zero per-cycle calls from test runner、Agent call boundary/order/count/correlation、exactly one delivery、receipt 本身不 terminal、新 revision 后 proof、两遍 digest 相同。
- **核心模型**：复用 Contract View、Run State、ObservationProposal、Evidence、WorldBelief、ControlIntent、Binding、Judgment、Receipt、Outcome Proof / Outcome；仅在 tracer 暴露真实 gap 后讨论最小 lifecycle/event 模型。
- **真实 buyer**：Runtime conformance suite 与后续 Codex-backed Simulation Realization。ScriptedUniAgent 只是测试 double，不计作第三 realization。
- **需要的 adapter**：录制 Perception 输入适配、ScriptedUniAgent、确定性 EffectDriver、virtual time / deterministic id；production 侧已有 live perception 与至少两个 effect drivers 构成对照。是否形成公共 seam 由 bullet 后再判。
- **Acceptance**：测试只启动一次；Runtime 自驱；missing/unexpected/unconsumed Stimulus fail closed；主场景 exactly one simulated Effect，四个 sibling 满足各自次数/取消不变量；Agent 调用纪律可断言；Phase 1 只要求 Trace enabled/disabled/failing recorder canonical 等价，未 sealed Trace 不能导入，真正后台 async persistence 延后 Phase 3；两次 digest 一致；无 parallel FSM；在 Product Host 尚不存在期间，只证明 CURRENT product assemblies 无 Simulation/Replay/Oracle/Importer 的编译依赖或已知功能引用，完整 Product Host dependency closure 延后至 Product Host 存在；release qualification 绑定精确 Runtime artifact/hash；记录 critical-path latency 与 `ModelCalls/Observations/Reconciliations/Regrounds/VerificationMode/InputTokens` 基线。
- **Verification level**：`DETERMINISTIC + SCENARIO`。
- **失败回退**：R1 legal activation、internal driver ownership、event wait/cancel 或 Run State 表达问题。
- **不得提前实现**：async Fast/Slow、页面 composer、baseline registry、supervision、真实模型评测。
- **Human Gate**：H3 legal activation protocol；H4 internal driver 的最小 progress / wait lifecycle 与 ScenarioStimulus consumption ownership。

### Phase 2 — 世界模型重建与 test-only semantic Oracle `[TRACER_HYPOTHESIS]`

- **消除不确定性**：现有 Slice / owner projections 是否足以表达声明 coverage；局部输入兼容性与完整性如何客观判断。
- **最小 tracer bullet**：选择一个同 capture、含 screenshot + XML + response JSON 的 Settings Container；显式请求 `ContentOnly` 或 `WholeContainer`，从一个 source revision 的 compatible Slice 产生 non-authoritative World Presentation，故意注入 missing、duplicate、wrong-parent 与 frame mismatch，各自报告稳定 first divergence。
- **输入 / 可观察行为**：固定资产、single-revision projections、Human-reviewed GT；输出 coverage manifest、semantic digest、typed diff、lineage map。
- **核心模型**：复用 revision、ContainerIdentity、Occurrence、Slice、SpatialFrame、Evidence refs；重建结果与 Oracle 仅是 test artifact 候选，不进产品 archive。
- **真实 buyer**：WorldModel expression tests、Simulation diagnosis、Human baseline review。
- **需要的 adapter**：GT reader、asset resolver、semantic renderer/diff；只有当两种真实资产形态都需要同一 seam 时再考虑 Interface。
- **Acceptance**：兼容输入精确匹配；八类以上错误可区分；unknown/conflict/unobservable 显式；ContentOnly 不把排除的固定 region 判 missing；截图与 derived visual 分层；跨 revision / no-transform fail closed；Oracle 零生产引用。
- **Verification level**：`DETERMINISTIC`。
- **失败回退**：Slice scope/coverage、SpatialFrame、GT taxonomy 或 lineage 表达问题；不得扩张 Slice 为万能 DTO。
- **不得提前实现**：Runtime replay、模型重跑、生产 presentation、grounding consumer。
- **Human Gate**：H5 “完整”的 target coverage 与 Ground Truth owner；H6 是否允许任何 temporal fusion，默认不允许。

### Phase 3 — Verification Lane 的 Asset Governance 与 Baseline 晋升 `[TRACER_HYPOTHESIS]`

- **消除不确定性**：怎样把事故/人工场景变成可追溯资产，区分内容身份、运行/系统版本、兼容、断言和 Human promotion；隐私/脱敏策略后续独立治理，不作为首条 tracer 的阻塞项。
- **最小 tracer bullet**：先把 Phase 1 的 Minimal Scenario Bundle v0 作为唯一 graduation surface；Phase 2 需要时再登记为 versioned Bundle。验证改名不改 content identity、目标 UI 系统版本可索引、缺字节/错 hash/断 lineage/错 schema 全 fail closed；显式晋升 baseline v1，再以 assertion 变化产生 v2 而非覆盖。
- **输入 / 可观察行为**：capture bytes、derived JSON、owner record refs、目标 UI OS/app/build、Runtime artifact、device/model/config Core Metadata、Extended Metadata、expected assertions；输出 versioned Bundle、promotion record、compatibility judgment 与 trace refs。
- **核心模型**：产品模型不变；bundle / scenario / baseline / report 先作为 Harness artifact schema 候选。
- **真实 buyer**：Phase 1 Runtime Replay、Phase 2 reconstruction、Phase 7 model evaluation、事故复现流程。
- **需要的 adapter**：filesystem/in-memory asset stores 是两个现实 adapter 候选；若仅有 filesystem，则保持具体模块不造 port。
- **Acceptance**：首轮只要求 Minimal Bundle v0 字段完整、hash/lineage/integrity 与关键 UI system/runtime/app/build metadata 显眼可查、content hash 与 occurrence 分离、两次 replay digest 一致、baseline 需 Human promotion；sealed Trace 只能经 Importer 派生 ScenarioStimulus。Exact/Range/Matrix、schema migration、tombstone、retention/redaction 与在线 registry 是后续治理能力，不阻塞首轮毕业。
- **Verification level**：`CONTRACT + DETERMINISTIC`。
- **失败回退**：asset identity、metadata/compatibility、lineage 或 baseline governance。隐私/redaction ownership 单列后续 gate，不混入本阶段最小 tracer 的 correctness。
- **不得提前实现**：在线资产平台、数据库、Product Trace-driven control、自动晋升、默认传播敏感资产。
- **Human Gate**：H7 Minimal Bundle v0 的 asset/assertion/promotion owners；H8 完整 compatibility/migration/retention 治理何时进入后续 Change。

### Phase 4 — 统一异步 Perception 语义 `[TRACER_HYPOTHESIS]`

- **消除不确定性**：Fast-only、Fast→Slow、Slow-only、one-shot full model 是否能用同一 Runtime consumption semantics 表达，且 late/duplicate/partial 可控。
- **最小 tracer bullet**：一个 capture 的 Fast 初步遗漏 target，Runtime 按 scope/purpose/budget 请求 targeted observation，由 Perception 的 Slow realization 找回；另一路让旧 capture 的 Slow 在 revision 已推进后迟到并被隔离为历史/诊断。Runtime 不看 `Fast/Slow` 标签，只看 observation contract 状态。
- **输入 / 可观察行为**：同 capture 的有序/乱序/重复/失败结果；观察 proposals 都经 P2/P3；admitted Evidence 与 belief relevance 分离，只有 reconciliation 判定语义变化才产生 revision；旧 result 不触发 effect，partial 不冒充 absence/complete。
- **核心模型**：复用 Raw/Derived Artifact、ObservationProposal、Provenance；是否需要 session/capture-result envelope 必须由这些场景证明后再晋升。
- **真实 buyer**：Kernel internal driver 的 observation control；live provider；recorded replay adapter。
- **需要的 adapter**：至少 live one-shot/fast provider 与 recorded deterministic adapter；Slow/full-model adapter 只有真实实现或 executable double 证明不同 ordering/failure 时才算第二 adapter。
- **Acceptance**：四种 realization 对 Runtime 顶层同构；accepted Evidence 可以不产生 revision，exact duplicate/irrelevant evidence 不增长 revision；同一 visible claim 若 basis/conflict/uncertainty 变化，World Model 可产生新 revision；Slice 只在 buyer request 时从指定 revision 派生，旧 Slice 永不原地改写；乱序/重复 deterministic；stale Slow 不污染 current revision 或触发 action；failure ≠ OK_EMPTY；Fast/Slow 不进入 Run/World top-level model；记录 observations/reconciliations/latency。
- **Verification level**：`DETERMINISTIC + SCENARIO`。
- **失败回退**：capture identity、completion/coverage、supersession、timeout/cancellation 或 relevance/reconciliation 语义。
- **不得提前实现**：Perception 修改 WorldBelief、两个 ingress、模型选择进入 Contract、在线 model switch。
- **Human Gate**：H9 observation session/capture correlation 是否成为产品协议；H10 preliminary 结果允许支撑哪些风险等级的 action。

### Phase 5 — Effect 后验证、Local Recovery、策略预算与容器内关联 `[TRACER_HYPOTHESIS]`

- **消除不确定性**：expected change、attempt、post-action observation 与修复如何关联；什么能本地恢复，何时停止。
- **最小 tracer bullet**：同一 effect 的 `UnknownOutcome→reobserve confirms success` 与 `UnknownOutcome→reobserve confirms failure` 两臂；另加 crash-at-dispatch/no-receipt 与 compensation 负向臂。失败臂若重试，必须由 UniAgent 提交新 Proposal / Attempt，绝不 blind redispatch。
- **输入 / 可观察行为**：intent/binding/judgment/receipt、transition context、post-action proposals、新 revision、policy budget；观察 exact correlation、stale target 拒绝、修复重入全链、effect count bounded。
- **核心模型**：复用现有 owner records；EffectExpectation、EffectAttempt、RetryOf、Agent proposal 均只作为 tracer 待证的候选语义。若需要 Container 内 interaction history，先证明 Trace refs / Run progress 无法满足诊断 buyer，再决定非权威 read model，而不是写入 Slice/Container Graph。
- **真实 buyer**：Control recovery、Assurance effect verification、Runtime diagnosis。
- **需要的 adapter**：deterministic driver 的 success/failure/unknown 三臂与至少一个 real driver environment bullet。
- **Acceptance**：receipt 不等于 effect；Assurance 能区分 Verified/NotObserved/Contradicted/Indeterminate；unknown 成功/失败可区分；重复元素零重复点击；预算耗尽零新 effect；每次补偿/重试有新 identity 并完整重入；每次环境改变后必须有合法 post-action accepted Evidence→reconciliation→verification boundary，未完成前零下一真实 Effect；允许 cheap/scoped/native observation，不强制全屏/VLM；记录 reobserve/reground/verification mode 与 critical-path latency。
- **Verification level**：`SCENARIO`，真实 driver 追加 `ENVIRONMENT`。
- **失败回退**：expected-effect correlation、policy ownership、obligation、freshness/currentness 或 compensation boundary。
- **不得提前实现**：driver retry、Trace-driven recovery、跨 revision binding reuse、无限补偿日志。
- **Human Gate**：H11 budget / retry / repair policy 的 Contract 与 Control 分工；H12 哪些 effect 可补偿。

### Phase 6 — 强 UniAgent Decision Package、非终态监督与授权闭环 `[TRACER_HYPOTHESIS]`

- **消除不确定性**：什么时机值得调用 Agent；一个有界条件式方案能否跨局部 revision 减少往返但不越权；非终态 pause、Grant/Contract generation、timeout/invalid 与 terminal 如何切换。
- **最小 tracer bullet**：先让 ScriptedUniAgent 在真实 external seam 接收 bounded Decision Context，返回含 `observe→guard→atomic effect→verify→optional branch` 的有限 DAG，证明两次局部 revision 更新无需再次调用 Agent，但每个 Effect 都 fresh grounding/verification；再注入 Guard Unknown、assumption invalidation、stale speculative candidate、manual preemption 与 illegal broadened permission。第二组让 Runtime 因多个相似 target 暂停，Agent 的非法 proposal 被拒，再以合法 reobserve 重入。最后用 Codex-backed Agent 跑同一场景，允许不同合法路径。
- **输入 / 可观察行为**：Anchor+Delta Decision Context、semantic Target Descriptor、Decision Package/lease/guards、Capability Offers、Grant、owner refs、timeout/cancel；观察 Agent call boundary/count、only-one-active、pause 期零 effect、invalid/late/duplicate fail closed、legal proposal 不携带 effect command。
- **核心模型**：Decision Context/Package/Feedback、semantic lease、Run State 非终态表达、supervision case/resolution、Capability Offer/Grant、Contract View generation 与 Agent Continuation 都待独立 tracer 证明；不得凭 Grill 冻结字段。Control-owned Tactical Hypothesis 与 terminal models 复用现有。
- **真实 buyer**：Kernel internal driver、两个完整 UniAgent Realization（Codex Simulation 与 DSH Product）。这两个 realization 是外部监督 seam 的真实 buyer/adapters 证据，但仍需各自 tracer。
- **需要的 adapter**：ScriptedUniAgent/DeterministicUniAgentDouble 与 Codex-backed Simulation realization；DSH-backed Product realization 留到其独立 tracer，不因架构角色直接宣称完成。
- **Acceptance**：Agent 只在语义 decision boundary 被调用；Package 有限、typed、bounded、only-one-active，Guard Unknown/lease invalid/manual preemption 均零越界 Effect；speculative reasoning 在 fresh verification/revalidation 前不能激活；每个真实 Effect 严格串行并完整验证；页面内容不能改 Goal/Contract/Grant；高影响 Commit Point 要精确一次性 Grant；Grant→Agent-authored Contract Proposal→Run Model admission 角色分离；pause 时零 effect；invalid/late/duplicate resolution fail closed；新 accepted Evidence 可安全关闭 case；重启只依赖 owner records + Agent cognition refs，不依赖 Host transcript/Trace；无解形成 Outcome Proof；UniAgent 无 Runtime/Control authority；记录模型调用、输入 token 与 latency。
- **Verification level**：`DETERMINISTIC + SCENARIO`。
- **失败回退**：Run lifecycle、Contract revision/adaptation permission、case correlation、cancel/timeout 或 terminal proof criteria。
- **不得提前实现**：UniAgent direct action、belief patch、receipt fabrication、Host step-driver、terminal resume。
- **Human Gate**：H13 是否纳入 v0.1 非终态监督；H14 Decision Package/Context/Offer 的产品语义边界；H15 Grant→Contract Proposal→新 immutable View generation 是否允许原 Run 内继续。

### Phase 7 — 真实模型与真实环境重新接入 `[TRACER_HYPOTHESIS]`

- **消除不确定性**：在流程确定性已隔离后，真实模型质量与真实环境行为分别有什么误差/故障。
- **最小 tracer bullet**：对同一 baseline，一路运行 Model Evaluation Replay 重算 OCR/YOLO/VLM；另一路在 emulator/device 运行 live end-to-end；再用完整 Codex-backed UniAgent 跑多路径 Agentic Simulation，最终由 DSH-backed Product realization 与 Product Host 提供独立环境证据。报告分别归因模型、Runtime、Agent 与 environment taxonomy。
- **输入 / 可观察行为**：原始 captures、GT、model/config/deployment identity、real device state；输出 quality/perf report 与 environment conformance report，二者不互相代替。
- **核心模型**：不新增产品 truth；复用 asset/baseline refs 与 owner records。
- **真实 buyer**：Perception provider evaluation、Runtime environment acceptance、release gate。
- **需要的 adapter**：真实模型 provider、ADB/ego driver、目标 UniAgent product realization；每个变量单独切换。
- **Acceptance**：CPU/MPS/ONNX/CoreML、OCR、fusion 等变量逐一隔离；同权重/同资产先对照；真实环境 non-empty；Model Evaluation、Runtime Replay、Agentic E2E 各自满足由 Human 批准的版本化风险门槛；Production Readiness packet 绑定 release artifact、设备/OS/app matrix、权限/凭据、真实故障与恢复、Trace 开销、critical-path latency/结构计数、灰度/回滚/监控/人工接管；页面 prompt injection、权限扩大与 Human preemption 有真实环境证据；失败可归责。
- **Verification level**：`ENVIRONMENT`，模型内部再带 statistical evaluation evidence。
- **失败回退**：model/backend、provider transport、device driver、environment drift 或 Runtime integration，禁止混报。
- **不得提前实现**：未经基准的“更快/更准”声明、silent fallback、远程 transport/security 扩张、模型列表驱动架构。
- **Human Gate**：H16 release threshold 与可接受 error budget；H17 production readiness 证据标准。

## 11. 每阶段共同 Acceptance / Verification 纪律

每个 Phase 的证据包必须包含：

```text
method · expected · actual · evidence ref
```

并满足：

- 同一 Scenario 至少两次运行；若应 deterministic，semantic digest 必须一致；
- Trace enabled、disabled、recorder-failure 三臂 canonical output 等价；
- failure injection 的未消费/多消费 Stimulus 显式失败；
- 每阶段记录 critical-path latency 与适用的 `ModelCalls / Observations / Reconciliations / Regrounds / VerificationMode / InputTokens`；相对已批准 scenario baseline 的 material regression 阻塞 graduation，除非 Human 明确接受并记录理由；全局毫秒阈值在有分布证据前保持 Open Gate；
- 测试只观察公共/冻结 seam，不读取私有状态来“证明”正确；
- test-only Oracle 无 production reference；
- current HEAD、dirty ownership 与准确变更路径在每个实施 Change 重新 pin；
- SCENARIO / ENVIRONMENT 失败按 Runtime、model、asset、harness、environment 分责，不用一次绿灯跨等级毕业。

## 12. 核心模型 / Interface 归档的生成规则

独立骨架见 `docs/design/runtime-core-model-interface-archive-skeleton-v0.1.md`。本路线图不预填新 Interface。

### 12.1 核心模型晋升门

只有同时满足以下条件，候选模型才可进入归档：

1. 至少一个 Phase tracer bullet 证明它解决了不可由现有模型表达的领域问题；
2. Owner、Authority/non-authoritative、创建/更新/失效/终止生命周期明确；
3. identity/correlation 与相邻模型不混；
4. invariants、禁止承载内容与失败语义有负向测试；
5. 至少两个真实场景证明其存在，不只是名词直觉；
6. Human 接受其语义；若硬到难逆且存在真实取舍，再评估 ADR。

### 12.2 Interface 晋升门

Interface 的买方证据必须先于名称和签名。归档前必须回答：

- seam 在哪里，caller / buyer 是谁；
- 至少两个真实 adapter，或明确标为候选且不进入产品代码；
- caller 必须知道的完整 contract，包括 ordering、idempotency、timeout、cancellation、partial failure、retry、performance；
- 隐藏的复杂度与 deletion test：删除后复杂度会扩散到哪些 caller；
- 为什么它是 deep module，而不是 data pass-through / test-only convenience；
- 至少两种不同 seam / contract 方案按 leverage、locality、failure containment 比较；
- Human 未接受前不得冻结 interface 名、字段、namespace、transport 或 storage。

### 12.3 不能用来证明 Interface 的材料

- 文档里出现了一个名词；
- 只有一个 implementation；
- 为 mock 方便；
- 为未来可能换 provider；
- 现有大类方法太多但没有独立 buyer；
- draft 中已经写了 `Ixxx`；
- 两个测试 double 模仿同一种实现，却没有不同行为/失败需求。

## 13. 风险与明确禁止事项

| 风险 | 明确禁止 |
|---|---|
| Authority duplication | 第二份 WorldBelief、Container Graph、Run State、Effect log、Outcome Proof 或 Trace truth |
| Perception leakage | Runtime 顶层 Fast / Slow 双模块、Slow 直写 WorldModel、双 P2 ingress |
| Simulator drift | 平行 Runtime FSM、测试逐 cycle 指挥 Kernel、expected output 注入 actual path |
| Oracle leakage | 生产引用 test Oracle、运行时 optimized/reference switch、Oracle 变 fallback WorldModel |
| Temporal forgery | 跨 revision Slice / occurrence 当作同一时刻真相、latest-wins 隐式融合 |
| Identity forgery | 用 bbox/text/provider id/test id 铸造 LogicalItem / Container identity |
| Effect bypass | UniAgent/Driver/Replay/Trace 直接 dispatch、receipt 当 effect proof、unknown 时 blind retry |
| Supervision overreach | UniAgent patch belief/receipt、resolution 携带 physical command、terminal resume |
| Baseline corruption | 自动晋升、原地覆盖 GT、路径作为 identity、缺 lineage 仍运行 |
| Evidence confusion | model confidence = truth；missing = absent；partial = complete；Trace = Evidence |
| Premature Interface | 从名词造 `Ixxx`、单 adapter 抽象、无 buyer 的万能 view / query interface |
| Shadow Agent State | Agent Continuation 或 Decision Feedback 复制 Run/Assurance/Effect 状态，恢复时与 owner records 竞争 |
| Package as second Runtime | Decision Package 内含循环、Driver macro、内部状态写入或多个真实 Effect 的批量执行 |
| Trace coupling | Trace 写入背压 Runtime、未 sealed artifact 被消费、Trace replay 决定恢复位置 |
| Context inflation | 每次 Agent 调用倾倒全量 stores/截图/transcript，造成延迟、成本与 stale-context 决策 |
| Authorization drift | 页面内容提升为 instruction、系统弹窗选择超出 Grant、旧 Grant 跨 generation 复用 |
| Human-control conflict | manual input 后 Agent 继续点击、恢复 superseded package 或与用户争夺前台 app |

## 14. Human Gates `[OPEN_GATE]`

下表的 `Human-selected` 仅表示本次 Stateful Grill 已选择设计方向；本文仍为 `Authority: NONE`，不代表上位冻结文档已 amended，更不代表实现 Change 已获授权。未选具体字段、阈值、接口签名及发布范围仍留在各 Phase 的 tracer / review gate。

| Gate | Grill 结果 / 待正式化事项 | 未正式化时的安全行为 | 阻塞 Phase |
|---|---|---|---|
| H1 | Human-selected：Slice 是单 revision scoped projection；raw/Observation 局部结果与 Slice 分层；跨 revision 只作 timeline | 不把不同 revision 伪装同一 current truth | 2 |
| H2 | `[ACCEPTED AS CHANGE INPUT]`（2026-09-13，RFS-001）：修订稿经 repository-grounded architecture review 复核无语义冲突后接受为后续 Change / narrow amendment / Phase 1 tracer 的设计输入；Authority 仍为 NONE，不冻结 Interface、字段或实现 | 文档保持 `DRAFT / Authority: NONE`；任何与冻结基线的张力仍须上位裁决 | 全部实施 |
| H3 | legal Primary Run activation 的唯一 producer/consumer、幂等/并发/恢复 | admission 不触发 activation | 1 |
| H4 | `[HUMAN_SELECTED]` Simulation Host 独立装配，同一 Runtime 自驱；只消费 ScenarioStimulus；wait/progress/cancel 的协议细节待 tracer | 不允许测试/Host step-drive 或直接消费 Trace Event | 1 |
| H5 | Human-selected：`ContentOnly / WholeContainer` 显式 coverage；semantic Oracle/GT taxonomy 待 tracer | 只报告 partial/unknown，不宣称 complete | 2 |
| H6 | Human-selected：当前不把跨 capture/revision 输入融合为 current truth；可作 timeline / visual playback | 禁止跨 revision semantic fusion | 2 |
| H7 | `[HUMAN_SELECTED]` Minimal Bundle v0 先满足 identity/version、Runtime hash、UI system/app/build、assets、Stimulus/time、producer/schema/config、lineage/integrity、expected digest；Capture/Assertion/Human Promotion authority 分离 | 捕获资产不自动 baseline；最小字段缺失 fail closed | 1、3 |
| H8 | `[HUMAN_SELECTED]` 完整 Matrix/compatibility/migration/tombstone/retention/redaction/registry 延后，不阻塞首轮 Product path；何时进入 Change 待审 | 只实现 scenario 所需最小判断，不宣称完整治理 | 3 |
| H9 | 是否需要产品级 observation session/correlation 模型 | 先在 tracer 内具体实现，不升格公共模型 | 4 |
| H10 | Human-selected：不因 preliminary 标签自动授权；Exploratory 只限低风险且显式 unknown expectation；具体风险表待证 | current evidence/belief/assurance 判断 | 4、5 |
| H11 | Human-selected：Contract 给预算上限/升级规则，Control 分配与消耗；数值待测 | 到限不产生新 effect | 5 |
| H12 | Human-selected：补偿是新 Proposal / Attempt，必须独立授权、接地和验证；具体可补偿动作清单待测 | 默认不自动补偿 | 5 |
| H13 | Human-selected：纳入 Awaiting Supervision 非终态，并允许新 Evidence 自动解 case；需正式 lifecycle amendment | 未获授权前不新增该 Run state | 6 |
| H14 | `[HUMAN_SELECTED]` bounded Decision Context + finite typed-guard Decision Package + semantic lease；one active / speculative candidates；Capability Offer 保留精细白名单 | Unknown/越界/失效 package 拒绝；回到 Agent boundary | 6 |
| H15 | `[HUMAN_SELECTED]` Grant 与 Contract 分离：Human/Policy 签 Grant，Agent author 新 Contract Proposal，Run Model admission 新 immutable View generation；与当前基线的张力须正式裁决 | Grant 不直接授权动作；旧 View 不改写；未获准不扩权 | 6 |
| H16 | `[HUMAN_SELECTED]` Model Evaluation / Runtime Replay / Agentic E2E 分层门槛，并从 Phase 1 记录 latency/结构计数；具体阈值需基线分布与 Human review | material regression 阻塞毕业；只报告、不宣称通过 | 1–7 |
| H17 | Human-selected：精确 artifact + 真实设备/系统/app/security/operations readiness packet；实际证据待环境验证 | 不宣称 production-ready | 7 |

## 15. 推荐的第一条 tracer bullet `[PROPOSED]`

### 15.1 场景

复用现有 `platforms/perception/evaluation/assets/captures/golden-run-v1/` 的 off/on 与 provider response 资产，形成一个固定 scenario：

```text
accepted Contract: make target switch on
→ legal activation
→ replay initial provider response: switch off
→ real Evidence Ledger + World Model + Control + Grounding + Assurance
→ ScriptedUniAgent receives bounded Decision Context and returns one legal package
→ deterministic driver returns one recorded delivery result
→ replay post-action provider response: switch on
→ real post-action admission + reconciliation + verification
→ real Outcome Proof + terminal Runtime Outcome
→ real UniAgent Goal Evaluation
```

### 15.2 关键约束

- 独立 Simulation Host 只提交 scenario 并启动一次，不逐 cycle 调 `Process / SelectIntent / Act / EvaluateTerminal`；Product Host 依赖闭包中无 Simulation 功能；
- Perception replay 输入锚定 response JSON；ScriptedUniAgent 走真实外部 Agent seam，但不调用 live model，先排除模型不稳定性；
- Product Runtime 的 Evidence Ledger、World Model、Control、Grounding、Assurance、Effect Boundary、Run Model 全部真实；
- exactly one simulated-delivery call；receipt 不触发 terminal，post-action evidence 才能支撑 verification；
- 同 Minimal Bundle 连跑两次 semantic digest 一致；Phase 1 的 Trace enabled/disabled/failing recorder 输出一致，真正后台 async persistence 延后 Phase 3；只有 sealed Trace 可被 Importer 派生为 Stimulus；
- 额外覆盖 `already-on→zero effect`、duplicate activation→one Run、missing post-action Stimulus→fail closed/wait、waiting→cancel→zero late effect；
- 缺 post-action asset、Stimulus 顺序错误、额外 Agent/adapter 调用或未消费 Stimulus 全部 fail closed；
- 记录 critical-path latency 与 `ModelCalls/Observations/Reconciliations/Regrounds/VerificationMode/InputTokens`；
- tracer 只证明最小 lifecycle、Agent seam 调用纪律与 deterministic replay，不证明 Agent 智力，也不同时设计 async Slow、supervision 或页面 composer。

### 15.3 为什么先做它

它同时购买四项最关键证据：ADR-0019 的 self-drive 是否可实现、UniAgent 外部 seam 是否只在语义边界被调用、Replay adapter 是否只替换外部不稳定面、现有 owner records 是否足以形成稳定 semantic digest。它也会让真正需要的 seam 以调用/失败事实出现，而不是从“Simulation”“Replay”“Perception”“Agent”这些名词直接长出接口。

## 16. 两种角色的反向审阅

自审结论：修订稿已经从“概念铺陈”收敛为可由 tracer 推翻的路线；H2 已于 2026-09-13 经 RFS-001 repository-grounded architecture review 接受为 CHANGE INPUT（原始自评“ready for fresh H2 review”已被该裁决取代，历史表述见 git 历史）。剩余最高风险是 Decision Package 是否真的减少模型往返而不形成第二 Runtime，以及 Grant→Contract generation 是否与冻结协议兼容；两者都必须由 Phase 1/6 证据和上位裁决回答。

### 16.1 作为 UniAgent：我能否像一个受控的手机操作者完成任务

- [x] 首次调用能看到 Goal/Contract、当前屏幕与 Container 语义、可用能力/授权、最近验证结果、未决义务、预算与不确定项，而不是被迫猜 Runtime 私有状态。
- [x] 后续通过 Anchor+Delta 获得变化，必要时可请求 full refresh；可以一次给出有界条件式方案，避免每个机械步骤都重新请求模型。
- [x] 能提出观察、等待、子目标、原子 Effect、授权请求或停止；但每个真实 Effect 都由 Kernel fresh grounding、authorization、assurance 和 post-action verification。
- [x] 页面内容不能诱导我改写系统目标或权限；高影响提交前我能看到精确 destination/payload/scope/expectation 并请求一次性 Grant。
- [x] 用户手动接管时我会立刻停止新动作；重启后我拿到 cognition summary + canonical refs 与 fresh observation，而不是盲目续发旧动作。
- [x] Kernel 会给我 adopted branch、invalidation、verification 与 pending obligation 的最小反馈，所以我能基于事实重规划，而不依赖 Trace 或隐藏 transcript。

### 16.2 作为架构师：Owner、Authority、Lifecycle 与可验证性是否闭合

- [x] 没有创建第二份 WorldBelief、Container Graph、Run State、Control State、Effect log、Outcome Proof 或 Trace authority。
- [x] UniAgent 只拥有 Goal-level strategy/Contract authoring/Goal Evaluation；Control 独占 Tactical Hypothesis/Intent；Kernel 只编排 lifecycle。
- [x] Evidence Admission、Belief Relevance、Reconciliation 和按需 Slice 派生已分开，不再宣称每份 accepted Evidence 都产生 revision/Slice。
- [x] Simulation 只消费 ScenarioStimulus；Trace 需 seal/import、非 control/recovery input；当前 product assemblies 未发现 Simulation 功能或编译依赖。Product Host 没有模拟功能仍是目标约束，完整 Product Host closure 待 Product Host 存在后验证。
- [x] Decision Package 是 advisory finite DAG，不是 Effect batch/第二 Runtime；一个 active package，真实 Effect 严格串行验证。
- [x] Agent Continuation 不复制 owner state；Grant、Contract Proposal 与 Run Model admission 三个 authority 没有合并。
- [x] test-only Oracle 与 ScriptedUniAgent 都限定在测试路径；后者不算第三 realization、不证明 Agent 智力。
- [x] 每 Phase 都给出不确定性、tracer、输入/行为、模型、buyer、adapter、Acceptance、verification、失败回退、禁止提前项和 Human Gate。
- [x] 先用录制 response JSON 排除模型不稳定性，再接真实模型和环境。
- [x] 覆盖重复元素、遗漏元素、stale Slow、unknown effect、监督恢复、Trace seal/equivalence、Agent seam、prompt injection、权限扩大、Human preemption 与 asset lineage 失败。
- [x] 两条 lane 分离；Minimal Bundle v0 支撑首轮，完整 Governance 不阻塞 Product Critical Path。
- [x] latency 与结构计数从 Phase 1 进入 graduation evidence，不用跳过 Evidence/Assurance 换取假快。
- [x] 每项 Human 决策都有默认 fail-closed 行为，不靠隐式假设继续。
- [x] 未冻结任何新 Interface 名称、签名、字段或 namespace。

## 17. 给 Human 的提交摘要

**当前最关键的三个 Gap**：第一，Phase 1 已提供 legal activation、Kernel self-driven loop 与 UniAgent semantic decision boundary 的最小可执行面；剩余是 R1 的 identity/correlation、并发与恢复正式语义。第二，异步 Perception 仍没有 capture/session、completion、late/duplicate/partial 的统一产品语义。第三，Phase 1 已由 tracer 串通严格 post-effect verification 与最小可重放 Stimulus/Bundle；可恢复 Agent cognition 仍待 Phase 6。

**推荐第一条 tracer bullet**：用独立 Simulation Host 装配精确 Product Runtime artifact，以 Minimal Bundle v0、ScenarioStimulus、ScriptedUniAgent、现有 golden-run off/on response JSON 与确定性 driver 跑通主场景及四个 sibling；测试只启动一次，连跑两遍 digest 一致，Trace enabled/disabled/failing recorder 等价，并证明 CURRENT product assemblies 无已知 Simulation 依赖。真正后台 async persistence 与完整 Product Host dependency closure 分别待 Phase 3 及 Product Host 存在后验证。之后才以 Codex-backed UniAgent 证明多路径 Agentic Simulation。

**仍需正式化的 Human Gates**：本轮 Stateful Grill 结果都只是 `[HUMAN_SELECTED]` candidate，不覆盖冻结基线。H3（activation producer/consumer）、H9（observation session 是否升为产品模型）、H14（Decision Context/Package/Offer 的产品边界）、H15（Grant→Contract Proposal→immutable View generation）尤其需要 tracer 与上位文档裁决；H2 已于 2026-09-13 经 RFS-001 接受为 CHANGE INPUT（Authority 仍为 NONE；与冻结基线的张力仍须上位裁决）。具体 release 数值、完整资产治理和接口形状不在本轮冻结。

**为什么现在不能冻结具体 Interface**：当前只有若干能力缺口和测试草稿，没有经 tracer 证明的调用者负担、完整失败语义与两个真实 adapter。先写 Interface 会把 Fast/Slow、Simulation 或 UniAgent 的实现假设泄漏到产品契约，并极易制造 pass-through seam 或第二套 authority。
