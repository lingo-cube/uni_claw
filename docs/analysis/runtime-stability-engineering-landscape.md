# Runtime Stability Engineering Landscape

> DocumentType: `RUNTIME_STABILITY_ENGINEERING_LANDSCAPE`
> Status: **ANALYSIS / REFERENCE / NON-NORMATIVE**
> Date: 2026-08-30
> Authority: `NONE`
> Scope: UniClaw Runtime Perception → Fusion → Canonicalization → Semantic Admission 稳定性
> Purpose: 为后续 Stability Architecture / Perception Architecture 设计提供事实、术语、外部技术参考和 Gap 清单
> ChangeMode: `ANALYSIS_AND_DOCUMENTATION_ONLY`
> AuthorityDelta: `NONE` · ArchitectureDelta: `NONE` · RuntimeBehaviorDelta: `NONE`
>
> 本文不是 Decision，不冻结架构；不是 Design，不定义最终实现；不是 Spec，不产生 Runtime authority 或 implementation obligation。
> 禁止边界：本文不得被引用为架构权威、不得改变 Runtime/Perception 的 authority / lifecycle / owner / behavior，不得作为实现授权；候选方向必须经 Human Gate 购买后才进入 Decision / OpenSpec / Design。
>
> 后续若某一候选方向被 Human Gate 购买，应从本文抽取相应结论，进入独立 Decision / OpenSpec / Design。

---

## 1. 背景

Phase 2.6 在真实 Settings traversal 验证过程中，持续暴露出一组表面不同、实质高度相关的稳定性问题。

早期这些问题可以分别视为单点 bug：

- checkbox 类型归一化回归；
- duplicate occurrence 抢先获得 `LocalControl`；
- textless Search icon 进入 Unknown；
- 同一 row 在不同 viewport 中出现 `text_block ↔ menu_item` 漂移；
- uniform-list `NOOP` 后 relation-head fallback 被错误阻断；
- source normalization 在相邻确认窗口无法稳定收口；
- 合法 bounds 因 float32 rounding 被误判越界；
- 单 occurrence projection exception 导致整帧 semantic evidence 被清空；
- Fusion 内部缺少足够 Trace，导致 First Divergence Point 需要多轮真实重放才能恢复；
- perception deployment/config/pipeline identity 漂移导致 validation receipt 无法复用。

随着这些问题逐步闭合，已经可以确认：

> 当前主要压力不再只是某几个 Settings-specific semantic pattern 缺失，而是 **Perception / Semantic Pipeline 缺少一套完整的 Stability Substrate**。

换句话说，当前系统已经具备较强的 fail-closed、安全边界和局部 semantic capability，但在以下几个方面仍然依赖局部 heuristic：

- frame-local composition；
- temporal identity；
- role continuity；
- operator ownership/fallback；
- fault containment；
- decision traceability；
- replayability；
- numerical robustness。

后续如果继续仅以"当前 blocker 能否消失"为优化目标，容易形成大量局部规则。

更合理的方向是：

> 从真实 Settings buyer evidence 中提炼可复用的 Stability Contracts，再判断哪些能力应该成为 Generic Runtime / Perception primitive。

---

## 2. 当前 Runtime 的基本链路

当前可近似抽象为：

```text
External UI
   ↓
Screenshot / Structured Auxiliary Evidence
   ↓
Detector / OCR
   ↓
Normalized Detection
   ↓
Fusion / Composition Operators
   ↓
Canonical Observation Occurrence
   ↓
Semantic Capability / Admission
   ↓
Interaction Affordance
   ↓
Agent Inventory / Grounding
   ↓
Authorization / Traversal
   ↓
Verification / Completeness
```

其中已经冻结的重要边界仍然成立：

```text
Vision = primary perception / grounding evidence

Structured hierarchy
= auxiliary corroboration
!= independent action authority
```

并且：

```text
DISCOVERED
!= GROUNDED
!= CURRENTLY_VISIBLE
!= AUTHORIZED
!= VISITED
!= COMPLETED
```

因此本文讨论的 Stability Architecture，**不得通过增强 perception continuity 来绕过 Runtime authority**。

特别需要维持：

```text
HISTORICAL_ASSOCIATION != CURRENT_WORLD_TRUTH
TEMPORAL_CONTINUITY != ACTION_AUTHORITY
TRACE != CONTROL
TRACE != SEMANTIC_AUTHORITY
```

---

## 3. 已经出现的稳定性问题

### 3.1 Geometry / Numerical Stability

真实案例：

```text
X1 = 0.002778f
X2 = 1.0f
```

本身是完全合法的 normalized bounds。

但当前曾出现：

```text
float width = X2 - X1
↓
float rounding
↓
double(left + width)
= 1.000000006...
↓
false-positive out-of-frame
```

因此：

> 合法 evidence 因 representation precision 被误判为 malformed evidence。

这属于：

```text
NUMERICAL_STABILITY
GEOMETRY_CONTRACT_STABILITY
```

它说明单纯依赖 example test 不足，需要更系统地验证 geometry invariants。

### 3.2 已确认的同类失败

此前 zero-width occurrence 已暴露过同类问题：单 occurrence 的异常导致整帧 semantic evidence 被丢弃。bounds rounding 再次暴露：

```text
1 occurrence exception
→ 38 semantic candidates discarded
```

---

## 4. Frame-Local Role Stability

已真实证明：

```text
Display
→ text_block

viewport translation

Display
→ menu_item
```

以及：

```text
Safety & emergency
→ text_block
→ menu_item
```

同时 blocker frame：

- viewport 已 settled；
- occurrence bounds 完整；
- 并非 viewport edge clipping；
- structured corroboration 已存在。

因此问题不是：

```text
PERCEPTION_NOT_SETTLED
```

而是：

```text
FRAME_LOCAL_FUSION_INSTABILITY
```

即：

> 同一个完整 UI object，在其自身 evidence 大体等价时，仅因为 viewport position / surrounding anchors 改变，Fusion 给出了不同 role。

这说明我们目前缺少一个明确 contract：

```text
SameOccurrence
+
EquivalentEvidence
→
StableRole
```

但这个 contract 不能简单写成：

```text
SameStableKey → SameRoleForever
```

因为新的 fresh evidence 可能合法推动：

```text
Unknown
→ NavigationCandidate
```

所以更合理的未来语义应该是：

```text
EquivalentEvidence
→ StableRole

NewEvidence
→ RoleTransition allowed

RoleTransition
→ MUST carry EvidenceDelta + Reason
```

---

## 5. Composition Stability

真实问题已经证明：

```text
checkbox
icon
subtitle
menu_item
text_block
```

不能只通过一个 `type` 字段表达完整 UI 意义。

例如：

```text
SearchActionBar
└── Icon
```

这个 Icon：

```text
VisualRole = Icon
CompositionRelation = ChildOf(SearchActionBar)
IndependentAffordance = None
```

而不是：

```text
Icon → SearchBar
```

也不是：

```text
Icon → NonInteractive
```

类似：

```text
PreferenceRow
├── Icon
├── PrimaryLabel
├── Description
└── Checkbox
```

说明当前逐渐形成三个不同维度：

```text
What it looks like
→ VisualRole

What it belongs to
→ CompositionRelation

What it can do
→ Affordance
```

即：

```text
VisualRole
!= CompositionRelation
!= Affordance
```

当前 `ChildOf` 已经通过真实 Search icon buyer evidence 得到实际购买价值。

但完整 Generic Composition Model 尚不存在。

---

## 6. Duplicate / Multi-Representation Stability

真实 perception 经常不是：

```text
1 UI Object
→ 1 Detection
```

而可能是：

```text
1 UI Object
→ detector row
→ OCR title
→ icon
→ structured row
→ toggle
→ caption
```

甚至同一物理 row 同时存在：

```text
menu_item
+
text_block
```

因此必须区分：

```text
Detection Identity
!= Physical UI Object Identity
!= Logical Source Identity
```

