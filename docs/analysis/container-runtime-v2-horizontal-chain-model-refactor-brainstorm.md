# Container Runtime V2 — 横向链路模型重构脑暴记录

> DocumentType: `ANALYSIS_BRAINSTORM`
> Status: `BRAINSTORM — v3-FINAL-CONSOLIDATION — REVIEW: PASS → READY_FOR_P0_CONTRACT_FREEZE（NOT_OPENSPEC_APPROVED / NOT_IMPLEMENTATION_AUTHORIZED / NOT_GRADUATED）`
> Authority: `NONE`
> Scope: Container Runtime V2 横向链路（看见 → 接受 → 积累 → 收敛 → 行动 → 回环）的 evidence model 最终收敛稿。本轮为**减法收敛**：恢复当日实际讨论形成的最小架构，整理过程中顺手扩展出的能力一律降级为 DEFERRED / REPOSITORY_MAPPING_REQUIRED，不冒充 frozen decision。
> EvidenceRef:
> - 主源: `docs/analysis/Container_Runtime_V2_证据模型重构思路与依据_2026-09-02.docx`
> - R8 / Phase 2.6 buyer 证据: `docs/analysis/container-runtime-v2-architecture-working-draft.md`
> - Repository Governance: `docs/decisions/repository-governance-authority-baseline.md`（EntryContext / return 语义原始出处）
> - 可行性证据: `docs/analysis/runtime-debugging-capability-landscape.md`（FRAME_LOCAL_FUSION_INSTABILITY 案例）
> - 代码锚点（已核验）: `src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs` · `src/UniClaw.Runtime/Agent/Agent.cs`（`_branchProgress`）· `src/UniClaw.Runtime/Model/BranchProgressEvidence.cs` · `src/UniClaw.Runtime/Model/Observation/ObservedElement.cs` · `src/UniClaw.Runtime.Adapters/PhysicalEnvironment.cs`
> 禁止边界: 本文档不建立或修改 authority、lifecycle、owner、Runtime behavior 或 implementation authorization；不登记 `docs/decisions/index.md`。任何落地属 Large Change → OpenSpec + Human Gate。本轮只做文档收敛，不新增实现 WorkItem；**本轮完成后停止脑暴新对象**。

> **v3-final 收敛记录**（相对 v3 的减法与恢复，逐条 Rebuttal 见 §9.3）：
> ① Occurrence 恢复为 **accepted primary viewport visual occurrence**（structured 仅 corroboration，撤回 source-neutral materialization）；
> ② Slice 恢复为 **accepted stable fresh viewport**（撤回 STABLE/TRANSIENT 分级；stability 属 acceptance 输入证据）；
> ③ `ContentRelativeBounds` → **`RegionRelativeBounds`**（SpatialRegion 改名后的坐标语义对齐）；
> ④ CurrentContainer / EntryContext **明确原样保留**（R8 语义不淡化）；
> ⑤ 全局 revision 冻结撤回 → **REPOSITORY_MAPPING_REQUIRED**；
> ⑥ LogicalItem hierarchy → **DEFERRED**（撤回 Day-1 refs）；
> ⑦ PerceptionDiagnosticEvidence → **REUSE/EXTEND 优先**（撤回强制 CREATE）；
> ⑧ Taxonomy 只冻结**组合建模方式**，enum 全集 = V1 candidate，不 contract-frozen；
> ⑨ Anchor barrier 修正为 **critical initial identity ambiguity 才触发**（首帧不自动调 Slow）；
> ⑩ Stage 编号与 P0 契约维度分离（B1 Spatial Foundation 先于 B2 Accepted Evidence Model）。

---

## 1. 结论摘要

- R8（ownership 层）**保留不推翻**；CurrentContainer / EntryContext **原语义保留**（§3.12）。
- 两个已被真机证据定罪的隐含假设实体：`StableKey = candidate.RowId`（`PhysicalEnvironment.cs:177`）+ known_rows 跨页 = `Fast row ≈ logical item`（Z4）；`NormalizeType` 的 `menu_item 单枚举`。
- 最小核心链（当日实际形成）：**Screenshot → Observation → Fast → Candidate → Runtime Acceptance → accepted stable Slice { accepted visual Occurrence[] } → Node.LocalModel → Canonicalization → LogicalItem → Agent Admission → Obligation → _branchProgress → 执行 grounding → Action → TransitionOccurrence → CurrentContainer / ContainerGraph**。
- Occurrence = **accepted visual** occurrence；StructuredEvidence **只 corroborate，不铸造 Occurrence**（§3.4 / §9.3 RB-F1）。
- Admission / Grounding / Authorization 三分离；canonical 改写 → Agent progress reconciliation；EvidencePolicy claim-specific 且可纠错防振荡；coverage 三分离且 region-scoped。
- taxonomy enum 全集 = **V1 CANDIDATE，NOT CONTRACT-FROZEN**（§4 / §9.3 RB-F6）。
- Revision / hierarchy / diagnostic type / exact enum 均**不被过早冻结**（§9.3 RB-F3/F4/F5/F6）。

## 2. 最终架构基线（主链 + 辅助链）

### 2.1 主链

```text
External World
      ↓
Observation
      ↓
Fast
      ↓
PerceptionCandidate[]             （transient provider output）
      ↓
SliceAcceptance
      ↓
accepted stable Slice
  └─ accepted visual Occurrence[]
      ↓
Node.LocalModel
  ├─ Slice[] / Occurrence[] / SliceRelation[]
  ├─ Fast/Slow Assessments
  └─ CanonicalProjection
      ↓
Incremental Canonicalization
      ↓
LogicalItem
      ↓
Agent Admission
      ↓
Traversal Obligation
      ↓
_branchProgress（Agent-owned evidence aggregate）
      ↓
Execution grounding
      ↓
Fresh Slice → Fresh Occurrence → Fresh ScreenBounds
      ↓
Action Authorization
      ↓
Driver → External World → Fresh Observation
      ↓
TransitionOccurrence
  ├─ CurrentContainer
  └─ ContainerGraph
```

### 2.2 辅助链

```text
StructuredEvidence → corroboration only（StateHints / SourceEvidenceRefs；无 visual correspondence 则 unmatched auxiliary evidence）
Slow → typed semantic claims → deterministic reconciliation（无 action / completion / graph mutation authority）
SliceRelation → correlation / coverage / relocation evidence
CurrentContainer = NodeRef + CurrentSliceRef + EntryContext（R8 原样）
```

## 3. 逐段契约草稿（v3-final）

> `[RB-Fn]` = §9.3 最终收敛轮 Rebuttal；`[R#]`/`[RB-n]` = 历史审核轮（§8，不回退）。

### 3.1 Fast 输出层

