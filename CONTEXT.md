# UniClaw

The repo for UniClaw development: the UniFlow development harness (workflow,
skills, delegation contracts) and the GREENFIELD product line — the Uni
Kernel of the Target Architecture v0.1 — living together, separated by
structure (harness mechanism layers vs `src/`/`tests/` product code), not by
branch. This glossary defines both: how development work is described and
controlled, and the product's canonical Evidence/Belief domain language.

## Language

### Workflow control

**UniFlow**: The single development workflow control plane — owns WHEN and
WHAT NEXT across a task's lifecycle.
_Avoid_: pipeline, process framework, orchestration skill

**Semantic Gate**: One of four readiness checkpoints (Explore Resolution,
Execution Readiness, Verification, Completion) a task must pass on
evidence; a semantic contract, not a form.
_Avoid_: phase gate, checklist, ceremony

**Pre-UniFlow Explore**: The stage that makes the problem clear — feature
grilling or bug diagnosis — before controlled execution starts.
_Avoid_: research phase, discovery sprint

**Outcome token**: The normative state label emitted by a gate or rule:
`EXPLORE_RESOLVED`, `STAY_IN_EXPLORE`, `STAY_IN_DIAGNOSIS`,
`EXECUTION_READY`, `VERIFIED`, `VERIFICATION_FAILED`, `COMPLETE`.
_Avoid_: ad-hoc status strings

**Human Gate**: The escalation point reserved for genuinely human
decisions — unresolved product meaning, major architecture choice,
authority conflict, irreversible action.
_Avoid_: approval step, sign-off

### Roles and execution

**Leader**: The context that owns the workflow — evaluates gates, decides
routing, holds the Plan, and declares completion.
_Avoid_: orchestrator, main agent

**SubAgent**: A fresh, disposable execution context that completes one
delegated WorkItem and returns result plus evidence.
_Avoid_: worker, thread, minion

**Direct**: Execution by the Leader inside its own current context; no
WorkItem is created.
_Avoid_: inline mode

**Delegate**: Routing a bounded, self-contained objective to a SubAgent
through a transient WorkItem.
_Avoid_: fan-out, dispatch-and-forget

**WorkItem**: A transient Leader→SubAgent delegation contract compiled
from the current Plan; never an issue tracker, backlog, or persistent
store.
_Avoid_: ticket, task, issue, spec

**Plan**: The Leader's current execution intent, persisted only when a
context boundary requires later resumption.
_Avoid_: spec, proposal, design doc

### Evidence and provenance

**Evidence**: Artifacts that prove completion — test output, review
conclusions, reproduction records. The only basis for VERIFIED and
COMPLETE; self-reports never qualify.（Harness 义；产品域的观察依据见
**Evidence Record**。）
_Avoid_: logs, claims, evidence record

**Evidence packet**: The structured return of the UniClaw debug extension —
evidence summary, failure class, First Divergence Point, owner, root-cause
support, remaining uncertainty, escalation.
_Avoid_: bug report, findings

**Change State**: The durable WHAT/WHY/ACCEPTANCE record of one change
(`changes/`), persisted at variable depth — never a plan, ticket, or ADR.
_Avoid_: ticket, spec, work item

**Smart Zone**: The early, sharp part of a session (~150K tokens, working
value). Past it, work splits into persisted change state plus fresh
subagents instead of stretching the session.
_Avoid_: long context, infinite chat

**NO_REAL_BUYER**: The marker recorded when a required element has no
genuine real-world vehicle, so it is waived honestly rather than fabricated.
_Avoid_: skip, N/A

**Owner / Authority**: The project-declared responsibility for a state or
decision. UniFlow identifies and validates it; it never invents or
overrides it.
_Avoid_: controller

**Vendored skill**: A third-party skill kept verbatim from its upstream
repository, updated only through the installer.
_Avoid_: fork, copy

**Skill provenance**: A skill's origin record — `UPSTREAM_MATT`,
`UPSTREAM_HUMANLAYER`, `LOCAL_UNIFLOW`, `LOCAL_UNICLAW` — carried by
skills-lock.json (upstream) or SKILL.md frontmatter (local).
_Avoid_: attribution file

## Product Domain — Uni Kernel（Evidence & Belief · Control & Effect）

Target Architecture v0.1 的产品域语言（已由 E2B-001 / C2E-002 在
`src/UniClaw.Kernel` 落地的部分）。注意与本仓 Harness 层的
**Evidence**（完成证明工件）区分：产品域的观察依据一律称
**Evidence Record**。组件名（Evidence Ledger / World Model / Run Model /
Control Loop / Assurance / Effect Boundary / Capability Plane / Memory
System）以 baseline §8-§10 为准，不另立词条。

**Uni Kernel**: 六个 L2 的 aggregate execution boundary 与唯一 Runtime
Outcome emission 点；不拥有任何 canonical domain truth（WorldBelief /
Run State / Assurance judgment / target binding），允许拥有组合与生命
周期协调状态（composition / lifecycle coordination / emission latch）。
_Avoid_: god context、兜底 owner、canonical state owner

