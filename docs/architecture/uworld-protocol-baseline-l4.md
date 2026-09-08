# UWM-009 — UIWorld Protocol Baseline L4

## 0. Status

```text
Status: FROZEN v0.2 — PROTOCOL READY FOR CODING
Scope: UIWorld L4 domain/protocol baseline（World Model L2 role 的 L4 realization）
Location: docs/architecture/（冻结文档驻权威目录，不驻 docs/analysis/；规则见 docs/README.md）
Provenance: v0.1 draft（docs/analysis）→ adversarial grill G1–G8
→ 人工裁决 D1–D6 → v0.2 窄修正 → 定向复核通过 → 冻结并迁移（2026-09-09）
```

本文件定义 UIWorld 的领域语义、Authority、跨边界协议与关键不变量。

本文件**不定义**具体识别算法、模型组合、阈值、权重或 Fast/Slow 调度策略。

---

# 1. Purpose

UIWorld 负责在部分可观测、持续变化的 GUI 环境中维护 UniClaw 当前的 canonical world belief。

核心链路：

```text
Accepted Evidence
+
Previous WorldBelief
        ↓
UIWorld Reconciliation
        ↓
New WorldBeliefRevision
        ↓
Consumer-specific projection
        ↓
Slice / Consumer Views
```

UIWorld 不回答：

```text
“下一步应该做什么？”
“这个动作是否安全？”
“任务是否完成？”
```

分别属于：

```text
Control Loop
Assurance
UniAgent / Goal Evaluation
```

---

# 2. Component Boundary

目标结构：

```text
UniClaw.UIWorld
└── IWorldModel
    └── ContainerGraphWorldModel
```

其中：

```text
UIWorld
= bounded component

IWorldModel
= WorldBelief Authority contract

ContainerGraphWorldModel
= 当前 realization

ContainerGraph
= UIWorld internal representation
```

关键红线：

> ContainerGraph 不成为跨组件公共协议。

外部消费者不依赖：

```text
ContainerNode
GraphNode
GraphEdge
InternalWorldClaim
```

命名裁决（D2）：

```text
World Model
= canonical L2 role（baseline §10/§12；术语不变）

UIWorld / ContainerGraphWorldModel
= L4 realization 命名，仅此而已

ContainerGraph
= baseline「World Graph」的当前 L4 内部 realization

Slice
= canonical P4 术语；本文不引入 WorldSlice 作为平行 canonical protocol term
```

## 2.1 Terminology Mapping（UWM-009 ↔ L0–L3 / P1–P22）

| UWM-009 术语 | 对应 canonical 概念 | 归属 |
|---|---|---|
| UIWorld | World Model（L2 role）的 L4 realization | baseline §10/§12 |
| ContainerGraph | baseline「World Graph」的 L4 内部 realization | baseline §12.2 / 不变量 19 语境 |
| Slice | P4 Slice（已 verified；原稿 WorldSlice 一律改称 Slice） | 协议基线 P4 / CONTEXT.md |
| ContainerIdentity | World Model 下的新 canonical 语义（L4 细化，无新 Owner） | baseline §12.1/§18 |
| WorldState | baseline「World State」 | baseline §12.2 / 不变量 17 |
| Accepted Evidence 入边 | P3（Evidence Ledger → World Model） | 协议基线 P3 |
| Consumer views（BindingView 等） | P11 三 view（EXP-008 / ADR-0011） | 协议基线 P11 |
| TransitionContext 入边 | P22（本文 §12.1 新增；ADR-0012） | 协议基线 P22 |
| AssociationEvidence / Candidate / Disposition | World Model 内部领域语义（不跨组件） | 本文 §30 |

---

# 3. Authority

## 3.1 UIWorld Authority

UIWorld / IWorldModel 是以下 canonical semantics 的唯一 Authority：

```text
WorldBeliefRevision
ContainerIdentity
Container relationships represented in current belief
WorldState / current claims
Claim Conflict
Identity association accepted into canonical belief
Slice derivation
```