- 两层输出（PrimitiveCandidate[] + StructuralHypothesis[]）+ SceneAssessment；Hypothesis 是 hint，`H1 != LogicalItem`；adapter 内瞬态。
- Fast provider 不要求按 member 粒度拆分检测。
- `PerceptionCandidate.RowId` 降级为 `StabilizerHint`（双轨 shadow 退役 `[Q10]`）。
- **拒绝/降级候选的诊断记录**（`[RB-F5]`）：只记录 `ObservationRef / candidate summary / reject reason / validator decision`；**优先 REUSE/EXTEND 既有 Trace / Validation evidence / perception causal trace，不强制 CREATE 新 Runtime domain object**；CREATE 仅当 Repository Mapping 证明无合适 owner。

### 3.2 Runtime Acceptance（三分职责 + atomic commit `[R/RB-02][K]`）

外部保留 `RuntimeAcceptance` facade；内部三个纯函数职责：

```text
1. SliceAcceptancePolicy    当前 Observation 是否足够 stable/fresh，可成为 accepted Slice？
2. SourceCorrespondence     structured evidence 是否对应某 accepted visual occurrence candidate？
3. OccurrenceMaterializer   哪些 accepted visual candidates 成为 Occurrence？

SLICE_ACCEPTANCE != SOURCE_CORRESPONDENCE != OCCURRENCE_MATERIALIZATION
```

- **Atomic commit**：`Slice + Occurrence[] + bound FastAssessment[]` 一次 reducer commit；**不得产生 dangling Slice.OccurrenceRefs**（与 `ContainerRuntimeV2Reducer` 原子模式同构）。
- correspondence 判定 = 确定性函数（bounds IoU + text 判据；alignment 非 grouping）。

### 3.3 Slice（v3-final：恢复 stable-only `[RB-F2]`）

```text
唯一问题：Runtime 接受的"这一眼/这一屏"是什么？
定义：SLICE = ACCEPTED STABLE FRESH VIEWPORT
Owner：acceptance 创建；Node.LocalModel 聚合持有；CurrentContainer 只持 ref
immutable：是（append-only）
禁止：逻辑 item identity、跨 Slice 世界、Agent plan、action authority
```

- 基数：**每个 accepted stable viewport Observation materialize 恰好一个 Slice；rejected / transient Observation 不 materialize 任何 Slice**（仍作为 raw capture + 诊断证据存在）。
- **撤回 STABLE/TRANSIENT 分级**：settling / transient 的 Observation → `StabilityEvidence / Trace / diagnostic`（acceptance 的**输入证据**，不是 Slice lifecycle state）。
- `SpatialRegion[]`：

```text
SpatialRegion {
    RegionKind:  ScrollableContent / FixedChrome / Overlay /
                 PersistentControlBar / Panel / Unknown        // V1 candidate taxonomy
    participatesInScroll / participatesInCoverage / participatesInGrounding
}
```

- 双坐标（v3-final 改名 `[RB-F7]`）：

```text
ScreenBounds        = fresh viewport grounding geometry
RegionRelativeBounds = region-local correlation geometry
REGION_RELATIVE_BOUNDS != ACTION_GROUNDING_AUTHORITY
```

  禁止 GlobalPageCoordinate。
- **Evidence ordering `[RB-F3]`**：Q5 = **REPOSITORY_MAPPING_REQUIRED**。repo 已有自然 run-local monotonic sequence 则 REUSE；不存在则**不得仅为架构整洁新造 global semantic clock**。语义冻结：`EvidenceOrdering = optional trace metadata`；`CausalBinding = explicit refs`；`Freshness = claim-specific binding`。既有不变量保持（`LATER_REVISION != STRONGER_EVIDENCE` / `REVISION_ORDER != CAUSAL_BINDING`）。Mapping 约束：所选 primitive 须保留异步候选 reduction 的乐观并发拒绝能力（现存买家：`ContainerRuntimeV2Reducer` stale 检查）；REUSE 候选（已核验）：`Observation.SequenceNumber` / `SemanticEvidenceRevision` / Observability trace 链。

### 3.4 Occurrence（v3-final：恢复 visual-only `[RB-F1]`）

```text
唯一问题：这一眼中 Runtime 正式接受看到了哪个局部视觉实例？
定义：OCCURRENCE = ACCEPTED PRIMARY VIEWPORT VISUAL OCCURRENCE
Owner：acceptance 创建（分配 OccurrenceRef）；LocalModel 持有实体
immutable：是；append-only，永不删除
禁止：长期 UI identity、是否必须点击、跨 run 永久 ID
```

- 字段骨架：

```text
Occurrence {
    OccurrenceRef / SliceRef / PrimitiveKind
    ScreenBounds                      // fresh grounding
    RegionBinding                     // 见下
    RegionRelativeBounds?             // region-local correlation（binding 明确时）
    RawEvidence（vision）/ StateHints / CorroborationRefs（StructuredEvidence）
    StabilizerHint? / EdgeClipped?
}
```

- **StructuredEvidence 仅 corroboration**：

```text
StructuredEvidence → correspondence → corroborate Occurrence → StateHints / SourceEvidenceRefs
找不到 accepted visual correspondence → unmatched auxiliary evidence
禁止：StructuredEvidence → Occurrence

STRUCTURED_EVIDENCE MAY CORROBORATE AN OCCURRENCE
BUT DOES NOT CREATE VISUAL OCCURRENCE TRUTH
（撤回 MULTI_SOURCE_MATERIALIZATION MAY BE SOURCE_NEUTRAL [RB-F1]）
SOURCE_AUTHORITY IS CLAIM_SPECIFIC
MULTI_SOURCE_CORROBORATION != OCCURRENCE_IDENTITY
```

  Vision = fresh visibility / grounding 的 primary evidence；Structured = clickable/checkable/state corroboration + auxiliary structural evidence，不自动获得 grounding / container identity / coverage / completion authority。（structured-primary 环境的建模问题随 `[RB-F1]` 一并 DEFERRED，非本模型范围。）
- **V1 GroundingPolicy（fail-closed policy，非 invariant）**：`EdgeClipped Occurrence → non-groundable（V1；未来可按"安全可点区域充分性"泛化）`。（TRANSIENT-origin 条目随 TRANSIENT Slice 撤回而删除 `[RB-F2]`。）
- **OccurrenceRegionBinding（P0-A Freeze blocker `[R/RB-08][L]`）**：

```text
OccurrenceRegionBinding { OccurrenceRef, PrimarySpatialRegionRef?, OverlapRatio, Ambiguous }
V1 规则：1) 计算 Occurrence 与各 SpatialRegion overlap；
        2) max-overlap 超阈值 → PrimarySpatialRegion；
        3) 无 dominant region → RegionAmbiguous；
        4) RegionAmbiguous → ScreenBounds 保留，RegionRelativeBounds 不作 authoritative correlation evidence。

OCCURRENCE_BELONGS_TO_SLICE
REGION_BINDING != OCCURRENCE_IDENTITY
REGION_BINDING != OWNERSHIP
```

- `StableKey` 降级为 `StabilizerHint`（双轨 shadow `[Q10]`）。

### 3.5 SliceRelation（region 对齐 `[R/RB-10][N]`）