**Domain Runtime（领域运行时）**: 某一个领域「自主动手 + 逐步验证」的
运行时机制集合：admission → belief → intent → grounding → assurance →
gate → dispatch → serial verification → terminal proof。Uni Kernel 是
**UI 领域的** Domain Runtime（ADR-0026），不是跨领域层；跨领域层是
Core 记录（已双域验证）。不需要运行时机制的领域 realization 直连
Core、不经 Kernel（CORE-016 先例）；第二个需要运行时机制的非 UI
领域出现时，与 UI 运行时 diff 出 SPI 再抽取——抽象来自两个真实实现
的对比，不来自单一实现的外推。
_Avoid_: 通用运行时（无买家的泛化声明）、god runtime（第二套总
Owner）、把 Core 记录层当运行时机制来源

**Perception**: Capability Plane 中从有界外部输入获取 raw artifact、或从
raw artifact 派生 `ObservationProposal` 的 typed capability；跨 owner 的观察
输出只经 P2 出面，不拥有 Evidence admission、WorldBelief、identity、control、
assurance 或 binding authority。
_Avoid_: L1/L2 Owner、canonical authority、直连 World Model

**Fast / Slow Perception**: Perception 内部用于选择与组合观察推导方式的
realization category，不是顶层产品组件、跨组件协议参与者或 authority class；
具体 realization 必须产生兼容的观察 / association evidence，并经同一 P2/P3
链汇入 UIWorld Authority。
_Avoid_: 顶层 Fast/Slow 模块、固定算法、独立 authority、跨组件直接依赖具体 realization

**Evidence Record**: Evidence Ledger admission 通过后形成的不可变
canonical 观察依据记录（EvidenceId + claim + kind + observation
context + provenance）。
_Avoid_: raw artifact、producer claim、数据、observation

**Perception Acquisition**: Capability Plane 观察侧的采集能力——从设备或
环境获取新鲜外部输入并产出 raw artifact，供 Perception 解释；只采集不解释
像素。属于可替换的 provider capability，非 Owner、非 Authority；其输出与
其他 producer 一样经 P2 ingress 出面。采集侧的传输形态与宿主位置属
realization 细节，不是域概念。
_Avoid_: 直连 World Model、解释像素、第二套 observation 语义

**Observation**: 对世界状态的观察声明（ingress 输入候选，admission
之前，不是 canonical 记录）；semantic kind。来源与触发时机（external /
post-action / 自产）属 provenance，不构成 kind 差异。
_Avoid_: raw event、self observation、post-action kind、canonical record

**AttemptReport**: 对一次 dispatch attempt 的投递报告（ingress semantic
kind）；必须可关联到具体 attempt；不是 world-state evidence，也不是
effect proof。
_Avoid_: delivery confirmation、result、self observation

**Observation Context**: 观察发生的流程上下文（External ｜
PostActionEffectFlow）；表达「为什么 / 在哪个流程被观察」，不表达谁生产
（ProducerIdentity），也不表达真实性（Deferred ⑦）。是 MaterialEffect
fulfillment policy 的判定输入。
_Avoid_: origin、producer identity、trigger、source

**Admission**: Evidence Ledger 对输入是否具备成为 canonical Evidence
Record 条件的结构性判定（integrity/provenance/来源/时间/scope/lineage），
只判 eligibility，不判真值、相关性或充分性。
_Avoid_: validation、truth judgment、acceptance（作为判定之名；结果值词 Accepted 不在避讳之列）

**Provenance**: 一条观察「谁产生、何时、声明何 scope、经历何种转换」
的不可变溯源记录。
_Avoid_: metadata、source info

**Belief Relevance**: World Model 对单条 accepted Evidence Record 是否
影响 Current WorldBelief 的独立判定（与 Admission 是两个产出）。
_Avoid_: interest、matching、admission

**Reconciliation**: World Model 将 accepted Evidence 转换为新
WorldBelief revision 的唯一路径；必须显式处理冲突并保留完整 basis。
_Avoid_: update、merge、sync

**WorldBelief**: 系统基于 accepted Evidence 形成的版本化、可修正的当前
世界判断；每次 Reconciliation 产生新 revision，历史 revision 只读。
_Avoid_: world state（整体义）、cache、snapshot

**World State**: WorldBelief 中动态 state/claim 的组成部分
（subject → claim + evidence 溯源），随 revision 一起产生，无独立
current-truth 生命周期。
_Avoid_: parallel truth、belief cache

**Evidence Basis**: 一个 WorldBelief revision 的全部 Evidence Record
引用集合。
_Avoid_: inputs、sources

**Conflict**: 同 subject 的不相容 claim 并存的显式记录（双方 evidence
引用都保留）；Reconciliation 不静默覆盖。
_Avoid_: contradiction error、overwrite

**Slice**：Core 语义中的局部观察或派生表示，必须在明确来源版本、观察时间、范围和
解释规则下可固定保存或可靠还原，不限定视觉场景；机器人局部地图、文件子树、API 分页
结果和数据库查询窗口均可采用。现有 Kernel/UI realization 的 Slice 是从 WorldBelief
revision 派生的 scoped 只读 Consumer View，其有效性仍由 revision currency 派生判定，
并锚定 RootContainerIdentity + buyer-required InScopeContainerRefs（UIW-002）。因此
“Core Slice”是上位语义，“Kernel WorldBelief Slice”是当前已验证 realization；来源、
被观察对象、局部范围、原始观察时间和处理版本不可混同。重新处理旧数据不等于重新观察。
_Avoid_: region、observation scope