## 3.2 Non-authorities

### Perception

可以产生：

```text
ObservationProposal
semantic evidence
visual evidence
structural evidence
spatial evidence
identity hints
```

不能建立：

```text
canonical ContainerIdentity
canonical WorldBelief
```

### Vector / Retrieval / Re-ID capability

可以产生：

```text
AssociationCandidate
similarity evidence
candidate ranking
```

不能建立：

```text
ContainerIdentity truth
WorldBelief truth
```

### VLM

可以提供：

```text
semantic interpretation
identity-related evidence
ambiguity-reduction evidence
```

VLM 输出仍然是 Evidence。

```text
VLM judgment ≠ canonical WorldBelief
```

### Control Loop

可以决定：

```text
what to observe
where to observe
whether to spend more perception budget
```

不能直接修改 WorldBelief。

ControlIntent / 期望的 transition 不进入 WorldModel 输入；runtime
attempt/effect 事实只能经 P22 以 non-evidentiary prior 身份进入（§12.1）。

---

# 4. Core Domain Model

## 4.1 Container

Container 是 UIWorld 认为具有相对独立交互语义边界的世界对象。

它不是：

```text
任意 UI node
任意 DOM element
任意视觉区域
任意 bounding box
```

典型候选包括：

```text
Page
Window
Dialog
Menu
Drawer
Overlay
Tab-associated interaction space
```

具体 ContainerKind taxonomy 不在本变更冻结。

核心定义：

> Container 表示一个能够被持续识别、进入、离开、覆盖、恢复或作为交互上下文存在的 UI world entity。

---

# 5. ContainerIdentity

ContainerIdentity 表示 UIWorld 对一个持续存在 UI world entity 的 canonical identity。

```text
ContainerIdentity
≠ screenshot identity
≠ OCR identity
≠ bounding-box identity
≠ DOM/tree identity
≠ Observation identity
```

ContainerIdentity 由 UIWorld Authority 建立和维护。

Perception、Vector、VLM 等只能提供 Association Evidence。

---

# 6. ContainerGraph

ContainerGraph 是 baseline「World Graph」的当前 L4 内部 realization，表达：

```text
Identity
+
World relationships
```

分界判据（D3 冻结，取代“相对稳定”启发式）：

> 按 semantic kind 分界，不按变化频率分界。

```text
ContainerGraph
= revision-bound、evidence-backed 的世界实体间关系 belief

WorldState
= 单实体 intrinsic / contextual state claims
```

因此：

```text
Contains / Overlays
= graph relation（即使生命周期很短）

Visible / Loading / Presence / CurrentContainer
= state semantics
```

```text
Graph relation ≠ timeless structural truth
```

graph relation 本身是随 revision 携带、可带 evidence basis / epistemic
state 的 belief claim，不是无证据的结构事实。

第一版只要求支持真实 buyer 已出现的关系。

当前最小语义：

```text
Contains
Overlays
```

不在本版本提前冻结：

```text
NavigatesTo
OpenedBy
ReturnedFrom
TransitionedFrom
ExecutionHistory
```

这些关系不得仅因为一次执行历史而自动成为 World Graph truth。

原则：

> ContainerGraph 描述“世界对象及其关系”，不承担 Trace Graph 或 Action History。

---

# 7. WorldState

WorldState 表示 WorldBelief 中相对动态的 current claims。

例如：

```text
CurrentContainer
Visible
Focused
Enabled
Selected
Loading
Value
Presence
```

这些语义不要求全部变成固定字段。

分界判据见 §6（semantic kind：单实体谓词属 WorldState，
实体间关系属 ContainerGraph）。

canonical representation 可以是 Claim-based model。

概念形式：

```text
Claim
├── Subject
├── Predicate
├── Value
├── EvidenceBasis
└── EpistemicState
```