这也是 checkbox false-positive 问题的本质之一：

```text
VisualRole = Toggle
```

并不能自动推出：

```text
IndependentAffordance = LocalControl
```

如果 occurrence 已经被证明是：

```text
DuplicateOf(MenuRow)
```

它就不应该获得独立 action authority。

当前已得到一个重要通用原则：

```text
COMPOSITION != TYPE_DESTRUCTION
```

---

## 7. Temporal / Occurrence Identity Stability

当前 Runtime 已经拥有：

- StableKey；
- SourceOccurrenceIdentity；
- SourceIdentity；
- DestinationIdentity；
- SourceEquivalenceNormalizer；
- viewport overlap / source union 等机制。

但当前 continuity 仍然主要依赖：

```text
StableKey
text
bounds
PerceptionType
```

而不是显式的跨帧 data association。

因此容易出现：

```text
Frame N:
row_016 | text_block

Frame N+1:
row_016 | menu_item
```

StableKey 能告诉我们：

> 文字对象具有一定连续性。

但不能回答：

> 这两个 occurrence 是否应该被正式视为同一物理 UI occurrence。

这正是计算机视觉中 **Multi-Object Tracking / Data Association** 长期处理的问题。

---

## 8. External Concept: Multi-Object Tracking / Data Association

Multi-Object Tracking 的核心不是"每帧重新检测对象"，而是：

> 将当前帧 detection 与之前的 object tracks 建立关联，从而维护 identity continuity。

ByteTrack 的核心贡献就是通过 association 而不是简单丢弃低置信 detection，减少 object trajectory fragmentation。

DeepSORT 则在 SORT 的基础上加入 appearance association，以减少 identity switch。

这些方法解决的问题和 UniClaw 并不完全一样，但概念高度相关：

```text
Computer Vision Tracking:

Detection N
        ↘
         Same Object Track
        ↗
Detection N+1
```

UniClaw 需要的是：

```text
UI Occurrence N
        ↘
         SameOccurrence
        ↗
UI Occurrence N+1
```

### 与传统 MOT 的重要区别

普通 MOT 中：

```text
object moves
camera may move
```

GUI traversal 中通常是：

```text
UI object itself is stationary
viewport scrolls
```

因此 UniClaw 不一定需要完整 ByteTrack / DeepSORT。

更适合的是：

```text
Viewport Motion Estimation
        ↓
Motion Compensation
        ↓
Occurrence Association
```

即先估计：

```text
Δscroll
```

再进行 occurrence matching。

---

## 9. Viewport Motion Compensation

对于纯滚动 UI，相邻截图大量变化可以近似看作全局 translation。

OpenCV 已提供 `phaseCorrelate`，专门用于检测两幅图之间的 translational shift，可用于 image registration 和 motion estimation。

因此可实验：

```text
Screenshot N
Screenshot N+1
      ↓
phase correlation
      ↓
estimated ΔY
```

然后将上一帧 occurrence 投影：

```text
predictedY(N+1)
=
Y(N) + ΔY
```

再进行 matching。

这比直接比较：

```text
current bounds
vs
previous bounds
```

更加符合 scroll UI 的真实运动模型。

需要注意：

> Phase correlation 只能作为 association evidence，不能成为 Runtime interaction authority。

---

## 10. Bipartite Matching / Hungarian Assignment

一旦得到上一帧 occurrence 的 predicted position，就可以把：

```text
PreviousOccurrences
```

和：

```text
CurrentOccurrences
```

构造成一个 bipartite assignment problem。

SciPy 的 `linear_sum_assignment` 直接提供 Linear Sum Assignment，目标是在 cost matrix 上求全局最优匹配。

例如：

```text
cost(A,B) =
    geometryDistance
  + IoUDifference
  + textDifference
  + typeCompatibility
  + StableKeyPenalty
  + relationDifference
```

然后：

```text
Hungarian / Jonker-Volgenant
↓
globally consistent matching
```

相比当前很多 pairwise heuristic：

```text
if same text
if overlap
if same row
```

全局 assignment 有一个明显优势：

> 不会让多个 candidate 同时"局部看起来都像"同一个 occurrence。

这与当前 duplicate / physical-row association 问题高度相关。

---

## 11. Scene Graph

Scene Graph 的核心思想是：

> 不只识别 object，还显式表示 object 之间的 relationship。

典型结构：

```text
Object A
   └── relation
          ↓
       Object B
```

Scene Graph Generation 已经形成完整研究领域，其目标就是从图像/视频中生成 object + relationship 的结构化表示。相关 survey 将其概括为将视觉场景映射为带 object labels 和 relationships 的 semantic structural graph。

这与 UniClaw 当前逐渐形成的模型高度一致：

```text
PreferenceRow
├── ChildOf → Icon
├── LabelOf → Text
├── DescriptionOf → Text
└── Contains → Checkbox
```

因此长期 Generic Composition 的候选形态不是：

```text
flat List<ObservedElement>
```

而可能更接近：

```text
UI Scene Graph
```

不过需要强调：

> UniClaw 不需要直接采用通用 Scene Graph neural model。

目前更合理的是借用 **representation idea**：

```text
Objects + Relations
```

而不是马上引入 Scene Graph Generation 深度模型。

---

## 12. Constraint / Assignment Solving

随着 composition relation 增多，会逐渐出现约束：

```text
一个 subtitle 最多属于一个 primary row

一个 occurrence 不能同时是
DuplicateOf(A)
和独立 NavigationTarget

checkbox child 可以独立 LocalControl

decorative icon child 不可继承 parent clickable

invented rows 不得超过 confirmed evidence capacity
```

简单阶段可以通过 bipartite matching 解决。

如果未来 relation constraints 显著复杂化，可以考虑 OR-Tools。

Google OR-Tools 对 assignment problem 提供：

- Linear Sum Assignment；
- Minimum Cost Flow；
- MIP；
- CP-SAT。

其中官方也明确指出，CP-SAT/MIP 更适合约束更复杂的 assignment 问题。

当前不建议直接引入 CP-SAT 到 production Runtime。

建议判断：

```text
简单 one-to-one relation
→ Hungarian / deterministic matching

复杂 multi-constraint composition
→ 再评估 CP-SAT
```

---

## 13. Operator Ownership / Fallback Contract

当前真实 Fusion bug 已证明：

```text
confirmedAnchors >= 4
→ router assigns uniform-list

uniform-list
→ NOOP

relation-head
→ still skipped
```

最终导致：

```text
complete UI row
→ text_block
→ Unknown
```

修复后改为：

```text
Operator actually activated
→ owns handled evidence

Operator NOOP
→ no ownership

Remaining evidence
→ eligible for fallback
```

这实际上应该提升为通用 Operator Contract。

未来 operator 应尽可能统一产生类似：

```text
OperatorResult
{
    Status:
        MATCH
        PARTIAL
        NOOP
        REJECTED
        ERROR

    ConsumedEvidenceRefs
    ProducedEvidenceRefs
    RemainingEvidenceRefs
    Reason
}
```

这样 router 不再通过：

```text
anchor count
type
position
```

间接猜测 operator 是否已经"拥有"某批 evidence。

核心 invariant：

```text
OPERATOR_SELECTED
!=
OPERATOR_ACTIVATED

NOOP
!=
OWNERSHIP
```

---

## 14. Property-Based Testing

目前很多真实 bug 都不是特定 UI copy 的问题，而是 invariant violation。

例如 bounds bug，本质应该满足：

```text
For any valid:
0 <= X1 <= X2 <= 1

Normalize(bounds)
must remain valid.
```

Python 侧可以使用 Hypothesis。

Hypothesis 的工作方式就是：开发者定义 property 和输入空间，由框架生成大量输入，包括开发者未主动考虑的 edge cases，并在失败时尝试缩减为最小反例。

.NET 侧可以使用 FsCheck。

FsCheck 同样允许开发者定义 properties，由框架生成大量随机输入；失败时会给出 minimal counter-example，并可与常见 .NET 测试框架集成。