- 计算在 Environment/感知 Adapter 能力端口（零 ProjectReference guard 不破）；四证据通道；量化不确定度 + 派生档位。
- 领域模型：

```text
SliceRelation { FromSliceRef, ToSliceRef, RegionRelations[] }
RegionRelation { FromSpatialRegionRef, ToSpatialRegionRef,
                 Translation, Uncertainty, Overlap, Continuity }
```

  V1：`RegionRelations.Count = 1`（Primary only）、translation-only。未来 IVI（Media dy=-380 / Sidebar dy=0 / ClimateBar dy=0）无需改领域语义。region ref 为 Slice 内局部 region 引用（From/To 各指本 Slice 的 region），不引入跨 Slice region identity。
- `SLICE_ALIGNMENT != ITEM_IDENTITY`；relocation 后必须 fresh perception → fresh Occurrence → fresh bounds；archived 证据不得剥离 relocation 锚。

### 3.6 LocalModel

- 挂载倾向入 `ContainerRuntimeV2State` per-Node 不可变聚合；NET_NEW_MUTABLE_TRUTH=0 **待 Repository Mapping 后 owner-budget 证明** `[R7]`（`Container/` page-local owner 的 REUSE/MOVE/DERIVE/DELETE 为 Mapping 强制项）。
- 四区：Evidence（active/archived 分层，archived 保留 relocation 锚）/ Assessments / CanonicalProjection / RegionCoverageProjection → ContainerCoverageProjection（§3.10）。
- 禁止：Agent plan、Action authorization、GoalEvidence、current physical authority、跨 run 永久 item identity、历史 bounds 点击权威。

### 3.7 LogicalItem + Canonicalization

```text
形态     = Structure × Affordance × MemberRole × State 组合建模（冻结的是组合方式 [RB-F6]）
           一个 actionable LogicalItem = 一个主 interaction semantics；禁止 Affordance = Set<...>
存在方式 = 单 owner 存算投影（evidence append-only → 重算 → immutable replace）
身份     = LocalModel 生命周期内 canonical；无跨 run 永久 ID
Hierarchy = DEFERRED [RB-F4]：P0-B 不含 Parent/Child refs；保留扩展 seam；
           待 Q11（independent interaction region 判据）有 deterministic buyer 后购买；
           V1 组合表达 = flat LogicalItem + GROUP structure + membership evidence
Reconciler = 纯函数 × EvidencePolicy[ClaimType]
unresolved = 非阻塞；四类 barrier（Anchor/Closure/Safety/Anomaly）升级
hard gate：Agent admission / 正式 Graph relation admission / Container closure
```

**EvidencePolicy[ClaimType]**（claim-specific；canonical claim = Evaluate(all relevant evidence, EvidencePolicy[ClaimType])；**同档持续一致的新证据累计后可推翻旧投影**；防振荡 = aggregation / decision margin / hysteresis / explicit conflict，**不用"同档永不翻转"** `[RB-04/O]`）：

| Claim | 强证据构成 |
|---|---|
| `SAME_LOGICAL_ITEM` | member layout + repeated Slice correlation + Slow semantic + interaction corroboration |
| `DIFFERENT_LOGICAL_ITEM` | distinct interaction outcome 强反证 |
| `MEMBER_ROLE` | visual structure + Slow semantic |
| `CURRENTLY_VISIBLE` | fresh Slice evidence（fresh binding） |
| `TRANSITION_OCCURRED` | fresh post-action Observation |
| `CONTAINER_IDENTITY` | fresh destination evidence + semantic/structure |
| `SEMANTIC_AFFORDANCE_RESOLVED` | visual structure + Slow semantic + structured corroboration（**不含 Agent policy** `[RB-05/P]`） |

```text
SEMANTIC_AFFORDANCE != AGENT_ADMISSION
```

  Runtime/Reconciler 只产出 Structure / Affordance / MemberRole / State / SemanticResolved；Agent 依 `LogicalItem + Goal + ScenarioPolicy` 决定 admission。

**Admission / Grounding / Authorization 三分离** `[R1]`：

```text
SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE != AUTHORIZED
```

### 3.8 Agent admission / obligation / progress 对账

- **`_branchProgress`（代码核验 `[RB-01/J]`）**：

```text
_branchProgress = Agent-owned branch progress evidence aggregate
  （Agent.cs:66；BranchProgressEvidence = Approved / Authorized / Completed SiblingEvidence）
PendingBranchObligations = AuthorizedSiblingEvidence - CompletedSiblingEvidence   ← derived view
```

  本轮 refactor 只改变**什么 canonical LogicalItem 有资格进入 Agent admission**；不重写 Agent progress authority。
- **Canonical 改写 → Agent Progress Reconciliation** `[R2]`：supersession 对账（`CanonicalDelta / LogicalItemSupersession`），`LOGICAL_ITEM_RECONCILIATION != SILENT_PROGRESS_REWRITE`。符号处置 = **REUSE PATTERN / EXTEND-CANDIDATE**（revision/binding-aware correction pattern + Agent reevaluation consumer pattern；payload 适配待 Repository Mapping；禁止把旧 correction type 扩成万能 envelope `[U/RB-14]`）。
- ungroundable 终态显式化：Recovery/ → incomplete-with-evidence，不假完成。

### 3.9 Slow + barrier（v3-final：Anchor barrier 修正 `[RB-F8/Q]`）

- 低频高精度 Semantic Corrector；typed claims + Binding/Revision Validation + 确定性 Reconciler；不铸造 Occurrence；无 action / completion / graph mutation authority。
- **Anchor Barrier 修正**：

```text
FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION
ANCHOR_BARRIER = CRITICAL_INITIAL_IDENTITY_AMBIGUITY
```

  只有 **critical initial container identity 未解决 / 冲突 / decision-critical** 时才触发 Slow —— 否则 Slow 从 low-frequency corrector 退化为高频 verifier。Closure / Safety / Anomaly 三类 barrier 语义不变；四类触发条件为确定性 Runtime 策略表（Safety 两层拆分保持：`SEMANTIC_SAFETY_ESCALATION != ACTION_SAFETY_AUTHORIZATION`）。
- 异步 Unknown 非阻塞；Slow payload P1 再冻结。

### 3.10 Coverage / Completeness（region-scoped `[RB-09/M]` + 泛化 ResolutionPolicy `[RB-13/R]`）

```text
RegionCoverageProjection { SpatialRegionRef, CoverageEvidence, Exhaustion }
ContainerCoverageProjection = aggregate(all SpatialRegions where participatesInCoverage = true)

COVERAGE IS SPATIAL_REGION_SCOPED BEFORE CONTAINER AGGREGATION
例：Media scroll region exhausted != whole IVI container exhausted
```

