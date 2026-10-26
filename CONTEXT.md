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
WHAT NEXT across a development task's lifecycle. It does not define or
control the product UniAgent's Profile, Agent Skill, or Memory semantics.
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
System）以 Product T0–T3 baseline §8-§10 为准，不另立词条。

**Product Topology**: 产品组件的唯一公共拓扑轴，使用命名空间
`Product T0–T3`：T0 是系统边界与外部参与者，T1 是 UniAgent、Uni Kernel、
Capability Plane、Memory System，T2 是 Uni Kernel 六个责任域，T3 是责任域
内部模型、协议实现和 realization 模块。拓扑层号只表达结构位置，不推断
Owner、Authority、生命周期或协议状态。
_Avoid_: 裸 Product L0/L1/L2/L3、把层号当作 Owner、把语义对象当作组件

**Product Semantic Lifecycle**: 独立于 Product Topology 的产品语义轴：
`Product Session → Primary Goal → Execution Contract → Primary Run →
Outcome State / Runtime Outcome → Goal Evaluation`。Evidence Record、
WorldBelief Revision、Control Intent、Canonical Binding、Effect 和 Outcome
Proof 沿 Evidence/Belief/Control/Effect flow 关联；它们不占用 Product T0/T1
组件位置。
_Avoid_: 用 T0/T1 表示 Goal、Run 或 Outcome、从层号推断 authority

**Product Architecture Baseline**: 当前产品拓扑与语义生命周期的唯一权威，
落在 `docs/architecture/product-architecture-baseline-l0-l3.md`（路径暂时
保持稳定），状态为 `ADOPTED / PRODUCT-T0-T3-CLOSED`，元数据 Authority 为
`PRODUCT_ARCHITECTURE_BASELINE`。旧 Product L0–L3 只可作为历史引用或迁移映射；
设计稿、实现状态、局部协议和未提交词汇不能覆盖该权威。
_Avoid_: CANDIDATE_FOR_ADOPTION、Authority:NONE、把代码存在当作架构接受

**Protocol Status**: 协议边的事实状态枚举：`verified`（有可复现证据）、
`known gap`（边界已知但缺实现/证据）、`known deviation`（实现有意偏离且已记录）、
`placeholder`（仅有语义占位）、`deferred`（等待真实 buyer 或后续裁决）、
`not applicable`（对当前范围不适用）。代码存在、文档描述或测试通过都不能单独
升级协议状态。
_Avoid_: implemented 即 verified、把 deferred 当作已闭合、用空 DTO 伪造协议

**Buyer Evidence**: 证明某字段或协议边进入当前最小完备性闭包的证据组合：必须同时
存在当前 consumer、已登记场景或可判定验收、以及可回指的 Change。Owner 裁决只冻结
语义，不单独构成实现 buyer；缺少 buyer 时可标记 `NO_REAL_BUYER` / `deferred`，但
不得掩盖对当前 identity、authority、Effect safety 或 terminal closure 的依赖。
_Avoid_: 用户提到即算 buyer、代码存在即算 buyer、用未来可能需求阻塞当前闭包

**Minimal Buyer Closure**: 由当前 Buyer Evidence 所要求的字段集合构成的最小闭包；
只对该集合判定字段 Presence、Domain、Authority、Correlation、Validity、Absence、
Retry 和 Recovery。无当前 buyer 的预留字段不进入分母，但必须保留 deferred 状态和
明确重开条件。
_Avoid_: 对想象中的全协议全集求完备、把 deferred 当 PASS、把安全依赖降级为无 buyer

**Uni Kernel**: 六个 Product T2 的 aggregate execution boundary 与唯一 Runtime
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

**Capability Plane**: 承载可替换 typed capability 的 Product T1 架构面；能力可以接收有界输入并产生 raw artifact、typed proposal、candidate binding 或 mechanical receipt，但不拥有 canonical Evidence、WorldBelief、Run State、Control、Assurance、Effect 或 terminal authority。
_Avoid_: Capability Integration Plane、插件总线、第二套 Product Owner

**Capability Management Role**: 管理 Capability Plane 能力注册、分类、协议、作用域、健康和生命周期的架构角色；它是装配与分发语义，不是新的 Product T2 或 canonical state owner。
_Avoid_: god hub、authority hub、万能回调中心

**Capability Hub**: Capability Management Role 在一个信任域中的 realization。Product Runtime、Runtime Integration 和 Development Harness 各有独立注册域与 Composition Root，只共享 Host-neutral lifecycle fact 和 correlation 词汇，不共享可变注册状态或权限。
_Avoid_: 跨域共享注册表、Capability Integration Plane、隐式插件加载

**Capability Definition**: Product 对外承诺的能力语义与协议边界；它定义消费者可以依赖的输入、输出、作用域和失败语义，不等于某个模型、Provider 或运行实例。
_Avoid_: model、provider response、implementation detail

**Capability Implementation**: Capability Definition 的具体实现来源，可以是模型链路、外部 Provider、内部算法或组合 realization；它不得因此取得 Product canonical authority。
_Avoid_: Product capability identity、canonical owner

**Capability Binding**: 将 Capability Definition 绑定到特定 Host、Task 或信任域的装配关系；Binding 决定注入、激活和暴露形态，但不改变 Capability 的语义或 authority。
_Avoid_: global singleton、second registry、Tool identity

**Capability Instance**: 某次 Host 或 Task 根据 Capability Binding 解析出的运行时实现实例；它具有本次作用域，不回写全局注册状态。
_Avoid_: global capability state、canonical capability truth

**Task-scoped Capability Binding**: 将能力实现与一次任务的固定配置、Runtime 身份和局部生命周期关联的装配关系；任务结束后关闭该关系，不改变全局能力注册状态或其他任务的关联。
_Avoid_: global singleton、registry scope、Product Owner

