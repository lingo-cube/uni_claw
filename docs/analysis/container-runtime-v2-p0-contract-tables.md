# Container Runtime V2 — P0 最小模型契约表

> DocumentType: `ANALYSIS_CONTRACT_DRAFT`
> Status: `DRAFT — P0-A/B/C CONTRACT TABLES（pre-OpenSpec；NOT_FROZEN / NOT_AUTHORIZED）`
> Authority: `NONE`
> Scope: 依据 `container-runtime-v2-horizontal-chain-model-refactor-brainstorm.md`（v3-final，READY_FOR_P0_CONTRACT_FREEZE）逐模型填写最小契约。**只含已收敛字段，禁止顺手加未来字段**；hierarchy / future IVI 多 region 算法 / Slow payload（P1）等均不在本表。
> EvidenceRef: 脑暴稿 §3 契约草稿 / §9.3 RB-F1..F8 / Repository Mapping（`container-runtime-v2-repository-mapping.md`）
> 禁止边界: 本表是契约草案输入，不是 OpenSpec spec、不是 Contract 条款、不授权实现。冻结属 OpenSpec + Human Gate。

---

## P0-A — Evidence Foundation

### A1. RuntimeAcceptance（facade；内部三纯函数）

| 项 | 契约 |
|---|---|
| Unique Question | 这一眼是否有资格成为 accepted stable viewport？其中哪些 accepted visual candidate materialize 成 Occurrence？structured evidence 对应哪些 visual candidate？ |
| Owner | Runtime Capabilities/Perception 层**无状态纯函数**（`SliceAcceptancePolicy` / `SourceCorrespondence` / `OccurrenceMaterializer`）；无 mutable owner |
| Minimal Fields | 输入：连续候选帧 + 上一 accepted Slice + settle/stability 证据 + correspondence 判据（IoU/text）。输出：`Accept/Reject(+reason)` × `correspondence pairs` × `Occurrence[] 构造` |
| Immutable? | 纯函数，无状态 |
| Evidence Source | Fast `PrimitiveCandidate[]`（瞬态）+ `StructuredElementEvidence` + settle 证据 |
| Derived Views | 无（产出 evidence 本体） |
| Legal Consumers | atomic reducer commit 构造（Slice + Occurrence[] + FastAssessment[] 一次提交，无 dangling refs） |
| Forbidden Authority | semantic truth、item identity、Agent decision、部分提交（Slice ref 先行） |

### A2. Slice

| 项 | 契约 |
|---|---|
| Unique Question | Runtime 接受的"这一眼/这一屏"是什么？ |
| Owner | acceptance 创建；Node.LocalModel 聚合持有；CurrentContainer 只持 `CurrentSliceRef` |
| Minimal Fields | `SliceRef` · `ObservationRef` · `ViewportBounds` · `SpatialRegionRefs[]` · `OccurrenceRefs[]` · `FastAssessmentRefs[]` · acceptance StabilityEvidence ref（输入证据，非 lifecycle state） |
| Immutable? | 是（append-only；rejected/transient Observation 不 materialize Slice） |
| Evidence Source | accepted **stable** viewport Observation（1 accepted Observation : 1 Slice） |
| Derived Views | 无（evidence 本体） |
| Legal Consumers | LocalModel 聚合、CurrentContainer 指针、SliceRelation 计算（Adapter 侧）、coverage |
| Forbidden Authority | logical world model、item identity、跨 Slice 累积、Agent plan、action authority |

### A3. SpatialRegion

| 项 | 契约 |
|---|---|
| Unique Question | 这一眼内的有效空间分区（滚动区/固定 chrome/overlay/persistent bar）是什么？ |
| Owner | 随 Slice 创建（acceptance 产出）；Slice 持有 |
| Minimal Fields | `RegionRef` · `RegionKind`（V1 candidate：ScrollableContent/FixedChrome/Overlay/PersistentControlBar/Panel/Unknown）· `Bounds` · `participatesInScroll` · `participatesInCoverage` · `participatesInGrounding`（三 flag 独立） |
| Immutable? | 是（随 Slice 不可变） |
| Evidence Source | acceptance 时的 region 划分证据（V1：Primary + fixed chrome 判定） |
| Derived Views | 无 |
| Legal Consumers | OccurrenceRegionBinding、SliceRelation.RegionRelation、RegionCoverageProjection、GroundingPolicy |
| Forbidden Authority | Occurrence ownership、item identity、独立 lifecycle |