**Consumer View**: Owner 从自身 canonical state 为单一 consumer 履职
派生的不可变最小 projection（ADR-0011）：Owner 唯一派生（consumer
不得从 aggregate 自行投影）；consumption-scoped / ephemeral（每次
消费前由 Owner 即时派生，consumer 不缓存、不跨 operation 重放）；
不是第二 truth（view 无独立 currentness / freshness 权威，一致性由
consumer 以 revision correlation anchor 校验）；只携带 Owner-owned
fact，不携带 consumer-owned judgment（裁决权留在 consumer）。字段
必须有真实读取证据；结构由 public shape allowlist 锁定。
BindingView / ActionAssuranceView / OutcomeAssuranceView 是
WorldBelief 侧实例（EXP-008）；GroundingView 是 grounding 消费路径
实例（P23 出面，UIW-002）。
_Avoid_: god DTO、shared mutable context、aggregate copy、cache、万能 view

**Freshness**: belief 依据对一次具体消费（action judgment）是否足够新
的判断维度；相对消费需求成立，不是 WorldBelief 自身属性，不形成全局
fresh/stale 真相；与 revision currency（是否仍 current）相互独立。
producer 自报的 CaptureTime 只是 temporal provenance 输入，不构成
freshness 权威。
_Avoid_: recency、TTL、revision currency、capture time（同义化）、belief 属性

**Freshness Basis**: World Model 随 revision 表达的 freshness 判定输入
聚合（temporal provenance 层面，如 basis 中最晚 CaptureTime）；是表达
不是裁决，不单独构成 freshness 权威。
_Avoid_: freshness judgment、latest capture time（绑定算法）、freshness 权威

**Freshness Judgment**: Assurance 在具体 action judgment 时对
Freshness Basis × 本次消费 Consumption Requirement 的关系做出的充分性
裁决，三态 Sufficient / Insufficient / Unknown；Insufficient 与
Unknown 都 fail-closed 且不得折叠；结果只对该次消费有效，不回写
belief，也不使 CanonicalBinding 派生 validity 失效（拒绝的是授权）。
_Avoid_: belief state、degradation、IsFresh flag

**Consumption Requirement**: 一次具体消费（action judgment）对 belief
freshness 提出的要求（target / scope、effect 语义等 action-local 要求）；
是 Freshness Judgment 的关系输入之一，字段集不随 realization 锁死。
_Avoid_: freshness policy（算法 / 阈值义）、constraint（泛义）

**UI World Realization**: 按 Core 顶层抽象契约实现的第一个领域
realization（ADR-0024）：拥有 UI 领域细节——identity 铸造与判别、
observation strategy、locator/grounding 形态、continuity adjudication；
revision（含 Container/Occurrence/LogicalItem）即其产物。物理落点 =
Kernel World 内模块（`World/UiRealization/`），出向依赖白名单由
UiRealizationBoundaryTests 执法（System / Core / World owner /
Evidence 输入缝）；Core 不反向生成 UI identity；程序集升格推迟至
第二个 realization。
_Avoid_: 把 WorldModel 原样搬成 UI Core、Core→UI 反向投影、独立程序集（当前义）

### Container & Association（UWM-009 落定）

**Container**: UIWorld 认为具有相对独立交互语义边界、能被持续识别 /
进入 / 离开 / 覆盖 / 恢复或作为交互上下文存在的 UI world entity；不是
任意 UI node / DOM element / 视觉区域。ContainerKind taxonomy 未冻结。
_Avoid_: UI node、DOM element、bounding box、任意视觉区域

**ContainerIdentity**: World Model 对持续存在 UI world entity 的 canonical
identity（≠ screenshot / OCR / bounding-box / DOM / Observation identity）。
Perception / Vector / VLM 只能提供 association evidence，不建立 identity
truth。
_Avoid_: detection id、track id、OCR identity、observation identity

**ContainerGraph**: baseline「World Graph」的 L4 内部 realization：
revision-bound、evidence-backed 的世界实体间关系 belief（Contains /
Overlays 是 graph relation，即使生命周期很短）；与 WorldState（单实体
intrinsic / contextual state claims）按 semantic kind 分界，不按变化频率
分界；graph relation ≠ timeless structural truth；不成为跨组件公共协议。
_Avoid_: timeless structure、DOM tree、跨组件协议对象

**Container Association**: World Model 内部维护 ContainerIdentity
continuity 的领域过程；合法输入 = accepted world-relevant Evidence（P3）+
previous WorldBeliefRevision + TransitionContext（仅 prior，P22）。结果见
**AssociationDisposition**。
_Avoid_: tracking、matching（泛义）、外部 Data Association 模型直接覆盖

**AssociationDisposition**: 一次 Container Association 的判别结果，四值
Matched / New / Ambiguous / Insufficient；Ambiguous = observation 充分但
存在多个成立的 identity interpretation；Insufficient = observation 缺乏
判别信息；两者都不得 create / replace canonical identity。与 claim 认知轴
（Known / Absent / Unknown / Conflicting）分属两条 epistemic 轴，词汇不得
混用。
_Avoid_: Unknown（association 轴义）、score、probability、forced pick