**Tool**: Development Harness 面向其消费者（AI Coder、Workbench、前端模型调用）暴露的调用单元，登记于 `tool-registry.yaml`（ADR-0039）；它可由 Capability 经 `capabilityRef` 单向映射得到，也可以是 harness-native 的确定性脚本或以 skill 为 procedure 载体的模型过程；调用形态（CLI / MCP / HTTP）是它的 implementation detail。Tool 条目只持暴露元数据，不定义产品能力语义、不拥有运行时状态；skill 单源双路径——研发态经 catalog 直载，产品态经 skillRef 由 adapter 解析注入，产品代码不得直接引用 harness skill 路径。
_Avoid_: Capability 类型、第二套 CapabilityHub、skill 同义词、产品 runtime 直接调用面

**Provider**: 可独立运行、通过明确跨进程或跨语言协议接入 Capability Plane 的外部实现；Provider 只拥有自己的实现和运行事实，不拥有 Product canonical state。
_Avoid_: platform bucket、Product Owner、authority service

**Provider Manifest**: Provider 的静态发现与装配声明，描述 provider identity、supported capability、协议版本、transport、启动入口、健康检查和测试入口；它不是 Product Capability 语义或运行时健康事实的权威。
_Avoid_: capability truth、health result、runtime fact

**DSH Development Configuration**: `.dsh/` 下供 DSH 开发、profile 装配和本地观测使用的配置；它不是 DSH 产品源码，也不是 Provider 实现或生成缓存。
_Avoid_: DSH product source、provider、build output

**Lifecycle Fact**: canonical Owner 或 Host 在自身 post-commit 点发布的、带来源和 correlation 的不可变生命周期事实；Hub 只能分发和投影，不能推断、重排、改写或把它升级为 Product fact。
_Avoid_: Hub 推断事件、Trace truth、可变上下文

**Perception**: Capability Plane 中从有界外部输入获取 raw artifact、或从
raw artifact 派生 `ObservationProposal` 的 typed capability；跨 owner 的观察
输出只经 P2 出面，不拥有 Evidence admission、WorldBelief、identity、control、
assurance 或 binding authority。
_Avoid_: Product T1/T2 Owner、canonical authority、直连 World Model

**Fast / Slow Perception**: Perception Capability Plane 中的两种能力层级；Fast
与 Slow 都是可被 Perception pipeline 集成和编排的 capability，具体模型、provider
或算法只是 realization。两者都必须产生兼容的 observation / association evidence，
并经同一 P2/P3 链汇入 UIWorld Authority。
_Avoid_: realization category、顶层 Owner、固定算法、独立 authority、绕过 P2 直连 WorldModel

**Semantic Perception**: 面向“界面或元素表达了什么”的外部感知协议；接收同一 capture/cycle 下的有界元素依据、文本/视觉依据和 claim 上下文，产生带 provenance、lineage 与 `PerceptionAssessment` 的 `SemanticObservationProposal`。它不拥有 Evidence、WorldModel、Assurance 或 Effect authority。
_Avoid_: Slow Text 专属接口、provider response、直接语义真相

**Semantic Evaluation Intent**: 调用方声明的预期页面及目的，表达“希望看到什么”，可以辅助语义解释但不是观察事实。与实际前页观察、已记录操作及当前观察分别关联；符合性判断与观察结论独立，不能进入世界事实或Runtime Transition Context。
_Avoid_: 预期等于到达、意图等于成功回执、把预期塞入观察字段

**Semantic Page Claim**: 从当前有界观察得到的页面类别、用途与可选已观察标题，保留capture/cycle/session和证据依据。类别不是实体身份；World可将已接受且关联到当前观察的含义补入Slice，但不能仅因类别相同合并容器或建立导航边。
_Avoid_: 页面类别等于ContainerId、语义相同等于同一遍历节点

**UI Element Perception**: 面向“capture 中有哪些元素、属性、几何和关联”的外部感知协议；XML/Hierarchy、YOLO/Visual、OCR 都是可声明的来源。输出的 occurrence 只在 capture 内有效，不能直接成为跨 revision identity 或 binding。
_Avoid_: XML 等于 Product truth、坐标等于 identity、数组拼接

**PerceptionAssessment**: Semantic Perception 与 UI Element Perception 共用的非权威结构化评估，表达 disposition、coverage、uncertainty、association quality、source path 和可选 provider confidence。provider confidence 只作校准/诊断输入，不能直接成为 Evidence admission、Assurance 或 Effect Gate 权威。
_Avoid_: bare confidence float、全局 source ranking、把缺失置信度当零

**Text Semantic Perception**: Fast（YOLO + OCR）与 Slow Text 构成的组合感知能力：对外至少实现 Semantic Perception，并可在 Fast 投影具备元素观察输出时同时实现 UI Element Perception；Slow Text 依赖同一 capture/cycle 的定位与文本依据完成语义解释，不能作为独立 Product capability 注册。Slow Visual 可以独立并按能力实现一个或两个协议接口。
_Avoid_: standalone Slow Text、把文本模型当作独立图像观察源、把 Fast 来源数组直接拼接

**Language Inspection**: Runtime Integration 侧的只读诊断能力；对一个 capture
的界面文字做格式检查并产出非权威 Finding。它在 canonical observation
commit 之后消费有界文本投影，不阻塞主 Runtime，也不改变 Assurance、Effect
或其他 Product authority。它按任务需求 task-scoped 加载：任务没有声明语言
检查时不建立 binding、不运行检查，也不生成语言检查产物。
能力先作为可选择项进入 Runtime Integration 能力集，由 `uni-agent` 根据任务
要求选择；Host/runner 不得看到配置后自行替 agent 打开能力。
任务配置只声明检查要求和参数，不提供 Host 侧 `enabled` 开关；必选要求没有
形成 agent 选择的 binding 时，任务必须安全停下。可选要求未被选择则不运行、
不生成语言检查产物。能力可以由任务直接写死，也可以由 agent 解析选择；任务
已经写死时优先；未写死时由 agent 在下发任务的统一
`AgentTaskEnvelope.task.initialization.capabilitySelection` 中注入，Host 校验后
形成 task-scoped binding。`task.payload` 可以承载计划或指令；runner 只消费已
形成的 binding，不自行按配置或 registry 重新选实例。
_Avoid_: 语言检查结果等于 Product truth、语言检查参与动作授权、读取设备
locale 代替任务声明、全局默认加载