### A4. Occurrence（含 OccurrenceRegionBinding）

| 项 | 契约 |
|---|---|
| Unique Question | 这一眼中 Runtime 正式接受看到了哪个局部**视觉**实例？ |
| Owner | acceptance 创建（分配 OccurrenceRef）；LocalModel 持有实体 |
| Minimal Fields | `OccurrenceRef` · `SliceRef` · `PrimitiveKind`（V1 candidate）· `ScreenBounds`（fresh grounding 唯一几何）· `RegionBinding { PrimarySpatialRegionRef?, OverlapRatio, Ambiguous }` · `RegionRelativeBounds?`（binding 明确时；correlation 专用，非 grounding 权威）· `RawEvidence`（vision）· `StateHints` · `CorroborationRefs[]`（StructuredEvidence refs）· `StabilizerHint?` · `EdgeClipped?` |
| Immutable? | 是；append-only，永不删除 |
| Evidence Source | accepted visual candidate（vision）；structured 仅经 correspondence 进入 `CorroborationRefs/StateHints`（**不得 materialize Occurrence**；无 correspondence → unmatched auxiliary evidence） |
| Derived Views | `RegionRelativeBounds`（acceptance 时由 ScreenBounds + region 派生存储） |
| Legal Consumers | LocalModel correlation、canonical membership、执行 grounding（**仅 fresh ScreenBounds**；V1 GroundingPolicy：EdgeClipped → non-groundable） |
| Forbidden Authority | 长期 UI identity、是否必须点击、跨 run 永久 ID、structured-only 铸造 |

### A5. SliceRelation

| 项 | 契约 |
|---|---|
| Unique Question | 两个 accepted Slice 的各 region 局部空间关系（平移/重叠/连续性）是什么？ |
| Owner | Adapter 能力端口计算；LocalModel 聚合持有（Runtime 只接受产出证据） |
| Minimal Fields | `FromSliceRef` · `ToSliceRef` · `RegionRelations[]`（V1 恰 1 条：`FromSpatialRegionRef` · `ToSpatialRegionRef` · `Translation` · `Uncertainty`（量化 + 派生档位）· `Overlap` · `Continuity` · 证据通道 refs） |
| Immutable? | 是 |
| Evidence Source | anchor matching / pixel registration / robust consensus / scroll prior（四通道，Adapter 内计算） |
| Derived Views | coverage chain 输入、RelocationHint（仅 prior，须 fresh 重 grounding） |
| Legal Consumers | RegionCoverageProjection、跨 Slice correlation、relocation |
| Forbidden Authority | item identity（`SLICE_ALIGNMENT != ITEM_IDENTITY`）、action grounding、scroll authorization |

### A6. FastAssessment

| 项 | 契约 |
|---|---|
| Unique Question | Fast 对这组 accepted Occurrence 的结构性假说（成员角色/结构/affordance hint）是什么？ |
| Owner | acceptance 时由 StructuralHypothesis 落成；随 Slice/LocalModel 持有 |
| Minimal Fields | `AssessmentRef` · `SliceRef` · `TargetOccurrenceRefs[]` · hint 集（member-role / structure / affordance，V1 candidate taxonomy）· source 标记 |
| Immutable? | 是 |
| Evidence Source | Fast StructuralHypothesis（瞬态）→ acceptance 落成 |
| Derived Views | 无（hint 是 canonicalization 的最低档 evidence） |
| Legal Consumers | SemanticReconciler（EvidencePolicy 最低档输入） |
| Forbidden Authority | 直接成为 LogicalItem、identity、obligation |

---

## P0-B — Canonical World

### B1. LocalModel

| 项 | 契约 |
|---|---|
| Unique Question | 这个 Node 生命周期内，至今积累了什么 accepted evidence，canonical 世界与 coverage 投影是什么？ |
| Owner | `ContainerRuntimeV2State` 内 per-Node 不可变聚合；唯一替换 seam = 纯 reducer（owner budget 见 Repository Mapping #1） |
| Minimal Fields | `NodeRef` · Evidence：`SliceRefs[]`（active/archived 分层，archived 保留 relocation 锚）· `OccurrenceRefs[]` · `SliceRelationRefs[]` · `Interaction/TransitionEvidenceRefs[]` · `AssessmentRefs[]`（Fast/Slow）· `CanonicalProjection`（LogicalItemRefs）· `RegionCoverageProjection[] → ContainerCoverageProjection` |
| Immutable? | 是（整体 immutable replace；evidence append-only，projection 重算） |
| Evidence Source | acceptance / SliceRelation / Slow typed claims / interaction-transition |
| Derived Views | CanonicalProjection、CoverageProjection（均存算快照，可被更强证据推翻重算） |
| Legal Consumers | SemanticReconciler、Agent admission（读 canonical）、coverage 判定 |
| Forbidden Authority | Agent plan、Action authorization、GoalEvidence、current physical authority、跨 run 永久 item identity、历史 bounds 点击权威 |

