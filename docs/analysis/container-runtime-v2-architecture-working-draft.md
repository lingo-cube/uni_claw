# Container Runtime V2 Architecture Working Draft

> Status: **WORKING DRAFT / NON-NORMATIVE**
> Scope: UniClaw Runtime Core，面向 Phase 2.6 真实设备遍历结果进行架构收敛。
> Purpose: 在不扩大 Runtime Authority 的前提下，将 Phase 2.6 暴露出的身份、跨 Container 转移、局部页面聚合、误入/误点纠正与 Fast/Slow 协作问题收敛为一套职责单一、流转闭环、可验证、可逐步购买的 Runtime Core V2。

---

## 0. Executive Summary

Phase 2.6 已经给出一个非常明确的信号：当前 Runtime 的主要问题不再只是“某一个 OCR、某一条结构化规则、某一种 row normalization 没做好”，而是**局部感知修补的收益正在下降，而 blocker 在不同层之间迁移**。

真实设备验证中：

- fresh real runs 累计 **19 次，0/19 Completed**；
- 最深 run 已能进入约 **4 层**，说明基础感知、遍历和返回能力并非完全失效；
- 多轮修补后 blocker 从 checkbox/switch、relation-head、source normalization、float projection、row representation、phantom satellites、occurrence semantic view、row-band subelements、return controls、title-off identity 等位置不断迁移；
- r5 出现“预期仍在 Display，但 fresh Observation 已回到 Settings Root”的真实异常，说明 `EXPECTED_TRANSITION != OBSERVED_TRANSITION` 必须被 Runtime 原生表达；
- Z4 暴露 StableKey 的 container scope 污染；进一步尝试通过 Tap/ScrollBackward 推导 transition 又被证明不可靠，形成 `MISSING_TRANSITION_OBSERVABILITY_SEAM`；
- Z5 `LoO`、Wallpaper/Bluetooth 等案例显示 Vision/OCR 仍有不确定性；UI-TARS-2B shadow 在 role 上出现约 36.4% false promotions，证明“大模型/视觉模型输出不能直接成为 authority”；
- Z7 的 Observation integrity campaign 38/38 healthy，而真实 blocker 转移到 deep Unknown，说明 capture 并非唯一主因；
- BGE-small 在已有 evaluation 上表现很好，说明**低延迟语义相似度有潜力成为 Fast verification prior**，但 `VECTOR_MATCH != CONTAINER_IDENTITY_TRUTH` 仍必须冻结。

因此，本 Draft 提议将 Runtime Core 收敛为两个核心世界概念：

```text
ContainerGraph
CurrentContainer
```

其他对象只用于描述一次运行中的局部事实或生命周期：

```text
Slice / LocalModel
Transition
Fast Assessment
Slow Assessment
Entry Context
```

核心目标不是构造更多 FSM 或 ontology，而是建立以下闭环：

```text
Action Context
    +
Fresh Observation
    ↓
Working Graph Node / CurrentContainer
    ↓
Fast low-latency interpretation
    +
Slow async high-confidence interpretation
    ↓
Graph / Node semantic reconciliation
    ↓
UniAgent recomputes remaining obligation
    ↓
继续执行
```

本方案的一个关键原则是：

> **Runtime 承认“物理上已经发生了什么”，Fast/Slow 只帮助理解“它意味着什么”，UniAgent 决定“接下来怎么办”。**

---

# 1. Evidence Classification: 哪些是 Phase 2.6 直接购买，哪些是架构假设

本 Draft 不把所有新设计都伪装成 Phase 2.6 的必然结论。需要明确区分三类。

## 1.1 Evidence-backed Buyers — Phase 2.6 已直接购买

以下能力有真实问题作为 buyer：

### B1. Expected transition 与 observed world 必须分离

**Evidence:** r5。Runtime 预期仍在 Display，但 fresh Observation 已是 Settings Root。

因此必须冻结：

```text
EXPECTED_TRANSITION != OBSERVED_TRANSITION
RETURN_EXPECTATION != RETURN_TRUTH
ACTION_EXPECTATION != WORLD_TRUTH
```

Runtime 不能通过“刚才点了什么”来证明“现在一定在哪里”。

---

### B2. Container identity 与一次当前 occurrence 必须分离

**Evidence:** Z4 StableKey contamination、旧 run-global known_rows 污染、unresolved child first frame。

必须冻结：

```text
SourceOccurrenceIdentity != logical SourceIdentity
BOUNDS != ITEM_IDENTITY
ORDINAL != ITEM_IDENTITY
TEXT != IDENTITY
```

Fresh occurrence 用于当前动作 grounding；Graph/LocalModel 用于结构和语义先验。

---

### B3. Graph Node 不能假设唯一 Parent

**Evidence:** Android/Settings 多入口场景本身即可作为设计 falsifier；Phase 2.6 的 return / entry behavior 也持续表明历史路径不能替代 current context。

必须支持：

```text
A --X→ D
B --Y→ D
```

同一个 D 的 return expectation 取决于**本次进入路径**，而不是 `D.Parent`。

冻结：