这与当前 Runtime 非常匹配。

值得建立的 Stability Properties 包括：

### Geometry

```text
ValidBounds
→ Normalize
→ ValidBounds
```

### Translation invariance

```text
Same row-local evidence
+ viewport translation
→ same role
```

### Sibling invariance

```text
Add unrelated sibling
→ existing occurrence identity unchanged
```

### Operator ownership

```text
NOOP
→ cannot consume ownership
```

### Composition authority

```text
ChildOf(parent)
→ must not inherit parent's affordance
```

### Duplicate rendering

```text
VerifiedDuplicateRendering
→ cannot acquire independent affordance
```

### Fail-closed preservation

```text
AmbiguousRelation
→ must not become authorized source
```

因此 Property-Based Testing 很可能是当前最适合**直接引库**的技术之一。

---

## 15. Trace / Decision Provenance

Fusion role instability 的定位过程已经证明：

没有 operator Trace 时，需要：

```text
real run
→ dump
→ offline reconstruction
→ hypothesis
→ another run
→ another dump
```

补充 Fusion Trace 后可以直接看到：

```text
confirmedAnchors = 7
↓
uniform-list attempted
↓
NOOP:
cadence model not inferable
↓
relation-head skipped
↓
remaining text blocks
```

因此 Trace 的长期角色不应只是 logging，而应成为：

> Runtime Decision Provenance。

OpenTelemetry 的 Trace 模型已经非常成熟：

```text
Trace
└── Span
    ├── Attributes
    ├── Events
    ├── Links
    └── Status
```

Span 表示一次 operation，也可以组成 parent/child trace tree；Span 内可以记录 Events 和 Links。

UniClaw 可以借这个模型：

```text
Run Span
└── Observation Span
    └── Perception Span
        ├── Normalize Span
        ├── Fusion Span
        │   ├── RouterDecision Event
        │   ├── OperatorAttempt Event
        │   ├── OperatorResult Event
        │   └── ValidatorDecision Event
        └── Canonicalization Span
```

之后：

```text
SemanticAdmission Span
Grounding Span
Authorization Span
Action Span
Verification Span
Completion Span
```

大体积 screenshot / raw YOLO 不应该直接塞入 Trace。

正确方向是：

```text
Trace
  EvidenceRef
  ArtifactRef
  OccurrenceRef
  FactRef
```

因此：

```text
TRACE != ARTIFACT STORAGE
```

同时继续维持：

```text
TRACE != CONTROL
TRACE != AUTHORITY
```

目前 Fusion Trace Coverage 已经证明价值，但 **Generic Runtime Trace Capability 尚未完成**。

---

## 16. Fault Containment

当前已经多次出现同类问题：

```text
one malformed occurrence
↓
throws
↓
entire semantic frame discarded
```

之前 zero-width occurrence 已经暴露过这一问题。

现在 bounds rounding 又再次暴露：

```text
1 occurrence exception
→ 38 semantic candidates discarded
```

因此未来需要更系统定义 Failure Domain。

可能形成：

```text
MALFORMED_OCCURRENCE
!=
MALFORMED_OBSERVATION

OPERATOR_FAILURE
!=
PIPELINE_FAILURE

UNRESOLVED_CHILD
!=
INVALIDATE_VALID_SIBLINGS
```

但不能简单变成：

```text
catch everything and continue
```

因为 global observation inconsistency 仍必须 fail-closed。

所以未来需要明确区分：

```text
LOCAL_INVALIDITY
GLOBAL_INVALIDITY
```

并明确每种异常的 containment boundary。

当前这一点仍然属于明显 GAP。

---

## 17. Replayability

当前 Runtime 已经逐渐形成：

```text
Real Device Failure
↓
Stage Evidence
↓
Offline Replay
↓
Deterministic Falsifier
↓
RED
↓
Repair
↓
GREEN
↓
Fresh Real Campaign
```

这其实已经是很好的稳定性闭环。

例如 frame-local fusion bug 就已经将真实 frame geometry 转换为了 deterministic regression corpus。

未来应把它正式工程化为：

```text
RealEvidencePackage
→ ReplayFixture
→ MinimizedFalsifier
→ RegressionCorpus
```

而不是每次由人手工构造。

最终应该支持：

```text
production failure
→ automatically export replayable evidence package
```

这会成为比"增加更多 example unit tests"更有价值的资产。

---

## 18. Deployment Reproducibility

Vision validation 已真实遇到：

```text
expected config identity
!= current config identity

expected pipeline revision
!= current pipeline revision
```

而 receipt gate 正确阻止了：

> 用新的 perception pipeline 直接复用旧 validation proof。

这一机制实际上已经属于 Stability Architecture 的一部分：

```text
Code
+
Model
+
Config
+
Pipeline
+
Dependencies
→ Deployment Identity
```

因此未来所有 perception benchmark / corpus / real campaign 都最好携带：

```text
ModelIdentity
ConfigIdentity
PipelineRevision
ReceiptIdentity
```

否则 replay result 很难判断究竟对应哪一套 perception behavior。

---

## 19. GUI-Specific Existing Work

除了通用 CV / tracking 技术，还有几项 GUI parsing 工作值得作为 benchmark。

### 19.1 UIED

UIED 是 GUI element detection 方法，将 GUI parsing 分为 text detection、graphic component detection 和 merging，并强调 merging 算法可被独立修改。

相关研究还系统比较了传统 CV、深度学习和组合方法在 GUI element detection 上的表现，并指出 GUI 元素具有与通用 object detection 不同的定位特性。

对 UniClaw 的价值主要不是直接替换当前 perception，而是：

> UIED 的 explicit text/component/merge decomposition 与我们当前 Fusion 架构高度类似，可以作为 merge/composition algorithm 的参考系。

---

## 20. OmniParser

Microsoft 的 OmniParser 目标是将 GUI screenshot 解析成 structured elements，并特别关注：

- interactable icon detection；
- element semantics；
- action 与 screenshot region 的 grounding。

这与 UniClaw 的问题空间高度相关。

但当前更合理的用途是：

```text
Benchmark
Second Opinion
Offline Comparison
```

而不是：

```text
OmniParser Output
→ Runtime Action Authority
```

因为 UniClaw 对：

- freshness；
- fail-closed；
- source identity；
- action authorization；
- completeness

还有额外 Runtime contract。

---

## 21. ScreenAI

Google ScreenAI 引入 Screen Annotation task，让模型识别 screen 上 UI element 的类型和位置，并使用这些 screen annotations 支撑 UI navigation 等任务。

ScreenAI 证明：

> "把屏幕显式解析成结构化 UI element annotations"是一个有价值的中间表示。

这与未来 UniClaw Stable UI Scene Graph 有概念上的一致性。

但 ScreenAI 属于 VLM-based screen understanding，暂时更适合作为：

```text
Research reference
Benchmark
Advisory semantic candidate source
```

而不是 Runtime authority。

---

## 22. 推荐的长期概念模型

综合当前真实问题和外部方法，可以考虑将当前 perception model 从：

```text
List<ObservedElement>
```

逐渐演进为：

```text
StableUIScene

Occurrence
├── PerceptionRole
├── Geometry
├── EvidenceRefs
└── OccurrenceIdentity

Relation
├── ChildOf
├── LabelOf
├── DescriptionOf
├── DuplicateOf
├── SameRow
└── AdjacentTo

TemporalAssociation
├── PreviousOccurrence
├── CurrentOccurrence
├── Confidence
└── Evidence

SemanticRole
└── ...

Affordance
├── NavigationCandidate
├── LocalControl
├── NonInteractive
└── Unknown
```

关键是：

```text
PerceptionRole
!= Relation
!= SemanticRole
!= Affordance
!= Identity
```

当前很多 bug 都来自这些维度被压在同一个 `type` / pattern decision 中。

---

## 23. 候选 Stability Pipeline

长期可考虑：