高频语义允许派生 typed projection，但 typed projection 不成为第二份 canonical truth。

---

# 8. Presence / Absence / Unknown / Conflict

UIWorld 必须区分：

```text
Known Present
Known Absent
Unknown
Conflicting
```

关键不变量：

```text
not observed
≠ absent

unknown
≠ false

conflict
≠ unknown

absence
requires supporting evidence
```

因此：

> 缺失检测结果本身不得自动形成 Absence Claim。

---

# 9. Container Association

Container Association 是 UIWorld 用于维护 ContainerIdentity continuity 的领域过程。

它对应业界 Data Association 问题，但协议采用 UniClaw 自身领域语义。

概念：

```text
Current accepted world Evidence
+
Existing Container Candidates
+
PreviousWorldBeliefRevision
+
TransitionContext（P22，non-evidentiary，仅 prior）
        ↓
Container Association
        ↓
AssociationDisposition
```

合法输入冻结（G1 修正）：

```text
(i)   accepted 且 world-relevant 的 EvidenceRecord（P3）
(ii)  previous WorldBeliefRevision 本身
(iii) TransitionContext（P22；仅 candidate prior/ranking 输入，见 §12）
```

ControlIntent / 期望的 transition / Control plan 不在合法输入内。

---

# 10. Association Evidence

Association Evidence 是所有可能帮助判断 Container continuity 的证据语义集合。

协议不冻结具体来源。

允许来源包括：

```text
OCR
YOLO / UI detection
layout analysis
semantic embedding
visual embedding
accessibility information
stable platform identity
VLM
learned Re-ID
rule-based inference
```

协议层只关心 Evidence 的语义及 provenance。

不得把以下实现细节冻结成协议：

```text
OCRScore
VectorScore
YOLOScore
CLIP threshold
VLM confidence threshold
weighted sum
specific embedding dimension
```

---

# 11. Association Candidate

v0.1 中 AssociationCandidate 只在 UIWorld 内部由 accepted Evidence 推导产生；
外部 capability 的 identity hints 只能经 P2/P3 以 evidence 内容进入。

外部 AssociationCandidate 直连 ingress：

```text
DEFER — NO CURRENT BUYER
```

UIWorld 内部 association process 可以提出：

```text
AssociationCandidate
├── CandidateContainerIdentity
├── SupportingEvidence
└── ContradictingEvidence
```

Candidate 只代表：

> “这个现有 Container 可能与当前 observation 对应。”

Candidate 不代表 canonical match。

---

# 12. Transition Context / Transition Prior

Container Association 可以消费与当前世界变化相关的 actual runtime causal context。

例如：

```text
Scroll
Type
Toggle
OpenOverlay
Navigate
Back
WindowSwitch
```

冻结（D1）：

```text
ControlIntent / 期望的 transition
MUST NOT 进入 WorldModel reconciliation

actual runtime attempt / effect context
MAY 经显式 non-evidentiary TransitionContext 进入 Container Association（P22）
```

TransitionContext 的协议身份：

```text
TransitionContext
≠ EvidenceRecord
≠ World claim
≠ ContainerIdentity truth
```

它只能影响 candidate prior / ranking：

```text
TransitionContext
MUST NOT 单独 establish Matched / New
MUST NOT create / replace ContainerIdentity
MUST NOT mutate canonical WorldState
```

canonical belief mutation 仍然只由 accepted world evidence 触发；
verified post-action effect 可另行由 accepted PostActionEffectFlow
Observation evidence 支撑（P2/P3，与 P22 正交）。

这些信息可以影响 association prior。例如：

```text
Scroll
→ SameContainer strongly plausible

Navigate
→ DifferentContainer more plausible

OpenOverlay
→ previous Container may remain
  + new overlay Container may appear
```

epistemic strength 必须保留来源语义：

```text
Intent says Scroll
（不进入 P22；Control 权域事实）

AttemptReport says Scroll attempted
（actual runtime attempt 腿，可经 P22 作 prior）

Evidence supports Scroll effect occurred
（accepted evidence 腿，P2/P3）
```