- 三分离：`COVERAGE_COMPLETE != SEMANTICALLY_RESOLVED != TRAVERSAL_COMPLETE`；coverage 证据源 = SpatialRegion + SliceRelation.RegionRelations chain + overlap + gap + exhaustion；shadow 双轨后切 `[X2]`。
- `ContainerLocalComplete ≈ ContainerCoverageProjection.Exhausted AND AllAdmittedObligationsResolved AND NoClosureCriticalUnresolvedSemantic`。
- **Closure ResolutionPolicy（不硬编码路线）**：

```text
ClosureCriticalUnresolvedSemantic
must exhaust an explicit bounded resolution policy
before incomplete-with-evidence.
```

  route（Slow / Reobserve / Relocation / Structured corroboration / Interaction evidence）按 blocker 类型选择；具体策略留 Stage D/E。

### 3.11 Transition 回环（R8，保留）

- `ContainerTransitionOccurrence` / 纯 Reducer / `ContainerGraphQuery` REUSE；期望在链路上无投票权。

### 3.12 CurrentContainer / EntryContext（R8 显式保留 `[D]`）

```text
CurrentContainer { NodeRef, CurrentSliceRef, EntryContext }
```

- EntryContext 保持原语义：**同一物理 Container 可经不同 relation 进入**。本轮：不新增 / 不替换 / 不降级 EntryContext，**不把 CurrentContainer 简化成 Node + Slice**。Evidence Model 只增强 `CurrentSliceRef` 所指向的 evidence world。

```text
ENTRY_RELATION != RETURN_RELATION
RETURN_EXPECTATION != RETURN_TRUTH
```

- Stage C 补充候选（未展开）：canonical relation admission / duplicate non-referencable view。

## 4. UI Taxonomy（v3-final：只冻结组合方式 `[RB-F6/H]`）

**冻结**：`PrimitiveKind != LogicalStructure != Affordance != MemberRole != State` 的**组合建模方式**。
**不冻结**：enum 全集 —— 下表全部标记 **V1 CANDIDATE TAXONOMY / NOT CONTRACT-FROZEN**，后续依真实 Settings / IVI buyer 可 merge / rename / remove / extend。

| 维度 | V1 candidate 值域（NOT CONTRACT-FROZEN） |
|---|---|
| PrimitiveKind | TEXT / ICON / IMAGE / CONTROL_REGION / REGION / UNKNOWN |
| LogicalStructure | LIST_ITEM / TILE / BUTTON / CONTROL / INPUT / RANGE / TAB / STATIC_CONTENT / GROUP / UNKNOWN |
| Affordance（每 actionable item 一个主语义） | NAVIGATE / INVOKE / TOGGLE / SELECT / EDIT / ADJUST / EXPAND / DISMISS / NONE / UNKNOWN |
| MemberRole | PRIMARY / SECONDARY / VALUE / STATE_INDICATOR / LEADING_VISUAL / TRAILING_VISUAL / CONTROL / DISCLOSURE / UNKNOWN |
| State | enabled / selected / checked / expanded + value/range/unit/mode |

- 禁止回到组合枚举爆炸：`MENU_ITEM_WITH_SUBTITLE` / `TOGGLE_MENU_ITEM` / `CAR_CLIMATE_TILE` 一类不允许再现。
- Climate tile V1 表达：flat items（ClimateTile=GROUP / TemperatureControl=ADJUST / AutoControl=TOGGLE / ACControl=TOGGLE）+ membership evidence；hierarchy DEFERRED。

## 5. 候选不变量清单（v3-final；先落文档，落位另行决策 `[X3]`）

```text
── 核心链（v1，保持）──
FAST_RESULT != ACCEPTED_RUNTIME_EVIDENCE
PERCEPTION_CANDIDATE != OCCURRENCE
OCCURRENCE != LOGICAL_ITEM
LOGICAL_ITEM != TRAVERSAL_OBLIGATION
SLICE != LOGICAL_WORLD_MODEL
SLICE_ALIGNMENT != ITEM_IDENTITY
GEOMETRIC_ALIGNMENT != LOGICAL_ITEM_IDENTITY
COVERAGE_COMPLETE != SEMANTICALLY_RESOLVED
COVERAGE_COMPLETE != TRAVERSAL_COMPLETE
SLOW_CORRECTION != ACTION_AUTHORITY
SLOW_CORRECTION != COMPLETION_AUTHORITY
SLOW_SEES_SOMETHING != ACCEPTED_OCCURRENCE
HISTORICAL_BOUNDS != ACTION_GROUNDING
RELOCATION_HINT != SCROLL_AUTHORIZATION
OBSERVED_CURRENT != EXECUTION_OBLIGATION
ACTION != TRANSITION_OCCURRENCE
CONTAINER_GRAPH != NAVIGATION_PLANNER
HISTORICAL_GRAPH_PRIOR != CURRENT_WORLD_TRUTH
── 接缝（审核轮一/二，不回退 [I]）──
SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE
CURRENTLY_GROUNDABLE != AUTHORIZED
LOGICAL_ITEM_RECONCILIATION != SILENT_PROGRESS_REWRITE
MULTI_SOURCE_CORROBORATION != OCCURRENCE_IDENTITY
LATER_REVISION != STRONGER_EVIDENCE
SAME_DESTINATION != SAME_LOGICAL_ITEM
SEMANTIC_AFFORDANCE != AGENT_ADMISSION
SLICE_ACCEPTANCE != SOURCE_CORRESPONDENCE != OCCURRENCE_MATERIALIZATION
（规则）Slice + Occurrence[] + bound FastAssessment[] = 一次 atomic reducer commit（无 dangling refs）
（规则）COVERAGE IS SPATIAL_REGION_SCOPED BEFORE CONTAINER AGGREGATION
OCCURRENCE_BELONGS_TO_SLICE
REGION_BINDING != OWNERSHIP
── v3-final（本轮收敛）──
（规则）SLICE = ACCEPTED STABLE FRESH VIEWPORT；rejected/transient Observation 不 materialize Slice
（规则）OCCURRENCE = ACCEPTED PRIMARY VIEWPORT VISUAL OCCURRENCE
（规则）STRUCTURED_EVIDENCE MAY CORROBORATE AN OCCURRENCE BUT DOES NOT CREATE VISUAL OCCURRENCE TRUTH
（规则）SOURCE_AUTHORITY IS CLAIM_SPECIFIC
REGION_RELATIVE_BOUNDS != ACTION_GROUNDING_AUTHORITY
REGION_BINDING != OCCURRENCE_IDENTITY
ENTRY_RELATION != RETURN_RELATION
RETURN_EXPECTATION != RETURN_TRUTH
FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION
（规则）ANCHOR_BARRIER = CRITICAL_INITIAL_IDENTITY_AMBIGUITY
（撤回）MULTI_SOURCE_MATERIALIZATION MAY BE SOURCE_NEUTRAL —— 已删除 [RB-F1]
```

## 6. Symbol 映射（v3-final 收紧）