**Transition Context**: 经 P22 进入 World Model 的 actual runtime
attempt/effect non-evidentiary 上下文；只影响 association candidate
prior/ranking，不是 EvidenceRecord、不是 World claim、不建立 identity、
不 mutate canonical WorldState；ControlIntent / 期望的 transition /
Control plan 永不进入（ADR-0012）。verified post-action effect 另由
accepted PostActionEffectFlow Observation 支撑，与 P22 正交。
_Avoid_: intent、expectation、plan、evidence、identity truth

**Observation Sufficiency**: observation 是否具备完成 identity
discrimination 所需信息覆盖的语义；与 match confidence 是不同语义
（low match + insufficient observation → Insufficient，不是 New）。
_Avoid_: confidence、score

**ObservationNeed**: UIWorld 内部领域概念，表达当前 belief 缺什么
information（信息型缺失，不指向任何 perception capability）；作为
WorldModel → Observation Control 的外部协议边 = DEFER — NO CURRENT
BUYER。
_Avoid_: CallVLM/RunOCR 类能力指令、外部协议对象、priority policy

### UI Entity Model & Continuity（UIW-002 落定）

**ObservationOccurrence**: revision 内 revision-local 的 observed UI
presentation（belief 侧、随 revision 携带）；occurrence-ref 协议引用是
revision-scoped，只在源 revision / binding 作用域内有效；序列化保存后
仅为历史记录，不得充当有效 target handle；provider node id / bbox /
OCR / DOM / UIA node / detection id 只是 evidence，永不是 identity。
_Avoid_: provider node、detection、DOM element、persistent element handle

**LogicalItem**: owning Container 内由 ContinuityDemand 购买 eligibility、
由 accepted evidence 经 reconciliation 建立的 actionable logical referent
有界连续性（demand-gated, evidence-established）；scope ⊆ Container
lifetime；四轴（Lifecycle / ContinuityAdjudication / Presence / Maintenance）
不混用；Ended 仅来自正面 lifecycle evidence，Ambiguous / Insufficient /
New / Absent / Contradicted / demand 消失皆 ≠ Ended；continuity 跟随
logical referent 而非 presentation node。
_Avoid_: UIEntity、Element、InteractiveEntity、affordance-minted identity、
Demand-Minted（术语已废弃）

**ContinuityDemand**: 经 P23 缝声明 same-referent continuity 需求的
non-evidentiary 输入；只购买 tracking eligibility / maintenance，不建立
身份、不产生 WorldBelief revision；DemandHandle 是不透明 correlation
token；生产者封闭（EffectTargetCommitment / EntityScopedObligation），
Control 仅引用不铸造。
_Avoid_: evidence、intent、identity request、tracking flag

**ContinuityResolutionOutcome**: continuity 缝结果：ReferenceEstablished ｜
ContinuityAdjudicationOutcome(SameReferent / Ambiguous / Insufficient /
Contradicted) ｜ NoCurrentCandidate；首次建立（无 previous referent 可比）
≠ same-referent 判别；Contradicted 只证伪候选、不终止 LogicalItem；判别
结果必须带类型限定，与 Container Association 词汇不混用。
_Avoid_: AssociationDisposition（container 轴）、Matched（跨轴裸用）、
New（continuity 轴无此值）

**CurrentCandidateSetResult**: ResolveCurrent 的投影相对候选集事实：
UniqueCandidate / NoCandidate / MultipleCandidates /
ScopeProjectionUnavailable；NoCandidate ≠ KnownAbsent ≠ Ended（投影
相对事实，不宣称世界 absence）。
_Avoid_: no-match error、absence 判定

**ReferentBasis**: World Model 内部维护 LogicalItem referent 的可修订
evidence/belief basis（owning container + logical role + semantic/context
anchors + relevant relations + optional platform stable key）；不是复合
identity key，不进消费者协议。
_Avoid_: identity key、fingerprint、composite hash、selector

### Control & Effect（C2E-002 落地）

**Execution Contract**: UniAgent 交给 Uni Kernel 的稳定执行语义边界，
描述 objective / scope / effect constraints / proof criteria / 可选
run-level obligations。accepted version 不就地改写；显式新 version 的
supersession / replacement 语义尚未锁定。
_Avoid_: task list、plan、SLA

**Execution Contract View**: Run Model 在 contract 被接受时建立的
immutable canonical view；同 version 重复 admit 幂等复用同一实例。
_Avoid_: contract copy、session config

**Run State**: Run Model 拥有的 canonical 执行状态聚合（Contract View +
Objective + Proof Obligation + Progress + terminal **Outcome State**）；
只经 typed legal transition 更新，不含 action-local assurance
state；terminal 后冻结，不可恢复 active。
_Avoid_: god context、execution log

**Run Snapshot**: Run Model 可提供给 Control Loop 决策的 run 侧状态
视图（objective status / run-level obligation statuses / progress）。
当前无 buyer：Control 对 run 侧信息的 runtime data dependency 为零
（EXP-008 实测），P5 载荷 deferred——没有 buyer 就没有协议载荷，不
为保协议编号制造空协议或空 DTO；未来真实 buyer 出现时恢复该边并按
Consumer View 规则立 view。不是全量 Run State，不含 action-local
assurance state，也不是 Goal Evaluation。
_Avoid_: run state dump、progress log、goal evaluation、空协议载荷