```text
NODE_HAS_NO_CANONICAL_PARENT
RETURN_IS_PATH_RELATIVE
```

---

### B4. CurrentContainer 必须承认“现在实际站在哪里”

**Evidence:** r5 reconciliation gap。

如果 fresh accepted evidence 已表明进入另一个独立 Container，Runtime 不能因为 identity 尚未证明，就继续把执行世界留在旧 Container。

冻结：

```text
CURRENT_PHYSICAL_CONTAINER
!= TRUSTED_SEMANTIC_IDENTITY
```

也就是说，Node 可以先存在、先承载 LocalModel，再逐步形成可信语义。

---

### B5. Container local coverage 与 semantic resolution 必须分离

**Evidence:** deep Unknown、OCR/Vision 不确定、Phase 2.6 中“已经滚过但某个 item 仍然 Unknown”的反复阻塞。

冻结：

```text
COVERAGE_COMPLETE != SEMANTICALLY_RESOLVED
ContainerComplete != SubtreeComplete
```

“我已经把这一页看完”不等于“每个 Item 都理解完”。

---

### B6. Fast semantic match 不得成为 world truth

**Evidence:** BGE / VLM shadow 结果。

BGE 可以很好地做低延迟 candidate ranking；UI-TARS/VLM 也能提供更强语义理解，但都不能越权。

冻结：

```text
VECTOR_MATCH != CONTAINER_IDENTITY_TRUTH
VLM_PROPOSAL != WORLD_TRUTH
SLOW_ASSESSMENT != FRESH_WORLD_TRUTH
```

---

## 1.2 Architecture-pressure Buyers — Phase 2.6 强烈支持，但不是唯一解

### B7. ContainerGraph 应成为 Runtime 的长期结构工作模型

Phase 2.6 多次重复探索相似 Settings 世界；如果每次都从零识别 Source/Destination/Return/children，成本和误差都会重复出现。

Graph 的购买目的：

- 复用已观察到的 Container 与 Entry relation；
- 为 Fast 提供候选 prior；
- 保存当前 Run 的已知结构；
- 支持 path-relative return；
- 支持 checkpoint / recovery reasoning；
- 为未来 Environment Memory 提供结构骨架。

这不是说 Graph 是 planner。必须冻结：

```text
CONTAINER_GRAPH != NAVIGATION_PLANNER
KNOWN_EDGE != ACTION_AUTHORIZATION
HISTORICAL_RELATION != CURRENT_WORLD_TRUTH
```

**必要性理由：** 当前问题不只是“识别某一个屏幕”，而是 Runtime 无法稳定表达“同一个 Container 的多个入口、这次从哪里进入、当前观测与预期不一致、如何在后续继续复用”。这些都属于结构世界模型问题。

**Falsifier:** 如果在不引入 Graph 的情况下，Phase 2.6 的 multi-entry、return、unknown destination、重复验证均能以更简单且同样可靠的结构表达，则 Graph 可缩减。

---

## 1.3 Strategic Architecture Hypotheses — 有明显价值，但需要显式促进购买

### H1. Fast + Slow 并行 semantic interpretation

Phase 2.6 并不能严格证明“必须有 Fast/Slow 双路径”。它证明的是：

1. 低成本 perception / deterministic patching 仍有价值；
2. 但 blocker 在不同 perception / semantic / transition 层之间迁移；
3. 单纯增加局部规则已经出现边际收益下降；
4. BGE 类 Fast semantic prior 已显示低延迟价值；
5. 大模型/VLM 对广告、弹窗、跑偏、页面语义理解明显比纯 embedding 更适合，但不能进入 authority path。

因此提出：

```text
Fast = low-latency working interpretation
Slow = higher-confidence async semantic interpretation
```

而不是：

```text
Fast failed → then call Slow
```

更推荐：

```text
Fresh useful Observation
     ├─ Fast
     └─ Slow Async
```

### 购买目的

- Fast 保证主循环延迟；
- Slow 对 Fast 做 Confirm / Challenge；
- Slow 能识别广告、loading、overlay、external/off-path 等复杂 Scene；
- Slow 可纠正“刚才点的是哪个父容器 Item / 实际进入的是哪个 child”；
- UniAgent 根据纠正后的语义重新计算 traversal obligation，而不是 Runtime 回滚整个 Run。

### 必要性理由

如果没有 Slow：

- deep Unknown / OCR ambiguity 仍然需要继续加局部规则；
- Fast semantic error 很难被独立校验；
- “广告/错误页面/系统弹窗/跑偏”只能被强行塞进 identity pipeline；
- traversal 错点后的局部 obligation 修正缺少高语义密度输入。

### 有界性

Slow 不是第二个 UniAgent：

```text
Slow can:
- assess scene
- verify/correct container semantics
- verify/correct trigger semantics
- challenge relation interpretation
- propose disposition / recovery hint

Slow cannot:
- authorize action
- mutate CurrentContainer directly
- mutate Graph directly
- declare Goal complete
- execute recovery
- own traversal planning
```

### Falsifiers

