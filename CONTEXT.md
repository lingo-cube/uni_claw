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

**Evidence Record**: Evidence Ledger admission 通过后形成的不可变
canonical 观察依据记录（EvidenceId + claim + kind + observation
context + provenance）。
_Avoid_: raw artifact、producer claim、数据、observation

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

**Slice**: 从某个 WorldBelief revision 派生的 scoped 只读投影；有效性由
revision currency 与 freshness（时间维度）共同派生判定，不存在显式
invalidation event。原始局部观察输入（Observation Scope/Region）不得称
Slice。
_Avoid_: view、region、observation scope

**Freshness**: 依据的时间有效性维度，与 revision currency（是否仍为
current）相互独立。producer 自报的 CaptureTime 只是 temporal provenance
输入，不构成 freshness 权威；是否 fresh 由 freshness policy 判定。
_Avoid_: recency、TTL、revision currency、capture time（同义化）

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

**Run Snapshot**: Run Model 提供给 Control Loop 决策的 run 侧状态视图
（objective status / run-level obligation statuses / progress）；不是
全量 Run State，不含 action-local assurance state，也不是 Goal
Evaluation。
_Avoid_: run state dump、progress log、goal evaluation

**Control Intent**: Control Loop 唯一签发的控制产出（observe / act /
recovery）。act-intent 只携带 target hint 与 basis revision，不是
binding，也不是 authorization。
_Avoid_: command、action、instruction

**Tactical Hypothesis**: Control Loop 内部的 disposable 假设，不是事实、
authorization 或 Completion 来源；不得进入 Assurance / Binding /
Reconciliation 的任何输入签名。
_Avoid_: plan、belief、strategy state

**Assurance Judgment**: Assurance 针对特定 (Control Intent, Canonical
Binding, WorldBelief revision) 三元组形成的不可变 action-local 判定
（admissibility / currentness / safety guard）；三元组任一成员改变后
必须重新判断。三元组是 correlation key，不是 canonical identity。
_Avoid_: validation result、gate check、permission

**Verified Effect**: Assurance 基于 accepted post-action Evidence 确认
外部环境发生了与预期 effect 一致的改变；不由 DispatchResult /
EffectReceipt / producer 自述直接建立。是 MaterialEffect obligation
fulfillment 的判定输入。
_Avoid_: effect confirmation、dispatch result、receipt、producer 自述

**Candidate Binding**: Grounding Provider 产出的候选目标绑定；只流向
Effect Boundary 认定路径，不进入 Assurance 输入，未经认定不具任何
dispatch 权威。
_Avoid_: binding、target、resolved element

**Canonical Binding**: Effect Boundary 认定的唯一有效 bounded target
binding，绑定 specific WorldBelief revision；是 Assurance judgment 的
授权对象。Validity 与 authorization 分离：validity 由 revision /
freshness / consumption 派生判定（无 event），judgment 拒销不使 binding
失效，binding 存在也不构成 authorization。
_Avoid_: locked target、final binding

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
Plane 之间只表达「做什么」（bounded command，无 authorization 语义）
与「本次 attempt 结果」（≠ Effect ≠ Verified Effect）的机械缝对象；
Capability 对授权态零感知，不得自行 retry / replan。
_Avoid_: command（泛义）、delivery confirmation、effect

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
anti-spoofing buyer 扩展。
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

### UniAgent & Goal Evaluation（GEV-004 定稿）

**UniAgent**: 面向用户的 L1 监督主体（Uni Kernel 之外的 L1 peer）；
拥有 Primary Goal 与 Goal Evaluation，不拥有 WorldBelief、Run State、
Assurance Judgment、target binding 或 effect delivery。
_Avoid_: 壳/shell、orchestrator、supervisor（泛称）

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

### Memory（边界占位，未实现）

**Memory Recall**: Memory System 的召回产出；只能以 prior / context /
hypothesis / historical reference 身份进入 Agent / Control / World Model
（non-evidentiary），不得直接建立、刷新或证明 current-world claim——
WorldBelief 的事实更新必须依据 accepted current Evidence。
_Avoid_: memory evidence、current truth、context injection（无约束义）