| 现有 symbol | 处置 | 说明 |
|---|---|---|
| `ContainerTransitionOccurrence` / `ContainerRuntimeV2Reducer` / `ContainerGraphQuery` | REUSE | R8 成熟 |
| `CurrentContainer` / `ContainerEntryContext` | **REUSE（原样保留）** | 不新增/替换/降级 `[D]` |
| `ContainerSemanticCorrectionFact` / `ContainerObligationReevaluationInput` / `Agent.ContainerReconciliation` progress 对账 | REUSE PATTERN / EXTEND-CANDIDATE | pattern 非 type；payload 待 Mapping `[U]` |
| `_branchProgress` / `BranchProgressEvidence` | NOT_REWRITTEN | Agent-owned evidence aggregate；只改 admission 资格 `[J]` |
| `Observation.SequenceNumber` / `SemanticEvidenceRevision` / trace 链 | REPOSITORY_MAPPING_REQUIRED | ordering primitive 候选；须保留 OCC stale-rejection 买家 `[RB-F3]` |
| Rejected candidate diagnostics | **REUSE / EXTEND 既有 Trace / Validation first** | CREATE 仅当 Mapping 证明无合适 owner `[RB-F5]` |
| `ContainerSliceRef` | REUSE | ref 类型 |
| `ContainerSlice`（薄） | EXTEND | + ViewportBounds / SpatialRegion[] / OccurrenceRefs / StabilityEvidence(acceptance 输入) |
| `ObservedElement` | EXTEND | 剥 identity；留观察事实 |
| `StructuredElementEvidence` | EXTEND | **corroboration only**；不 materialize Occurrence `[RB-F1]` |
| `PerceptionCandidate`（Adapters） | SPLIT | Primitive 层留 adapter；瞬态 |
| `RowIdentityContext` / known_rows header | 降级 | stabilizer 内部机制 |
| `NormalizeType` | 退役 | PrimitiveKind + taxonomy 映射（双轨 shadow） |
| `Container/` page-local owner | Repository Mapping 强制项 | `[R7]` |
| Occurrence / LogicalItem / LocalModel / SliceRelation(RegionRelations) / SpatialRegion / OccurrenceRegionBinding / RegionCoverageProjection / Acceptance 三函数 / EvidencePolicy / CanonicalDelta | CREATE | 无现成 owner |
| `SlowContainerSemanticRequest` / `ISlowContainerSemanticAdvisor` | P1 再改 | payload 等模型稳定 |

## 7. Contract Freeze 维度 × Implementation Sequencing（v3-final 分离 `[S]`）

**P0 = 契约冻结维度；Stage = 实施顺序维度；两者不是同一套编号。**

```text
P0-A — Evidence Foundation contract
P0-B — Canonical World contract
P0-C — Consumption Boundary contract

Stage B1 — Spatial Foundation        ：SpatialRegion / Region association / SliceRelation
Stage B2 — Accepted Evidence Model   ：Acceptance / Slice / Occurrence / FastAssessment
Stage C1 — Canonical World           ：LocalModel / LogicalItem / CanonicalMembership /
                                       EvidencePolicy / Reconciliation
Stage C2 — Consumption Boundary      ：Agent admission / Grounding / Canonical supersession /
                                       Progress reconciliation
Stage D  — Slow Semantic Repair
Stage E  — Coverage Cutover
Stage F  — Fresh Phase 2.6 Acceptance
```

- 实施顺序：B1 → B2 → C1 → C2 → D → E → F；统一双轨 shadow 模式。
- Mapping 前置约束：RB-F3 ordering primitive + R7 owner budget 是 P0 契约表输入。

## 8. 决策记录（三轮汇总）

### 8.1 问答轮（v1）——最终状态标注

原 20 问决策表见历史；v3-final 最终状态：**Q3（stability 分级）撤回** `[RB-F2]`；**Q6（双通道合并）收敛为 structured corroboration-only** `[RB-F1]`；Q5 = REPOSITORY_MAPPING_REQUIRED `[RB-F3]`；Q1 表述精确化（accepted stable）；其余保持。

### 8.2 审核轮一（v2 `[R#]`）+ 审核轮二（v3 `[RB-n]`）

处置记录见历史稿；以下核心接缝**不回退** `[I]`：

```text
PERCEPTION_CANDIDATE != OCCURRENCE / OCCURRENCE != LOGICAL_ITEM / LOGICAL_ITEM != TRAVERSAL_OBLIGATION
SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE != AUTHORIZED
Canonical rewrite → explicit Agent progress reconciliation
EvidencePolicy = claim-specific（可纠错、防振荡不靠"同档永不翻转"）
Coverage != Semantic Resolution != Traversal Completion
Slow → typed claims → deterministic reconciliation；!= action/completion/graph-mutation authority
Runtime semantic claim != Agent Admission
Acceptance 三分 + atomic commit
OccurrenceRegionBinding + Region-scoped Coverage + Region-aligned SliceRelation
```

### 8.3 最终收敛轮（v3-final）

见 §9.3 Architecture Rebuttal Record（RB-F1..F8）。

## 9. Architecture Rebuttal Record

> 9.1/9.2 历史轮记录已并入 §8.2 汇总（全文见 git 历史）；9.3 为本轮最终收敛的撤回/修正记录，每条含 Original Claim / Why Rejected / Failure Consequence / Revised Contract / Affected Models / Status。

### 9.3 最终收敛轮（v3-final）

#### RB-F1 · structured-only → Occurrence（撤回 source-neutral materialization）

- **Original Claim**: v3 写"materialization source-neutral：vision-only / structured-only 均可 materialize Occurrence，另一通道 corroboration"。
- **Why Rejected**: 当日原始模型中 Occurrence = Runtime accepted **visual** viewport occurrence，回答"这一眼中正式接受了哪个局部视觉实例"。structured-only 也能铸造 Occurrence 后，Occurrence 从 accepted visual instance 变成 generic multi-source observed entity，必须新增 `IsVisuallyObserved / SourceKind / GroundableSource / PrimaryEvidence` 等字段重新区分 —— 模型膨胀；且削弱已确立的 Vision primary / Structured auxiliary 权威结构。
- **Failure Consequence**: structured 节点离屏/不可见（报 clickable 但屏上无视觉实例）时产生无视觉对应的"权威"实体，重新打开 `HISTORICAL_BOUNDS != ACTION_GROUNDING` 要封的类别；Occurrence 定义漂移传染 Slice/SliceRelation/coverage 全链。
- **Revised Contract**: `OCCURRENCE = ACCEPTED PRIMARY VIEWPORT VISUAL OCCURRENCE`；`StructuredEvidence → correspondence → corroborate（StateHints/SourceEvidenceRefs）`；无 visual correspondence → unmatched auxiliary evidence；禁止 `StructuredEvidence → Occurrence`。删除 `MULTI_SOURCE_MATERIALIZATION MAY BE SOURCE_NEUTRAL`；保留 `SOURCE_AUTHORITY IS CLAIM_SPECIFIC` / `MULTI_SOURCE_CORROBORATION != OCCURRENCE_IDENTITY`。
- **Affected Models**: §2.2 / §3.2 / §3.4 / §5 / §6（StructuredElementEvidence → corroboration only）。
- **Status**: ACCEPTED_REVISION（structured-primary 环境建模 = DEFERRED 附注）