**Control Intent**: Control Loop 唯一签发的控制产出（observe / act /
recovery）。act-intent 只携带 target hint 与 basis revision，不是
binding，也不是 authorization。act-intent 的 target hint 可为语义
descriptor 序列化（如 "role:descriptor"）——hint ≠ binding ≠ UI
targeting；CONTEXT Avoid 的描述字符串约束作用于 binding 通道（UI
通道恒绑 OccurrenceRef，UIW-004）。
_Avoid_: command、action、instruction

**Tactical Hypothesis**: Control Loop 内部的 disposable 假设，不是事实、
authorization 或 Completion 来源；不得进入 Assurance / Binding /
Reconciliation 的任何输入签名。
_Avoid_: plan、belief、strategy state

**Assurance Judgment**: Assurance 针对特定 (Control Intent, Canonical
Binding, WorldBelief revision) 三元组形成的不可变 action-local 判定
（admissibility / currentness / freshness sufficiency / safety guard）；
三元组任一成员改变后必须重新判断，Freshness Judgment 只对该次消费
有效。三元组是 correlation key，不是 canonical identity。
_Avoid_: validation result、gate check、permission

**Verified Effect**: Assurance 基于 accepted post-action Evidence 确认
外部环境发生了与预期 effect 一致的改变；不由 DispatchResult /
EffectReceipt / producer 自述直接建立。是 MaterialEffect obligation
fulfillment 的判定输入。
_Avoid_: effect confirmation、dispatch result、receipt、producer 自述

**Candidate Binding**: Grounding Provider 产出的候选目标绑定；只流向
Effect Boundary 认定路径，不进入 Assurance 输入，未经认定不具任何
dispatch 权威。UI target 为已解析引用（OccurrenceRef ｜ LogicalItemRef，
UIW-002）；descriptor 匹配属 provider 内部过程，输出是 resolved ref。
_Avoid_: binding、target、resolved element、描述字符串（UI targeting 义）

**Canonical Binding**: Effect Boundary 认定的唯一有效 bounded target
binding，绑定 specific WorldBelief revision；是 Assurance judgment 的
授权对象。Validity 与 authorization 分离：validity 由 revision
currency / consumption 派生判定（无 event），freshness 充分性经消费侧
Freshness Judgment 执法；judgment 拒销不使 binding 失效，binding 存在
也不构成 authorization。UI target shape：Target 恒绑 CurrentOccurrenceRef
（optional LogicalItemRef / ContinuityAdjudicationRef 表达 referent 依据）；
LogicalItem 永不直接承载 effect（UIW-002）。
_Avoid_: locked target、final binding、LogicalItem 直连 dispatch

**Authorization**: 某次 dispatch 被允许的复合可消费态——admissible
Assurance Judgment ∧ 仍 valid 的 Canonical Binding ∧ 三元组匹配；消费
时点派生，非独立 protocol object；唯一消费面是 Effect Gate。
_Avoid_: permission、approval、judgment（同义化）

**Effect Gate**: Effect Boundary 内只执行或拒绝既有 authorization
judgment 的执法点；不重新判断、不改变 target、不扩大 effect。
_Avoid_: validator、checker、approver

**Effect Receipt**: dispatch 后的不可变投递留痕；是 attempt evidence，
不证明 Effect（回流语义见 **Attempt Evidence**）。
_Avoid_: effect confirmation、result、feedback

**Attempt Evidence**: 以 AttemptReport 语义回流并被 admit 的投递报告
证据统称——attempt 留痕，不作为 world-state evidence / effect proof。
lifecycle：Effect Receipt（Effect Boundary 留痕）→ AttemptReport
（回流 kind）→ Attempt Evidence（admitted 统称）。
_Avoid_: effect confirmation、feedback、world observation

**Dispatch Request / Dispatch Result**: Effect Boundary 与 Capability
Plane 之间只表达「做什么」（bounded command，无 authorization 语义；
binding identity 不下穿）与「本次 attempt 结果」的机械缝对象。
Result 的 outcome 是对世界效果的三态认知：DeliveryCompleted /
DeliveryFailed / UnknownOutcome（成因入 reason diagnostic，Cancelled
是 reason 不是第四种 outcome）；UnknownOutcome 唯一合法后继是
re-observe，永不盲补发。Capability 对授权态零感知，不得自行
retry / replan。
_Avoid_: command（泛义）、delivery confirmation、effect

**Reliable Execution Source（可靠执行源）**: Effect Boundary 经构造注入
的可靠执行记录源（ADR-0023 / CORE-013）：在 gate 通过、driver 调用
之前可靠登记准备集合（correlation 原语：binding/judgment/请求/执行端/
准入摘要），追加 submission / receipt / 迟到反馈，提供无 Attempt ID 的
未决发现与重试（LinkRetry = 新 Attempt 同 Effect）/补偿
（LinkCompensation = 新 Effect）关联。只存 correlation 与不可变记录，
append-only，不重铸 CanonicalBinding、不重判 admissibility；与 Trace
零依赖零供给；Discovery 只读，消费决策（重试/补偿/重观察）归
Control/Agent 流。默认实现 = 文件 append-only journal；提交非 success
在 Effect Gate 以 `execution-commit-failed/unknown` reason fail-closed
（循 delivery-closed 先例），零 driver 调用；driver 之后的追加失败不吞
delivery——记录停留未决。
_Avoid_: 恢复权威、第二 delivery truth owner、Trace 存储、数据库