- Slow 无法显著降低 Phase 2.6 deep Unknown / wrong-branch / transient blockers；
- Slow 的 challenge 率高且稳定性低于 Fast；
- Slow latency/成本导致收益不足；
- perception-only 或 Fast-only 能以显著更低复杂度达到相同 real-device acceptance。

---

### H2. Checkpoint-based semantic recovery

这也不是 Phase 2.6 直接证明的唯一架构，但价值明显。

定义不是新 GraphNode 类型，而是派生角色：

```text
Checkpoint
= 当前正确 execution path 上
  最近一个足够确认的 Graph Node
```

目的：当 Slow 识别出 current branch 明显跑偏时，不必整次 Run 失败或重启；只需告诉 UniAgent“当前语义已偏离，最近可信位置是 X”。

但恢复动作仍由 UniAgent 决定。

必要性：Phase 2.6 已证明真实设备上错误会迁移且可能发生在深层；如果每次深层误点都整棵树重跑，真实 acceptance 成本会指数放大。

Falsifier：如果 rollback/restart 成本足够低，或者 execution path 本身无法提供可靠 checkpoint，则该能力可延后。

---

# 2. Runtime Core V2 — 核心概念

## 2.1 只有两个核心世界概念

```text
ContainerGraph
CurrentContainer
```

其他结构均是 supporting concepts，而不是同级 subsystem。

---

# 3. ContainerGraph

ContainerGraph 是 Runtime 当前掌握的 Container 世界模型。

```text
ContainerGraph
├─ Nodes
└─ Edges
```

它回答：

> Runtime 目前观察到这个设备/系统世界里有哪些 Container，以及这些 Container 曾经通过哪些 Trigger 发生过连接。

它不回答：

> 下一步应该点什么。

冻结：

```text
CONTAINER_GRAPH != NAVIGATION_PLANNER
KNOWN_EDGE != ACTION_AUTHORIZATION
```

---

# 4. Graph Node

## 4.1 Node 可以在身份尚未证明时立即存在

当具有 `MAY_ENTER_CONTAINER` 预期的 Action 之后出现首个相关 Fresh Observation，Runtime 可以创建一个 working Node：

```text
Node N
TrustView = INITIALIZED
```

目的不是提前宣称“这是 Wallpaper”，而是提供一个工作实体承载：

```text
N.LocalModel
N.Assessments
N.Evidence
```

冻结：

```text
NODE_EXISTS != NODE_IDENTITY_PROVEN
NODE_EXISTS != CONTAINER_COMPLETE
```

## 4.2 为什么“一开始就创建”比“身份证明后再创建”更可靠

真实设备中首屏可能是：

- 正常目标页面；
- partial loading；
- 广告；
- overlay；
- transient；
- 新页面但 title 未出现；
- Fast 暂时无法判断的深层页面。

如果必须先证明 identity 才允许 Node 存在，那么 LocalModel、Slice、Slow assessment 都没有自然归属，并且 Runtime 会被 identity gate 卡住。

Working Node 允许：

```text
先承认“我已经进入了一个新的工作空间假设”
再回答“它到底是谁”
```

Fast 如果判断仍是 SAME_CONTAINER，可撤销/归并该 working Node；这比“先卡住整个 Runtime 等身份真相”更简单。

---

# 5. Node Trust: Evidence-derived，不做 FSM

建议 Node 保存 assessments，Trust 只作为 derived view：

```text
Node
├─ FastAssessment
├─ SlowAssessment
└─ TrustView   // derived
```

TrustView 最小集合：

```text
INITIALIZED
FAST_TRUSTED
CONFIRMED
CHALLENGED
```

但它不是 FSM，不要求严格线性：

```text
INITIALIZED ─Fast→ FAST_TRUSTED
INITIALIZED ─Slow→ CONFIRMED
FAST_TRUSTED ─Slow confirm→ CONFIRMED
FAST_TRUSTED ─Slow challenge→ CHALLENGED
```

优先级：

```text
Fresh accepted world evidence
    >
Slow semantic assessment
    >
Fast semantic assessment
    >
Historical Graph prior
```

注意：Slow 更可信，但不能越过更新后的 Fresh Observation。

---

# 6. Graph Edge

Edge 必须是一等公民，并且**Trigger 属于 relation 语义的一部分**。

```text
Edge
├─ SourceNode
├─ Trigger / EntryAffordance
├─ DestinationNode
└─ Evidence[]
```

例如：

```text
Desktop --[Settings icon]→ Settings
Search --[Settings result]→ Settings
QuickSettings --[gear]→ Settings
```

即使 Destination 相同，也是不同 Edge。

冻结：

```text
SAME_DESTINATION != SAME_RELATION
```

Edge 表达：

> 当前/历史真实执行中，Runtime 曾观察到从 Source 经某个 Trigger 到达 Destination。

它不是世界永久真理。

```text
HISTORICAL_RELATION != CURRENT_WORLD_TRUTH
```

## 6.1 Edge 不维护完整 maturity 状态机

为避免 Graph 演化成状态同步系统，Edge 只保存 append-only evidence；使用时派生 `RelationAssessment`：

```text
RelationAssessment
├─ observed
├─ fast support
├─ slow confirmation
├─ challenge
└─ eligible-as-verification-prior
```