```text
Screenshot
   ↓
Detector / OCR
   ↓
Raw Frame Evidence
   ↓
Normalization
   ↓
Frame-local Composition
   ↓
Frame UI Graph
   ↓
Viewport Motion Estimation
   ↓
Temporal Occurrence Association
   ↓
Stable UI Scene Graph
   ↓
Semantic Admission
   ↓
Affordance Reduction
   ↓
Agent
```

横向基础设施：

```text
                 Trace
                   │
     ──────────────┼──────────────
                   │
             Evidence Refs
                   │
     ──────────────┼──────────────
                   │
          Replay / Falsifier
                   │
     ──────────────┼──────────────
                   │
        Property-Based Tests
```

这不是当前设计结论，只是本文依据真实 pressure 得出的候选 architecture shape。

---

## 24. Current Gap Matrix

| Area | 当前已有能力 | 外部可借概念 | 当前 Gap |
|---|---|---|---|
| Geometry | normalized bounds + validity checks | property-based testing | edge precision invariants 未系统化 |
| Frame stability | settle / confirmation | temporal consistency | settle 只证明 frame stable，不证明 role stable |
| Composition | relation-head、uniform-list、ChildOf | Scene Graph | relation model 仍局部化 |
| Role | semantic capability patterns | temporal role transition | 没有正式 RoleTransition contract |
| Identity | StableKey / SourceNormalizer | MOT / data association | 缺显式 SameOccurrence association |
| Scroll continuity | viewport overlap | phase correlation / registration | 当前未正式利用 global motion estimate |
| Matching | local heuristic | Hungarian assignment | 缺全局 association solver |
| Operator routing | bespoke result semantics | pipeline/operator result algebra | ownership contract 尚未统一 |
| Duplicate handling | audited duplicate predicates | graph relation / assignment | 仍以 case-specific predicates 为主 |
| Fault containment | 部分 occurrence containment | failure-domain isolation | exception domain 尚未统一 |
| Trace | Fusion causal trace | OpenTelemetry | 尚未 end-to-end |
| Testing | deterministic fixtures | Hypothesis/FsCheck | invariant fuzzing 不足 |
| Replay | 部分 real→fixture | reproducible corpus | 仍较手工 |
| Deployment | receipt identity | reproducible build/deploy principles | validation/prod receipt lifecycle仍需整理 |
| GUI semantic parsing | YOLO + OCR + capability | OmniParser/UIED/ScreenAI | 尚无系统 A/B benchmark |

---

## 25. 建议直接引入的库

## P0 — 可以近期直接试用

### Hypothesis

用途：

```text
Python perception/fusion property testing
```

推荐程度：

**HIGH**

原因：几乎零 architecture invasion，直接提高 stability test coverage。

---

### FsCheck

用途：

```text
C# Runtime model / geometry / source normalization properties
```

推荐程度：

**HIGH**

特别适合：

```text
bounds
source equivalence
identity invariants
state transitions
```

---

### SciPy `linear_sum_assignment`

用途：

```text
OccurrenceAssociation prototype
Composition assignment experiment
```

推荐程度：

**HIGH FOR PROTOTYPE**

先在 Python perception / validation 中实验即可。

---

### OpenCV phaseCorrelate

用途：

```text
scroll displacement estimation prototype
```

推荐程度：

**MEDIUM-HIGH**

它非常适合快速验证：

> global viewport motion compensation 是否能显著提高 occurrence association 稳定性。

---

## 26. 建议借设计，不急于引库

## ByteTrack / DeepSORT

建议：

```text
borrow data-association architecture
do not embed tracker directly
```

原因：

GUI scroll 与 pedestrian/object tracking 的运动模型不同。

重点借：

- track identity；
- association；
- lost/reacquired；
- measurement-to-track matching。

---

## OpenTelemetry

建议：

```text
align Trace data model first
adopt SDK later if useful
```

重点借：

```text
Trace
Span
Event
Link
Attributes
Status
```

而不是立即把 Runtime observability 全部替换成 OTel。

---

## 27. 暂时不要引入

## OR-Tools CP-SAT

目前 composition constraint 还没有复杂到需要 solver。

先：

```text
explicit relations
+
deterministic predicates
+
assignment
```

如果以后真的出现大量：

```text
cross-object exclusivity
cardinality
global consistency constraints
```

再评估 CP-SAT。

---

## VLM as Runtime Perception Authority

OmniParser、ScreenAI 等可以提供很强的 semantic prior。

但它们不能自动解决：

```text
temporal identity
freshness
determinism
source normalization
fault containment
completion authority
traceability
```

所以当前不应把：

```text
VLM says Button
```

直接升级为：

```text
AUTHORIZED Button
```

---

## 28. 需要未来 Design 回答的问题

本文不回答以下问题，只记录 design gaps。

### GAP-01 Temporal Association Owner

跨帧 SameOccurrence 应由谁拥有？

候选：

```text
Python perception
Runtime canonicalization
独立 association layer
```

这是 authority boundary，需要正式 Decision。

---

### GAP-02 Occurrence Identity Model

是否需要：

```text
TrackId / OccurrenceContinuityId
```

以及它与：

```text
StableKey
SourceOccurrenceIdentity
SourceIdentity
```

分别是什么关系。

---

### GAP-03 UI Scene Graph Representation

是否正式引入：

```text
ChildOf
LabelOf
DescriptionOf
DuplicateOf
SameRow
```

作为 generic relation contract。

---

### GAP-04 Role Transition Contract

需要明确：

```text
什么证据变化允许：
text_block → menu_item

什么变化属于 instability。
```

---

### GAP-05 Operator Result Contract

是否统一：

```text
MATCH
PARTIAL
NOOP
REJECTED
ERROR
```

以及：

```text
Consumed
Produced
Remaining
Reason
```

---

### GAP-06 Fault Containment

需要正式定义：

```text
Occurrence-level
Stage-level
Observation-level
Run-level
```

分别何时 fail closed。

---

### GAP-07 Trace Contract

需要回答：

```text
Span taxonomy
Event taxonomy
EvidenceRef
FactRef
OccurrenceRef
ArtifactRef
```

如何统一。

---

### GAP-08 Replay Contract

需要定义：

```text
RealRunEvidence
→ ReplayFixture
```

最低需要哪些 identity 和 evidence。

---

### GAP-09 Stability Acceptance Metrics

未来不能只看：

```text
Run PASS/FAIL
```

还需要指标，例如：

```text
Role Flip Rate
Occurrence Identity Switch Rate
Duplicate Representation Rate
Unexplained Unknown Rate
Frame Drop Rate
Operator NOOP/Fallback Rate
Replay Determinism Rate
```

---

### GAP-10 Performance Budget

Temporal association / graph construction / tracing 都有成本。

未来设计必须回答：

```text
per-frame latency
memory retention
trace volume
graph size
association search complexity
```

---

## 29. 建议的前置实验

在正式 Stability Architecture Decision 前，建议先做几个小实验。

## Experiment A — Translation Stability Corpus

将同一个完整 row 放在：

```text
viewport top
viewport middle
viewport bottom
```

改变 surrounding inventory，但保持 row-local evidence。

验证：

```text
Role Flip Rate
```

---

## Experiment B — Occurrence Association Prototype

实现：

```text
phaseCorrelate
+
motion compensation
+
linear_sum_assignment
```

只在 offline replay 做。

比较：

```text
current StableKey heuristic
vs
association prototype
```

看 identity switch 是否显著下降。

---

## Experiment C — Explicit Scene Graph Prototype

从现有 Settings frame 生成：

```text
Row
├── Label
├── Subtitle
├── Icon
└── Control
```

暂不让 Runtime 消费。

只看：

> 当前已知 duplicate/child/subtitle cases 能否被一个统一 relation model 表达。

---

## Experiment D — Property Stability Suite

优先覆盖：

```text
geometry
translation
sibling addition
duplicate
ChildOf authority
operator NOOP
source confirmation
```

---