**Commit Boundary（提交边界）**: `CommitResult=success` 的判定语义：
记录已到达实现声明故障范围内可恢复读取的边界（CORE-012 Q3 限定）。
文件 journal 的边界 = 帧完整写入并 Flush（托管缓冲 → OS 文件系统），
未 Dispose 的进程死亡后同机重开可完整读取；页缓存在单机单进程故障
范围内达标。torn 尾帧 = 该提交从未成功（append-only 正确语义，非损坏
恢复）；仅写内存、发起异步写或依赖未定义缓冲不得报 success；fsync
调优、OS 崩溃/断电、磁盘损坏恢复、跨节点是显式 non-goal。
_Avoid_: 内存落库即成功、异步提交、fsync 承诺（v1 义）

**Pending Attempt（未决尝试）**: 执行源中已越过 commit-success 但无
确认 Receipt 的 attempt 状态集（CommittedPending / CommitUnknown /
Dispatched / UnknownOutcome）；未决可被无 ID 发现（声明范围 = 执行源
实例/journal 路径）。从未提交、CommitFailed、已确认 Completed 不可
发现为未决——这是「无可发送 Attempt」与「重启不重复投递」的结构
保证。未决不解释为成功、失败或已发送；确认 Receipt 后脱离未决集合。
_Avoid_: in-flight（泛义）、失败队列、重发队列

**Desired-State Satisfaction**: Control 在签发非幂等 act-intent 前对
「目标期望终态是否已满足」的决策判断（ADR-0017）：已满足 → 不签发
intent（decision outcome，不是 effect）；未满足 → 正常签发；Unknown →
observe/resolve/safe-stop per policy。判定输入是 occurrence 携带的
revision-local state（null = Unknown ≠ false）；期望终态属 authoring
意图（desired-state 型 intent 才有；Click 型无期望终态，不适用）。
_Avoid_: NoOp effect（canonical 动作义）、Operate / IOperation /
Executor（作为 canonical noun——其候选需求由 Effect Boundary Dispatch、
P14/P15、AttemptReport、Control Recovery 既有面承载）、post-action
verification 兜底（只能发现破坏，不能防止破坏）

**DeliveryTarget**: Effect Boundary lowering 产出的 driver-executable
目标地址（DSE-002）：OccurrenceReference 仅溯源（receipt/attempt/trace
关联），永不参与执行——invariant：Reference identity ≠ executable
locator。可执行锚 = SpatialLocator（归一化 bounds + 开放 frame 词汇）
与 NativeLocator（平台原生键，开放 kind 词汇；execution anchor 非
identity）；两者并存 = 同一 target 的不同 delivery material，driver
固定消费自身支持集（adb 只认 spatial、ego-browser 只认 native），
永不挑选；driver 只做物理翻译，不做 locator fallback 或
语义重定位——locator 失效的正确后继是回上游 re-observe → re-ground →
新 binding。五层名词不混：TargetDescriptor（找谁）→ ObservationOccurrence
（看到谁）→ CanonicalBinding（作用谁）→ DeliveryTarget（去哪执行）→
DispatchRequest（送什么）。
_Avoid_: element handle（persistent 义）、selector、坐标元组（无 frame 的裸值）、provider node key

### Outcome & Terminal（OUT-003 落地）

**Proof Obligation**: contract/run-level 证明要求（objective / material
effect / completion / failure / safe-stop / escalation 六类）。Run Model
只记录；满足判定由 Assurance 执行。action-local requirements（target
freshness / one-step precondition / admissibility / grounding validity）
属 Assurance 短生命周期 judgment，永不进入 Run State。
_Avoid_: task、checklist、acceptance criteria

**Obligation Fulfillment**: 单条 Proof Obligation 的 evidence-backed 满足
状态（Assurance 判定产出，Run Model 记录）：current WorldBelief 内存在
accepted Evidence 支持的 subject=value claim（backing EvidenceId ∈
basis）。MaterialEffect 需要足够的 **Verified Effect** evidence——当前
fulfillment policy：accepted Observation 且 ObservationContext =
PostActionEffectFlow（**AttemptReport 永不满足**；ProducerIdentity 不
参与判定）；合法 context 集合 / provenance trust policy 待
anti-spoofing buyer 扩展。entity-scoped obligation（ESO-002）：满足判定 =
owner-derived fulfillment fact（tri-state Satisfied/Unsatisfied/Unknown，纯
派生自 Current occurrences 的 descriptor 唯一匹配 + State）；Satisfied 是
belief fact，fulfilled 判定权在 Assurance（Unknown/Unsatisfied 如实未满足）。
_Avoid_: satisfied flag、done