**Capability Selection**: `uni-agent` 从当前信任域已注册、可用的能力集中，
依据任务要求选择本次要绑定的能力实例；选择结果才进入 task-scoped binding。
能力注册、能力可用性和 agent 选择是三件事，不能把“已注册”当作“本次已启用”。
_Avoid_: Host 隐式代选、配置文件存在就自动启用、全局单例能力

**Declared Text / Rendered Text**: 两种不同的文字依据。Declared Text 是
层级/XML/OCR 等来源直接声明的字段值；Rendered Text 只在有实际屏幕渲染或
明确视觉/OCR 投影依据时使用。当前 Settings 接线把 `text`、`content-desc`
和 `hint` 各作为独立的 Declared Text 项，不能把层级字段伪装成 Rendered
Text。
_Avoid_: XML text 等于像素渲染、把同一字段同时当成两种依据

**Language Ignore Route**: 任务配置声明的精确页面 route；命中后跳过该
capture 的语言检查，并在运行产物中披露跳过及其影响。默认列表为空，不能
用模糊文本关键词代替 route 身份。
_Avoid_: 全局静默忽略、关键词误匹配、把跳过当作 Pass

**Unknown Language Finding**: 某个 capture 因没有可用文字、输入不完整或
capture 关联不一致而无法判断时产生的 Finding。每个 capture 都保留一条
Unknown，不能为了让报告看起来干净而静默丢弃；Unknown 仍是非权威诊断结果。
_Avoid_: Unknown 折叠成 Pass、缺输入等于 Violation、只写日志不留产物

**Evidence Record**: Evidence Ledger admission 通过后形成的不可变
canonical 观察依据记录（EvidenceId + claim + kind + observation
context + origin + provenance）。
_Avoid_: raw artifact、producer claim、数据、observation

**Perception Acquisition**: Capability Plane 观察侧的采集能力——从设备或
环境获取新鲜外部输入并产出 raw artifact，供 Perception 解释；只采集不解释
像素。属于可替换的 provider capability，非 Owner、非 Authority；其输出与
其他 producer 一样经 P2 ingress 出面。采集侧的传输形态与宿主位置属
realization 细节，不是域概念。
_Avoid_: 直连 World Model、解释像素、第二套 observation 语义

**Observation**: 对世界状态的观察声明（ingress 输入候选，admission
之前，不是 canonical 记录）；semantic kind。来源与触发时机不构成 kind
差异；producer 身份属于 provenance，观察属于外部观察还是动作后的 effect
flow 观察由独立 origin 语义表达。
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

**Observation Origin**: Observation 的来源语义，封闭二值：external
observation 与 post-action effect-flow observation；它与 ProducerIdentity、
Observation Context 分开。MaterialEffect fulfillment 必须能读取 origin。
判定权（2026-10-12 grill 裁决）：由**显式 attempt 关联的机械推导**持有——
存在精确 attempt correlation 才判 post-action，否则判 external；**禁止以
时间邻近作推导依据**（时间相邻 ≠ 因果）；producer 自报仅作诊断，不参与
判定。已落地（PROTOCOL-FIX-001）：`ObservationOrigin` 封闭二值，账本在
admission 从 Context 单点推导 canonical 值；producer 自报核验仍 Deferred ⑦。
_Avoid_: producer name、context alias、truth judgment、时间启发式

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

**Core**: Product T3 的跨域 typed immutable record/projection surface；它承载
跨领域共享的语义形状和 owner 引用，不拥有 Product canonical state、Runtime
authority、独立生命周期或新的 Product T2。需要运行时闭环的领域仍由自己的
Runtime/Kernel owner 持有；不需要运行时机制的 realization 可以经明确缝直接投影
到 Core。Core 的 record/projection 可替换，但不能反向生成 UI identity 或覆盖
Evidence、WorldBelief、Run State、Assurance、Binding、Outcome 的 authority。
_Avoid_: Product T1 组件、Product T2 Owner、共享 mutable hub、第二 Runtime、Core truth

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

**ContainerGraph**: baseline「World Graph」的 L4（UIWorld 域命名空间，非
Product 层号）内部 realization：
revision-bound、evidence-backed 的世界实体间关系 belief（Contains /
Overlays 是 graph relation，即使生命周期很短）；与 WorldState（单实体
intrinsic / contextual state claims）按 semantic kind 分界，不按变化频率
分界；graph relation ≠ timeless structural truth；不成为跨组件公共协议。
_Avoid_: timeless structure、DOM tree、跨组件协议对象

**Traversal Graph（遍历图）**: 由运行时从World有依据结构中按任务范围摘取引用、组织形成，供Traversal消费的可重建结构视图，覆盖累计已观察的页面、元素及关系；身份与依据沿用World，当前有效性随World更新，历史定位不成为当前可执行目标。它不包含任务处理进度、不反写World，也不构成独立世界真值；调用方不必另行提交图，即时查询或缓存不改变这一语义关系。
_Avoid_: 任务进度图、执行历史、第二套World、Effect授权

**Traversal（遍历执行）**: Control Loop中依据计划与当前事实推进结构探索、覆盖和控制权返回的职责，由Kernel内部driver协调既有Owner执行链；对外接受一次执行，运行中提供只读状态查询，本次执行结束后返回结果。需新决策时暂停并保留进度，经明确继续/修订和事实重检后恢复，具体恢复意图仍属Control Loop的Recovery职责；不拥有Goal、Belief、canonical Run进度、Completion或Effect投递权威，查询本身不推进执行。
_Avoid_: 每查询一次推进一步、调用方反复取NextStep、第二个Run驱动、Traversal结果即目标完成

**Traversal Status（遍历执行状态）**: 由同源Run/执行记录派生的只读概览，表达当前状态、执行位置、完成/剩余工作、暂停原因及待决定事项，详细依据通过引用读取。它不是新的状态权威，查询不触发执行、恢复或咨询。
_Avoid_: 整图/全量日志转储、查询即推进、状态查询即控制命令