#### RB-F2 · TRANSIENT Slice（撤回 stability 分级）

- **Original Claim**: v2/v3 写"Slice 带 stability 分级（STABLE/TRANSIENT）；TRANSIENT 进 evidence 链、不计 coverage、correlation 降权"。
- **Why Rejected**: 当日最初定义 Slice = Runtime-accepted **stable** fresh viewport evidence + local spatial frame。TRANSIENT 成为 Slice 后必须继续回答：能否 grounding / 是否参与 canonicalization / SliceRelation / relocation anchor / coverage / 与 stable Slice 的 authority 差 —— 这些复杂度无当前 buyer，是模型自己制造的。
- **Failure Consequence**: 每个下游消费者（grounding、correlation、coverage、relocation）都要携带"transient 降权"分支；acceptance 判定与 Slice 消费语义纠缠。
- **Revised Contract**: `SLICE = ACCEPTED STABLE FRESH VIEWPORT`；settling/transient Observation → StabilityEvidence / Trace / diagnostic（acceptance **输入**证据），不 materialize Slice；删除 `Slice.Stability = STABLE/TRANSIENT`；连带删除 TRANSIENT-origin grounding 条目（EdgeClipped policy 保留）。
- **Affected Models**: §3.3 / §3.4 / §12。
- **Status**: ACCEPTED_REVISION

#### RB-F3 · global SemanticEvidenceRevision mandatory（撤回冻结）

- **Original Claim**: v1/v2 将"全 Run 单一 SemanticEvidenceRevision 流"列为 frozen decision。
- **Why Rejected**: correctness 真正依赖显式 causal binding（NodeRef/ObservationRef/SliceRef/OccurrenceRef/TransitionOccurrenceRef/Slow bindings），不是 global semantic clock；且已冻结 `LATER_REVISION != STRONGER_EVIDENCE` / `REVISION_ORDER != CAUSAL_BINDING` —— 统一 sequence 的买家只剩 trace / total ordering / debug convenience。
- **Failure Consequence**: 为架构整洁新造 global clock 会诱导下游把 revision 当 freshness/truth 使用（正是已冻结不变量要防的）。
- **Revised Contract**: Q5 = **REPOSITORY_MAPPING_REQUIRED**；已有自然 run-local monotonic sequence 则 REUSE，否则不得新造；`EvidenceOrdering = optional trace metadata / CausalBinding = explicit refs / Freshness = claim-specific binding`。Mapping 约束：保留 OCC stale-rejection 买家（`ContainerRuntimeV2Reducer`）。
- **Affected Models**: §3.3 / §6 / §7（Mapping 前置）。
- **Status**: REPOSITORY_MAPPING_REQUIRED

#### RB-F4 · LogicalItem hierarchy Day-1（撤回 P0 冻结）

- **Original Claim**: v2 写"V1 模型留 parent/children refs，不实现深层级"。
- **Why Rejected**: Q11（什么算 independent interaction region）无 deterministic criterion；提前冻结 refs 迫使 canonicalization → nested canonicalization，复杂度提前爆炸；也违反"未实现功能不提前建 stub"纪律。
- **Failure Consequence**: B2/C1 被迫实现无 buyer 的嵌套 membership/merge/split 语义。
- **Revised Contract**: `LOGICAL_ITEM_HIERARCHY = DEFERRED`；P0-B 不含 Parent/Child refs，保留扩展 seam；V1 组合表达 = flat + GROUP structure + membership evidence；未来 Climate Tile 类 buyer 出现明确 independent interaction criteria 后再购买。保持"一个 actionable LogicalItem = 一个主 interaction semantics"；禁止 `Affordance = Set<...>`。
- **Affected Models**: §3.7 / §4。
- **Status**: DEFERRED（解锁条件 = Q11 deterministic criterion）

#### RB-F5 · PerceptionDiagnosticEvidence mandatory CREATE（撤回）

- **Original Claim**: v2/v3 将 `PerceptionDiagnosticEvidence` 作为新 Runtime domain type CREATE。
- **Why Rejected**: Candidate 原定义即 transient provider output；rejected candidate 需要的是 traceability/diagnostics，不等于 Runtime world model entity。
- **Failure Consequence**: 候选层以诊断之名重新成为 persisted model（第四种实体），违背"candidate 瞬态"原则。
- **Revised Contract**: 优先 REUSE/EXTEND 既有 Trace / Validation evidence / perception causal trace，只记录 `ObservationRef / candidate summary / reject reason / validator decision`；CREATE 仅当 Repository Mapping 证明无合适 owner。
- **Affected Models**: §3.1 / §6。
- **Status**: REPOSITORY_MAPPING_REQUIRED（REUSE/EXTEND first）

#### RB-F6 · exact taxonomy enum frozen（撤回全集冻结）

- **Original Claim**: v1–v3 的 taxonomy 表隐含 enum 全集已定。
- **Why Rejected**: 当日达成的是**组合建模方式**（PrimitiveKind != LogicalStructure != Affordance != MemberRole != State），不是具体值集；真实 Settings / IVI buyer 未逐值购买。
- **Failure Consequence**: 未购买值被冻结后，merge/rename/remove 都变成 contract change；或反向诱导回到 `MENU_ITEM_WITH_SUBTITLE` 式组合枚举爆炸。
- **Revised Contract**: 冻结 = 组合建模方式；全部 enum 值标记 `V1 CANDIDATE TAXONOMY / NOT CONTRACT-FROZEN`，可 merge/rename/remove/extend；禁止组合枚举回归。
- **Affected Models**: §4。
- **Status**: ACCEPTED_REVISION

#### RB-F7 · ContentRelativeBounds 命名残留（SpatialRegion 改名后未对齐）

- **Original Claim**: v3 已将 ContentRegion 改名 SpatialRegion，但坐标仍叫 `ContentRelativeBounds`。
- **Why Rejected**: SpatialRegion 不再只表示 content（FixedChrome / PersistentControlBar / Overlay 都不是 content）；命名残留会让"region 坐标"被误读为"内容坐标"。
- **Failure Consequence**: 概念漂移：fixed chrome 内 occurrence 的 region-local 坐标被当作"content 坐标"消费，coverage/correlation 语义混乱。
- **Revised Contract**: `ContentRelativeBounds → RegionRelativeBounds`；Occurrence = `ScreenBounds + RegionBinding + RegionRelativeBounds?`；ambiguous binding 时 ScreenBounds 有效、RegionRelativeBounds 不作 authoritative correlation evidence；`REGION_RELATIVE_BOUNDS != ACTION_GROUNDING_AUTHORITY`。
- **Affected Models**: §3.3 / §3.4 / §3.5。
- **Status**: ACCEPTED_REVISION

#### RB-F8 · 首帧自动调 Slow（Anchor barrier 语义修正）