**Outcome Proof**: Assurance 对 Run-level Proof Obligation State 是否具备
足够 accepted Evidence 支持具体 terminal claim 的终局判断；四分类：
Completion / Failure / SafeStop / Escalation，各自独立 evidence-backed。
「证据不足 / 未知」不是分类成员——由 proof absence 表达，不得伪装成功或
失败。
_Avoid_: completion certificate、result

**Terminal Outcome State**: Run Model 在终局判断接受后记录的 terminal
OutcomeState 快照（Outcome Proof ref + classification + obligation
statuses + evidence refs + unresolved uncertainty）。只记录、不重判；
exact-prior single-winner；至多成功进入一次；terminal 后不可恢复 active。
_Avoid_: result state、final status

**Runtime Outcome**: Uni Kernel 唯一产出的 immutable terminal envelope
（exactly once；Run identity / terminal classification / fulfilled /
unfulfilled obligations / Outcome Proof ref / evidence refs）。只从
Terminal Outcome State 投影；Kernel 不重判完成、不解析 Evidence、不改
classification。载荷三层：outcome semantics / proof-provenance（表达
「基于什么被证明」，非日志）/ explanatory metadata。
_Avoid_: result、final answer

**Delivery Closure**: terminal 后 Effect Boundary 关闭 external effect
delivery 的机制；任何 dispatch 请求被显式拒绝且不重判、不扩权，late
candidate binding / late authorization / late driver callback 均不得
恢复 Run。
_Avoid_: lockout、freeze

**Kernel Self-Driven Primary Run**: Primary Run 合法激活后，由 Uni Kernel 推进至
terminal Runtime Outcome；UniAgent 与 Host 不逐 cycle 驱动。该关系不改变任何
L2 Owner、Authority 或 Effect Boundary。
_Avoid_: UniAgent step driver、Host-driven Kernel、agent turn = run cycle

**Legal Activation**: accepted Execution Contract View 之后、Kernel self-drive
开始之前的一次性幂等 lifecycle command；admission ≠ activation。同一
accepted Contract View generation 下至多一个 Primary Run，重复激活零副作用。
_Avoid_: contract admission（同义化）、run creation、start command（泛义）

### UniAgent & Goal Evaluation（GEV-004 定稿）

**UniAgent**: 面向用户的 L1 监督主体（Uni Kernel 之外的 L1 peer）；
拥有 Primary Goal 与 Goal Evaluation，不拥有 WorldBelief、Run State、
Assurance Judgment、target binding 或 effect delivery。
_Avoid_: 壳/shell、orchestrator、supervisor（泛称）

**UniAgent Realization**: 在具体 Host Runtime 上完整承担 UniAgent 产品职责与
lifecycle、并满足 UniAgent Conformance Surface 的实现；不是模型 Provider、
单次 agent turn、普通 AI Coding workflow 或 UniAgent 内部 Adapter。
_Avoid_: reasoning adapter、model wrapper、coding agent task

**Simulation Realization**: 用于产品语义模拟、场景验证、差分 conformance 与
回归的完整 UniAgent Realization；它遵守相同核心产品契约，但不声明生产就绪。
_Avoid_: deterministic fake、mock agent、lower-standard realization

**Product Realization**: 作为目标产品实现方向、并须以 recovery、security、
isolation、audit 与 operations evidence 证明生产就绪的完整 UniAgent Realization。
_Avoid_: production-ready by designation、coding profile

**Host Session**: Host Runtime 提供的 thread/session 承载体；它可保存
realization-private state 并显式关联 Product Session，但不是 Product Session，
也不取得任何 Product Authority。
_Avoid_: Product Session、Primary Run、canonical session

**UniAgent Conformance Surface**: 多个 UniAgent Realization 共享的 Host-neutral
可观察 Interface，由 canonical inputs/records、lifecycle、failure 与 evidence
组成，不包含 transcript、Host id、tool sequence 或 transport vocabulary。
_Avoid_: transcript equality、Host API、tool-call script

**Primary Goal**: 用户希望现实世界达到的结果；由 UniAgent 创建并携带
显式 Goal Criteria。Goal revision（显式澄清链）不在当前语义内。
_Avoid_: task list、plan、objective（contract 的）

**Goal Criterion**: Primary Goal 携带的结构性 satisfaction 判据，以语义
身份（obligation id、terminal classification 词汇）引用稳定的 Outcome
语义；不耦合 Kernel object graph 或 envelope 字段布局，不引用
Evidence / WorldBelief。
_Avoid_: proof criteria（contract 的）、acceptance criteria、predicate、field path

**Goal Evaluation**: UniAgent 消费 Runtime Outcome envelope 后形成的、带
自身 EvaluationId 的 immutable 监督评价记录（satisfaction 分类 + 处置 +
逐 criterion 结果 + rationale）；只依据 envelope、Goal Criteria 与
Goal Evaluation Context，不回写 Runtime Outcome / Run State /
WorldBelief / Outcome Proof。
_Avoid_: result、completion certificate、re-judgment

**Goal Satisfaction**: Goal Evaluation 的 satisfaction 维度，封闭四值：
Satisfied / PartiallySatisfied / Unsatisfied / Undetermined；判据不可
验证时唯一诚实取值是 Undetermined，不得伪装满足或不满足。
_Avoid_: outcome classification、status、rating、needs follow-up（处置义，见 Evaluation Disposition）