三者不能被协议压平成同一种事实。

核心原则：

> Transition Context 可以形成 Identity Prior，但不能直接建立 ContainerIdentity。

## 12.1 P22 — Runtime Transition Context → World Model

```text
Producer
= runtime effect flow（actual attempt/effect 事实的 owner 侧；
  具体导出缝 = realization）

Consumer
= World Model（Container Association 的 prior 输入）
```

- **Meaning**：一次 actual runtime attempt/effect 已发生的 non-evidentiary
  上下文；仅作为 association candidate prior / ranking 输入。
- **Minimal Payload**：transition kind（实际发生的 runtime 语义）；与具体
  dispatch attempt 的 correlation；epistemic strength 标注（attempt 腿 /
  effect-flow 腿）。
- **Forbidden Payload**：ControlIntent、期望的 transition、expected next
  page/container、desired outcome、Control plan、Tactical Hypothesis。
- **Validity**：单次 association 消费作用域，ephemeral；无独立生命周期，
  不 append、不缓存。
- **Authority**：World Model 消费为 prior；Evidence Ledger admission 面不受
  影响（P22 不是 admission 路径）。
- **Forbidden Use**：不得被当作 EvidenceRecord / World claim / identity
  truth；不得单独 establish Matched/New；不得 create/replace
  ContainerIdentity；不得 mutate canonical WorldState；不得回写任何
  canonical state。
- **Absence/Failure**：无 P22 输入 → association 按 evidence-only 进行
  （合法且完整，不是降级）。
- **Buyer**：UWM-009 S2 Scroll Continuity（真实 scenario buyer，见 §32）。
- **Status**：target 锁定（ADR-0012，2026-09-09）；实现随 UWM-009
  vertical slice 另立 change。

---

# 13. AssociationDisposition

Container Association 的领域结果冻结为四类（D6；与 §8 claim epistemic 是两条不同轴）：

```text
Matched
New
Ambiguous
Insufficient
```

## Matched

当前 observation 足以支持：

```text
current observed container
=
existing canonical ContainerIdentity
```

## New

当前 observation 足以支持：

```text
current observed container
≠ relevant known candidate set
```

并允许 UIWorld 建立新的 canonical ContainerIdentity。

重要：

> Low similarity 本身不能推出 New。

New 必须建立在足够 observation evidence 上。

## Ambiguous

observation 充分（sufficient），但仍存在多个成立的 identity interpretation。

UIWorld 不得为了推进流程而强制选择 candidate。

## Insufficient

当前 observation 不具备完成 identity discrimination 所需的判别信息。

Insufficient 不得被自动解释为：

```text
New
Different
Failure
```

两条不变量（D6）：

```text
Ambiguous
MUST NOT create / replace canonical identity

Insufficient
MUST NOT create / replace canonical identity
```

Matched / New 之外的 disposition 不产生 identity claim mutation；
受影响 claim 按其自身证据状态诚实降级。

两轴词汇不得混用：

```text
Claim epistemic（§8）
= Known / Absent / Unknown / Conflicting
（单个 claim 的认知状态）

AssociationDisposition（本节）
= Matched / New / Ambiguous / Insufficient
（一次 association 的判别结果）
```

---

# 14. Observation Sufficiency

Association 必须能够表达：

> 当前 observation 是否具备完成该 identity discrimination 所需要的信息覆盖。

因此：

```text
Match confidence
```

与：

```text
Observation sufficiency
```

是不同语义。

例如：

```text
low match
+
insufficient observation
→ Insufficient
```

而不是：

```text
→ New
```

---

# 15. ObservationNeed

当当前 WorldBelief 无法安全完成 reconciliation 时，UIWorld 可以表达 ObservationNeed。

例如（non-exhaustive 示例，信息型缺失描述，不指向任何 capability）：

