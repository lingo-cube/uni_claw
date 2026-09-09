# Inter-Component Protocol Baseline（L1–L3）v0.1

> 状态：PROTOCOL grill 定稿（2026-09-09，四轮问答）。
> 证据源：E2B-001 / C2E-002 / OUT-003 / GEV-004 四个 CLOSED vertical
> slice + Target Architecture v0.1（`docs/architecture/product-architecture-baseline-l0-l3.md`）。
> 本文档是 inter-component 协议的 canonical 落点。`docs/architecture/`
> 为正式架构 authority 目录（目标树含 `product-architecture-baseline`、
> `adr/` 的物理迁移，另立 change 处理；本档先驻 `protocols/`）。
> 术语以根 `CONTEXT.md` 为准；本档新增决策见 ADR-0009。

## 0. 定位与读法

- 只收口 L1–L3 组件间协议：不新增业务能力、不设计组件内部实现、不含
  Memory implementation、不做 F2 hardening、不做 legacy cutover。
- **协议对象 ≠ 实现类型**：每条边锁定语义（Producer / Consumer /
  Meaning / Minimal Payload / Validity / Authority / Forbidden Use /
  absence-failure 语义），C# record 仅作 reference realization 标注。
  语义字段类别指 identity / revision / scope / status / correlation /
  refs，不锁字段名与物理布局（baseline §22 精神）。
- 协议 harness / implementation neutral：组件实现可替换，协议语义不得
  漂移（baseline §21 Replaceability Contract 全条适用）。

## 0.1 通则（grill 固化，全图适用）

1. **三条反误锁原则**：不把当前实现顺序当目标协议；不把实现机制当
   语义；不把现有缺口反过来定义成架构规则。
2. **validity ≠ authorization**：对象的有效性与它是否被授权使用是两
   个判定；judgment 拒销不销毁对象，对象存在不构成授权。
3. **semantic kind ≠ 来源差异**：语义本质不同的输入才是不同 kind；
   来源 / 触发时机差异一律入 provenance。
4. **词汇锁法**：锁当前已验证 semantic categories / rejection
   reasons，但不声明 exhaustive closed set。新增**不同跨组件语义**
   要求协议变更；新增实现内策略或更细 reason 不必改协议。
5. **identity 锁法**：协议锁 stable identity / ordering-currentness /
   correlation / idempotency **语义**；id 生成算法（hash、自增、派生
   函数）属 realization / 既有 change decision，不自动成为全局规则。
6. **absence 逐边显式**：每条边自带 absence / unknown / rejected /
   not-applicable 的表达定义；null / default 的含义不得全局统一约定。
   canonical authority 的拒绝可观察、有 reason，但不是所有失败都必须
   生成 record。
7. **payload classification vocabulary**：outcome semantics /
   proof-provenance / explanatory metadata / observability 四类是
   分类词汇，按边适用，**不要求每条边四层齐全**（例：ControlIntent
   无 proof-provenance 层）。
8. **时间**：CaptureTime 是 temporal provenance，**不是 freshness
   权威**；是否可信、是否 fresh 由 admission / freshness policy 判定
   （policy deferred）。系统无 canonical clock（deferred ⑪）。
9. **失效制度**（已验证三制）：派生失效（无 event，消费时对照
   currentness / consumption——freshness 属消费相对 sufficiency
   judgment，不参与派生失效，ADR-0010）；append-only immutable
   （supersede 不就地改写）；latch / terminal（exactly-once、冻结、
   delivery closure）。
10. **Uni Kernel ownership 限定**：Kernel 不拥有任何 canonical domain
    truth（WorldBelief / Run State / Assurance judgment / target
    binding）；允许拥有组合与生命周期协调状态（composition state /
    lifecycle coordination / emission latch）——与 exactly-once
    emission、delivery closure 不冲突（不变量 3 的精确化）。

## 0.2 状态标注

| 标注 | 含义 |
|---|---|
| verified | 已验证切片直接证明的语义 |
| known deviation | 实现与目标协议有偏差（ADR-0009 台账） |
| known gap | 目标协议要求、实现尚未具备 |
| known leak / potential overexposure | 实现传递面大于协议语义需要（不迫改） |
| placeholder | 边界占位，无实现 |

## 1. Protocol Map

```text
UniAgent(L1) ──P1 Contract Proposal──▶ Run Model
Capability Plane（外部观察 / post-action effect flow 观察 acquisition） ──P2 Observation Ingress(kind=Observation)──▶ Evidence Ledger
Effect Boundary ──P2 AttemptReport──▶ Evidence Ledger
Evidence Ledger ──P3 Accepted Evidence Record──▶ World Model（+P12 只读查询──▶ Assurance）
World Model ──P4 Slice──▶ Control Loop
World Model ──P11 Current WorldBelief View──▶ Assurance / Effect Boundary
Run Model ──P5 Run Snapshot──▶ Control Loop
Run Model ──P6 Contract View──▶ Control Loop / Assurance
Run Model ──P7 Obligation View──▶ Assurance
Control Loop ──P8 Control Intent──▶ Assurance / Effect Boundary
Grounding Provider(Capability Plane) ──P9 Candidate Binding──▶ Effect Boundary
Effect Boundary ──P10 Canonical Binding──▶ Assurance          [ADR-0009]
Assurance ──P13 Assurance Judgment──▶ Effect Boundary(Gate)
Effect Boundary ──P14 Dispatch Request──▶ Capability Plane
Capability Plane ──P15 Dispatch Result──▶ Effect Boundary
Assurance ──P16 Outcome Proof──▶ Run Model
Run Model ──P17 Terminal Outcome State──▶ Uni Kernel
Uni Kernel ──P18 Runtime Outcome──▶ UniAgent
UniAgent ──P19 Goal Evaluation──▶ user/session（未来）
Canonical Protocol Publications ──P20 Persist(placeholder)──▶ Memory System
Memory System ──P21 Recall(placeholder)──▶ Authorized Consumer(s)（consumer / persisted set = Deferred ⑩）
Runtime Effect Flow ──P22 Runtime Transition Context──▶ World Model（Container Association prior；ADR-0012 / UWM-009 §12.1）
Effect / Obligation path ──P23 Continuity Demand──▶ World Model（continuity eligibility / adjudication；GroundingView 出面 = P11 族；ADR-0014 / UWM-009 §39）
```

组合缝产物（KernelResult / ActResult / TerminalEvaluation）是 Uni Kernel
编排返回的引用聚合，不是 inter-component 协议对象；Kernel 操作面在目标
态由谁驱动 = deferred ①。