**Traversal Result（遍历执行结果）**: 本次Traversal执行结束后交付的报告，说明结束方式和原因、完成/未完成工作、跳过/未知及证据引用。它不是单步回执，也不能单独作为Goal完成证明。
_Avoid_: 每步处理结果、暂停即最终结果、任务完成授权

**Viewport Transition（视口观察关联）**: 同一容器的前后观察由已验证的实际操作关联，可携带有观察依据的空间位移；位移未知时保留原因。它不代表新容器、对象连续身份或可执行定位。
_Avoid_: 跨页导航边、操作意图、手势距离真值、当前完整长页

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

**ObservationNeed**: World Model 派生的信息缺失描述，表达当前观察不足以
支持哪些世界判断，例如页面身份判别或元素归属。它不指定感知能力；
Observation Control 据消费需求安排补证，World 根据接纳后的证据重新判断。
描述缺失信息不等于建立身份、授权动作或宣告失败；当前观察的身份补证由World表达缺口，Control有界调度，World采信后仍须Assurance验证到达，Traversal才继续。
_Avoid_: CallVLM/RunOCR 类能力指令、provider命令、identity真值、priority policy

**World Knowledge Gap（世界知识缺口）**: World Model 从具体观察或消费需求中识别、具有对象、范围与依据的未决世界知识，当前包括身份判别、元素归属、关系依据及观察覆盖四类；未建模能力或未探索区域本身不自动构成条目。缺口描述不等于身份、许可或任务失败，是否影响某次消费由该消费的既有Owner判断。
_Avoid_: 所有未知的完整台账、能力缺失清单、自动失败或授权结论

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

**Execution Contract**: UniAgent 根据 Task 与 Product Session context 交给
Uni Kernel 的稳定执行语义边界，描述 objective / scope / effect constraints /
proof criteria / 可选 run-level obligations。它把用户任务细化成 Kernel 可以接收、
执行和验证的约束。当前 Run 允许受白名单约束的 ContractRevision：只可修订预算、
适应策略和咨询策略；objective、scope、effect permissions、proof criteria 不可
在当前 Run 内修改。每次允许的修订生成新的 immutable ContractRevision 和
ContractViewRef，由 Run Model 接受，旧 revision 保留为历史依据。
_Avoid_: task list、plan、SLA

**Execution Contract View**: Run Model 在 Execution Contract 或受限
ContractRevision 被接受时建立的 immutable canonical view；Kernel 以 current
ContractViewRef 作为当前 Run 的契约输入，旧 view 不被就地改写。同一
acceptedContractViewRef + contractGeneration 的 P24 activation key 只能关联一个
Primary Run。`/runs` 必须显式携带 taskInstanceId、productSessionId、
acceptedContractViewRef、contractGeneration；contractRef 从已接受 View/binding 解析，
launchId/idempotencyKey 只作查询和传输幂等键。
_Avoid_: contract copy、session config

**ContractRevision**: 当前 Primary Run 内对 Execution Contract 的受限修订记录；只
允许预算、适应策略和咨询策略字段，必须有 revision、审计记录并由 Run Model 接受。
它生成新的 ContractViewRef，不重新执行 P24 activation；P25 使用最新 current view。
_Avoid_: Agent 覆盖 Contract、静默改 objective、scope 或 effect permissions

**Budget Grant（预算续授）**: 决策边界上的步数/咨询轮次额度续授，由Runtime按资源状态触发明确复核，UniAgent结合目标和实际进展判断与提案，规则侧校验并记录授拒。单笔有限、累计消耗保留，不修改目标或权限，不是ContractRevision或新的canonical Run State转移；累计边界依所接受的资源策略定义（ADR-0050及其部分替代ADR-0052）。
_Avoid_: contract revision、无复核自动续额、policy自续、Run State膨胀

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

**Execution Overview（执行概览，overview）**: 关联同一Run目标、已知结构、累计及当前授额区间进展、剩余工作和阻碍的同源只读事实视图，可并列呈现UniAgent基于这些事实作出的方向判断，判断不覆盖事实。剩余工作可以表示为已知分支、范围或待查明问题，细度随证据逐步展开，不要求预先穷举详细任务。当前可行动事项由事实Owner以稳定键、来源引用及相关稳定成果键投影；Agent用NextItemKey选择，Runtime不从方向文字推断事项身份。
_Avoid_: 第二份Run State、模型自评即事实、完整任务清单、完成或续额裁决

**Control Intent**: Control Loop 唯一签发的控制产出（observe / act /
recovery）。act-intent 只携带 target hint 与 basis revision，不是
binding，也不是 authorization。act-intent 的 target hint 可为语义
descriptor 序列化（如 "role:descriptor"）——hint ≠ binding ≠ UI
targeting；CONTEXT Avoid 的描述字符串约束作用于 binding 通道（UI
通道恒绑 OccurrenceRef，UIW-004）。
当前 issuance ownership 只在进程内验证；跨 Host 的 authority token 等真实买方
出现后再单独定义。
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

**Expected Effect（预期效果）**: 验证动作效果的对照物——authoring/意图侧
的期望规格，分两族：状态型期望（目标应处于何态）与变化型期望（应发生
何种改变，如内容变化而路由不变）。是 PostActionEffectVerification 的
判定输入；不是 canonical effect 语义、不是 Effect Receipt，也不同于
Desired-State（后者是 pre-dispatch 跳过判定，本词是 post-action 验证
对照）。（2026-10-12 grill 裁决；实现 known gap——现以特设布尔代偿）
_Avoid_: effect 语义本身、receipt、desired-state（混用）、硬编码验证开关

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
只保存 obligation definition；满足判定由 Assurance 计算并输出状态投影。
action-local requirements（target
freshness / one-step precondition / admissibility / grounding validity）
属 Assurance 短生命周期 judgment，永不进入 Run State。
_Avoid_: task、checklist、acceptance criteria