这样 Node 被 challenge 时，不需要递归修改所有 Edge 状态。

---

# 7. Transition — “这一次发生了什么”

必须区分：

```text
Transition = runtime occurrence
Graph Edge = evidence-backed world relation
```

例如三次真实进入：

```text
T1: Display --Wallpaper→ Wallpaper
T2: Display --Wallpaper→ Wallpaper
T3: Display --Wallpaper→ Wallpaper
```

可以共同支持一个 Edge。

## 7.1 Transition 最少回答

```text
Source
Trigger
Observed Destination
Outcome
```

## 7.2 Transition completion 不等待 identity confirmation

当 fresh accepted evidence 已足以判断“当前已经进入一个独立 Destination working Container”时：

```text
create/init Node N
CurrentContainer = N
Transition = completed
```

冻结：

```text
TRANSITION_COMPLETED != DESTINATION_IDENTITY_TRUSTED
TRANSITION_COMPLETED != SLOW_CONFIRMED
```

这是可靠性的关键：

> Runtime 先承认物理世界已经发生的转移，语义身份后续再解。

## 7.3 异常 Transition 不自动形成正常 Edge

比如：

```text
Display --Wallpaper→ Launcher
```

Slow 判断 `OFF_PATH`。

这次 Transition 可以被完整记录，但：

```text
OBSERVED_TRANSITION != NORMAL_GRAPH_EDGE
```

否则偶发异常会污染 Graph。

---

# 8. CurrentContainer

CurrentContainer 应保持极瘦：

```text
CurrentContainer
├─ NodeRef
├─ CurrentSlice
└─ EntryContext
```

其中：

```text
EntryContext
├─ SourceNodeRef
└─ EntryEdgeRef / TransitionRef
```

它回答：

> “我现在在哪里，以及这一次我是怎么来到这里的。”

而不是：

> “这个 Node 的固定 Parent 是谁。”

---

# 9. Return / Back — Path-relative，不是 Node-relative

Android 行为是最佳模型：

```text
Desktop → Settings
Back → Desktop

Search → Settings
Back → Search
```

因此：

```text
RETURN_TARGET
= function(Current Entry / Execution Context)
```

而不是：

```text
RETURN_TARGET = GraphNode.Parent
```

冻结：

```text
ENTRY_RELATION != RETURN_RELATION
RETURN_EXPECTATION != RETURN_TRUTH
```

Back 产生 expectation；fresh Observation 验证真实返回位置。

第一版无需把所有 Back 都建成长期 Graph Edge。Return relation 是否进入 Graph，可后续基于实际 buyer 决定。

---

# 10. Slice

Slice 是：

> 某一个 Fresh Observation 中可见的 Container 局部窗口。

它不是稳定 PageSegment，不要求跨 Run 可复用。

最小 geometry：

```text
Local Bounds [x1,y1,x2,y2]
Viewport Frame
optional SliceOrdinal
relative DeltaX / DeltaY
optional TraversalAxis
```

冻结：

```text
GEOMETRIC_ALIGNMENT != LOGICAL_ITEM_IDENTITY
ORDINAL != IDENTITY
BOUNDS != IDENTITY
```

但在当前生命周期内，这些信息可以用于 correlation、traversal 和 current action grounding。

---

# 11. Node.LocalModel

LocalModel 属于 Graph Node，而不是 Current Slice。

```text
Node.LocalModel
= 当前 Container 生命周期内累计的 accepted local knowledge
```

例如：

```text
S1: A B C D
S2: C D E F
S3: E F G H
```

聚合：

```text
A B C D E F G H
```

但 LocalModel 不追求跨 Run stable item identity。

## 11.1 Slice merge 使用组合 correlation evidence

可使用：

```text
semantic/text
relative order
geometry/bounds
spacing
item role/type
action context
```

但任何单项都不是 identity truth。

## 11.2 Scroll 本身提供 same-container prior

```text
Scroll
→ strong SAME_CONTAINER expectation
```

如果新 Slice 没有 overlap，不应立即创建新 Container；可以保留 coverage gap：

```text
KNOWN REGION
GAP
KNOWN REGION
```

## 11.3 LocalModel 不提供 stale action authority

```text
LOCAL_MODEL_ITEM != CURRENT_ACTION_OCCURRENCE
```

点击必须使用 CurrentSlice 中 fresh occurrence / bounds。

---

# 12. Container Coverage / Complete

`NewItems == 0` 不能直接推出 Complete。

Frontier exhausted 至少要求：

```text
Action settled
AND
Fresh Observation accepted
AND
same-container continuity supported
AND
current frontier overlap/reconciliation valid
AND
no new inventory beyond frontier
AND
bounded stability confirmation
```

然后：

```text
all relevant traversal frontiers exhausted
→ ContainerComplete
```

必须冻结：

```text
COVERAGE_COMPLETE != SEMANTICALLY_RESOLVED
ContainerComplete != SubtreeComplete
```

Unknown Item 可以存在于一个已经 coverage-complete 的 Container 中。

这直接避免 Phase 2.6 deep Unknown 把 scroll traversal 永久卡死。