## 2. 逐边协议

### P1 Contract Proposal（UniAgent → Run Model）

- **Producer**：contract author——canonical 唯一合法作者 = UniAgent；
  切片由测试脚本代演（协议锁"谁可以"，不锁谁构造了载体）。
- **Consumer**：Run Model（唯一 admission 面）。
- **Meaning**：作者对一次 Run 的执行要约：objective / scope / effect
  约束 / proof 要求 / 可选 run-level obligations。只是 proposal，不建
  立任何 canonical state。
- **Minimal Payload**：version；objective；scope；effect 约束
  （allowed + forbidden）；proof criteria；可选结构化 obligations。
- **Validity**：proposal 无生命周期；同 version 重复 admit 幂等复用；
  accepted version 不就地改写；不同 version 取代语义 deferred ②。
- **Authority**：Run Model 是 admission authority（作者不是）；接受后
  产物 = Contract View（P6）。
- **Forbidden Use**：被拒不得绕过 admission；不得携带 action-local
  assurance 要求（属 Assurance，不变量 36）；不得内嵌 WorldBelief /
  Run State 引用。
- **Absence/Failure**：非法 / 不完整 → ContractAdmission 拒绝 + 首因
  reason，零 Run State 副作用。
- **Reference Realization**：`ExecutionContract` / `ContractAdmission`。
- **Status**：verified（作者面为 canonical 声明 + 代演注记）。

### P2 Observation Ingress（观察生产者 → Evidence Ledger）

- **Producer**：Capability Plane provider（外部世界观察；post-action
  effect flow 观察的实际 producer）；Effect Boundary（AttemptReport
  导出）。post-action 观察 acquisition 的编排归属（EB / Control Loop /
  Kernel orchestration）= Deferred ①。
- **Consumer**：Evidence Ledger（唯一 admission 面）。
- **Meaning**：`Observation` = 对世界状态的观察声明；`AttemptReport` =
  对一次 dispatch attempt 的投递报告。皆为 proposal，不是 Evidence
  Record。
- **Minimal Payload**：semantic kind（已验证二成员，不声明
  exhaustive）；claim 内容；provenance（producer 身份、CaptureTime、
  scope、lineage）。AttemptReport **必须可关联到具体 dispatch
  attempt**（correlation 语义必锁，形式自由）。自产 post-action
  Observation 的 **origin 语义 = post-action effect flow 的自产观察**
  （区别于外部 provider 声明）是协议不变量（MaterialEffect 判定
  load-bearing）；实际观察 producer 记入 provenance；origin 编码
  （如 `effect.boundary` 命名空间）属 realization。
- **Validity**：无（proposal）；admission 决定。
- **Authority**：Ledger admission authority；kind 表达语义差异，来源 /
  触发差异只入 provenance。
- **Forbidden Use**：producer 不得自造 EvidenceId 或 admission 结论；
  AttemptReport 不得作为 world-state evidence 或 effect proof；
  producer confidence 不得升级为 truth（不变量 11）。
- **Absence/Failure**：provenance 不完整 → AdmissionRecord Rejected +
  首因 reason，零 canonical 副作用、零 belief 变化。
- **Reference Realization**：`ObservationRecord` / `Provenance` /
  `AdmissionRecord` / `EffectBoundary.ExportAttemptEvidence`。
- **Status**：verified（ING-006，2026-09-09：显式 kind + ObservationContext
  落地 `ObservationProposal` / `EvidenceRecord`；kind / context recognized
  fail-closed；relevance kind-aware；`attempt.*` subject 降级为描述性
  provenance）。
- **Terminology note**：与 baseline §11.2 "Observation Record" 的一词
  两义已由 ING-006 rename（`ObservationProposal`）消除——2026-09-09
  模型挑战裁决执行完毕，baseline 不动。

### P3 Accepted Evidence Record（Evidence Ledger → World Model）

- **Producer**：Evidence Ledger（admission 通过时唯一创建；内容寻址
  幂等去重——算法属 realization）。
- **Consumer**：World Model（relevance / reconciliation）；Assurance
  （P12 只读查询）；允许多只读 consumer。
- **Meaning**：不可变 canonical 观察依据。只证明"被 admit 的观察"，
  不证明 truth / belief relevance / proof sufficiency（不变量 10）。
- **Minimal Payload**：EvidenceId；claim；provenance；**kind + origin
  语义随 record 携带**（MaterialEffect 判定输入；当前实现缺 = P2 同源
  known gap）。
- **Validity**：immutable append-only；重放同内容幂等复用；
  supersession / 降权语义 deferred ⑧。
- **Authority**：Ledger owns canonical Evidence semantics。
- **Forbidden Use**：不得被当作 WorldBelief 或 Reality（不变量
  12/13）；plan / expectation 无路径成为 record。
- **Absence/Failure**：无 record = 未被 admit，不存在"未知的 record"。
- **Reference Realization**：`EvidenceRecord` / `EvidenceLedger.Admit`。
- **Status**：verified（ING-006：kind + ObservationContext 随记录携带；
  Evidence identity 覆盖全部 canonical semantic content）。

### P4 Slice（World Model → Control Loop）

- **Producer**：World Model（唯一派生）。
- **Consumer**：Control Loop；允许多只读 consumer。
- **Meaning**：某 WorldBelief revision 的 scoped 只读投影——"该 revision
  下该 scope 的 claims"；不表达 reality，不含 intent。
- **Minimal Payload**：source revision identity；scope；freshness basis
  （维度——World Model 表达的判定输入聚合，ADR-0010）；projection。
- **Validity**：派生失效（无 event）：Slice 自身有效性只判 **revision
  currency**（World Model 侧）；freshness 不参与 Slice 派生 validity，
  属消费侧 Freshness Judgment（Assurance，action 授权时独立执法）。
  双维度各归其 owner，任一不满足都不得产生 action（ADR-0010 精化，
  原"currency 与 freshness 双维度评估"措辞按此理解）。
- **Authority**：World Model owns derivation 与 currency-validity 判定；
  freshness sufficiency judgment 归 Assurance（消费时）。
- **Forbidden Use**：不得回写 WorldBelief；不得缓存为独立 current
  truth；原始局部观察输入不得称 Slice（不变量 20）。
- **Absence/Failure**：无 current revision → 无法派生（fail-closed
  显式拒绝）。
- **Reference Realization**：`Slice` / `WorldModel.DeriveSlice` /
  `IsSliceValid` / `Assurance.FreshnessJudgment`。