**Obligation Definition View**: P7 中由 Run Model 提供的“要证明什么”视图，
包含 obligation identity、kind、subject、required value 和 mandatory 等定义；
不包含 Assurance 的当前结论。
_Avoid_: pass/fail、proof result、assurance judgment

**Obligation Status Projection**: Assurance 根据 Evidence、WorldBelief 和
相关视图计算的“现在满足了吗”投影；不反向成为 P7 的第二个 canonical authority，
也不改写 obligation definition。
_Avoid_: requirement definition、Run Model authority、terminal Outcome Proof

**Obligation Fulfillment**: 单条 Proof Obligation 的 evidence-backed 满足
状态（Assurance 判定产出）：current WorldBelief 内存在
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
P24 先按 Activation Key 预留记录，再绑定真实 RunId；成功 finalize，失败保留
failed/terminal record，重试只回放原记录。reserved 中断后只能按原 key 查询/恢复；
无法证明 owner 时转 BLOCKED/failed，禁止重新 mint，恢复必须显式 resume。
_Avoid_: contract admission（同义化）、run creation、start command（泛义）

**Activation Key**: 一份具体已接受执行契约版本的启动 identity，由
`acceptedContractViewRef + contractGeneration` 共同标识；同一 key 至多关联一个
Product Session / Primary Run，重复请求返回已有关联且不重放动作。
`launchId` 和 `idempotencyKey` 只作查询/传输幂等键，不替代该组合键。
_Avoid_: 单独的版本数字、launchRequestId、runId（作为启动去重来源）

**Cancel Command**: 从 Host 接口进入、经鉴权和传输适配后由 Kernel/Runtime 铸造的
Product 生命周期命令；它必须携带并校验 `productSessionId + runId`。Cancel 立即
禁止新 Effect；在途 Attempt 对账成功后形成 SafeStop，无法对账则进入 BLOCKED。
Cancel 不修改 Contract，Host 不直接写 Run State。
_Avoid_: Host 直接改 Run、Agent 自行取消、取消后继续 dispatch

### UniAgent & Goal Evaluation（GEV-004 定稿）

**UniAgent**: Product T1 中、Uni Kernel 之外的监督主体；
拥有 Primary Goal 与 Goal Evaluation，不拥有 WorldBelief、Run State、
Assurance Judgment、target binding 或 effect delivery。
执行期间，UniAgent 结合目标预期与实际事实判断是否朝目标推进；该方向判断
不替代 Run Progress 的事实记录或 Assurance Judgment。
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

**UniAgent Profile**: 产品拥有的 host-neutral UniAgent realization 装配描述
（身份、能力声明、职责边界、整体规划姿态、Skill 使用约束、冻结能力词汇、
模型角色必需性、产品工件引用与修订号）；它不含 provider/model 名、服务端点
或任何运行态，也不授予新的 Effect authority。宿主 realization 只能消费它、
不能定义它。这里的职责是产品责任与预期输出边界，不是开发流程控制权。
_Avoid_: DSH profile、deployment profile、model selection config

**Product Agent Skill**: UniAgent 可消费的、有边界的 SOP；告诉 Agent 何时进入
某个方向、应采用什么思路和经过什么处理流程，可声明输入、输出形态、验证方式
以及失败/升级边界，但不必规定每个具体动作。具体做法由 Agent 结合任务与当前
Evidence 决定。它不拥有 Product Goal、Run State、Effect authority，也不创建
第二套开发流程；开发 Harness 的 vendored skill 与它是不同语义面。
_Avoid_: workflow、lifecycle controller、tool implementation、persona prompt

Product Skill 的公开命名采用 `name`、`displayName`、`description` 三层：
`name` 是与目录一致的稳定可读 slug（小写字母、数字、短横线），用于 Profile
绑定；`displayName` 面向人和 UI；`description` 面向发现与触发判断。Profile
通过 `allowedSkillRefs` 引用 `name`、revision、path 与 sha256；当前不引入独立
的 opaque `skillId`。跨来源注册表或远程 Skill 出现时，再增加 URI/namespace
身份，不改变本地 Skill 的可读名称。

当前 Product Skill 包采用 `manifest.yaml + guidance.md + references/`；
`.agents/skills` 仍是开发 Harness 的独立语义面。一个任务第一阶段最多选择一个
`primary planning skill`，辅助 Skill 只能提供参考，不能改变规划或权限边界；
候选冲突时进入澄清、降级或 defer。Skill 通过 `memoryPolicy` 声明 Memory
Recall 的触发条件，但 Recall 必须显式发起，且返回内容只能作为 prior、hypothesis
或 historical reference。Skill 可以声明 direction/execution/precise 三档规划
切换条件与 bounded exploration 要求，但执行约束仍由现有 AgentDecision Kernel
和 AgentPlanProposal 负责，不新增第二套计划类型或权限模型。

Product Skill 的最小接入缝是现有 Product Prompt/DSH consultation assembly：
Skill manifest 与 guidance 由 Profile allowlist 管理，在 Session/Run 初始化时
解析、校验并固定，作为 scoped static prompt section 参与模型上下文；每轮仍只
追加 Kernel 从 immutable `ExecutionContractView` 投影出的 `AgentDecisionContext`
和可选 Memory Recall。Skill 不进入 `ExecutionContractView`、`AgentDecision` union
或当前封闭的 `AgentTaskInitialization`；后者继续只承载一次 Runtime Integration
capability selection。初始化审计 envelope 记录 Skill 的 name、revision、sha256，
运行中不热切换。