```text
NeedMoreEvidence
NeedIdentityDisambiguation
NeedSemanticEvidence
NeedBroaderCoverage
NeedTargetedRegionObservation
```

ObservationNeed 描述：

> UIWorld 当前缺少什么 information。

它不描述：

```text
CallVLM
RunOCR
UseYOLO
RunVectorSearch
```

这些属于 Observation Control / Perception strategy。

因此：

```text
UIWorld
→ ObservationNeed

Observation Control
→ chooses acquisition strategy

Perception
→ produces Observation

Evidence Ledger
→ accepts Evidence

UIWorld
→ reconcile again
```

ObservationNeed 是 UIWorld 内部领域概念（表达缺失信息）；v0.1 不冻结任何
WorldModel → Observation Control 的外部协议边：

```text
DEFER — NO CURRENT BUYER
```

---

# 16. Fast / Slow Perception Boundary

UIWorld 不定义固定 Fast/Slow algorithm。

以下都允许：

```text
Fast-only
Fast → targeted Slow
Slow-first
rule-only
vector-assisted
learned Re-ID
platform-native identity
hybrid strategy
```

本协议只要求：

```text
Different strategies
→ produce compatible Association Evidence
→ converge through same UIWorld Authority
```

例如 Scroll 通常可以在 Fast evidence 充分且无强 contradiction 时完成 association。

但：

> “Scroll 一定 Fast-only”

不属于 protocol invariant。

它是 strategy/policy。

---

# 17. Reconciliation

UIWorld reconciliation 的 canonical boundary：

```text
Accepted Evidence
+
Previous WorldBeliefRevision
        ↓
Reconciliation
        ↓
New WorldBeliefRevision
```

Reconciliation 内部可以实现：

```text
associate
reaffirm
revise
supersede
withdraw
conflict
identity merge / supersession
```

但这些内部 operation 不自动成为跨组件 protocol。

外部 canonical output 是：

```text
WorldBeliefRevision
```

canonical belief mutation 只由 accepted world evidence 触发；
TransitionContext（P22）不是 mutation 输入。Belief relevance gate
语义不变（AttemptReport 仍定义性非 world-relevant）。

---

# 18. Claim Evolution

WorldState 不允许退化为：

```text
dictionary[key] = newestValue
```

新 Evidence 与旧 Claim 的关系需要通过 reconciliation 解释。

至少语义上允许：

```text
Reaffirm
Revise
Supersede
Conflict
Withdraw
```

无法合理消解的冲突必须保持 Conflict。

原则：

> WorldModel 的职责不是始终产生确定答案，而是产生诚实的 belief。

---

# 19. Identity Evolution

如果后续 Evidence 证明此前多个 identity 实际代表同一个 world entity：

```text
A
B
↓
canonical reconciliation
C
```

历史 identity 不应被物理抹除。

应允许：

```text
superseded
alias / lineage
canonical replacement
```

具体 realization 本版本不冻结。

要求：

> 历史 Evidence / Trace 对旧 identity 的引用仍然可解释。

---

# 20. Slice（canonical P4 术语）

Slice 是：

> 某个 WorldBeliefRevision 派生出的 scoped immutable world projection。

因此：

```text
Container
= canonical world entity

Slice
= projection
```

两者不存在 1:1 关系。

---

# 21. Slice Core Semantics

冻结的只有 semantic invariants（D5）：

```text
revision-bound（绑定 SourceRevisionId）
scoped
immutable
projection omission ≠ world absence（见 §28）
```

以及条件性空间规则（见 §24）：

```text
若 Slice 暴露 spatial value，则必须标识其 SpatialFrame
```

Temporal 语义保留 P4 verified 口径：

```text
SourceRevisionId + FreshnessBasis（temporal provenance 表达，
非 freshness 裁决——ADR-0010）
```

以下概念维度仅作 realization 参考，不冻结（字段形状 / 表示法
buyer-driven；见 §30、§33）：