- **Status**：verified（FRS-007，2026-09-09：known gap 消除——freshness
  sufficiency 经 Assurance Freshness Judgment 落地（ADR-0010）；Slice
  validity 保持 currency 派生）。
- **UWM-009 澄清（2026-09-09）**：Slice 语义 invariant 冻结为
  revision-bound / scoped / immutable / projection omission ≠ world
  absence；若暴露 spatial value 必须标识 SpatialFrame；Coverage 表示法
  与字段形状 buyer-driven（不冻结 SliceId / Bounds / Transform / HOW
  MUCH / mandatory Uncertainty-Conflict 字段）。载荷扩展随 UWM-009
  实现 change 落地。
- **UWM-009 v0.3 修订（2026-09-08，UIW-002 / ADR-0015）**：Slice =
  Control 的 consumer view；Scope 锚定 **RootContainerIdentity +
  buyer-required InScopeContainerRefs**（可覆盖 Container 子图；多屏 /
  多区域 / 横纵混合兼容；不假设 page/viewport/DOM）。载荷 = container
  facts + occurrence 景观（OccurrenceRef / role / descriptor 摘要 /
  frame-bound spatial）+ ScopedClaim 分区兼容通道；排除全量 LogicalItems /
  demand registry / Maintenance / ReferentBasis / provider node key /
  固定 H·V 类型 / Region identity（UWM-009 §41）。Control 在 continuity
  场景经 LogicalItemRef / DemandHandle 引用，不从 Slice 扫全量 item。

### P5 Run Snapshot（Run Model → Control Loop）

- **Producer**：Run Model。
- **Consumer**：Control Loop。
- **Meaning**：控制决策所需的 run 侧状态视图；不是 Goal Evaluation，
  不是全量 Run State。
- **Minimal Payload**：objective status；run-level obligation
  statuses；progress 推进。**仅锁场景验证所需语义，不因现有对象有
  字段而自动升格。**
- **Validity**：随 typed transition 演进；terminal 后冻结。
- **Authority**：Run Model（canonical recording）。
- **Forbidden Use**：不得含 action-local assurance state（不变量
  36）；Control 不得成为 run progress truth 的平行 owner。
- **Absence/Failure**：无 accepted contract → 无 snapshot（下游
  fail-closed）。
- **Reference Realization**：无——实测 Control 对 run 侧信息零字段
  消费（全部 policy 实现忽略 inputs），`RunState` 已退出
  `SelectIntent` / `ControlInputs` 签名（EXP-008）。
- **Status**：**deferred（no-current-buyer）**——当前无 buyer，
  Run Model → Control 的 runtime data dependency 暂时消失；不造
  零载荷协议或空 DTO（ADR-0011）。未来出现 progress / obligations
  的真实 Control buyer 时恢复本边，并按 consumer view 规则
  （ADR-0011）立 view。（原：verified + known leak——`RunState`
  聚合整传，2026-09-07 由 EXP-008 闭合。）

### P6 Execution Contract View（Run Model → Control Loop / Assurance）

- **Producer**：Run Model（contract 接受时建立；同 version 幂等复用
  同一实例）。
- **Consumer**：Control Loop、Assurance（多只读 consumer）。
- **Meaning**：已接受 contract 的 immutable canonical 约束面。
- **Minimal Payload**：version；objective；scope；effect 约束；proof
  criteria。
- **Validity**：对当前 Run immutable；terminal 后冻结。
- **Authority**：Run Model owns；UniAgent 侧不得持有作为权威的平行
  copy。
- **Forbidden Use**：不得就地改写；不得成为 UniAgent 回写面。
- **Absence/Failure**：未接受 contract → 无 View，下游全 fail-closed。
- **Reference Realization**：`ExecutionContractView`。
- **Status**：verified。

### P7 Obligation View（Run Model → Assurance）

- **Producer**：Run Model（记录面）。
- **Consumer**：Assurance（满足判定输入）。
- **Meaning**：run-level proof obligations 的当前状态；满足判定由
  Assurance 执行、Run Model 只记录——两个产物、两个 owner。
- **Minimal Payload**：obligation identity / kind / subject / required
  value / mandatory；满足状态记录（Assurance 判定产出回填）。
- **Validity**：随 discharge / typed transition 演进。
- **Authority**：Run Model records，Assurance judges。
- **Forbidden Use**：action-local requirements（target freshness /
  one-step precondition / admissibility / grounding validity）永不进入。
- **Absence/Failure**：contract 无结构化 obligations → 占位不可判定
  obligations（显式诚实，不伪装可满足）。
- **Reference Realization**：`ProofObligationState` / `RunObligation` /
  `ObligationStatus`。
- **Status**：verified。

### P8 Control Intent（Control Loop → Assurance / Effect Boundary）

- **Producer**：Control Loop（sole Control Intent Authority）。
- **Consumer**：Assurance（judgment 输入）；Effect Boundary
  （canonicalize 输入）。
- **Meaning**：控制产出——observe / act / recovery 为已验证语义
  categories（不声明 exhaustive；Recovery 未来可能只是产出普通
  intent 的控制模式）。act-intent 携带 target hint 与 basis revision，
  不是 binding、不是 authorization。
- **Minimal Payload**：intent identity；kind；effect class（act）；
  target hint（act）；basis revision anchor。
- **Validity**：一次性控制产出；**issuance 归属校验**（只有 Control
  Loop 签发的 intent 可进 act pipeline）是协议语义，校验机制（引用 /
  ID / 签名 / registry）属 realization；terminal 后签发 fail-closed。
- **Authority**：Control Loop。
- **Forbidden Use**：不得附着 Tactical Hypothesis；不得自判
  admissibility；不得直接成为 command。
- **Absence/Failure**：forged / 未签发 intent → pipeline 拒绝（归属
  校验失败，fail-closed）。
- **Reference Realization**：`ControlIntent` / `ControlLoop.IsIssued`
  （引用同一性 = realization）。
- **Status**：verified（词汇 non-exhaustive 注记）。

### P9 Candidate Binding（Grounding Provider → Effect Boundary）

- **Producer**：Grounding Provider（Capability Plane）——proposal。
- **Consumer**：Effect Boundary（唯一认定路径；**目标协议下不进
  Assurance**，ADR-0009）。
- **Meaning**：候选目标绑定；无任何 dispatch 权威（不变量 24）。
- **Minimal Payload**：target subject / value；source revision
  anchor；ambiguity 声明。
- **Validity**：proposal 无独立生命周期；认定拒绝语义三态
  （stale-revision / ambiguous / unknown-target，不声明 exhaustive）。