---

# 13. Fast Container Resolution

Fast 不是“纯 identity classifier”，而是结合 Action Context + Fresh Observation 的低延迟 Container interpretation。

输入：

```text
Source Container
Trigger semantics
Action expectation
Fresh Slice / observation semantics
Graph prior
```

输出可包含：

```text
Boundary:
  SAME_CONTAINER
  NEW_CONTAINER
  TRANSIENT / AMBIGUOUS

IdentityCandidate
SemanticSupport
Conflict
```

## 13.1 Action expectation 是 prior，不是 truth

示例：

```text
Tap menu_item
→ strong MAY_ENTER_CONTAINER

Scroll
→ strong SAME_CONTAINER

Toggle
→ strong SAME_CONTAINER

Back
→ strong MAY_RETURN
```

冻结：

```text
ACTION_EXPECTATION != WORLD_TRUTH
```

## 13.2 Entry semantics 是 Fast 的高价值证据

例如：

```text
Trigger: Wallpaper
Destination:
  Wallpaper
  Change wallpaper
  Lock screen wallpaper
```

Fast 可以判断：

```text
Trigger ↔ Destination semantic consistency = strong
```

但：

```text
TRIGGER_DESTINATION_MATCH != CONTAINER_IDENTITY_TRUTH
```

## 13.3 Fast Trust Gate

第一版不需要复杂打分 ontology。

建议逻辑：

```text
IndependentContainerSupport
AND
SemanticSupport
AND
NoHardConflict
```

SemanticSupport 可综合：

```text
Trigger ↔ Destination
Destination internal consistency
Existing Graph candidate similarity
```

达到 Gate：

```text
TrustView = FAST_TRUSTED
```

意义：

- Runtime 可用该身份继续工作；
- 可作为后续 verification prior；
- 不阻塞等待 Slow。

不意味着：

```text
ACTION_AUTHORIZED
ContainerComplete
LongTermMemoryPublished
WorldTruth
```

---

# 14. Slow Semantic Advisor

建议将 Slow 从 `SlowContainerVerifier` 提升为有限语义顾问，但保持 advisory-only。

Slow 可做：

```text
1. Scene Assessment
2. Container Semantic Verification / Correction
3. Trigger Semantic Verification / Correction
4. Relation Assessment
5. Evidence Usefulness Assessment
6. Suggested Disposition / Recovery Hint
```

例如识别：

```text
TARGET_CONTAINER
ADVERTISEMENT
TRANSIENT
LOADING
BLOCKING_OVERLAY
EXTERNAL
OFF_PATH
AMBIGUOUS
```

## 14.1 Fast / Slow 推荐并行，而不是 fallback chain

如果 Observation 有正常语义内容：

```text
Fresh Observation
   ├─ Fast
   └─ Slow Async
```

Fast 先返回则 Runtime 先跑。

Slow 晚到：

```text
CONFIRM
CHALLENGE
CORRECT
INSUFFICIENT
```

如果 Fast insufficient，已经启动的 Slow 可以自然成为 blocking resolution support；无需再造“Slow Fallback Mode”。

## 14.2 Slow 可以判断 evidence 是否有用

例如首屏广告：

```text
Slow:
Scene = ADVERTISEMENT
TargetIdentityEvidence = NOT_USEFUL
```

这比强行让 Fast/identity pipeline 解释广告更可靠。

## 14.3 Slow 可以给一点决策，但不拥有 Action authority

例如：

```text
OFF_PATH
SuggestedDisposition:
  recover to checkpoint
```

或者：

```text
BLOCKING_OVERLAY
SuggestedDisposition:
  resolve overlay first
```

但：

```text
SLOW_PROPOSAL != ACTION_AUTHORIZATION
```

---

# 15. Slow Semantic Correction 与 UniAgent Obligation Repair

这是本 Draft 中非常重要的闭环。

Slow 的主要价值不只是“把 Graph 标成 challenge”，而是修正：

```text
Current Container semantic
Parent trigger semantic
Relation interpretation
```

然后 UniAgent 根据修正后的事实重新计算剩余 obligation。

## 15.1 Traversal mis-click 示例

Parent items：

```text
A
B
C
D
```

Fast 认为刚进入 C；Slow 发现实际上进入 D。

则语义事实修正为：

```text
D visited
C still pending
```

UniAgent 后续只补 C，而不是整次 Run 回滚。

## 15.2 Directed-entry mis-click 示例

目标是 Wallpaper，实际点入 Screen saver。

Slow 修正：

```text
ObservedChild = ScreenSaver
ExpectedBranch = Wallpaper
```

UniAgent 决策：

```text
return to parent
locate Wallpaper fresh occurrence
re-enter
```

因此冻结：

```text
SLOW
= correct what the last operation meant

UNIAGENT
= decide what to do next
```

这比为每种错误发明 recovery FSM 更简单。

---

# 16. Checkpoint

Checkpoint 不作为新 Graph 对象，而是当前 execution path 的派生角色：

```text
Checkpoint
= current correct execution path 上
  最近一个足够确认的 Graph Node
```

Slow 发现 OFF_PATH 时可以输出：