### B2. LogicalItem

| 项 | 契约 |
|---|---|
| Unique Question | 多次 observation 综合后，Runtime 认为这里有几个逻辑 UI 对象、各是什么？ |
| Owner | LocalModel CanonicalProjection（存算快照；Reconciler 产出） |
| Minimal Fields | `LogicalItemRef` · `Structure` · `Affordance`（**单主 interaction semantics**）· `MemberRoleMap`（OccurrenceRef → role）· `State` · `SemanticResolved` · membership evidence refs · anchor `SliceRefs[]`（relocation 锚） |
| Immutable? | 是（快照；merge/split/reclassify 经 CanonicalDelta 整体替换） |
| Evidence Source | membership / 跨 Slice correlation / Slow typed claims / interaction evidence（按 EvidencePolicy 聚合） |
| Derived Views | `SEMANTIC_AFFORDANCE_RESOLVED`（语义判定，**不含 Agent policy**） |
| Legal Consumers | Agent admission（语义条件）、执行 grounding 反查、supersession 对账 |
| Forbidden Authority | = obligation、当前点击坐标、跨 run 永久 ID、Agent Goal/ScenarioPolicy 消费、parent/children hierarchy（DEFERRED） |

### B3. CanonicalMembership（evidence-backed 记录）

| 项 | 契约 |
|---|---|
| Unique Question | 这条归属决策（attach/create/unresolved/merge/split/reclassify）由哪些证据、按哪条 policy 裁出？ |
| Owner | SemanticReconciler 产出；随 LocalModel evidence 持有 |
| Minimal Fields | `LogicalItemRef` · `OccurrenceRefs[]` · claim kind · evidence refs · policy tier/规则引用 |
| Immutable? | 是（决策可追溯到权重/规则行） |
| Evidence Source | EvidencePolicy[ClaimType] 聚合 |
| Derived Views | 无 |
| Legal Consumers | 重算/推翻逻辑、supersession 构造、对抗测试 |
| Forbidden Authority | 独立 mutation 通道 |

### B4. EvidencePolicy[ClaimType]

| 项 | 契约 |
|---|---|
| Unique Question | 每类 claim 由什么证据构成、如何聚合、何时可推翻旧解释？ |
| Owner | Runtime 静态策略表（结构 P0-B 冻结；**参数 Stage C1 调校并对抗验证**） |
| Minimal Fields | per claim：证据构成（如 SAME_LOGICAL_ITEM = layout + correlation + Slow + interaction corroboration）· aggregation 规则 · decision margin · hysteresis · conflict 处理 |
| Immutable? | 配置不可变（版本化替换） |
| Evidence Source | 架构收敛结论（脑暴稿 §3.7 表） |
| Derived Views | 无 |
| Legal Consumers | **仅 SemanticReconciler** |
| Forbidden Authority | Agent Goal/ScenarioPolicy 混入、全局线性排序、同档永不翻转（已废除） |

### B5. SemanticReconciler

| 项 | 契约 |
|---|---|
| Unique Question | 给定全部相关证据与 policy，下一版 canonical projection 是什么？ |
| Owner | 无状态纯函数 |
| Minimal Fields | 输入：LocalModel evidence + EvidencePolicy。输出：`CanonicalDelta`（attach / create working / keep unresolved / merge / split / reclassify） |
| Immutable? | 纯函数 |
| Evidence Source | LocalModel evidence |
| Derived Views | CanonicalDelta |
| Legal Consumers | reducer（重算投影）、Agent progress reconciliation（消费 CanonicalDelta） |
| Forbidden Authority | 直接改 Agent progress（静默改写禁止）、直接产 obligation、铸造 Occurrence |

---

## P0-C — Consumption Boundary

### C1. Agent Admission（语义条件）