- **Authority**：Effect Boundary canonicalizes；provider 无 binding
  authority。
- **Forbidden Use**：不得直接 dispatch；不得作为 authorization 依据。
- **Absence/Failure**：认定拒绝 → BindingDecision + reason；**无
  CanonicalBinding、无 AssuranceJudgment**（短路于 Judge 之前）。
- **Reference Realization**：`CandidateBinding` / `BindingDecision`。
- **Status**：target 锁定；known deviation 已闭合（CBA-005，2026-09-09：
  Act pipeline 已迁移 Bind→Judge(canonical binding)→Gate，candidate 不再
  流入 Assurance）。
- **UWM-009 v0.3 修订（2026-09-08，UIW-002）**：UI target = **已解析引用**
  （`UiTargetReference = OccurrenceRef | LogicalItemRef`）——provider 基于
  GroundingView（P23 出面）解析；descriptor 匹配属 provider 内部过程，
  其输出是 resolved ref，不是描述字符串。

### P10 Canonical Binding（Effect Boundary → Assurance）

- **Producer**：Effect Boundary（sole Canonical Binding Authority）。
- **Consumer**：Assurance（**judgment 的授权对象**，ADR-0009）；Gate /
  Dispatch 为 EB 内部消费，不跨组件。
- **Meaning**：唯一有效 bounded target binding，绑定 specific
  revision；授权必须针对最终执行的 target 本身。
- **Minimal Payload**：binding identity；intent correlation；effect
  semantics；bounded target；revision anchor；freshness（维度）。
- **Validity**：**validity ≠ authorization**——validity 由 revision
  currency / consumption 派生判定（无 event）；freshness 不参与派生
  validity，经消费侧 Freshness Judgment 执法（freshness 拒绝 =
  authorization denied ≠ binding invalidation，ADR-0010——产品基线
  §20.4「freshness loss 后失效」按该 supersede 理解）；judgment
  拒销不使 binding 失效；被拒 binding 是否允许 re-judgment =
  deferred ③。
- **Authority**：Effect Boundary；binding 存在不构成 authorization。
- **Forbidden Use**：不得外溢为 authorization；不得跨 revision 复用；
  同 binding 消费（dispatch）后不得二次 dispatch。
- **Absence/Failure**：认定拒绝见 P9；dispatch 拒绝见 P13 消费面。
- **Reference Realization**：`CanonicalBinding` / `IsBindingValid`。
- **Status**：target 锁定（ADR-0009）；known deviation 已闭合（CBA-005：
  CanonicalBinding 已作为 Judge 授权对象跨入 Assurance，携带三元组）；
  freshness validity 措辞由 FRS-007 / ADR-0010 精化（2026-09-09）。
- **UWM-009 v0.3 修订（2026-09-08，UIW-002）**：CanonicalBinding 保持
  唯一 canonical binding noun；其 **UI target shape** = Target 恒绑
  `CurrentOccurrenceRef`（+ OwningContainerIdentity / SourceRevisionId /
  IntentCorrelation / EffectSemantics / optional LogicalItemRef /
  optional ContinuityAdjudicationRef——UWM-009 §41）。LogicalItem 永不
  直接承载 effect（P-UW-33）；UI 字符串寻址退役，不得作为 typed
  resolution 失败后的 fallback；非 UI 字符串通道暂留（去留 = deferred ⑮）。

### P11 Current WorldBelief View（World Model → Assurance / Effect Boundary）

- **Producer**：World Model（sole Belief / Reconciliation Authority）。
- **Consumer**：Assurance、Effect Boundary（多只读）。
- **Meaning**：当前世界判断的 canonical 视图。Assurance 真实语义需要
  = applicable revision identity / currentness + 与 intent / binding
  相关的 scoped claims + freshness + uncertainty / conflict。
- **Minimal Payload**：revision identity / currentness；world-state
  claims；evidence basis refs；conflicts；uncertainty；freshness。
  WorldGraph schema 明确排除（本会话排除项）。
- **Validity**：revision immutable；currentness 单调演进；历史 revision
  只读。
- **Authority**：World Model。
- **Forbidden Use**：消费方不得缓存为平行 current truth；
  plan / expectation 无写入路径（不变量 16）；Memory recall 的进入
  语义见 P21。
- **Absence/Failure**：无 revision → 下游 fail-closed。
- **Reference Realization**：三个 consumer-specific immutable view
  （Owner 即时派生，ADR-0011 / EXP-008）：`BindingView`（EB：revision
  锚 + HasTargetSubjectClaim fact）、`ActionAssuranceView`（Judge：
  revision 锚 + FreshnessBasis + HasConflictOnTarget fact）、
  `OutcomeAssuranceView`（obligation 路径：revision 锚 + scoped
  claims（`ScopedClaim`）/ scoped conflicts + 全量 basis refs +
  总冲突计数）。`Conflict` 类型随本边显式升格 protocol vocabulary；
  `WorldClaim` 保持 owner-internal。view 是 ephemeral projection：
  消费点派生、不缓存、不跨 operation 重放，一致性由 consumer 以
  revision correlation anchor 校验（无 view validity API）。
- **Status**：verified（potential overexposure 已闭合——EXP-008，
  2026-09-07：`WorldBeliefRevision` 聚合不再进入 Assurance / Effect
  Boundary 签名，各 consumer 只收履职所需 view；WorldGraph /
  ParentRevisionId 零外部暴露）。
- **UWM-009 v0.3 修订（2026-09-08，UIW-002）**：本边新增第四个
  consumer-specific view **`GroundingView`**（P23 的出面形态；ADR-0011
  四原则全条适用）：ResolveCurrent 出面 = revision 锚 + OwningContainer +
  CurrentCandidateSetResult + 候选 occurrence facts（descriptor +
  frame-bound spatial）；ResolveContinuity 出面 = revision 锚 +
  ContinuityResolutionOutcome + DemandHandle。不得携带 CanonicalBinding /
  BindingAllowed / ActionAdmissible / ShouldRetry / effect authorization。
  **Grounding Provider（Capability Plane）成为本边合法只读 consumer**
  （基线 §8"接收有界输入"；provider 无 binding authority 不变）。

### P12 Canonical Records Lookup（Evidence Ledger → Assurance）

- **Producer**：Evidence Ledger（只读视图）。
- **Consumer**：Assurance（origin / producer 核验：MaterialEffect 自产
  origin 判定）。
- **Meaning**：canonical EvidenceRecord 的只读检索面，不是第二存储。
- **Minimal Payload**：按 identity 检索 record（含 kind / origin 语义）。
- **Validity**：随 admission append-only 增长。
- **Authority**：Ledger。
- **Forbidden Use**：不得经此面写入；不得成为其他组件的 evidence
  解释面。