首批 Product Skill 采用领域级粒度，聚焦车机 Android 自动化测试（Android
automotive UI automation testing），不按单一设置页面、车型、控件或测试用例
拆分 Skill。具体任务、设备/车型、当前界面、测试目标和策略差异由持久化 Task
引用的 Task Profile、DeviceProfile、Runtime Context、当前 Evidence 及后续
references/strategy 承载；
只有当领域边界、规划方式或权限/验证要求确实不同，才新增独立 Skill。
首个候选 Skill 名称为 `android-automotive-ui-testing`，displayName 为“车机
Android UI 自动化测试”。其范围包括 typed UI observation、grounding、bounded
exploration/planning、受控 Effect、post-action verification、evidence 与安全
停止/重规划；不包含具体页面/控件/车型/OEM、固定 AVD、任务预算、provider/model/
endpoint 或 Effect authority。`automotive` 是适用领域标签，不等于当前已有真实
AAOS/OEM 生产证据；Settings 只作为当前 buyer 的首个任务覆盖面。
Product Skill 不拥有、不创建也不持久化 Task；持久化 Task/Task Profile 通过
`TaskRef` 由 Host/Task Catalog 管理，Task Launch Seam 负责引用它并建立
TaskInstance、Run、Product Session 与 Host Session 的关系。Task 可以引用领域
Skill，但 Skill 不反向持有 Task 清单；`product/tasks/` 中的现有文件继续按各自
专用 loader 的静态配置或临时声明来源理解，不作为通用 Task Catalog。
当前 `TaskRef` 是 Launch 的逻辑引用；现有 Runtime 持久化的是 Run/事件投影，
DSH Task Workbench 另有 TaskDefinition/TaskInstance JSON repository。仓库已有
任务 profile 与 ExecutionContract 的接线，但尚没有统一的 Host-owned Task
Catalog schema/resolver。因此首版只落 Product Skill manifest 与 Profile allowlist，
不假设已经存在通用 Task→Skill 解析；待 Catalog buyer 和接口确定后，再把 Skill
引用写入持久化 Task 或其 profile，并在 launch-resolved snapshot 中冻结。
当前已有的 `dsh/uniclaw-task-workbench` 提供 TaskRepository JSON 持久化和
Task definition/instance HTTP 路由，可视为现阶段 Host/DSH adapter 对该 seam 的
具体实现；它不改变 `product/tasks/` 不是 Task database 的边界，也不让 DSH 取得
Product Runtime、Run 或 Effect authority。
TaskDefinition 与 Task Profile 仍是两个层次：当前 TaskRepository 的
TaskDefinition 只有任务身份/标题/需求/项目/状态等持久字段，尚未承载
profileRef、profileRevision 或完整能力/预算/终止契约。目标设计应把 Task Profile
作为持久化 Task/Catalog 的任务级契约或版本引用，不能把 `product/tasks/*.yaml`
直接当成它的 canonical store。

**Adaptive Planning**: Product Agent 根据目标清晰度、当前 Evidence、边界确定性
和动作可验证性，在方向计划、执行计划与精确指令之间切换的规划姿态。首次执行
允许先进入 bounded exploration：每一步都必须有探索目的、停止条件、观察结果和
计划更新依据，探索不能绕过 Capability/Effect authority 或把暂时假设升级为事实。
_Avoid_: always-precise instruction、unbounded exploration、exploration as authority

**Product Execution Plan（产品执行计划）**: UniAgent提出并修订的advisory执行意图，保留阶段预期，并以具体步骤或已声明语义范围内可判定的有界规则表达当前可执行内容，供Runtime依据当前事实逐步展开并记录实际采纳与执行情况。它不改变目标、权限或事实权威；计划内规则无法覆盖所需判断时返回Agent，执行与验证仍由既有Owner负责，计划修订不改写已发生事实。
_Avoid_: 开发Leader Plan、完整预知的点击脚本、Effect授权、第二套执行器

**PlanTree（计划树）**: Product Execution Plan的结构化表达，以任务/子任务和已声明语义的处理规则组织目标、范围、安全限制及结束条件，供Control Loop中的Traversal消费，由Kernel协调执行；不要求预先列出运行中才会发现的全部页面或动作。它表示执行意图，不表示当前世界事实、任务进度或Effect许可。
_Avoid_: UI页面树、World图副本、预展开的完整点击脚本、开发Leader Plan

**Plan Handler（计划处理步骤）**: 绑定于计划中具体操作、引用任务已提供并有效绑定的Capability及其声明操作和参数的处理步骤；不同操作可绑定不同列表，分别声明操作前或完成并验证后的时机，各时机内有序。独立只读处理也可绑定，不要求产生UI交互；目标与观察须明确关联，原目标消失不能自动替换。它不是独立注册的能力组件，不因引用能力而获得事实、裁决或Effect权威。 调用完成、领域通过、继续条件与Goal满足相互独立；能力按自己的typed协议给出领域结果，计划仅按显式继续条件协调。运行中的状态查询只读，最终遍历报告保留发现不足、排除、深度截断及未完成事项。

_Avoid_: 独立Handler注册表、注册顺序即任务顺序、任意方法调用、绕过Kernel直达能力

检查范围、完成及通过的领域含义由该handler对应的能力协议定义；Kernel负责协调调度，经原Owner缝记录执行状态与返回结果，不重判领域结论。检查不符合与调用执行异常分开：前者可按声明策略记录并继续，只有明确必需的继续条件不满足才暂停；具体恢复意图归Control Loop的Recovery职责，handler不直接改写canonical状态或成为控制/安全裁决者。

**Handler Call（处理步骤的一次调用）**: 一个具体操作在声明时机对已绑定能力发起的一次处理尝试，有独立关联、运行状态和单一终态，由Run记录。异步等待保留执行位置，查询不推进；超时或取消后，迟到结果不改写终态。取消请求不等于底层已物理停止，领域结论仍归Capability，继续与恢复仍归原职责。
_Avoid_: 处理步骤已声明即已完成、取消请求即物理停止、后台能力自行续行

**DSH Realization Binding**: UniAgent Profile 在 DSH realization 上的映射
文件（逻辑角色到 provider/model 的偏好序、服务端点），由 DSH adapter
拥有；更换 provider 或端点不改变 UniAgent Profile。
_Avoid_: product profile、UniAgent Profile、model-routing（开发 Harness 的）