```text
SliceId / Anchor 表示法
Bounds / Relative Transform / 坐标表示
Coverage 表示法
Containers / Relations / Claims 的具体投影形状
HOW MUCH（数量维度）
mandatory Uncertainty / Conflict 字段
```

---

# 22. Slice Scope

Slice 由 consumer purpose / requested scope 派生。

Container 可以作为常见 anchor，但：

```text
Slice
≠ one Container
```

一个 Slice 可以包含：

```text
0..N Containers
```

例如：

```text
SettingsPage
└── AccountDialog
    └── DropdownMenu
```

如果 consumer 需要当前 Dialog 的 overlay context：

```text
Anchor = AccountDialog

Slice:
AccountDialog
DropdownMenu
必要 parent context
```

因此：

> Container 是 world entity；Slice 是围绕消费需求裁剪出的 world projection。

---

# 23. CurrentContainer

CurrentContainer 属于 WorldState。

它可以作为 Slice derivation anchor。

但：

```text
CurrentContainer
≠ Slice
```

关系可以理解为：

```text
CurrentContainer
→ common Slice anchor

Slice
→ scoped projection around anchor
```

---

# 24. SpatialFrame

Slice 若暴露空间信息，必须绑定明确 coordinate frame
（条件性规则：Slice 并不必须携带空间信息）。

禁止协议中出现无语义裸坐标：

```text
x = 120
y = 380
```

而应表达：

```text
CoordinateSpace
+
Coordinates / Bounds
```

CoordinateSpace 可以是：

```text
Screen
Window
Container-relative
Slice-relative
other explicit spatial frame
```

具体实现类型本版本不冻结。

核心 invariant：

> Spatial value exposed through a Slice without its SpatialFrame
> is invalid protocol semantics.

---

# 25. Temporal Semantics

Slice 必须绑定其来源 WorldBelief revision。

因此至少存在：

```text
SourceRevisionId
```

以及必要的：

```text
FreshnessBasis / temporal provenance
```

Slice 不承担 Run execution history。

以下内容不属于 Slice 本体：

```text
PreviousAction
PreviousSlice
ActionNumber
ControlStepHistory
```

---

# 26. SliceContext

消费 Slice 所需要的 causal/control context 应与 Slice 本体分离。

概念上可以存在：

```text
SliceContext
├── PreviousSliceRef?
├── TransitionContext?
├── TriggeringEffectRef?
└── ConsumerPurpose
```

SliceContext 不是 WorldBelief truth。

它表达：

> 当前 consumer 是在什么控制与因果上下文中使用这个 Slice。

SliceContext 作为跨组件 payload：

```text
DEFER — NO CURRENT BUYER
```

v0.1 只冻结其非 truth 身份；不冻结其跨组件形状。

---

# 27. Observation Scope ≠ Slice

必须严格区分：

```text
Observation Scope
= perception 看到了哪里

Slice
= WorldModel 从 belief 投影了什么
```

链路：

```text
Observation Scope
        ↓
Observation
        ↓
Evidence
        ↓
WorldBelief
        ↓
Slice
```

不能因为 Observation 没覆盖某区域，就自动认为该世界事实不存在。

---

# 28. Slice Coverage

强 invariant（协议级）：

> Projection omission 与 World absence 必须可区分。

```text
not present in Slice
≠
not present in World
```

Coverage 的具体表示法（显式 coverage 对象、scope 声明或其他机制）
= implementation / buyer-driven，本版本不冻结（D5）。

---

# 29. Consumer Views

EXP-008 已冻结 consumer-specific views：

```text
BindingView
ActionAssuranceView
OutcomeAssuranceView
```

UWM-009 不重新把这些统一回 Slice。

因此：

```text
Slice
```

主要服务需要 richer scoped world representation 的消费者，例如：

```text
Control
Traversal
Exploration
future observation reasoning
```

Assurance / Effect Boundary 继续消费其专用 immutable views。