- **Absence/Failure**：查无 record → 按"无 backing"处理（判定不满足，
  不猜测）。
- **Reference Realization**：`EvidenceLedger.CanonicalRecords` 只读
  字典。
- **Status**：verified。

### P13 Assurance Judgment（Assurance → Effect Boundary）

- **Producer**：Assurance（sole Runtime Assurance Judgment Authority）。
- **Consumer**：Effect Boundary Gate（唯一授权输入；Gate 只执法不
  重判，不变量 26）。
- **Meaning**：针对 **(exact Intent + exact CanonicalBinding +
  applicable revision)** 三元组的不可变 action-local 判定
  （admissibility / freshness / safety guard）。
- **Minimal Payload**：correlation 三元组（IntentId + BindingId +
  RevisionId——correlation key，**非 canonical identity**，⑥）；
  admissibility；rejection reason。checks 明细清单 = observability
  （realization），不进最小载荷。
- **Validity**：immutable；三元组任一成员或 freshness 改变必须重新
  判断；单 act 作用域，不得跨 revision / 跨 act 重用。
- **Authority**：Assurance。
- **Forbidden Use**：Gate 不得据此外推到其他 binding / intent；
  judgment ≠ command；Intent → Authorization 禁止。
- **Absence/Failure**：拒绝 = 显式 reason；Bind 拒绝短路时无 judgment
  （不伪造）；三元组不匹配（同 IntentId 异 BindingId / RevisionId）→
  Gate 必须拒绝。
- **Reference Realization**：`AssuranceJudgment`（三元组已落地，CBA-005）。
- **Status**：target 锁定；known deviation 已闭合（CBA-005：载荷三元组
  IntentId + BindingId + RevisionId 已落地，correlation key 非 identity）；
  freshness 维度已落地（FRS-007，2026-09-09：`freshness-sufficiency`
  检查 + FreshnessJudgment 载体，ADR-0010 消费相对语义）。

### P14 Dispatch Request（Effect Boundary → Capability Plane）

- **Producer**：Effect Boundary（Effect Delivery Authority 的执法
  出口；terminal 后 delivery closed，全部请求 fail-closed）。
- **Consumer**：Capability Plane（dispatch adapter / provider；driver
  是实现角色）。
- **Meaning**：bounded command——只表达"做什么"，不携带任何
  judgment / authorization 语义。
- **Minimal Payload**：bounded target；effect semantics / parameters；
  revision anchor。
- **Validity**：单次 delivery 作用域；capability 不得自行 retry /
  replan（不变量 27）。
- **Authority**：Effect Boundary；capability 对授权态零感知、无
  authorization authority。
- **Forbidden Use**：不得含 admissibility / 授权状态；capability 不得
  因感知授权与否改变行为。
- **Absence/Failure**：delivery closed（terminal）→ 拒绝（reason:
  delivery-closed）。
- **Reference Realization**：`IEffectDriver.Deliver(DispatchRequest)`（DSE-001
  收窄 + DSE-002 正式化，2026-09-09）。**EB lowering seam**：Runtime-authorized
  target → driver-executable `DeliveryTarget`（OccurrenceReference 仅溯源
  **永不参与执行**——invariant：Reference identity ≠ executable locator；
  Spatial = SpatialLocator 归一化 bounds + frame，v0.1 无 NativeLocator——
  无数据链不建）。三规则：A. spatial 无 frame = 无效载荷（构造执法，
  P-UW-16）；B. 至少一个 **driver-supported** locator（supported 集由
  driver 判定）；C. locator 只能来自当前授权 Binding 的 lowering——
  **driver 不得语义重定位 / fallback 猜测**（ID 失效 ≠ 改用坐标；正确
  后继 = DeliveryFailed/Unknown → 上游 re-observe → re-ground → 新
  binding → 新 DispatchRequest）。首个产品 driver = AdbEffectDriver
  （dry-run）。五层名词：TargetDescriptor → ObservationOccurrence →
  CanonicalBinding → DeliveryTarget → DispatchRequest。
- **Status**：verified（DSE-001 载荷收窄 + DSE-002 DeliveryTarget 落地）。

### P15 Dispatch Result（Capability Plane → Effect Boundary）

- **Producer**：Capability Plane（dispatch adapter）。
- **Consumer**：Effect Boundary（**唯一转换点**：Result → canonical
  EffectReceipt）。
- **Meaning**：对本次 delivery attempt 的显式结果 + provider report /
  reference。**≠ Effect，≠ Verified Effect**（不变量 33/34）。
- **Minimal Payload**：显式 attempt 结果——**三态 outcome（对世界效果的
  认知状态）：DeliveryCompleted / DeliveryFailed / UnknownOutcome**（DSE-001
  裁决，2026-09-09：两态压扁 timeout/unavailable/indeterminate，⑤ 闭合）；
  **reason / diagnostic**（成因分类：timeout-killed / device-offline /
  result-uninterpretable / cancelled-by-caller / …——string，不声明
  exhaustive，不进共享协议词汇；provider diagnostic 保留）。provider
  report / reference。不要求 wall-clock timestamp（无 canonical
  clock，⑪）。
- **Validity**：单次 attempt 产物；由 EB 转换为 receipt。
- **Authority**：EB owns receipt canonicalization；provider 无。
- **Forbidden Use**：不得自行 retry / recover / replan / 改变策略；
  不得自证 Effect。**UnknownOutcome → re-observe → NEVER blind
  redispatch**（DSE-001 invariant；Cancelled 是 reason 非第四种 outcome——
  cancel request ≠ effect did not occur）。
- **Absence/Failure**：结果缺失 / 不可解读 → **UnknownOutcome + reason
  （result-uninterpretable），fail-closed 不猜测成功**（⑬ 闭合：DSE-001
  起可表达）。
- **Reference Realization**：`DispatchResult`（三态 + Reason + CompletedAt =
  realization，不锁）。
- **Status**：verified（DSE-001 三态落地；⑤⑬ 闭合）。

### P16 Outcome Proof（Assurance → Run Model）

- **Producer**：Assurance（sole Outcome Proof Authority，不变量
  37）。
- **Consumer**：Run Model（terminal transition 的记录输入）。
- **Meaning**：run-level obligations 是否有足够 accepted Evidence 支持
  具体 terminal claim 的终局判断；四分类词汇（Completion / Failure /
  SafeStop / Escalation）为已验证语义，enum 名不锁。