- **Original Claim**: v3 §12 R2' 容器写"Anchor barrier（首帧 Slow 验结构）"，隐含每个 Container 首帧自动过 Slow。
- **Why Rejected**: 这会让 Slow 从 low-frequency corrector 退化为高频 verifier，直接违背其定位；Anchor barrier 的本义是 critical initial identity ambiguity。
- **Failure Consequence**: Slow invocation rate 失控（Stage F 条件 C3 直接被击穿）；高频低价值调用稀释 Slow precision 指标。
- **Revised Contract**: `FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION`；`ANCHOR_BARRIER = CRITICAL_INITIAL_IDENTITY_AMBIGUITY`（仅 critical initial container identity 未解决/冲突/decision-critical 时触发）。
- **Affected Models**: §3.9 / §12（R2' 容器改写）。
- **Status**: ACCEPTED_REVISION

## 10. 残留开放问题

- **Q11**：independent interaction region 判据（hierarchy DEFERRED 的解锁条件）。
- EvidencePolicy aggregation / margin / hysteresis / conflict 参数设计 → Stage C1（R3' TO_VALIDATE，对抗用例 = RB-04 反例）。
- Closure ResolutionPolicy 具体设计 → Stage D/E。
- Evidence ordering primitive + OCC 约束 → Repository Mapping（RB-F3）。
- LocalModel owner-budget 证明 → Repository Mapping（R7）。
- Rejected candidate diagnostics owner → Repository Mapping（RB-F5）。
- Q2（settle 检测接口）、Q7（archived 上界）、RB-08 阈值、barrier 判据字段、Graph duplicate view、不变量落位 `[X3]`。

## 11. 下一步（仅限以下两项）

1. **P0-A / P0-B / P0-C 最小模型契约表**（唯一问题 / Owner / 字段 / immutable? / 证据来源 / derived view / 合法消费者 / 禁止 authority）。
2. **Repository Mapping**（RB-F3 ordering primitive / R7 owner budget / RB-F5 diagnostics owner / `Container/` page-local owner 逐项 / supersession payload 适配）。

**本轮完成后停止脑暴新对象。** 不生成实现 WorkItem；不进入 OpenSpec；不改 Runtime behavior。

## 12. 可行性推导（论证强度修正版 `[RB-15/T]`）

**结论表述**：所有当前已分析 blocker 均能映射到候选架构中的 **proposed** containment/cut seam；是否真实成立需 **Stage B–F implementation + fresh real-device evidence 验证**。本节不声称"已证明可行"。

### 12.1 逐案例 proposed seam 映射

| 案例 | 旧穿透路径 | proposed seam | 机制（待验证） |
|---|---|---|---|
| r5 | 转场穿透 | cut | 期望无投票权；CurrentContainer 跟随 observed |
| Z4 StableKey 污染 / transition seam | 身份穿透 | 污染 cut（Node-scoped correlation）；boundary seam contained | Anomaly barrier + 可推翻投影 + Graph CHALLENGED —— 最弱环，Stage B1 优先 instrument |
| FRAME_LOCAL_FUSION_INSTABILITY | 清单穿透 | cut | 帧级翻转 = 低档 hint；aggregation 压倒；completeness 解耦 |
| title-off / 孪生文本 | 身份+清单穿透 | cut（语义 actionability）；残留 confidently-wrong | Anchor barrier（**critical identity ambiguity 才触发** `[RB-F8]`）+ interaction 证据 + 可推翻性 —— 次优先 instrument |
| 快滚/sticky 假完成 | 覆盖穿透 | cut | gap 证据；SpatialRegion 排除 |
| Z5 + UI-TARS-2B 36.4% | 模型权威穿透 | 权威 cut / 性能 contained | typed claims + EvidencePolicy；Slow 精度 = Stage F 条件 |
| Z7 deep Unknown | 完成穿透 | contained | 非阻塞 + bounded ResolutionPolicy |
| BGE 向量匹配 | — | 一致 | correlation prior |

### 12.2 新架构自身失效模式

| # | 失效模式 | 容器 | 状态 |
|---|---|---|---|
| R1' | boundary 误判 | Anomaly barrier + 可推翻投影 | CONTAINED / TO_VALIDATE；Stage B1 instrument |
| R2' | confidently-wrong 解析 | Anchor barrier（critical ambiguity 触发）+ interaction 证据 + 可推翻性 | CONTAINED / TO_VALIDATE；次优先 instrument |
| R3' | membership 振荡 | aggregation + margin + hysteresis + conflict state | CONTAINED / TO_VALIDATE（同档永不翻转已废除） |
| R4' | 双轨 shadow 不收敛 | 分歧率阈值 + kill criteria | 流程风险 |
| R5' | barrier 过宽 / Slow 高频化 | 策略表可调 + Stage F 量测（RB-F8 后首帧不再自动调 Slow） | 条件 |
| R6' | ungroundable obligation | Recovery/ → incomplete-with-evidence | 需显式化（§3.8） |
| R7' | archived 膨胀 | Q7 待定 | 低 |

### 12.3 可证伪预言（Stage F）

- **P1**：blocker **可以迁移**，但不得再无边界穿透至 **Action authority / Graph authority / Agent progress / Completion**；新 blocker 应稳定落入明确的 **owner / classification seam / evidence boundary / recovery-closure path** —— 否则架构 containment 模型被证伪。
- **P2**：fresh run completed > 0/19，假完成率（coverage 独立复核）下降。
- **P3**：Slow invocation rate 有界（首帧自动调用废除后）。
- **P4**：membership 翻转率低且由 aggregation/margin 机制解释（RB-04 反例过对抗测试）。
- **P5**：boundary 误判被 Anomaly barrier 捕获比例可度量。

## 13. 完成条件自检（`[X]` 十项；gate 由 Leader 独立检查后发布）

| # | 条件 | 自检 | 依据 |
|---|---|---|---|
| 1 | Occurrence 恢复 accepted visual occurrence | ✅ | §3.4 / RB-F1 |
| 2 | Slice 恢复 accepted stable viewport | ✅ | §3.3 / RB-F2 |
| 3 | Structured 仅 corroboration，不从后门获 primary authority | ✅ | §3.4（corroborate-only 链）/ §6 / RB-F1 |
| 4 | SpatialRegion / RegionRelativeBounds / RegionCoverage 闭环 | ✅ | §3.3–3.5 / §3.10 / RB-F7 + RB-08/09/10 |
| 5 | CurrentContainer.EntryContext 明确保留 | ✅ | §3.12 / `[D]` |
| 6 | Admission / Grounding / Authorization 三分离 | ✅ | §3.7 / §5 |
| 7 | Canonical supersession → Agent progress reconciliation 闭环 | ✅ | §3.8（REUSE PATTERN / EXTEND-CANDIDATE） |
| 8 | EvidencePolicy claim-specific 且可纠错、防振荡 | ✅ | §3.7 / RB-04·O（aggregation+margin+hysteresis；TO_VALIDATE = Stage C1 对抗测试） |
| 9 | Revision / hierarchy / diagnostic type / exact enum 均不被过早冻结 | ✅ | RB-F3（MAPPING_REQUIRED）/ RB-F4（DEFERRED）/ RB-F5（REUSE first）/ RB-F6（candidate taxonomy） |
| 10 | 文档没有新增第二 truth owner | ✅ | 全文 owner 检查：Runtime 只增 evidence/projection owner（acceptance、LocalModel 聚合）；Agent/Graph/Current 权威未动；无并列 truth 源 |