## Experiment E — Trace Completeness

抽一个真实 failure，要求：

> 不打开源码、不进行离线猜测，仅沿 Trace + EvidenceRef 即可定位 First Divergence Point。

如果做不到，就说明 Trace coverage 仍不足。

---

## Experiment F — GUI Parser Benchmark

用同一 corpus 对比三条 pipeline：

```text
UniClaw（当前 YOLO + OCR + Fusion）
UIED-style pipeline
OmniParser
```

不只看 element accuracy。重点指标：

```text
element recall
interactive recall
duplicate rate
parent-child correctness
role stability under viewport translation
false independent affordance
unresolved rate
```

对应 §24 Gap Matrix 中 "GUI semantic parsing 尚无系统 A/B benchmark" 一行；详细设计见附录 C。

---

## 30. 推荐推进顺序

不是立即进行一次大型 refactor。

建议：

```text
P0
继续清当前 Phase 2.6 blocker
+
建立 Property-Based Stability tests

P1
做 Occurrence Association offline prototype

P2
做 explicit UI Scene Graph prototype

P3
整理 Operator Result + Trace contracts

P4
真实 buyer evidence 足够后
→ Stability Architecture Decision

P5
Decision 通过
→ OpenSpec / Design / implementation
```

这样可以避免：

> 因为当前 Settings bug 很多，就提前发明一个过大的 Generic Semantic Engine。

---

## 31. 最核心的 Architecture Pressure

目前所有真实案例最终都指向同一个变化：

当前 perception 更接近：

```text
"What detections exist in this frame?"
```

未来需要逐渐升级成：

```text
"What stable UI objects exist,
how are they composed,
which occurrences persist across observations,
what changed,
and what evidence supports that interpretation?"
```

也就是：

```text
Frame Detection
→ Stable UI World Model
```

但这个 World Model 必须继续遵守：

```text
Fresh Evidence > Historical Continuity

Association != Truth

Semantic Proposal != Authorization

Trace != Authority

Unknown must remain fail-closed
```

---

## 32. External Reference Reading List

### Multi-Object Tracking