- **Minimal Payload**：proof identity；classification；obligation
  statuses；basis / effect / situation evidence refs；unresolved
  uncertainty；reason（explanatory）。分类优先级规则 = Assurance
  policy，不进协议。
- **Validity**：immutable；写入 Outcome State 后冻结引用。
- **Authority**：Assurance judges；Run Model 只记录（不重判）。
- **Forbidden Use**：不得由 receipt / attempt / provider claim 直接
  构成；Kernel / Run Model 不得重判。
- **Absence/Failure**：**证据不足 = 无 proof 对象**（显式 absence，
  保持 non-terminal；「不足 / 未知」不是分类成员，不得伪装成功或
  失败）。
- **Reference Realization**：`OutcomeProof` / `JudgeOutcome` 返回
  null = 本边 absence 定义。
- **Status**：verified。

### P17 Terminal Outcome State（Run Model → Uni Kernel）

- **Producer**：Run Model（sole recording；exact-prior / single-winner
  typed transition，一个 Run 至多一次，terminal 后不可恢复 active）。
- **Consumer**：Uni Kernel（emission 投影源）。
- **Meaning**：terminal 结论的 canonical 记录快照——只记录，不重判。
- **Minimal Payload**：proof ref；classification；obligation
  statuses；evidence refs（basis / effect / situation）；unresolved
  uncertainty；reason。
- **Validity**：terminal 冻结；late evidence / receipt / provider
  claim 不得改写、不得恢复 Run、不得二次 emission。
- **Authority**：Run Model。
- **Forbidden Use**：Effect / Receipt 不得直接写 terminal state；Kernel
  不得重判完成。
- **Absence/Failure**：竞争失败 / 已 terminal → OutcomeTransition
  拒绝 + reason。
- **Reference Realization**：`OutcomeState` / `OutcomeTransition` /
  `TransitionToTerminal`。
- **Status**：verified。

### P18 Runtime Outcome（Uni Kernel → UniAgent）

- **Producer**：Uni Kernel（唯一 emission boundary；exactly once）。
- **Consumer**：UniAgent（GEV-004 已验证）；未来 Memory / 审计（只读）。
- **Meaning**：terminal 结论 envelope——只从 Terminal Outcome State
  投影；Kernel 不重判 completion、不解析 Evidence、不改
  classification。
- **Minimal Payload**（三层）：**outcome semantics**（RunId /
  classification / obligation results / unresolved uncertainty）；
  **proof-provenance**（OutcomeProofId / basis refs / effect &
  situation refs——表达"基于什么被证明"，**非日志**）；
  **explanatory metadata**（reason）。
- **Validity**：immutable exactly once；emission 完成后 Effect
  Boundary 关闭 external effect delivery（不变量 42）。
- **Authority**：Kernel emission；不改任何 judgment。
- **Forbidden Use**：消费者不得回写任何 Runtime 事实（不变量 41）；
  不得作为 Goal Evaluation 镜像（classification ≠ satisfaction）；
  不得二次 emission。
- **Absence/Failure**：无 terminal → 无 envelope（fail-closed，无
  中途评价）。
- **Reference Realization**：`RuntimeOutcome` / `TerminalEvaluation`
  （RunId = first-accepted Contract View 的确定性内容派生——RUN-001；
  哈希拼法 = realization）。
- **Status**：verified。

### P19 Goal Evaluation（UniAgent → user/session，未来）

- **Producer**：UniAgent（sole Goal Evaluation Authority，不变量
  40）。
- **Consumer**：user / session（未来）；Memory 语料（未来）。
- **Meaning**：监督评价记录（satisfaction × disposition 正交两维 +
  逐 criterion 结果 + rationale）；不回写任何 Runtime 事实。
- **Minimal Payload**：evaluation identity（确定性派生——算法属
  realization）；goal / run correlation；satisfaction；disposition；
  criterion results；rationale。
- **Validity**：immutable、幂等（同输入同 id 同记录）。
- **Authority**：UniAgent。
- **Forbidden Use**：不得回写 Runtime Outcome / Run State /
  WorldBelief / Outcome Proof；不得 dereference raw runtime 形成
  第二套 judgment；无 classification→satisfaction 硬编码映射。
- **Absence/Failure**：无 envelope → fail-closed 不评价；判据不可
  验证 → Undetermined（≠ Unsatisfied）。
- **Reference Realization**：`GoalEvaluation` / `UniAgent.Evaluate`。
- **Status**：verified（consumer 面为未来占位）。

### P20 Memory Persist（placeholder，→ Memory System）

- **语义**：Memory 可持久化对 protocol objects 的只读观察 / 引用；
  持久化不改变任何 canonical ownership。
- **Forbidden Use**：持久化副本不得成为任何 canonical authority 输入
  的权威来源；Memory 不得成为 Evidence admission / WorldBelief / Run
  State / Control Intent / Assurance / binding / delivery 的平行
  owner。
- **Deferred**：may-persist 对象清单与载荷形状（⑩，Memory buyer）。
- **Status**：placeholder。

### P21 Memory Recall（placeholder，Memory System → Agent / Control / World Model）

- **语义**：recall 产出 = prior / context / hypothesis / historical
  reference，可进入授权 consumer（候选含 Agent / Control / World
  Model——baseline §9；consumer / payload 集合 = Deferred ⑩）；必须
  保持 **non-evidentiary**——不得直接建立、刷新或证明 current-world
  claim；Current WorldBelief 的事实更新仍必须以 accepted current
  Evidence 为依据（baseline §9 / 不变量 7 / §21.3）。
- **Forbidden Use**：recall ≠ current truth；historical EvidenceRef 不
  自动复活为 current evidence；Memory 不得经 recall 取得任何
  current-state authority。
- **Status**：placeholder（不阻塞未来 probabilistic reconciliation——
  锁的是 non-evidentiary 约束，不是"禁止进入"）。

### P22 Runtime Transition Context（Runtime Effect Flow → World Model）

- **Producer**：runtime effect flow——actual attempt/effect 事实的 owner
  侧（Effect Boundary dispatch/receipt 事实；具体导出缝 = realization）。
- **Consumer**：World Model（Container Association 的 candidate prior
  输入）。
- **Meaning**：一次 actual runtime attempt/effect 已发生的 non-evidentiary
  上下文，仅作为 association prior/ranking 输入。不是 EvidenceRecord、
  不是 World claim、不是 ContainerIdentity truth（ADR-0012 /
  UWM-009 §12.1）。