```text
Semantic mismatch
LastConfirmedCheckpoint = X
```

UniAgent 决定：

- Back；
- 回 Root；
- 重新进入；
- 从 checkpoint 继续；
- 或失败关闭。

Checkpoint 的目的：避免深层误点导致整棵树从头重跑。

Checkpoint 是否要求 “Node confirmed + Entry relation sufficiently supported” 可在真实验证阶段决定；第一版建议优先采用更保守定义。

---

# 17. ContainerTransitionFSM — 保持唯一且极小

不引入 Global FSM，也不引入 ContainerLocalFSM。

仅保留真正有 async / stale / pending 生命周期价值的 Transition FSM：

```text
IDLE
WAITING_FOR_OBSERVATION
WAITING_FOR_RESOLUTION
```

### IDLE
无 pending cross-container transition。

### WAITING_FOR_OBSERVATION
已发生可能跨 Container 的动作，等待 correlated fresh accepted observation。

### WAITING_FOR_RESOLUTION
Fresh Observation 已存在，需要判断 SAME / NEW / TRANSIENT / unexpected boundary 等。

注意：

```text
SAME
NEW
OFF_PATH
CONFIRMED
FAILED
```

都不是 FSM state，而是 analysis/result。

## 17.1 两种进入路径

Expected transition：

```text
IDLE
→ WAITING_FOR_OBSERVATION
→ WAITING_FOR_RESOLUTION
→ IDLE
```

Unexpected boundary already observed：

```text
IDLE
→ WAITING_FOR_RESOLUTION
→ IDLE
```

这足以覆盖 r5 类“本来只做 local action，但 fresh world 突然变了”的情况。

---

# 18. Reliability Invariants

建议作为 V2 必须冻结的不变量：

```text
EXTERNAL_WORLD = TRUTH

HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH
HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY

ACTION_EXPECTATION != WORLD_TRUTH
EXPECTED_TRANSITION != OBSERVED_TRANSITION
RETURN_EXPECTATION != RETURN_TRUTH

NODE_EXISTS != NODE_IDENTITY_PROVEN
NODE_HAS_NO_CANONICAL_PARENT

SAME_DESTINATION != SAME_RELATION
ENTRY_RELATION != RETURN_RELATION

TRANSITION_COMPLETED != DESTINATION_IDENTITY_TRUSTED
OBSERVED_TRANSITION != NORMAL_GRAPH_EDGE

VECTOR_MATCH != CONTAINER_IDENTITY_TRUTH
SLOW_ASSESSMENT != FRESH_WORLD_TRUTH

GEOMETRIC_ALIGNMENT != LOGICAL_ITEM_IDENTITY
ORDINAL != IDENTITY
BOUNDS != IDENTITY

LOCAL_MODEL_ITEM != CURRENT_ACTION_OCCURRENCE

COVERAGE_COMPLETE != SEMANTICALLY_RESOLVED
ContainerComplete != SubtreeComplete

SLOW_PROPOSAL != ACTION_AUTHORIZATION
GRAPH_PATH_HINT != AUTHORIZED_PLAN
```

---

# 19. Phase 2.6 Evidence → V2 Architecture Mapping

| Phase 2.6 结果 / 事故 | 暴露的问题 | V2 对应架构 |
|---|---|---|
| 19 fresh runs, 0/19 Completed | 局部修补后 blocker 迁移，整体闭环不足 | Graph + CurrentContainer + Transition + Fast/Slow 协同 |
| V run deepest ~4 levels | 基础 traversal 已具备，问题转向深层 identity/unknown/recovery | checkpoint + semantic correction + obligation repair |
| r5 fresh observation 回 Settings Root | expectation 与 fresh world 不一致 | Transition FSM + `EXPECTED != OBSERVED` + CurrentContainer commit |
| StableKey Z4 cross-container contamination | run-global identity/correlation scope 错误 | Node-owned LocalModel + current-lifecycle Slice correlation |
| unresolved child first frame | action 无法证明 transition | working Node + fresh observation driven resolution |
| Z5 LoO / Wallpaper / Bluetooth | OCR/Vision 语义不足 | Fast semantic prior + Slow async semantic interpretation |
| UI-TARS role false promotion ~36.4% | VLM 不可直接成为 authority | Slow advisory-only + fresh world precedence |
| Z7 observation ledger 38/38 healthy，但 deep Unknown 仍阻塞 | capture 非唯一主因 | coverage 与 semantics 分离；Slow 处理 Unknown |
| BGE round2 strong performance | embedding 适合低延迟 semantic candidate | Fast Trust Gate，但 vector != identity truth |
| row/phantom/representation 多轮局部修补 | 当前 page reconstruction 容易过拟合 | Slice + LocalModel 的当前生命周期聚合，不追跨 Run stable identity |
| return-control/title-off 等修补 | return/identity 依赖 context | path-relative EntryContext / Return expectation |

---

# 20. Why This Architecture Is More Complete

本方案的“完备”不是功能更多，而是每一种关键现实状态都有明确归属：

### 20.1 正常进入新页面