**自检结论**：10/10 满足。据此本文档为 `CONTAINER_RUNTIME_V2_EVIDENCE_MODEL` **READY_FOR_P0_CONTRACT_FREEZE 的候选**，正式发布由 Leader 对 v3-final 独立检查确认。

**边界重申**：该状态仅代表"脑暴模型收敛完毕，可以开始写 P0-A/B/C 最小契约 + Repository Mapping"；**不代表** OpenSpec approved、implementation authorized、architecture graduated。本轮不生成实现 WorkItem。

---

## 14. FINAL ARCHITECTURE CONSISTENCY REVIEW（独立审核记录，2026-09）

> 审核方式：不以 §13 自评为据，对全文执行残留语义 grep 扫描（TRANSIENT/settling、ContentRegion/ContentRelativeBounds、materialize/STRUCTURED_EVIDENCE、`_branchProgress` 公式、同档/升档、首帧/Anchor、DEFERRED/MAPPING 标记、groundable/admission）+ 主链/辅助链与 baseline 逐段比对 + truth-owner 清单核对。历史撤回记录（RB-F*）中的旧语义引用不计为残留（撤回记录必须保留 Original Claim）。

| Concern | Expected Contract | Document Evidence | Verdict | Residual Risk |
|---|---|---|---|---|
| Occurrence | = accepted primary viewport **visual** occurrence；无 structured-only → Occurrence 语义 | §3.4 / §5 / §6（corroboration only）；旧语义仅存于 RB-F1 撤回记录 | PASS | structured-primary 环境建模 DEFERRED（范围外） |
| Slice | = accepted **stable** fresh viewport；TRANSIENT/settling 不成 Slice entity | §3.3 / RB-F2；transient 仅存于 acceptance 输入证据路由 | PASS | "stable" 的 deterministic 判据（Q2/settle 接口）待 P0-A 契约定 |
| Structured authority | corroborate / state hint / auxiliary only；不获 visual/grounding/identity/coverage truth | §3.4 corroborate-only 链 / SOURCE_AUTHORITY IS CLAIM_SPECIFIC | PASS | 无（同 Occurrence 行的 DEFERRED 附注） |
| SpatialRegion | 全文一致 SpatialRegion + RegionRelativeBounds；binding = association 非 ownership | 命名残留仅存于 RB-F7 撤回记录；§3.3/§3.4 一致 | PASS | 无 |
| Region Coverage | 先 Region-scoped 再 Container aggregate；无 PrimaryRegion-exhausted→complete 捷径 | §3.10（RegionCoverageProjection → aggregate(participatesInCoverage)） | PASS | 阈值/判据 TO_VALIDATE |
| SliceRelation | 保留 region-bound seam；不写死 one global dy；ALIGNMENT != IDENTITY | §3.5 RegionRelations[]（V1 Count=1） | PASS | V1 单 region 算法不确定度 TO_VALIDATE |
| LogicalItem | LocalModel-scoped canonical；不持历史 bounds 权威/obligation 等价/Agent policy/cross-run ID；hierarchy DEFERRED | §3.6/§3.7（RB-F4） | PASS | Q11 判据未定（hierarchy 解锁条件） |
| Admission/Grounding | 三分：SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE != AUTHORIZED；无"必须 groundable 才能 admission"残留 | §3.7 / §5；全文无反向表述 | PASS | 无 |
| Supersession | CanonicalDelta → Agent progress reevaluation；不静默改写 _branchProgress；_branchProgress = evidence aggregate（非 Authorized-Completed 等式） | §3.8（aggregate + derived view 公式正确）/ LOGICAL_ITEM_RECONCILIATION != SILENT_PROGRESS_REWRITE | PASS | payload 适配 → Repository Mapping（已产出结论） |
| EvidencePolicy | claim-specific；无全局排序；无"同档永不翻转"；canonicalization 不消费 Agent Goal/Policy | §3.7（同档可推翻、aggregation/margin/hysteresis）/ SEMANTIC_AFFORDANCE != AGENT_ADMISSION | PASS | 参数设计 TO_VALIDATE（Stage C1 对抗测试） |
| Slow | low-frequency corrector；无 first-Slice 自动调用；Anchor = critical initial identity ambiguity | §3.9 FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION / §12 R2' 已改写 | PASS | barrier 判据字段待 Stage D |
| Revision | global revision 非冻结；不新造 global semantic clock；REVISION != FRESHNESS/TRUTH/CAUSAL_BINDING | §3.3（MAPPING_REQUIRED + OCC 约束）/ RB-F3 | PASS | primitive 选择 → Repository Mapping（已产出结论：REUSE） |
| Taxonomy | 只冻结组合方式；enum 全集 = V1 candidate 非 Contract | §4 / RB-F6 | PASS | 无 |
| EntryContext | CurrentContainer = NodeRef + CurrentSliceRef + EntryContext 原样保留 | §3.12 / §6（REUSE 原样） | PASS | 无 |
| Truth-owner duplication | 无双 current/canonical/coverage/progress owner | CurrentContainer(指针) vs LocalModel(per-Node 工作记忆) 角色分离；canonical 唯一 owner=LocalModel；coverage owner=region projection in LocalModel；progress owner=Agent（NOT_REWRITTEN） | PASS | 迁移期 Container 旧 page-local state 与 LocalModel 并存 → 由 Repository Mapping 的处置表 + 双轨 shadow 界定（见 mapping 文档） |

**审核结论**：15/15 PASS，无残留旧语义（全部撤回项仅存于 Rebuttal Record 历史引用），讨论结论与新假设无混写（六项延迟项均处 NOT_FROZEN / DEFERRED / REPOSITORY_MAPPING_REQUIRED），feasibility 表述为 proposed seam（非 proof），falsifier 为穿透边界判定。

**正式发布**：

```text
CONTAINER_RUNTIME_V2_EVIDENCE_MODEL
READY_FOR_P0_CONTRACT_FREEZE

NOT_OPENSPEC_APPROVED
NOT_IMPLEMENTATION_AUTHORIZED
NOT_GRADUATED
```

**Architecture Brainstorm 就此停止。** 下一工作阶段仅：① P0-A/B/C 最小模型契约表；② Repository Mapping（两项产物已产出：`container-runtime-v2-p0-contract-tables.md` / `container-runtime-v2-repository-mapping.md`）。进入 OpenSpec propose 需 Human 再授权。