- **Minimal Payload**：transition kind（实际发生的 runtime 语义）；与
  具体 dispatch attempt 的 correlation；epistemic strength 标注
  （attempt 腿 / effect-flow 腿）。
- **Forbidden Payload**：ControlIntent / 期望的 transition / expected
  next page/container / desired outcome / Control plan / Tactical
  Hypothesis。
- **Validity**：单次 association 消费作用域，ephemeral；无独立生命周期，
  不 append、不缓存。
- **Authority**：World Model 消费为 prior；Evidence admission 面不受影响
  （P22 不是 admission 路径）；canonical belief mutation 仍只由 accepted
  world evidence 触发（不变量 15 的 UIWorld 侧精化）。
- **Forbidden Use**：不得单独 establish Matched/New；不得 create/replace
  ContainerIdentity；不得 mutate canonical WorldState；不得回写任何
  canonical state；不得被当作 evidence 使用。
- **Absence/Failure**：无 P22 输入 → association 按 evidence-only 进行
  （合法完整路径，不是降级）。
- **Buyer**：UWM-009 S2 Scroll Continuity（真实 scenario buyer）。
- **Status**：producer 导出缝已落地（PER-003，ExportTransitionContext +
  ActResult.Transition）；consumer 侧自 UIW-001。

### P23 Continuity Request Seam（Effect / Obligation path ↔ World Model）

- **Producer**：Effect / Obligation 消费路径——`EffectTargetCommitment`
  （EB bind / re-bind 路径）与 `EntityScopedObligation`（**语义保留，
  物理入口 deferred ⑭**）。Control / traversal 仅引用既有 LogicalItem，
  无 mint 权；UniAgent 不直连。**ContinuityDemandSourceKind 合法性由调用
  端口 + 运行时 authority 验证，不信载荷自述**（P8 forged-intent 同族）。
- **Consumer**：World Model（continuity eligibility / adjudication 唯一
  owner；GroundingView 唯一派生）。
- **Meaning**：单缝双模，副作用契约显式分离（UWM-009 §39 / ADR-0014）：
  `ResolveCurrent` = 只读 current-revision resolution（不铸 LogicalItem、
  不登记 demand）；`ResolveContinuity` = 声明 same-referent continuity
  demand（登记 / 延续 eligibility）+ 基于 accepted evidence 的 continuity
  adjudication。demand 是 **non-evidentiary** 输入（ADR-0012 族）。
  禁止实现成含义模糊的 `resolve(..., track=true/false)`。
- **Minimal Payload**：mode（ResolveCurrent / ResolveContinuity）；
  TargetDescriptor 或 occurrence-ref / LogicalItemRef；demand idempotency
  identity；source kind（运行时验证）；revoke。出面（GroundingView，P11
  族）：ResolveCurrent → SourceRevisionId / OwningContainerIdentity /
  **CurrentCandidateSetResult**（UniqueCandidate | NoCandidate |
  MultipleCandidates | ScopeProjectionUnavailable）+ 候选 occurrence facts；
  ResolveContinuity → SourceRevisionId / **ContinuityResolutionOutcome**
  （ReferenceEstablished | ContinuityAdjudicationOutcome(SameReferent /
  Ambiguous / Insufficient / Contradicted) | NoCurrentCandidate）+
  DemandHandle。判别结果必须带类型限定（与 Container Association 两套
  词汇，UWM-009 §40）。
- **Validity**：demand eligibility = owner-internal 非 revision 化状态；
  demand 状态变化不产生 WorldBelief revision（若激活 reconciliation 且
  belief 变化，由该 reconciliation commit 产生）；**timing——源 occurrence
  仍属 current revision 时登记，过期 fail-closed；descriptor-scoped 回退
  = 新 demand，不继承旧 basis**；DemandHandle = 不透明 correlation token；
  GroundingView ephemeral（消费点派生、不缓存，ADR-0011）。
- **Authority**：World Model（eligibility / adjudication / GroundingView
  derivation）；EB 保留 Canonical Binding Authority；Grounding Provider 无
  authority（基于 GroundingView 形成 CandidateBinding）。
- **Forbidden Use**：demand ≠ evidence；不建立 / 终止 identity；不强制
  Matched/New；不 mutate claims；ResolveCurrent 不铸 item 不登记 demand；
  GroundingView 不携带 BindingAllowed / ActionAdmissible / ShouldRetry /
  authorization；LogicalItem 不得直接 dispatch（P-UW-33）；Contradicted
  只证伪候选，不终止 LogicalItem。
- **Absence/Failure**：源 occurrence 过期 → 拒绝 occurrence-anchored 登记
  （fail-closed）；ScopeProjectionUnavailable → fail-closed，可进入
  observation refresh；NoCandidate（投影相对）≠ KnownAbsent ≠ Ended；
  NoCandidate（coverage 路径）与 Insufficient（判别证据不足）不得合并。
- **Buyer**：EffectTargetCommitment 中显式 cross-revision same-referent
  requirement（主）；entity-scoped verification（次）。普通 revision
  advance 后的 fresh re-ground 不是本边 continuity 模式的 buyer
  （P-UW-27 默认路径原则）。
- **Status**：target 锁定（ADR-0014 / UIW-002，2026-09-08）；无当前
  实现——实现随 UIW-002 vertical slices 另立 change。producer ② 入口已落地
  （ESO-001：accepted contract 的 entity-scoped obligation → standing demand；
  fulfillment 判定 = ESO-002，blocked by CDS-001 state 载荷）。
  fulfillment 判定已落地（ESO-002：owner-derived tri-state fact →
  JudgeOutcome；deferred ⑭ 全闭）。

## 3. Deferred Protocol Questions