**Task Profile**: 单次任务的执行契约声明，包含能力要求、参数、预算、终止条件、
证明/证据和策略引用（ADR-0040）；它只引用、不复制 UniAgent Profile 或策略工件
的内容。当前仓库已有 `product/tasks/*.yaml` 形式的产品任务 profile，Host loader
会把其中的 bounds/objective/termination/evidence/policy 编译进 Kernel 的
`ExecutionContract`，因此不能再设计第二套 Task Contract。它与持久化
`TaskDefinition` 是不同层次：前者是任务执行契约，后者是任务身份/需求/状态及其
实例关联。Task Profile 是否最终内嵌或引用于 Host/Task Catalog 的持久化 Task，
待 Catalog seam 的 buyer 决定；`TaskRef` 进入 Task Launch，`TaskInstanceId` 是
执行实例关联，不是 Task 定义本身。
_Avoid_: agent profile、realization binding、capability facts、product/tasks as task database

**Host Session**: Host Runtime 提供的 thread/session 承载体；它可保存
realization-private state 并显式关联 Product Session，但不是 Product Session，
也不取得任何 Product Authority。
_Avoid_: Product Session、Primary Run、canonical session

**Host Session Identity Mapping**: 产品/Harness 跨边界只使用
`productSessionId`、`taskInstanceId`、`runId` 和 `hostSessionRef` 等中性字段。
`dshSessionId` 是 DSH adapter 内部 alias；映射必须记录 `source`、`authority` 和
`valueOrigin`，不能把 DSH alias 外泄为 Product identity 或 Runtime correlation。
_Avoid_: dshSessionId 作为公共 identity、sessionId 猜测、Host alias 取得 Product authority

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
WorldBelief / Outcome Proof。它是只读交付；交付失败保留 EvaluationId 和待交付
状态，后续重试必须幂等，最终可形成 NeedsFollowUp。
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

**Consultation**: 只发生在 Primary Run 已经启动后的语义 decision boundary
（T1-T6 触发点），由 Kernel 经外部 seam 向 UniAgent 发起同步请求-回答：输入 =
有界 AgentDecisionContext，输出 = 恰一个 AgentDecision（或 null = no-response
fail-closed）。P1 只负责提交/审核 Execution Contract，P24 只负责启动已接受的
Contract View，P25 只负责运行中的边界咨询。同一 decision boundary 可以重复咨询，
但每次必须有独立 `consultationCorrelationId`，资源消耗受初始额度、逐笔明确复核及已声明的资源授权边界
限制；P25 不创建、修改或替换 Contract/Contract View。咨询时机、boundary 记录和
生命周期推进由 Kernel 独占；Agent 只回答，永不发起。Host realization 内部的
turn/step 结构是回答的产生方式，不是第二个生命周期 Owner。P25 journal key 为
`runId + contractViewRef + decisionBoundaryId + consultationCorrelationId`；同
correlation 的 transport retry 返回原结果且不重复消耗咨询额度。timeout/no-response
计为一次 semantic consultation，持久化 fail-closed disposition；新 correlation
才重新计数。
_Avoid_: agent invocation（泛义）、chat round、turn（跨 realization 混用）、
Agent 发起的询问

`AgentDecisionContext.availableCapabilities` 只在一次 Run 的第一次 Consultation
请求中由 Host 注入当前可用 Runtime Integration profile map；Agent 从 map key
选择任务能力，后续请求沿用 task-scoped binding，不重复发送或重新选择。

**Agent Strategy State**: UniAgent 侧允许持有的认知状态——Plan
Hypothesis、被弃策略及原因、policy 耗尽史、语义假设、decision
rationale；只回答「我准备怎么做、以前为何失败」，永不充当「现在是什么」。
当前现实断言的唯一来源是最新 AgentDecisionContext；策略记忆影响推理质量，
不产生 World/Run/effect/proof/binding 真值（baseline §24.5 Agent
Continuation 的 realization 载体；与 Memory Recall 同受 non-evidentiary
纪律）。
_Avoid_: shadow state、第二 World Model、cached world summary（作权威义）、
execution log

### Task Workbench（PNL-001 立项 2026-09-30）

**UniClaw Task**: 任务定义——一项可重复发起的工作的规范：需求文本、
项目归类、状态；不承载执行过程。同一 Task 可被实例化多次。
_Avoid_: WorkItem（委派契约）、ticket、Product Session、plan

**Product Session**: 一次产品执行的 correlation root；由 UniClaw Runtime 根据
Task 创建并分配自己的 `productSessionId`，发生在 Primary Run 之前。它承载 UniAgent
根据 Task 与 Session context 形成 Contract 的过程，之后再关联 Primary Run、Outcome
与 Goal Evaluation。Task Launch 只提交 Task 引用并读取 Runtime binding；Host 只铸造
`hostSessionRef`；UniAgent 只消费和监督。
它与 Task Instance、Primary Run、Host Session 是四个不同概念；当前范围可以是一对一，
但必须由显式 Binding 关联，不能从目录、transcript 或 Host session 推导。
_Avoid_: Task Instance、Primary Run、Host Session、shared mutable context

**Task Instance**: UniClaw Runtime 根据 Task 定义创建的一次执行实例，拥有自己的
`taskInstanceId`，并在创建时与一个 Product Session 建立显式关联。当前范围可以一对一，
但两者不能共享语义 identity。对话、trace、诊断过程属于实例投影，不属于 Task 定义。
_Avoid_: productSessionId 同义化、task run（与 Primary Run 混淆）、subagent

**Task Discovery Authority**: 只有 Product Runtime 或 Task Repository 对
TaskInstance/ProductSession identity 做确认；仅有 Host session、transcript 或
扫描到的运行产物时，Workspace 只能展示未关联观察，不能自动升级为任务实例。
_Avoid_: observed task、session-as-task、host-discovered task

**UniClaw Workspace**: 面向 Task Project 和 Task Instance 的只读观察面；它组合
Product Session 的对话、Trace、Evidence 和 Metadata 投影，允许按来源查看详情，
但不拥有停止、重试、批准、任务编辑或任何 Product Runtime Authority。Workspace
可以把一个 Product Session 映射到一个 primary Host Session；Host Session 的
生命周期和 transcript 仍属于 Host realization。
_Avoid_: control plane、Product Runtime、DSH workspace（Host 承载称呼）、identity authority