**ByteTrack: Multi-Object Tracking by Associating Every Detection Box**
核心价值：data association、trajectory fragmentation、identity continuity。
[ByteTrack paper](https://arxiv.org/abs/2110.06864)

**Simple Online and Realtime Tracking with a Deep Association Metric (DeepSORT)**
核心价值：measurement-to-track association、减少 identity switches。
[DeepSORT paper](https://arxiv.org/abs/1703.07402)

### Scene / Relationship Modeling

**Scene Graph Generation: A Comprehensive Survey**
核心价值：Object + Relationship 的结构化视觉表示。
[Scene Graph survey](https://arxiv.org/abs/2201.00443)

### Image Registration

**OpenCV phaseCorrelate**
核心价值：两幅图之间的 translation estimation，可作为 GUI viewport scroll compensation 原型。
[OpenCV phase correlation documentation](https://docs.opencv.org/4.13.0/d7/df3/group__imgproc__motion.html)

### Assignment / Constraint Solving

**SciPy Linear Sum Assignment**
核心价值：二分图全局最优 occurrence matching。
[SciPy linear_sum_assignment documentation](https://scipy.github.io/devdocs/reference/generated/scipy.optimize.linear_sum_assignment.html)

**Google OR-Tools Assignment / CP-SAT**
核心价值：未来复杂 composition constraints 的候选 solver。
[OR-Tools assignment documentation](https://developers.google.com/optimization/assignment)

### Property-Based Testing

**Hypothesis**
Python property-based testing。
[Hypothesis documentation](https://hypothesis.readthedocs.io/en/latest/)

**FsCheck**
.NET property-based testing。
[FsCheck documentation](https://fscheck.github.io/FsCheck/QuickStart.html)

### Trace

**OpenTelemetry Trace API Specification**
核心价值：Span / Event / Link / Attribute / Status 数据模型。
[OpenTelemetry Trace API](https://opentelemetry.io/docs/specs/otel/trace/api/)

### GUI Parsing

**OmniParser for Pure Vision Based GUI Agent**
核心价值：GUI screenshot parsing、interactable element detection、semantic grounding。
[OmniParser paper](https://arxiv.org/abs/2408.00203)

**Object Detection for Graphical User Interface: Old Fashioned or Deep Learning or a Combination? / UIED**
核心价值：GUI-specific detection、text/component merging。
[UIED research paper](https://arxiv.org/abs/2008.05132)

**ScreenAI: A Vision-Language Model for UI and Infographics Understanding**
核心价值：Screen Annotation、UI type/location understanding、UI navigation research。
[ScreenAI paper](https://arxiv.org/abs/2402.04615)

---

## 33. 文档定位

建议最终保存为：

```text
docs/analysis/runtime-stability-engineering-landscape.md
```

理由：

```text
Decision
→ 我们决定怎么做

Design
→ 我们准备怎么实现

Spec
→ 系统必须满足什么

Analysis
→ 我们目前知道什么、外部有什么、还缺什么
```

本文显然属于第四种。

未来可以形成：

```text
docs/analysis/
  runtime-stability-engineering-landscape.md
        ↓
真实实验 / buyer evidence
        ↓
docs/decisions/
  runtime-stability-architecture.md
        ↓
openspec/changes/
  ...
        ↓
design / implementation
```

所以这份文档的生命周期应该是：

> **持续更新的设计输入，而不是需要"毕业"的 contract。**

等真正产生 Stability Architecture Decision 后，这份 Analysis 可以保留，作为"为什么最终设计走到这里"的研究和证据背景。

---

# 附录

> 以下附录由旧稿（`docs/anaylzer/runtime-stability-engineering-landscape.md`，76KB 草稿）独有内容压缩合并而来；性质与正文一致：ANALYSIS / REFERENCE / NON-NORMATIVE，Authority: NONE。旧稿归档后，本文是唯一副本。

## 附录 A — 当前已有的 Stability 基础盘点

> 来源：旧稿 §3。以下能力在进入 Stability Architecture Design 前**已经存在**，不是从零开始；但大多随真实 blocker 局部成长，尚未组织成统一 Stability Model。

| # | 已有能力 | 说明 |
|---|---|---|
| 1 | Vision-first perception | Vision 是 primary perception / grounding evidence |
| 2 | structured hierarchy 作为 auxiliary corroboration | 只作佐证，≠ 独立 action authority |
| 3 | canonical occurrence | 规范化后的观察对象模型 |
| 4 | StableKey | 感知层稳定行标识，用于文字连续性的弱证明 |
| 5 | source occurrence / source / destination identity 区分 | 已区分 `SourceOccurrenceIdentity` / `SourceIdentity` / `DestinationIdentity` |
| 6 | `ChildOf` generic composition | 已被真实 Search icon buyer evidence 购买 |
| 7 | duplicate-primary-row reconciliation | 已实现 `IsDuplicatePrimaryRowRendering` 等 audited predicate |
| 8 | Vision control type 与最终 affordance 分离 | `VisualRole != Affordance` 已成立 |
| 9 | source equivalence normalization | `SourceEquivalenceNormalizer`（含 logical-order projection） |
| 10 | viewport settle / scroll stability | settle 只证明 frame stable，不证明 role stable |
| 11 | adaptive exploration | 自适应滚动步长 / adaptive revisit recovery 可用 |
| 12 | fusion operator routing + validator | 已存在 router + validator 机制，但结果语义仍 bespoke |
| 13 | Unknown fail-closed | 不确定时保持 fail-closed |
| 14 | stage evidence capture | 真实 campaign 可 dump stage evidence 帧 |
| 15 | Fusion causal trace | 已产生真实调试价值（见正文 §15） |
| 16 | validation-scoped deployment receipt | `current-active-identity.json` 类 receipt，阻隔旧 proof 复用 |
| 17 | real run → replay falsifier 调试方法 | Real Evidence → Offline Replay → Deterministic Falsifier 已实践 |
| 18 | occurrence-level semantic fault containment 先例 | 已有先例，但 exception domain 尚未统一 |

**已冻结边界**（正文 §2 已重申，此处保留事实）：`Vision = primary`；`structured hierarchy = auxiliary != action authority`；`HISTORICAL_ASSOCIATION != CURRENT_WORLD_TRUTH`、`TEMPORAL_CONTINUITY != ACTION_AUTHORITY`、`TRACE != CONTROL`、`TRACE != SEMANTIC_AUTHORITY`。

> 核对注：表格第 4/5/6/7/9/11/14/16 项（StableKey、SourceEquivalenceNormalizer、SourceGroundingValidator、ChildOf、IsDuplicatePrimaryRowRendering、adaptive step/revisit、stage evidence dump、current-active-identity.json）已在 `src/` 中 grep 确认；其余项由正文 §4–§18 直接佐证。

---

## 附录 B — 外部机制与方法对照（按 Gap Family）

> 每条含：机制要点（压缩，去重复论证与"人话版"）+ 原始引用。仅收录正文已删除或仅部分覆盖的内容；正文已述的概念只给一行指引与增量引用。

### Family A — Geometry / Numerical Stability（对应正文 §3）

**A1. IEEE 754 / 浮点鲁棒性 ——"数学等价"≠"浮点等价"**
- 浮点运算每步舍入：`right` 与 `left + (right - left)` 在有限精度下不保证严格相等。
- 工程做法：明确坐标 representation 与 precision；避免"先低精度损失性运算、再提升精度"；关键 boundary predicate 用数值鲁棒实现；geometry conversion 收敛到少数共享 primitive。
- 对应旧稿 `float32(X2-X1) → double` 的 false out-of-bounds（正文 §3.1）。
- 参考：David Goldberg, *What Every Computer Scientist Should Know About Floating-Point Arithmetic*, 1991 — https://doi.org/10.1145/103162.103163

**A2. Robust Geometric Predicates —— 关键判断比普通算术更需要可靠性**
- 计算几何长期面对"输入合法但浮点舍入让 orientation/intersection/bounds predicate 误判"；Shewchuk 的 adaptive precision 先走快速浮点路径，仅在接近数值不确定边界时提精度。
- CGAL 将 "Exact Predicates / Inexact or Exact Constructions" 工程化。
- 对 UniClaw 的启发不是立即引入 arbitrary precision，而是收敛为 `Raw Bounds → Canonical Geometry Conversion → Shared Predicate`，避免每层自己做 x2-x1、clamp、epsilon。
- 参考：Shewchuk, *Adaptive Precision Floating-Point Arithmetic and Fast Robust Geometric Predicates*, 1997 — https://people.eecs.berkeley.edu/~jrs/papers/robustr.pdf；CGAL Manual *Robustness Issues* — https://doc.cgal.org/latest/Manual/devman_robustness.html

**A3. Property-Based Testing（补充引用）**
- 工具与概念已在正文 §14 覆盖（Hypothesis / FsCheck）；此处仅补源头文献。
- 参考：Claessen & Hughes, *QuickCheck: A Lightweight Tool for Random Testing of Haskell Programs*, 2000 — https://dl.acm.org/doi/10.1145/357766.351266

### Family B — Frame-Local Role Stability（对应正文 §4）

**B1. Tracking-by-Detection —— 单帧分类与跨帧身份分层**
- SORT / DeepSORT / ByteTrack 均不让每帧 detection 单独决定"对象是谁"；对 UniClaw 最值得借的分层是 `Frame-local role != Temporal occurrence identity`。
- 同一 occurrence 平移后 `menu_item → text_block`（正文 §4），应能回答"是 fresh evidence 变了，还是 frame-local composition 变了"。
- 参考：SORT, 2016 — https://arxiv.org/abs/1602.00763（正文 §32 未列此篇）；DeepSORT, 2017 — https://arxiv.org/abs/1703.07402；ByteTrack, 2021 — https://arxiv.org/abs/2110.06864

**B2. Metamorphic Testing —— 对"语义保持的输入变换"测试输出不变量**
- 定义输入变换（完整 row 从 viewport 中部平移到底部、row-local evidence 不变）+ metamorphic relation（Role 保持一致或一致 fail-closed）；同样可测"增加 unrelated sibling → 已有 occurrence role 不变"。
- 比给每个屏幕位置写独立 golden fixture 更能捕捉真正 stability contract；直接适用正文 §4 的 `Display @ middle → menu_item` vs `Display @ bottom → text_block`。
- 参考：Chen et al., *Metamorphic Testing: A Review of Challenges and Opportunities*, 2018 — https://doi.org/10.1145/3143561；Segura et al., *A Survey on Metamorphic Testing*, IEEE TSE, 2016 — https://doi.org/10.1109/TSE.2016.2532875

**B3. Guarded State Transition —— 角色变化要有原因**
- 成熟状态机表达：`CurrentState + Event/Evidence + Guard → NextState`；候选 `RoleTransition { From, To, EvidenceDelta, Reason }`。
- 要点：不是把历史 role 变 truth，而是 `text_block → menu_item` 必须能解释"新获得了什么 fresh evidence"；反对 `StableKey 相同 → Role 永不改变` 的硬绑定。

### Family C — Composition Stability（对应正文 §5）

**C1. Scene Graph（补充引用与表达要点）**
- 概念见正文 §11；补充 `Object + Attributes + Relationships` 分离表达，与 UniClaw `VisualRole != CompositionRelation != Affordance` 高度一致（`Icon --ChildOf--> SearchActionBar`、`Text --LabelOf--> PreferenceRow` 等）。
- 参考（正文 §32 未列的第一篇 survey）：Chang et al., *A Comprehensive Survey of Scene Graphs: Generation and Application*, 2021 — https://arxiv.org/abs/2104.01111；Zhu et al., 2022 — https://arxiv.org/abs/2201.00443

**C2. Relation Proposal + Reconciliation —— 局部关系先作为候选，不直接成为 truth**
- 视觉图模型常见流程：`Generate relation candidates → score/validate → reconcile conflicts → accepted relations`。
- 对 UniClaw 映射：`ChildOf / LabelOf / DescriptionOf / DuplicateOf` candidate 与最终 Runtime semantic admission 保持分离，减少"局部 predicate 一命中 → 直接破坏原 visual type / authority"。
- 参考：Scene Graph survey — https://arxiv.org/abs/2201.00443

**C3. GUI-specific Detection + Merge —— UI 本身就需要独立 composition 层**
- UIED 系工作把 GUI parsing 拆成 Text Detection + Graphic Component Detection + Merging，说明 GUI 领域并不认为 detector output 就是最终 UI object；对当前 `row + icon + title + subtitle + checkbox` 问题应参考 GUI-specific merge/composition，而不是把问题全塞进 detector label。
- 参考：Chen et al., 2020 — https://arxiv.org/abs/2008.05132；UIED repository — https://github.com/MulongXie/UIED

**C4. OmniParser**
- 概念与定位（benchmark / second opinion，不直接作为 authority）见正文 §20 与 §27。
- 参考：Lu et al., 2024 — https://arxiv.org/abs/2408.00203

### Family D — Duplicate / Multi-Representation Stability（对应正文 §6）

**D1. NMS / Soft-NMS —— 先承认"一个物体可能产生多个 detection"**
- Object Detection 长期面对 `one physical object → multiple overlapping detections`；NMS 保留高置信框并抑制重叠框，Soft-NMS 按 overlap 衰减 score 而非直接删除。
- 意义："多 detection ≠ 多世界对象"是成熟领域基本问题；简单 hard suppression 有副作用。
- 参考：Bodla et al., *Soft-NMS — Improving Object Detection With One Line of Code*, ICCV 2017 — https://arxiv.org/abs/1704.04503

**D2. Weighted Boxes Fusion —— 有时应融合表示，而不是只删除**
- WBF 把多个 detector 对同一 object 的 box 聚合成更稳定的 box；对 UniClaw 的启发：先确定多个 representation 是否指向同一 physical object，再决定 reconcile 方式。
- 参考：Solovyev et al., 2019 — https://arxiv.org/abs/1910.13302

**D3. 为什么不能直接照搬 NMS/WBF**
- UI 中 menu row / checkbox / subtitle / icon 可高度 overlap 却是真实 parent/child，不是 duplicate：`high IoU != Duplicate`、`same text != Duplicate`、`same row != automatically Duplicate`。
- 需结合 geometry、row identity、text、relation、independent interaction evidence 综合判定。

**D4. Assignment / Entity Resolution —— 从"框去重"升级到"representation 对 physical object 的归属"**
- 把 detector row / OCR title / structured row / toggle / icon 视为多个 representations，全局匹配到 candidate physical objects；避免两个局部 predicate 把同一 evidence 归给不同 parent。
- 参考：SciPy `linear_sum_assignment` — https://docs.scipy.org/doc/scipy/reference/generated/scipy.optimize.linear_sum_assignment.html；Scene Graph survey — https://arxiv.org/abs/2201.00443

### Family E — Temporal / Occurrence Identity（对应正文 §7）

**E1. Tracking-by-Detection —— track identity 独立于 detector class**
- 概念见正文 §8；增量要点：`track_id` 表达跨帧 continuity，不等于 detector label，也不等于业务身份 → `StableKey != Temporal Occurrence Identity != Logical SourceIdentity`。
- 参考：SORT — https://arxiv.org/abs/1602.00763；DeepSORT — https://arxiv.org/abs/1703.07402；ByteTrack — https://arxiv.org/abs/2110.06864

**E2. Viewport Motion Compensation**
- 概念与结论见正文 §9（phase correlation 只作 association evidence，不作 authority）。
- 参考：OpenCV *Motion Analysis and Object Tracking*（phaseCorrelate）— https://docs.opencv.org/5.0/main_modules/imgproc_motion.html

**E3. Hungarian / Linear Assignment**
- 概念与 cost 矩阵构成见正文 §10（geometry + IoU + text + type + StableKey + relation 兼容惩罚），此处不重复。

**E4. Association Metrics —— detection accuracy 与 identity accuracy 应分开**
- MOT 领域单独衡量 ID Switch、Fragmentation、IDF1、HOTA / Association Accuracy；HOTA 把 detection、association、localization 分开评估。
- 未来 UniClaw 不应只看"元素检测对不对"，还要看"跨帧是不是同一个对象"。
- 参考：Luiten et al., *HOTA: A Higher Order Metric for Evaluating Multi-Object Tracking*, 2020 — https://arxiv.org/abs/2009.07736；MOTChallenge — https://motchallenge.net/

### Family F — Operator Ownership / Fallback（对应正文 §13）

**F1. Parser Combinator —— Consumed vs Empty 是 fallback 的一等语义**
- Parsec 的 alternative 语义：`fail without consuming input → alternative can run`；`fail after consuming input → alternative does not automatically run`；核心是 `Attempted != Consumed`。
- 映射 Fusion：NOOP → 无消费 → fallback eligible；PARTIAL → 只消费子集 → fallback 只见 Remaining；MATCH → ownership；ERROR → explicit failure disposition。比"operator 被选中就算 ownership"安全得多。
- 参考：Leijen & Meijer, *Parsec: Direct Style Monadic Parser Combinators for the Real World*, 2001 — https://www.microsoft.com/en-us/research/publication/parsec-direct-style-monadic-parser-combinators-for-the-real-world/；Parsec `try`/alternative — https://hackage.haskell.org/package/parsec/docs/Text-Parsec-Prim.html

**F2. Chain of Responsibility —— 处理不了就继续传递**
- `Handler A → handled? stop : pass to B`；正文 §13 真实 bug（uniform-list 被选中却 NOOP 后 relation-head 被跳过）违反的正是 `selected != handled`。
- 参考：https://refactoring.guru/design-patterns/chain-of-responsibility

**F3. Compiler Pass Manager —— 明确 pass 的结果、依赖和 invalidation**
- LLVM Pass Manager 的价值不只是按顺序调用，而是显式管理 pipeline、analyses、preserved/invalidated state、pass dependencies。
- 对应原则：后一个 operator 不应靠猜测前一个 operator 到底做了什么。
- 参考：LLVM New Pass Manager — https://llvm.org/docs/NewPassManager.html

> 注：候选 `OperatorResult { MATCH|PARTIAL|NOOP|REJECTED|ERROR; Consumed/Produced/Remaining/Reason }` 结构已在正文 §13 给出，不重复。

### Family G — Fault Containment（对应正文 §16）

**G1. Bulkhead Pattern —— 显式隔离故障域**
- 原则：局部组件失败不无边界扩散；UniClaw 适合隔离 Occurrence / Operator / Observation / Run 四级。
- one malformed occurrence 默认先判 occurrence-local invalidity，而不是自动升级成整帧 evidence 清空。
- 参考：Azure Architecture Center, *Bulkhead Pattern* — https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead

**G2. Erlang/OTP Supervision —— failure scope 应是模型的一部分**
- supervision tree + `one_for_one` 等 restart strategy 明确"哪个 worker 失败影响到哪一级"；把 Occurrence / Operator / Observation / Run failure 正式分级。
- 参考：Erlang/OTP Design Principles — https://www.erlang.org/doc/system/design_principles.html；`supervisor` — https://www.erlang.org/doc/apps/stdlib/supervisor.html

**G3. Typed Error Disposition —— 不要 `catch(Exception) { continue; }`**
- 候选错误模型：`Failure { Domain, Severity, Recoverability, EvidenceRefs, Disposition }`；典型 disposition：`DROP_OCCURRENCE / RETRY_STAGE / FAIL_OBSERVATION / FAIL_RUN`。
- 补充原则：`DIAGNOSTIC_FAILURE != RUNTIME_BEHAVIOR_CHANGE`；真正的 global inconsistency 仍必须 fail-closed。

### Family H — Trace / Decision Provenance（对应正文 §15）

**H1. OpenTelemetry Trace API**
- 数据模型（Trace/Span/Event/Link/Attributes/Status）已在正文 §15 覆盖，不重复。
- 参考：https://opentelemetry.io/docs/specs/otel/trace/api/

**H2. Semantic Conventions —— Trace 字段也要有统一词汇**
- 各模块各写 `reason / why / decisionReason / router_note` 会导致无法跨模块查询 First Divergence；建议统一为 `fusion.operator.name / fusion.operator.status / fusion.remaining.count / occurrence.ref / evidence.ref / decision.reason`。
- 参考：https://opentelemetry.io/docs/concepts/semantic-conventions/

**H3. Trace 与 Artifact 分离 + 语义四层映射**
- 大体积数据（screenshot、raw YOLO、stage views）不复制进 Span，通过 `EvidenceRef / ArtifactRef / OccurrenceRef / FactRef` 关联（正文 §15 的 `TRACE != ARTIFACT STORAGE` 一致）。
- 语义映射：Span = 有持续时间的 operation；Event = point-in-time decision；Link/Ref = causal evidence/occurrence/fact 关系；Artifact = 大体积数据单独存储。

**H4. Sampling / Selective Retention —— 生产 Trace 必须有成本模型**
- Normal success → compact / sampled trace；Unknown / failure / invariant violation → 更丰富的 causal trace + artifact refs。
- 参考：https://opentelemetry.io/docs/concepts/sampling/

### Family I — Replayability（对应正文 §17）

**I1. Record / Replay —— 先稳定回放同一次失败**
- `rr` 的目标是 record nondeterministic execution → deterministic replay；UniClaw 不需要 CPU/syscall 级 replay，借核心目标：`Real Runtime Failure → Evidence Package → Deterministic Perception/Semantic Replay`。
- 参考：rr Project — https://rr-project.org/；Geels et al., *Replay Debugging for Distributed Applications*, USENIX 2006 — https://www.usenix.org/legacy/events/usenix06/tech/geels/geels_html/index.html

**I2. Delta Debugging —— 把复杂真实失败缩成最小 falsifier**
- 解决"复杂 failing input 中哪些最小条件是 failure-inducing"；与当前人工流程（38 candidates 真实帧 → 抽几行/bounds → deterministic falsifier）高度一致。
- 未来可自动化：`RealEvidencePackage → Remove irrelevant evidence → Replay → Keep failure-inducing subset → Minimal falsifier`。
- 参考：Zeller & Hildebrandt, *Simplifying and Isolating Failure-Inducing Input*, IEEE TSE, 2002 — https://www.st.cs.uni-saarland.de/papers/tse2002/

**I3. Property Shrinking —— 生成式失败也应自动最小化**
- Hypothesis / QuickCheck 在 property failure 后自动 shrink；与 Delta Debugging 互补：Real failure → Delta-minimized replay；Generated failure → Property shrink。

**I4. Replay package 最低内容（GAP-08 增量）**
- candidate 内容：deployment identity、raw/normalized/fused evidence、operator decisions、canonical occurrences、semantic admission、accepted reason、terminal reason。
- 未决问题：保存所有 stage output，还是保存足以重算 stage output 的上游 evidence。

### Family J — Deployment Reproducibility（对应正文 §18）

**J1. Reproducible Builds —— proof 必须绑定可重建输入**
- 相同 source、build instructions 和 environment 应尽可能重建相同 artifact；UniClaw 对应问题："这次真实 campaign 到底证明了哪一个 model/config/pipeline/runtime 组合？"
- 参考：https://reproducible-builds.org/docs/definition/

**J2. SLSA Provenance —— 记录 artifact 是怎么产生的**
- SLSA Build Provenance 记录 build definition、parameters、resolved dependencies、builder identity、output subject。
- 与当前 receipt 接近：`Model + Config + Pipeline + Dependencies + Runtime Revision → Deployment Identity → Validation Receipt`。
- 参考：https://slsa.dev/spec/v1.2/build-provenance

**J3. in-toto —— 每个 supply-chain step 都携带 materials/products/provenance**
- 不只相信最终 artifact 名字，而要知道它由哪些材料、步骤和 actor 产生。
- 参考：https://in-toto.io/；Torres-Arias et al., *in-toto: Providing farm-to-table guarantees for bits and bytes*, USENIX Security 2019 — https://www.usenix.org/conference/usenixsecurity19/presentation/torres-arias

**J4. Content-Addressed Identity —— 用 digest 而不是名字证明"还是同一个东西"**
- 对 model weights、config、pipeline revision、evidence package 使用 hash/digest，避免"文件名没变，内容已漂移"；当前 receipt 的 config/pipeline identity 属正确方向。

### Future Design Questions 增量（旧 §31 独有、未进正文）

- **GAP-02 —— 候选四层 identity 模型**：`DetectionId`（单帧 detector output）/ `OccurrenceId`（当前 canonical occurrence）/ `ContinuityId`（跨帧 association）/ `LogicalSourceId`（Runtime logical source）；约束 `ContinuityId != automatically LogicalSourceId`；`StableKey` 是 association feature 还是 identity component，需 buyer evidence。
- **GAP-10 —— Performance budget 业界手法**：① Gating before matching：先用 geometry/motion 排除不可能 candidate，再做 Hungarian 等较贵匹配；② Bounded temporal state：只保留最近 N 帧 / 当前 viewport 所需 track state；③ Sampling / selective diagnostics：成功路径 compact，failure/Unknown 路径保留 richer trace。参考 SORT/DeepSORT/OTel Sampling。

---

## 附录 C — 补充实验与指标

### Experiment F — GUI Parser Benchmark（正文实验仅 A–E）

同一 corpus 对比三条 pipeline：

```text
UniClaw（当前 YOLO + OCR + Fusion）
UIED-style pipeline
OmniParser
```

不只看 element accuracy。重点指标：

- element recall；
- interactive recall；
- duplicate rate；
- parent-child correctness；
- role stability under viewport translation；
- false independent affordance；
- unresolved rate。

对应正文 §24 Gap Matrix 的 "GUI semantic parsing 尚无系统 A/B benchmark" 缺口，是该行 gap 的对照实验设计。

### GAP-09 — Stability Acceptance Metrics 定义细节（正文 §28 GAP-09 扩展）

候选指标（含正文 §28 GAP-09 未列的 Association Conflict Rate）：

| 指标 | 说明 |
|---|---|
| Role Flip Rate | 同一对象跨 viewport 的 role 变化率（对 translation 过敏度） |
| Occurrence Identity Switch Rate | 跨帧身份切换率 |
| Duplicate Representation Rate | 同一物理对象的重复表示占比 |
| Unexplained Unknown Rate | 无解释的 Unknown 占比 |
| Frame Drop Rate | 整帧 evidence 丢弃率 |
| Operator NOOP/Fallback Rate | operator 空转/回退率 |
| Replay Determinism Rate | 回放确定性比率 |
| Association Conflict Rate | 同一 evidence 被多个候选归属导致的冲突率（扩展新增） |

**业界分解方法**（HOTA / MOTChallenge）：detection accuracy ≠ association accuracy；HOTA 将 detection、association、localization 分开评估；MOTChallenge 长期使用 ID Switch、Fragmentation、IDF1。对 UniClaw 借该分解，把指标按五维拆分：Perception quality / Association quality / Role stability / Composition quality / Runtime admission stability。

未决问题：哪些指标真正与 Runtime completion / safety 相关，需通过真实 campaign 验证，而不是为 metric 而 metric。

### 其它指标 / 评测方法论要点（未进正文）

- **Experiment A（Translation Stability Corpus）指标**：Role Flip Rate，衡量同一完整 row 在 viewport top/middle/bottom + 改变 neighboring inventory 下 role 是否对 translation 过敏。
- **Experiment B（Occurrence Association Prototype）对比指标**：identity switch rate、false association、duplicate source rate、role transition explainability（current StableKey/source heuristic vs motion-compensated assignment）。
- **Experiment E（Trace Completeness）判定法**：若仅凭 Trace + EvidenceRef 无法定位 First Divergence Point，则记 `TraceCoverageGap = PRESENT`。
- **旧 §25 Gap Matrix 独有列 "业界典型方法"**（正文 §24 三列版已删除该列及 Global constraints 行）：

| Area | 业界典型方法 |
|---|---|
| Geometry | robust predicates、adaptive precision、Hypothesis/FsCheck |
| Frame settle / role | MOT continuity、metamorphic testing、guarded role transition |
| Composition | Scene Graph、GUI component merging、relation reconciliation |
| Duplicate | WBF/NMS 思想 + assignment + `DuplicateOf` relation |
| Identity | motion compensation + Hungarian / linear assignment |
| Operator routing | Parsec consumed/empty semantics、explicit OperatorResult |
| Fault containment | Bulkhead、OTP supervision、typed error disposition |
| Trace | OpenTelemetry Span/Event/Link + semantic conventions |
| Replay | rr、Delta Debugging、property shrink |
| Deployment | SLSA、in-toto、Nix reproducible builds |
| GUI parsing | UIED、OmniParser、ScreenAI |
| Global constraints | linear assignment；复杂后再评估 CP-SAT（当前无明确 buyer） |

---

## 附录 D — 一句话总结（旧 §35）

> 如果只看当前 Settings，我们像是在修：checkbox、icon、Display、Safety、bounds、scroll。
>
> 但如果把这些问题放在一起看，真正需要建设的是：
>
> **一个能把单帧视觉证据稳定组合成 UI 对象、能跨 viewport 维持对象连续性、能解释角色变化、能隔离局部故障、能回放真实失败、并且每一步都可 Trace 的 Runtime Stability Substrate。**
>
> 这才是这一轮 Phase 2.6 调试最值得留下来的长期资产。