原则：

> 不允许 Slice 演化成新的万能 WorldBelief DTO。

---

# 30. Protocol vs Strategy Boundary

## Protocol Freeze（D4 拆分）

### Owner 内部领域语义（可冻；无需外部 buyer）

```text
Container
ContainerIdentity
Container Association
AssociationEvidence
AssociationCandidate（仅 UIWorld 内部推导）
AssociationDisposition
Observation Sufficiency
Claim epistemic 轴与 AssociationDisposition 轴的分离
ContainerGraph / WorldState semantic separation（§6 判据）
ObservationNeed（内部领域概念）
WorldBeliefRevision
Authority boundaries
```

### 既有跨组件协议（消费 / 澄清，不新建）

```text
P3  Accepted Evidence Record → World Model
P4  Slice → Control Loop（语义 invariant 澄清见 §21/§24/§28）
P11 Current WorldBelief View → Assurance / Effect Boundary
```

### 新增跨组件协议

```text
P22 Runtime Transition Context → World Model（§12.1；ADR-0012）
```

### DEFER — NO CURRENT BUYER

```text
ObservationNeed 作为 WorldModel → Observation Control 外部协议边
外部 AssociationCandidate 直连 ingress
AssociationDisposition 外部发布
SliceContext 跨组件 payload
```

## Strategy — Not Frozen

不冻结：

```text
YOLO model
OCR engine
embedding model
vector database
distance metric
thresholds
weights
candidate Top-K size
VLM model
VLM prompt
Fast/Slow threshold
rule scoring
probabilistic model
Bayesian implementation
learning-to-rank
Container Re-ID model
graph matching algorithm
layout fingerprint algorithm
```

这些 realization 可以替换，而不改变 protocol semantics。

同样不冻结 Slice 的字段形状（SliceId、Bounds、Transform、HOW MUCH
维度、mandatory Uncertainty/Conflict 字段——D5；见 §21）。

---

# 31. Core Invariants

```text
P-UW-01
Perception Evidence ≠ ContainerIdentity Truth

P-UW-02
AssociationCandidate ≠ canonical match

P-UW-03
TransitionPrior ≠ Identity Truth

P-UW-04
Low similarity ≠ New Identity

P-UW-05
Insufficient Observation ≠ New Identity

P-UW-06
Insufficient (association) / Unknown (claim) ≠ Failure

P-UW-07
Conflict ≠ Unknown

P-UW-08
Missing detection ≠ Absence

P-UW-09
ContainerGraph ≠ WorldState

P-UW-10
ContainerGraph ≠ Trace Graph

P-UW-11
Container ≠ Slice

P-UW-12
CurrentContainer ≠ Slice

P-UW-13
Observation Scope ≠ Slice

P-UW-14
Slice is revision-bound and immutable

P-UW-15
Slice omission ≠ World absence

P-UW-16
Spatial value exposed through a Slice
must identify its SpatialFrame

P-UW-17
PreviousAction / PreviousSlice do not belong to Slice truth

P-UW-18
ObservationNeed is internal domain semantics;
no external protocol edge is frozen for it

P-UW-19
UIWorld does not call VLM as a domain requirement

P-UW-20
WorldModel is sole canonical WorldBelief and ContainerIdentity authority

P-UW-21
TransitionContext (P22) is non-evidentiary prior only:
never EvidenceRecord, never World claim,
never establishes Matched/New by itself,
never mutates canonical WorldState

P-UW-22
ControlIntent / desired transition / Control plan
never enters WorldModel reconciliation

P-UW-23
Canonical belief mutation requires accepted world evidence
```

---

# 32. Initial Vertical Slice for Coding

UWM-009 implementation应优先验证以下最小闭环：

```text
Accepted Evidence
↓
Container Association
↓
ContainerGraph / WorldState reconciliation
↓
new WorldBeliefRevision
↓
Slice derivation
```

建议最少场景：