```text
Action → Fresh Observation → working Node → Fast trust → Slow confirm
```

### 20.2 仍在同一页面

```text
Scroll/toggle prior → Fast SAME_CONTAINER → merge Slice into LocalModel
```

### 20.3 首屏广告 / loading / overlay

```text
working hypothesis exists
Fast ambiguous
Slow scene assessment
→ wait / proposal / collect evidence
```

### 20.4 误点进入错误 child

```text
Fast working interpretation
Slow semantic correction
→ parent trigger + child semantics corrected
→ UniAgent recomputes pending/visited obligation
```

### 20.5 完全跑偏

```text
Slow OFF_PATH
→ identify last confirmed checkpoint
→ proposal to UniAgent
→ UniAgent decides recovery path
```

### 20.6 Back 返回不同来源

```text
Current EntryContext
→ ReturnExpectation
→ Back
→ Fresh Observation verifies actual world
```

### 20.7 Fast 与 Slow 冲突

```text
Slow > Fast for same evidence revision
→ Node/trigger semantic challenge/correction
→ never asynchronously rewrite newer fresh world
```

### 20.8 Unknown Item 但页面已经看完

```text
CoverageComplete = true
SemanticUnknown remains
→ traversal can close local coverage
→ Slow/UniAgent handles semantic obligation separately
```

这些路径共同形成闭环，而不是依赖异常 case-by-case patch。

---

# 21. What We Explicitly Do NOT Buy Yet

为避免再次语义膨胀，以下内容暂不进入 Runtime Core V2：

```text
Global FSM
ContainerLocalFSM
Canonical Parent
Full Graph navigation planner
Slow-owned recovery executor
Slow-owned action authorization
Cross-run stable item identity
Global container coordinates
Relation maturity FSM
Long-term Environment Memory formation
Memory consolidation / forgetting / version publishing
Cross-run Relation identity merge policy
Full subagent communication protocol
```

它们需要独立 buyer。

---

# 22. Proposed Implementation / Purchase Roadmap

## R0 — Phase 2.6 Scenario & Buyer Catalog

先冻结 buyer，不先写代码。

必须包含：

- r5 unexpected root observation；
- multi-entry Settings / Search / Desktop；
- title-off identity；
- Z4 cross-container StableKey contamination；
- unresolved child first frame；
- Z5 LoO / Wallpaper / Bluetooth；
- Z7 deep Unknown；
- row-band `Not set / Will never`；
- unknown destination；
- scroll overlap / gap / exhausted；
- wrong-child semantic correction；
- off-path recovery checkpoint。

每个 buyer 形成：

```text
Expected Reality
Observed Failure Mode
Required Runtime Property
Falsifier
```

---

## R1 — Core Data Model Reconciliation

购买：

```text
ContainerGraph
GraphNode
GraphEdge
CurrentContainer
EntryContext
Slice
Node.LocalModel
Transition occurrence
```

要求：behavior-neutral 优先，先替代重复的旧状态源，不同时引入 Slow。

重点验证：

- Node 无 canonical parent；
- CurrentContainer 只有一个 authoritative NodeRef；
- LocalModel strictly node-scoped；
- Edge 带 Trigger；
- Transition 与 Edge 分离。

---

## R2 — Minimal Transition Lifecycle

只购买：

```text
IDLE
WAITING_FOR_OBSERVATION
WAITING_FOR_RESOLUTION
```

用 r5 / unresolved child / Back 场景验证。

不得新增其他 FSM state，除非出现独立 buyer。

---

## R3 — Slice / LocalModel / Coverage

购买：

- current-lifecycle Slice correlation；
- overlap / gap；
- vertical + horizontal axis-neutral support；
- frontier exhaustion；
- coverage complete 与 semantic unresolved 分离。

目标：替代此前反复依赖 global ordinal / stable key / bounds identity 的局部修补。

---

## R4 — Fast Container Resolution

接入 BGE / semantic candidate，但只作为 Fast prior。

验证：

- trigger ↔ destination semantic consistency；
- existing Graph candidate ranking；
- SAME / NEW / AMBIGUOUS；
- no hard conflict gate。

真实 acceptance 必须证明：

> Fast 提升速度/稳定性，而没有把 vector similarity 升格为 identity truth。

---

## R5 — Slow Semantic Advisor Shadow

先 Shadow，不进入 Runtime authority。

输出：

```text
SceneAssessment
ContainerSemantic
TriggerSemantic
RelationAssessment
Mismatch
SuggestedDisposition
```

验证指标不只看 classification accuracy，还要看：

- Fast false trust 能否被有效 challenge；
- deep Unknown 是否减少；
- wrong-child 是否可正确纠正；
- transient/ad/off-path 是否可识别；
- false correction 是否在可接受范围。

若不能显著改善 Phase 2.6 buyer，则不 graduation。

---

## R6 — Slow Async Correction → UniAgent Obligation Repair

购买闭环：

```text
Slow correction
→ Runtime semantic reconciliation
→ UniAgent recompute obligation
```

验证两大场景：

1. traversal mis-click → 补遗漏 item；
2. directed-entry mis-click → 返回并重新进正确 branch。