| 项 | 契约 |
|---|---|
| Unique Question | 哪些 canonical LogicalItem 在当前 Goal/ScenarioPolicy 下应成为 obligation？ |
| Owner | **Agent**（独占） |
| Minimal Fields | 输入：`LogicalItem(SEMANTIC_AFFORDANCE_RESOLVED)` + Goal + ScenarioPolicy。输出：authorized obligation（进入 `BranchProgressEvidence.AuthorizedSiblingEvidence`） |
| Immutable? | Agent 状态语义（NOT_REWRITTEN） |
| Evidence Source | LocalModel canonical projection（只读） |
| Derived Views | `PendingBranchObligations = Authorized - Completed`（Agent 侧 derived view） |
| Legal Consumers | obligation 生成、progress admission |
| Forbidden Authority | grounding 条件混入 admission（`SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE != AUTHORIZED`）、Runtime 侧决策 |

### C2. Execution Grounding（执行条件 + V1 GroundingPolicy）

| 项 | 契约 |
|---|---|
| Unique Question | 此刻能否为该 obligation 找到 fresh 可点击几何？ |
| Owner | Runtime grounding 纯函数（判定）+ Agent（authorization） |
| Minimal Fields | `GroundableNow?` → NO: Relocation → fresh Slice → fresh Occurrence；YES: fresh `ScreenBounds`。V1 GroundingPolicy：`EdgeClipped → non-groundable`（fail-closed policy，非 invariant） |
| Immutable? | 策略可版本化 |
| Evidence Source | 当前/可达 Slice 的 fresh Occurrence |
| Derived Views | RelocationHint（仅 prior） |
| Legal Consumers | Action Authorization 前置 |
| Forbidden Authority | 历史 bounds grounding、RegionRelativeBounds 作 grounding 权威 |

### C3. CanonicalDelta / LogicalItemSupersession

| 项 | 契约 |
|---|---|
| Unique Question | canonical 世界发生了什么改写？Agent progress 应如何重投影？ |
| Owner | SemanticReconciler 产出（delta）；**Agent 消费并重投影 progress**（pattern REUSE；payload = 新最小契约，见 Repository Mapping #5） |
| Minimal Fields | `PriorLogicalItemRefs[]` · `ResultingLogicalItemRefs[]` · delta kind（merge/split/reclassify）· evidence refs · binding refs |
| Immutable? | 是 |
| Evidence Source | Reconciler 决策 |
| Derived Views | Agent progress reevaluation input（authorization 有效性 / 重复 attribution / superseded obligation） |
| Legal Consumers | Agent progress reconciliation（`Agent.ContainerReconciliation` pattern） |
| Forbidden Authority | 静默改写 `_branchProgress`（`LOGICAL_ITEM_RECONCILIATION != SILENT_PROGRESS_REWRITE`）、万能 correction envelope |

### C4. Coverage input boundary

| 项 | 契约 |
|---|---|
| Unique Question | 各 region 的 coverage 状态如何聚合为 Container coverage？ |
| Owner | LocalModel 内投影（region → container aggregate） |
| Minimal Fields | `RegionCoverageProjection { SpatialRegionRef, CoverageEvidence, Exhaustion }` → `ContainerCoverageProjection = aggregate(participatesInCoverage = true)` |
| Immutable? | 是（存算投影） |
| Evidence Source | SliceRelation.RegionRelations chain + overlap + gap + exhaustion |
| Derived Views | `ContainerLocalComplete ≈ CoverageExhausted AND AllAdmittedObligationsResolved AND NoClosureCriticalUnresolvedSemantic` |
| Legal Consumers | Container closure 判定 |
| Forbidden Authority | Fast item 数量输入、PrimaryRegion-exhausted → Container-complete 捷径、completion authority（归 GoalEvidence） |

---

## 附：P0 契约表与 Stage 的对应

| 契约 | 实施阶段 | 备注 |
|---|---|---|
| A1–A6 | Stage B1（A3/A5）/ Stage B2（A1/A2/A4/A6） | B1 只产 spatial evidence，零行为变化 |
| B1–B5 | Stage C1 | 双轨 shadow（taxonomy 映射 / StableKey 退役） |
| C1–C4 | Stage C2 | supersession / coverage 切源 shadow 双轨 |
| P1（Slow payload） | Stage D | 等 P0 稳定后另定 |

**本表完成后停在 Human Gate。** 进入 OpenSpec propose 需 Human 再授权。