**Workspace Query Core**: Workspace 的 Host-neutral 查询、关联、规范化和读模型
投影核心；它只消费 `projectRef`、`testSetRef`、`taskRef`、`fixtureRef` 等逻辑引用，
不直接依赖目录、文件或 Host API。它不拥有来源数据、权限真相或 Product Runtime
Authority；独立 Web、DSH 和其他 Host 通过各自 Adapter 提供来源、权限、传输和挂载能力。
_Avoid_: DSH service、万能数据服务、Product Runtime query owner、直接读文件

**Workspace Snapshot**: 一次 Workspace 查询涉及的各来源结果、版本或读取时间的
可识别组合；来源不支持历史读取时只能表达读取时间和一致性范围，不能宣称跨来源原子
一致性。新结果不能覆盖更新快照中的旧结果。
_Avoid_: canonical world snapshot、global transaction、source truth

**Host-owned Retention**: 来源 Host 或 Source 自己负责缓存、归档、删除、保留和权限
撤销；Workspace 不复制并长期保留来源事实。Workspace 只能通过显式 capability 读取或
管理这些能力，不能直接访问 Host 存储。
_Avoid_: Workspace archive、UI-owned retention、second source of truth

**Auxiliary Execution**: Product Session 下为完成、解释或诊断主任务而发生的辅助能力
执行；可以由 DSH 子 session、外部工具、文件解析器、Trace 查询器或其他 capability
提供。它通过 `AuxiliaryExecutionRef` 和 correlation 关系归属于 Product Session，
不创建新的 Task Instance/Product Session，不取得 Product Authority，也不自动成为
主时间线节点。
_Avoid_: second task、second Product Session、DSH-only session、canonical run

**Task Project**: 任务的归类维度——"一堆任务的分组"；其 canonical identity 和生命周期由 Host/Task Catalog 管理，在 DSH realization 上可落为一个 workspace。产品语义层称 Project，不称 workspace；Workspace 只读取并投影 Project，不拥有其长期存储。
_Avoid_: workspace（产品语义层）、repo、directory

**Test Set**: 具有 identity 和 revision 的任务定义与 fixture 集合；Task Instance 必须能够指向实际采用的 Test Set revision。Host/Task Catalog 是长期 owner；Host 尚未提供持久化能力时，仓库专用目录可以作为临时声明式来源，但不等于 Worktree、Workspace 存储或 Product Runtime truth。
_Avoid_: taskSet 字符串、项目别名、worktree、运行时缓存

**Repository Test Catalog**: 在 Host Project/Test Set 持久化能力落地前，代码库内用于提供 Project、Test Set、Task definition 与 fixture 关系的临时声明式资产来源；当前统一使用 `default` 版本，并可附带由目录内容产生的 `sourceRevision`。它由发起任务接口引用，供查询接口读取，不取得 Product Session、Primary Run 或执行结果的 authority；目录结构是实现细节，不是公共契约。
_Avoid_: Workspace database、Host session store、second source of truth

**Catalog Revision**: 测试集内容、任务定义、fixture 关系或验收条件的一组可定位版本；它跟随测试目录内容变化，不跟随前端样式、查询实现或整个应用版本变化。当前本地来源使用用户可读的 `default`，历史可重现性由 source revision 或 Host 版本补充。
_Avoid_: app version、UI version、directory mtime、runId

**Run-Session Binding**: UniClaw Runtime 在已有 Product Session 上创建 canonical
`runId`，并将一次 Primary Run 与该 Session 及其 primary Host Session 建立显式关联；
Host Session 不是 Run identity，目录名、artifact 文件名、transcript 或 Host session
都不能推导或替代 `runId`。Task Instance 与 Product Session 的关联由 Runtime 创建时
记录。
_Avoid_: session-as-run、directory-as-run、traceId-as-run

**Task Launch Seam**: 发起任务的显式接口；它先接收带元信息的 launch request，再把
Task/Test Set 资产引用交给 Runtime。Runtime 负责创建 Task Instance、Product Session
并返回 binding，UniAgent 随后根据 Task/Session context 形成更细的 Contract，Kernel
再创建 Primary Run，Host 创建 Host Session。Launch 不自行铸造 Product identity；
创建请求不得传入或覆盖 taskInstanceId/productSessionId；只有恢复/查询已有 binding
时，Runtime 才能按启动钥匙查回它们。当前 Workbench 仍存在本地 instanceId 与
Runtime taskInstanceId 两套编号，属于待实现的 known deviation；旧
`/tasks/instantiate` 的 `psi-*` 继续作为兼容路径单独理解。每个阶段都留下带
来源和权威的可查询记录，查询接口只读取这些关系。
_Avoid_: 页面直接创建目录、查询接口隐式创建运行、文件名驱动关联、一次性伪造完整元信息

**Launch Metadata**: 任务发起和创建过程中的分阶段元信息集合；每个字段都要标明来源类别：`configured`（测试集、manifest、Host 或 launch config 提供）、`generated`（authority 在创建过程中生成的 ID、时间和绑定）、`observed`（设备、Runtime、Host 实际观察）、`derived`（由已有关联记录计算出的展示值）。这些类别不能互相冒充，且都要带 source、authority、availability；需要时间语义时再带 observedAt。
_Avoid_: 一次性 metadata blob、配置冒充实测、派生值冒充原始事实、空值即完成、模型输出元信息

**缺省标注（Deferred Marker）**: Workspace 表面对"功能属于已规划的后续切片、当前未实现"的显式不可用占位（可见入口 + 归属切片说明 + 变更记录引用）；它与 World 认识的"未知"、链路正常的"无数据"、"加载失败"是四个互不冒充的界面状态，不静默隐藏入口。（WORLD-VISUALIZATION-001 QV7，2026-10-23）
_Avoid_: 未实现伪装成未知或空态、静默隐藏入口、以面板存在声称能力已交付

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