**Evaluation Disposition**: Goal Evaluation 的处置维度：Final（终局监督
结论）/ NeedsFollowUp（需后续澄清、补充契约或重评）；只表达处置，
不实现 follow-up 机制。唯一长期不变量：Undetermined ⇒ NeedsFollowUp；
其余 satisfaction→disposition 组合是 slice policy 而非永久语义。
_Avoid_: satisfaction 成员、follow-up mechanism、action item

**Goal Evaluation Context**: Goal Evaluation 第三输入（user/supervisory
context）的 opaque 契约；当前仅允许 Empty，不携带业务语义。
_Avoid_: user profile、session state、config

**Consultation**: Kernel 在语义 decision boundary（T1-T6 触发点）经外部
seam 向 UniAgent 发起的一次同步请求-回答：输入 = 有界
AgentDecisionContext，输出 = 恰一个 AgentDecision（或 null = no-response
fail-closed）。咨询时机与次数由 Kernel 独占（D1-D7 纪律）；Agent 只回答，
永不发起。Host realization 内部的 turn/step 结构是回答的产生方式，不是
第二次咨询。
_Avoid_: agent invocation（泛义）、chat round、turn（跨 realization 混用）、
Agent 发起的询问

**Agent Strategy State**: UniAgent 侧允许持有的认知状态——Plan
Hypothesis、被弃策略及原因、policy 耗尽史、语义假设、decision
rationale；只回答「我准备怎么做、以前为何失败」，永不充当「现在是什么」。
当前现实断言的唯一来源是最新 AgentDecisionContext；策略记忆影响推理质量，
不产生 World/Run/effect/proof/binding 真值（baseline §24.5 Agent
Continuation 的 realization 载体；与 Memory Recall 同受 non-evidentiary
纪律）。
_Avoid_: shadow state、第二 World Model、cached world summary（作权威义）、
execution log

### Simulation Substrate（RFS-001 落定）

**ScenarioStimulus**: 由 sealed Trace 或 reviewed fixture 经 Scenario Importer
派生的 immutable、显式版本化外部输入（观察帧、cancel、virtual time）；
不是 Trace Event、不是 canonical Evidence、不是内部 owner-state 注入。
Simulation 只消费 ScenarioStimulus，不逐 cycle 驱动 Kernel。
_Avoid_: trace event、command、recorded script（逐 cycle 义）

**Simulation Host**: 独立 composition root，装配同一 Product Runtime artifact
与 sim-only 外部 adapter（Stimulus consumer、recorded perception、
deterministic driver、test-only Oracle）；不是 Product Host 的运行模式，不
保存第二份 Run/World/Control state。
_Avoid_: Product Host 的 simulation=true 模式、Simulator FSM

**Product Host**: 产品 composition root；其依赖闭包不包含 ScenarioStimulus
consumer、Replay、Oracle、Scenario Importer 或任何 Simulation 功能。
_Avoid_: 带 hidden sim flag 的统一 host

**ScriptedUniAgent**: 走与真实 UniAgent 相同外部 seam 的 deterministic 测试
double；按脚本返回 proposal 并断言调用边界/顺序/次数/correlation；不是第三
种 UniAgent realization，不证明 Agent 智力，不调用 live model。
_Avoid_: 第三 realization、mock agent（作为 realization 义）、intelligence proof

### Multi-Source Observation & Trust（PER-009 立项 2026-09-22）

**Shared Subject（共享 subject）**:
跨 producer 共用的 claim key 语义层，仅语义三件套（ui.screen /
*.state / screen.frame），由常量类承载统一拼写；元素级 subject
永不共享（防假合并）。
_Avoid_: 全局统一命名、元素级共享 key、词汇表（运行时组件义）

**Producer Trust Grade（源信任等级）**:
(producer × claim 类别) → A/B/C 的采信等级，CSS 级联覆盖
（包名/系统特例 > 类别 > 源默认）；等级挂组合、不挂单条证据，
每级对应明确采信动作（A=孤证可授权常规动作）。
_Avoid_: confidence score、数值权重、全局源排名

**Conflict Resolver（冲突裁决器）**:
同 key 多源分歧的统一裁决管道：Tier 0 类别权威（标准控件 XML
定案）→ Tier 1 裁剪重扫 → Tier 2 深模型（后置）；所有销案记录
同构留档（由谁定案、依据什么）。
_Avoid_: 投票、平均、按源加权合并

**Semantic Migration Mismatch（语义迁移不一致）**:
已冻结的前向 observation/fusion contract 与现有 realization 在 claim domain、source
authority 或 evidence lineage 上不一致的状态；该状态必须显式记录，并在新实现开始前
由独立 migration decision 定义过渡边界与验收，不自动回写旧 change 或把现状宣称为已对齐。
_Avoid_: silent compatibility、runtime-ready、automatic backport

### Memory（边界占位，未实现）

**Memory Recall**: Memory System 的召回产出；只能以 prior / context /
hypothesis / historical reference 身份进入 Agent / Control / World Model
（non-evidentiary），不得直接建立、刷新或证明 current-world claim——
WorldBelief 的事实更新必须依据 accepted current Evidence。
_Avoid_: memory evidence、current truth、context injection（无约束义）