## Scenario 1 — First Container

```text
No existing candidate
+
sufficient accepted observation
→ New
→ canonical ContainerIdentity established
```

## Scenario 2 — Scroll Continuity（P22 buyer）

```text
Existing Container A
+
actual runtime scroll attempt/effect TransitionContext（P22，prior only）
+
sufficient Fast evidence
+
no strong contradiction
→ Matched A
→ revision changes
→ ContainerIdentity remains stable
```

不得要求 Slow perception 必然执行。
ControlIntent / 期望的 transition 不进入；无 P22 输入时
evidence-only association 亦为合法路径。

## Scenario 3 — Similar Appearance, Different Identity

```text
High visual/layout similarity
+
strong semantic/context contradiction
→ must not incorrectly force Matched
```

验证：

```text
Visual similarity ≠ Identity Authority
```

## Scenario 4 — Ambiguous Association

```text
Multiple plausible candidates
→ Ambiguous
→ no forced identity mutation
→ ObservationNeed may be produced（内部领域概念；外部边 deferred）
```

## Scenario 5 — Insufficient Observation

```text
Low candidate match
+
insufficient coverage
→ Insufficient
```

不得：

```text
→ New
```

## Scenario 6 — Conflicting Claim

```text
Accepted Evidence A
supports X

Accepted Evidence B
supports not-X

No sufficient basis to resolve
→ Conflict
```

不得 latest-wins。

## Scenario 7 — Slice Derivation

Slice 必须：

```text
bind SourceRevisionId
have explicit Scope
remain immutable
uphold omission ≠ absence invariant
identify SpatialFrame for any spatial value exposed
not expose entire WorldBelief aggregate by default
```

---

# 33. Deferred Questions

以下问题不在 UWM-009 v0.2 冻结：

```text
1. ContainerKind complete taxonomy
2. Exact association scoring algorithm
3. Hard vs soft association thresholds
4. Vector retrieval architecture
5. Fast/Slow escalation policy
6. VLM invocation strategy
7. Learned Container Re-ID
8. Full identity merge/alias persistence model
9. Navigation relationship taxonomy
10. ObservationNeed prioritization policy
11. Exact Slice consumer request language
12. Exact coordinate transform representation
13. Outcome-level use of UIWorld freshness
14. Cross-session Container identity persistence
15. Memory ↔ Container identity interaction
16. ObservationNeed 作为 WorldModel → Observation Control 外部协议边
17. 外部 AssociationCandidate 直连 ingress
18. AssociationDisposition 外部发布
19. SliceContext 跨组件 payload 形状
```

这些应等待真实 buyer / scenario evidence。

---

# 34. Freeze Statement

v0.2 窄修正经定向复核（G1–G8）通过后，UWM-009 冻结以下架构结论：

```text
World Model (L2) owns canonical WorldBelief and ContainerIdentity;
UIWorld / ContainerGraphWorldModel are realization names only.

ContainerGraph represents revision-bound, evidence-backed relational
belief between world entities (realization of the baseline World Graph);
WorldState represents intrinsic / contextual state claims;
the split is by semantic kind, not by change frequency.

Container continuity is resolved through Container Association.

Perception / Vector / VLM provide evidence, not authority.

Runtime Transition Context (P22) may influence association candidate
prior/ranking only; it is not evidence, not a World claim, cannot
establish Matched/New by itself, and never mutates canonical WorldState.
ControlIntent / desired transition never enters reconciliation.

Ambiguous and Insufficient are valid outcomes;
neither creates or replaces canonical identity.

ObservationNeed expresses missing information as internal domain
semantics, without selecting a perception strategy and without a
frozen external protocol edge.

Slice is a scoped immutable projection of one WorldBeliefRevision:
revision-bound, scoped, immutable, and its omissions never imply
world absence. It is not a Container, not an Observation,
not Trace history, and not a universal world DTO.

Implementation strategy remains replaceable
behind these semantics.
```