| # | 问题 | 不现在锁的原因 / buyer |
|---|---|---|
| ① | Kernel 操作面（Process / SelectIntent / Act / EvaluateTerminal）在目标态由谁驱动 | 未验证（UniAgent 直驱 vs Kernel 自驱） |
| ② | Contract version 取代语义 | baseline §17 允许显式新 version 取代；切片只验证拒绝，multi-run buyer |
| ③ | 被拒 binding 是否允许 re-judgment | 无 buyer；validity≠authorization 已锁，重判许可 deferred |
| ④ | freshness policy（计算方式 / 阈值 / 来源 owner） | 无已验证 policy；避免提前设计 Contract 字段 |
| ⑤ | DispatchResult / EffectReceipt outcome 词汇表 | 两态会压扁 timeout / unavailable / indeterminate；词汇不提前锁。**（已裁决闭合：DSE-001，2026-09-09——三态 outcome × reason 两层；reason 不锁词汇表，见 P15。）** |
| ⑥ | Judgment canonical identity | 无独立引用 / audit buyer；三元组即 correlation key |
| ⑦ | Producer identity / origin 声明的真实性核验（anti-spoofing） | 当前 realization 无核验手段（场景 8 暴露） |
| ⑧ | Evidence supersession / 降权语义 | CONTEXT 已提及、无实现无 buyer |
| ⑨ | Goal ↔ Run ↔ Session correlation | GEV-004 slice assumption 延续；Session buyer |
| ⑩ | Memory may-persist 清单与 recall 载荷形状 | Memory buyer |
| ⑪ | canonical clock / 时间权威是否存在及归属 | 系统现无 canonical clock；CaptureTime 仅 provenance |
| ⑫ | Primary Goal → Execution Contract 的 derivation / authoring / revision semantics | Contract authoring buyer 未进入；防止把"UniAgent 是作者"误读为"authoring 已设计" |
| ⑬ | Dispatch Result「结果缺失 / 不可解读」（P15 absence 语义）在当前实现不可表达（IEffectDriver 必返回非 null），无 fail-closed 路径 | 模型挑战 C3-3 发现；随 P15 outcome 词汇解锁一并裁决。**（已闭合：DSE-001，2026-09-09——UnknownOutcome + reason(result-uninterpretable) 可表达且 fail-closed。）** |
| ⑭ | EntityScopedObligation → P23 物理入口 | 语义已锁（ADR-0014）；无真实 entity-scoped contract 场景前不建边（P5/ObservationNeed 先例：无 buyer 不造空协议） |
| ⑮ | 非 UI 字符串 target 通道的长期去留 | UIW-002 裁决暂留兼容；去留由独立 buyer audit 决定（UWM-009 §33 deferred 23） |

## 4. Scenario Pressure-Test List

| # | 场景 | 期望协议行为 |
|---|---|---|
| 1 | 双 accepted 冲突观察 | 显式 conflict revision，双方 evidence 保留，不静默覆盖 |
| 2 | stale slice | currentness 判定拒绝（Bind stale-revision / Judge intent-basis-currentness），无有效 dispatch（FRS-007 词汇精化：原"freshness 判定"系 currentness 误称） |
| 3 | ambiguous candidate | Bind 拒绝（ambiguous），无 CanonicalBinding、无 AssuranceJudgment（新序短路语义） |
| 4 | judgment 拒绝 | binding 仍 valid、Gate 拒绝 dispatch、revision 前进后 binding 派生失效（validity ≠ authorization 正反两面） |
| 5 | 同 binding 二次 dispatch | consumption 失效，Gate 拒绝 |
| 6 | receipt 成功但无 post-action Observation | MaterialEffect 不满足、无 completion proof（Attempt ≠ Effect） |
| 7 | AttemptReport 冒充 Observation kind | 结构上可 admitted（provenance 完整），MaterialEffect 判定按 kind + origin 拒绝 |
| 8 | origin 冒名（外部 producer 伪装自产 origin 编码） | 当前 realization 无核验手段——暴露 ⑦ 缺口，显式记录不假装覆盖 |
| 9 | terminal 后 late receipt / late evidence | 可 admitted（AttemptReport / 历史 evidence），不改 terminal、不恢复 Run、无二次 emission |
| 10 | 证据不足 | 无 proof、保持 non-terminal，不伪装分类 |
| 11 | envelope 重复 Evaluate / 第二消费者 | 幂等同 id 同记录，不回写 |
| 12 | replaceability（替换 World Model / Assurance / Provider 实现） | 全部协议语义含 forbidden use 不变 |
| 13 | judgment 与 binding 不匹配（同 IntentId、异 BindingId / RevisionId） | Gate 必须拒绝——证明 ADR-0009 锁的是 Intent + Binding + Revision 三元组，不是只认 Intent |
| 14 | revision 仍 current 但 freshness 不满足 | Assurance 拒绝授权（`freshness-sufficiency` fail-closed），binding 派生 validity 不因此消失——证明 revision currency ≠ freshness（FRS-007 已锚定，ADR-0010） |

## 5. Recommended First Protocol Implementation/Change

**Canonical-Binding Assurance Cutover**（ADR-0009 迁移）：

- 目标：把 Act pipeline 从当前 realization
  `Judge(intent, candidate) → Bind → Gate` 迁移到
  `Bind → Judge(canonical binding) → Gate`；AssuranceJudgment 增加
  IntentId + BindingId + RevisionId 三元组 correlation。
- 保持 C2E 已锁的上层 invariants（四类产出留痕与次序不可合并、
  Candidate ≠ Canonical、Gate 只执法、Attempt ≠ Effect、E2B 权威零
  穿透、recovery 无 blind retry）；**允许旧测试因 reference
  realization 改变而迁移 / 替换**——保护的是架构事实与验收语义，
  不是旧测试本身。
- 不在本协议会话内执行；按 UniFlow 全流程另立 change。
  **（已完成：CBA-005，2026-09-09，56/56 GREEN——ADR-0009 known
  deviation 闭合，见 `changes/CBA-005/state.md`。）**

后续第二梯队（各自独立、可拆可缓，不承诺排序）：ingress 显式 kind
字段 + origin 语义落地（P2/P3 known gap）、receipt / DispatchResult
词汇解锁对齐（P15）、freshness 双维度评估义务落地（P4 known gap）
**（已完成：FRS-007，2026-09-09——ADR-0010 消费相对语义精化 + Scenario
14 锚定，见 `changes/FRS-007/state.md`）**、
Run Snapshot / WorldBelief View 曝射面收缩（P5 known leak / P11
potential overexposure）
**（已完成：EXP-008，2026-09-07——ADR-0011 consumer view 四原则 +
P5 deferred（no-current-buyer）+ P11 三 view 落地，见
`changes/EXP-008/state.md`。）**。

## 6. Gate Recommendation

- **PROTOCOL 收口：READY（本会话 CLOSED-ready）**。frontier 已清空：
  全部跨组件语义分歧经四轮问答裁决，难逆转决策已落 ADR-0009，术语已
  同步 CONTEXT.md，known deviation / gap / leak / overexposure 台账
  完整（见各边 Status）。
- **不进入实现**：本会话产物 = 本文档 + ADR-0009 + CONTEXT.md 词条；
  首个实现变更 = Canonical-Binding Assurance Cutover，另立 change 走
  UniFlow（UNDERSTAND→…→CLOSED）。
- **重开条件**：Deferred ①–⑫ 任一 buyer 出现；或任一 Status 非
  verified 标注的迁移完成时同步更新本档。