禁止 Slow 直接执行恢复动作。

---

## R7 — Checkpoint Recovery Proposal

只有当 R6 证明“局部 semantic correction”有效后再购买。

目标：

```text
OFF_PATH
→ last confirmed checkpoint
→ UniAgent recovery decision
```

验证是否显著减少 deep traversal restart 成本。

---

## R8 — Phase 2.6 Real-device Acceptance

最终不是以 unit tests graduation，而是回到 fresh real runs。

至少评估：

```text
Completion rate
Deepest traversal depth
Wrong-branch recovery rate
Unknown blocker rate
False identity commit rate
Fast-only vs Fast+Slow delta
Slow correction precision
Average recovery cost
Repeated-run verification cost
```

只有真实 acceptance 支持，Fast/Slow/Checkpoint 才从 Architecture Hypothesis graduation 为 Runtime baseline。

---

# 23. Purchase Discipline

为了避免再次陷入“每一个新概念都需要很多轮 semantic gate”，建议采用两种购买标准。

## 23.1 Direct Buyer

适用于 Transition、CurrentContainer、path-relative return 等。

要求：

```text
真实 Phase 2.6 failure
→ 当前架构缺少表达能力
→ 新概念提供唯一/最小职责
→ 可测试 falsifier
```

## 23.2 Architecture Hypothesis Buyer

适用于 Fast/Slow、Checkpoint 等。

不要求证明“这是唯一正确架构”，而要求证明：

```text
1. 存在重复真实压力
2. 局部 repair 已出现收益递减/阻塞迁移
3. 提案针对的是一类重复压力，不是单个 case
4. authority 边界有界
5. 实现可 reversible / shadow
6. 有明确 falsifier
7. 最终由 Phase 2.6 real-device acceptance 决定 graduation
```

这允许我们购买“明显有价值但无法用最小语义定理严格推出”的架构能力，同时仍保持证据驱动。

---

# 24. Final Proposed Runtime Flow

```text
UniAgent Goal / Obligation
        ↓
Authorized Action
        ↓
Action Context / Transition Prior
        ↓
Fresh Observation
        ↓
CurrentContainer / Working Node
        ↓
┌──────────────────┬────────────────────┐
│ Fast             │ Slow Async         │
│ low latency      │ higher confidence  │
│ working meaning  │ semantic meaning   │
└─────────┬────────┴─────────┬──────────┘
          ↓                  ↓
   FAST_TRUSTED       CONFIRM / CORRECT /
                      CHALLENGE / PROPOSAL
          └──────────┬───────┘
                     ↓
        Node / Trigger / Relation
        semantic reconciliation
                     ↓
            UniAgent recomputes
            remaining obligation
                     ↓
             next authorized action
```

Graph 在整个过程中提供结构 prior，但不获得行为 authority：

```text
ContainerGraph
= world structure knowledge

CurrentContainer
= current active world location

Fresh Observation
= current truth evidence

Fast / Slow
= interpretation

UniAgent
= decision / planning / obligation
```

---

# 25. Recommended Graduation Claim

在 Phase 2.6 真机 acceptance 完成前，本方案只能称为：

```text
CONTAINER_RUNTIME_V2_ARCHITECTURE_CANDIDATE
```

不能提前声明：

```text
PRODUCTION_READY
FULL_TRAVERSAL_COMPLETE
SEMANTIC_RECOVERY_PROVEN
```

建议 graduation 至少要求：

1. r5 类 expected/observed mismatch 可被 Runtime 原生处理；
2. multi-entry / path-relative return 正确；
3. Slice/LocalModel 不再依赖跨 Container StableKey；
4. deep Unknown 不再等价于 Container coverage failure；
5. Fast 不产生不可接受的 false trust；
6. Slow 能在 shadow/evaluation 中稳定纠正 Fast / transient / wrong-branch；
7. UniAgent 能基于 correction 修复 traversal obligation；
8. real-device completion/depth 相比 Phase 2.6 baseline 有显著改善；
9. 所有 authority invariants 保持 fail-closed。

只有这些 buyer 在真实设备证据中闭环后，才应进入 architecture graduation。

---

# 26. Working Conclusion

Phase 2.6 最重要的价值不是证明某个 perception patch 成功或失败，而是帮助我们识别出 Runtime 下一阶段真正需要的结构：

> **用 ContainerGraph 表达“这个世界可能怎样连接”，用 CurrentContainer 表达“我现在真实站在哪里”，用 Transition 表达“刚才实际发生了什么”，用 Slice/LocalModel 表达“当前 Container 已经观察到了什么”，用 Fast/Slow 分层解释“这些 evidence 意味着什么”，最后把“接下来怎么办”的 authority 留给 UniAgent。**

这套结构的目标不是增加更多 semantic gate，而是减少 gate：

```text
物理发生 → 承认
语义不确定 → Fast/Slow解释
语义纠错 → 局部修正
任务义务变化 → UniAgent重算
```

如果后续 Phase 2.6 acceptance 能证明它显著降低 blocker migration、deep Unknown、wrong-branch 和重复探索成本，则 V2 才值得成为新的 Runtime baseline。
