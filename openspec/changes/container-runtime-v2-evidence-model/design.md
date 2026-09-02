# Design: Container Runtime V2 — Evidence Model & Semantic Convergence Refactor

> 架构事实源（优先级）：P0 Contract Tables（`docs/analysis/container-runtime-v2-p0-contract-tables.md`）> Final Brainstorm（`docs/analysis/container-runtime-v2-horizontal-chain-model-refactor-brainstorm.md`，v3-final）> Repository Mapping（`docs/analysis/container-runtime-v2-repository-mapping.md`）> Historical working drafts。本 design 不重新设计任何已收敛模型；发现不自洽将报告 SPEC_BLOCKER / HUMAN_GATE_REQUIRED。

## Context

R8 已落地 ownership 层（`src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs`：`ContainerRuntimeV2State` / `CurrentContainer` / `ContainerGraphSnapshot` / 纯 `ContainerRuntimeV2Reducer` + `ContainerGraphQuery`）。当前缺位的是 evidence model 左半段：`ContainerSlice` 仅为薄记录；无 Occurrence / LogicalItem / LocalModel / SliceRelation 领域模型；`ObservedElement.StableKey`（= `PerceptionCandidate.RowId`，`PhysicalEnvironment.cs:177`）承担 identity 权威；`NormalizeType` 以 menu_item 单枚举整流类型。Phase 2.6 证据（r5 / Z4 / Z5 / Z7 / FRAME_LOCAL_FUSION_INSTABILITY）已在 brainstorm §14 审核闭环。

机械约束：ArchitectureGuardTests（零 ProjectReference / 禁旧 namespace）；`scripts/check-consistency.sh`；dotnet build/test 全绿。

## Goals / Non-Goals

**Goals:**

- 将 P0-A/B/C 契约表（16 个模型契约）落地为真实 symbol，完成"不可靠感知 → accepted evidence → container-local canonical world → Agent consumption → fresh grounding → execution loop"闭环。
- 完成 truth-owner 集中迁移（NET_NEW_MUTABLE_TRUTH = +1）与三项 shadow 切换（StableKey / taxonomy / coverage source）。

**Non-Goals:**

- 不提升感知精度（不修 YOLO/OCR 病灶）；不实现 Slow payload 细节（P1/Stage D）；不引入 LogicalItem hierarchy（DEFERRED，解锁条件 Q11）；不新建 global semantic clock；不修改 R8 reducer 语义、GoalEvidence、Recovery；不做车机多 region 算法（模型留 seam，V1 只实现 Primary）。

## Decisions

### D1. 模型放置与 reducer 延伸

新模型全部落在 `src/UniClaw.Runtime/Model/`（纯不可变，无 owner）；Acceptance 三纯函数落 `Capabilities/Perception/`；SliceRelation CV 计算落 `UniClaw.Runtime.Adapters` 能力端口（保零 ProjectReference guard）。`ContainerRuntimeV2Reducer` 延伸：`SliceAcceptance` 的 atomic commit（Slice + Occurrence[] + FastAssessment[]）作为新的 reduction input 种类进入同一纯替换 seam，复用 `SemanticEvidenceRevision` 作 commit version（OCC stale 拒绝原位保留，Repository Mapping #3）。
*替代案*：为 Slice/Occurrence 建独立 reducer —— 拒绝，理由：引入第二个替换 seam 与 revision 域，违背既有原子模式。

### D2. Truth-owner 迁移（Repository Mapping #1/#2 落地）

LocalModel 为**集中式**新 canonical local-world owner（per-Node 不可变聚合，入 `ContainerRuntimeV2State`），不是平行 truth owner。`Container.cs` 7 项 page-local world state 逐项处置：`_viewportExplorationObservations` → MOVE（LocalModel.Evidence.SliceRefs）；`_localPageBeliefState` → MOVE（Assessments；`SemanticReconciliation.FuseBelief` 保留为聚合器）；`_executedSteps` → MOVE/DERIVE（Agent `BranchProgressEvidence` + interaction evidence）；`_observation` → DERIVE（CurrentSliceRef→Slice）；`_isLocalComplete` → DERIVE（三条件判定）；`_objectBindings` → DELETE/REPLACE（B2，由 Occurrence membership evidence 取代）；`_objectStateBeliefs` → DERIVE（StateHints + LogicalItem.State）。保留：identityRule/SemanticPageName（降级为 SemanticIdentityCandidate 证据源）、CP12 executor forwarding。
迁移期 shadow 只用于 comparison / divergence measurement，**不得形成双写正式 truth**；切换后旧 world-state 退役并以 ArchitectureGuard 机械验证（Container 不再出现 world-state 字段）。

### D3. 三项 shadow 切换（统一模式：shadow → divergence → cutover）

- **StableKey 退役**：B2 起 Occurrence 携带 StabilizerHint；身份判断切 LocalModel correlation；旧 StableKey 签名路径 shadow 跑分歧率（复用 `RowIdentityContext` 对账），阈值收敛后删除 identity 权威。
- **taxonomy 映射**：`NormalizeType` 的旧 label → PrimitiveKind + 组合映射函数 shadow 比对；收敛后退役 `NormalizeType`。
- **coverage 切源**：E 阶段旧 no-new/settle 出正式判定，region-scoped chain shadow 跑；收敛后切正式源。
每项预设分歧率阈值与 kill criteria；禁止 big-bang。

### D4. Supersession 最小契约（Repository Mapping #5）

`ContainerSemanticCorrectionFact` / `ContainerObligationReevaluationInput` 仅作 REUSE PATTERN（revision-bound fact + owner reevaluation input + no-mutation flags）；不 EXTEND（payload 是 container-identity 域，扩成万能 envelope 被 RB-14 禁止）。CREATE 最小 `LogicalItemSupersession { PriorLogicalItemRefs[], ResultingLogicalItemRefs[], DeltaKind, EvidenceRefs, BindingRefs }`；消费机制 REUSE `Agent.ContainerReconciliation` 的 progress 对账 pattern（`ProgressLedgerKeysMatch` / `IsExactCompletedSiblingReplacement` / `SameStableProgress`）。

### D5. 诊断承载（Repository Mapping #4）

EXTEND `RuntimeObservability` span/event 通道承载 acceptance validator 决策（ObservationRef + candidate summary + reject reason + validator decision）。不 CREATE Runtime domain diagnostic entity；若 Stage B2 发现 span 通道无法满足 anomaly 结构化消费，回到 mapping 升级（需新证据 + Human Gate）。

### D6. 验收 buyer → 场景/测试映射（可证伪设计）

验收重点：错误不得无边界穿透至 Action / Graph authority / Agent progress / Completion。falsifier 判定 = 新 blocker 可出现、可迁移，但必须落入明确 owner / classification seam / evidence boundary / recovery-closure path。

| Buyer（证据源） | 验证场景（specs 场景） | 穿透边界 |
|---|---|---|
| r5 observed != expected | consumption-boundary "期望与观察脱节以观察为准" | Action/Graph |
| Z4 StableKey 污染 | evidence-foundation"structured 无视觉对应" + canonical-world"帧级翻转" + shadow 分歧率 | Graph/identity |
| FRAME_LOCAL_FUSION_INSTABILITY | canonical-world"帧级类别翻转不污染逻辑对象" | inventory→obligation |
| title/subtitle/twin grouping | canonical-world"孪生文本不产生重复清单项" / "标题不被误纳" | Progress |
| sticky / fast-scroll 假完成 | evidence-foundation"快滚 gap" + consumption"快滚假完成被阻止" | Completion |
| deep Unknown | consumption"深层 Unknown 不无限阻塞" + 三条件完成 | Completion |
| Slow false promotion | consumption"Slow 错误提升被容纳" | 全部 |
| merge/split after admission | consumption"合并不吞没已完成进度" | Progress |
| ungroundable relocation | consumption"relocation 后重新验证" / "无法 grounding 以证据收尾" | Action |
| multi-region IVI fixture | consumption"单一区域穷尽不等于容器完成" | Completion |
| structured-visual 冲突 | evidence-foundation"structured 与 visual 冲突" | 全部 |

## Risks / Trade-offs

- [boundary 误判 → 证据进错 LocalModel] → Anomaly barrier + 可推翻投影 + Graph CHALLENGED 路径；Stage B1 即埋观测（P5 指标），不等 Stage F。
- [confidently-wrong 语义解析（标题实可点被判 STATIC）] → Anchor barrier（仅 critical identity ambiguity）+ interaction 证据 + 可推翻性；Stage B1 起观测。
- [EvidencePolicy 参数不收敛 / membership 振荡] → C1 阶段以 RB-04 反例（1 旧 Slice vs 5 新同档）做对抗测试；margin/hysteresis 参数化。
- [shadow 双轨不收敛] → 预设分歧率阈值 + kill criteria；不收敛即回滚旧路径并报告 blocker。
- [Container 旧 state 迁移破坏既有场景] → 每 Stage 走既有 dotnet test 全绿 + ArchitectureGuard；处置表逐项迁移、每项独立验证。
- [LocalModel 聚合体积膨胀] → active/archived 分层（archived 保留 relocation 锚）；容量上界策略 DEFER（Q7）。

## Migration Plan

Stage B1（Spatial Foundation：SpatialRegion / RegionBinding / SliceRelation —— 只产 evidence，零行为变化）→ B2（Acceptance / Slice / Occurrence / FastAssessment + Container state 处置 + StableKey/taxonomy shadow）→ C1（LocalModel / LogicalItem / EvidencePolicy / Reconciler）→ C2（admission 输入迁移 / grounding / supersession / coverage 消费边界）→ D（Slow payload，P1 契约另定）→ E（coverage shadow 切源）→ F（fresh Phase 2.6 acceptance：冻结 Fast baseline，量测 completed / blocker 穿透 / Slow precision-rescue-invocation rate / membership churn / boundary 捕获率）。每 Stage 末 dotnet build+test+guard 全绿；回滚 = shadow 未切旧路径仍为正式 authority。

## Open Questions

- EvidencePolicy 数值参数（margin/hysteresis 阈值）→ C1 调校，不改变 spec 行为。
- RegionBinding overlap 阈值、acceptance settle 判据的具体取值 → B1/B2 实验定值，spec 只约束行为。
- Graph duplicate / non-referencable view 语义细节（Stage C 范围内、不影响本 change 契约）。

---

## Appendix A — Static Model / Ownership View（解释性视图）

> Diagrams are explanatory views of the normative requirements in the specs. If a diagram conflicts with a normative requirement, the spec requirement is authoritative.
> 图中字段以 P0 Contract Tables 为准（如 `anchor SliceRefs` 为 P0-B2 契约字段，非图自创）。

```text
                                ┌──────────────────────────┐
                                │      External World      │
                                └─────┬──────────────┬─────┘
                            screenshot│              │物理动作（均经授权）
                                ┌─────▼─────┐   ┌────▼─────┐
                                │Observation│   │  Driver  │
                                └─────┬─────┘   └────▲─────┘
                                      │ raw           │
                                ┌─────▼─────┐        │
                                │   Fast    │        │
                                └─────┬─────┘        │
                                      ▼              │
                            PerceptionCandidate[]    │
                            （瞬态·无身份权威）        │
                                      │              │
 ╔════════════════════════════════════▼══════════════╪═════════════════╗
 ║                Runtime Acceptance（一次原子提交）   ║
 ║  SliceAcceptancePolicy │ SourceCorrespondence │ OccurrenceMaterializer ║
 ╠═══════════════╦═══════════════════════════════════╩═════════════════╣
 ║ 路径一：REJECTED ║ candidate/observation 被拒                       ║
 ║   → Trace / Observability span（不建 Runtime world entity）        ║
 ║ 路径二：UNKNOWN ≠ REJECTED ║ 可靠视觉实例但无法分类                ║
 ║   → accepted Occurrence，PrimitiveKind = UNKNOWN                   ║
 ╚═══════════════╩═══════════════════════════════════════════════════╝

 ── 基数关系（两条独立关系，不得合并） ──────────────────────────────────

   Accepted stable Observation  1 ──── materializes ──── 1  Slice
   （rejected / transient Observation ── materializes ── 0  Slice）

   Node.LocalModel  1 ──── owns / aggregates ──── *  Slice
                      ├────────────────────────── *  Occurrence（实体在此）
                      ├────────────────────────── *  SliceRelation
                      ├────────────────────────── *  Assessments（Fast/Slow）
                      ├────────────────────────── *  LogicalItem（投影）
                      └────────────────────────── *  CoverageProjection

 ── 聚合内部结构 ──────────────────────────────────────────────────────

 ┌───────────────────────────────────────────────────────────────────┐
 │ Node.LocalModel（per-Node 不可变聚合 · 唯一 canonical local-world   │
 │                 owner · NET_NEW_MUTABLE_TRUTH = +1 集中式）         │
 │                                                                   │
 │  Evidence（append-only · active/archived）       Assessments        │
 │  ├─ Slice[]（1:1 各自对应 accepted stable obs）  ├─ FastAssessment │
 │  ├─ Occurrence[]                     （hint 档）  └─ Slow typed     │
 │  ├─ SliceRelation[]                                claims（D）     │
 │  └─ Interaction/Transition refs    CanonicalProjection              │
 │                                     └─ LogicalItem[]（重算快照）    │
 │  CoverageProjection（Region→Container）                            │
 └──────┬──────────────────────────────────────────────┬──────────────┘
        │ Slice 持 ref                                  │ reconciler 产出
   ┌────▼─────┐      ┌──────────────┐             ┌─────▼──────────┐
   │  Slice   │ 1..N │SpatialRegion │      ┌─────►│  LogicalItem   │
   │ (stable  │─────►│ Kind + 3 参与 │      │      │ Structure×     │
   │ viewport)│      │ flag（独立）  │      │      │ Affordance×    │
   └────┬─────┘      └──────▲───────┘      │      │ MemberRole×State│
        │ OccurrenceRefs   │              │      │ 单主 affordance  │
        │（实体在 LocalModel│              │      │ anchor SliceRefs │
        ▼）               │              │      │ （P0-B2 契约字段 │
   ┌────────────┐         │ RegionBinding │      │  ·relocation 锚）│
   │ Occurrence │─────────┤ 空间关联      │      └─────┬────────────┘
   │ (VISUAL ◆) │ 1     * │ ≠ ownership  │            │ merge/split/
   └─────┬──────┘         │ ≠ identity   │            │ reclassify
         │                └──────────────┘            ▼
         │ corroborate only              ┌──────────────────────────┐
 StructuredEvidence ─────────────────────┐│ LogicalItemSupersession │
 （clickable/checkable…）  无对应 →        │ 最小 payload · 无 progress│
 unmatched auxiliary（不铸造）             │ authority                │
                                          └───────────┬──────────────┘
 ── Agent 侧（authority 不变） ──                      │ 显式重估
 ┌────────────────────────────────┐                   ▼
 │ _branchProgress = evidence     │        ┌─────────────────────┐
 │ aggregate（Agent-owned）        │◄───────│ Agent progress       │
 │ Pending = Authorized−Completed │        │ reevaluation         │
 │ （derived view）                │        │（REUSE pattern）     │
 └────────────────────────────────┘        └─────────────────────┘

 ── R8 层（全部保留，原语义） ──
 CurrentContainer { NodeRef + CurrentSliceRef + EntryContext } ──► Node / Slice
 TransitionOccurrence（真实发生证据）──► CurrentContainer / ContainerGraph
 ContainerGraph（evidence-only · ≠ Planner）
```

## Appendix B — Runtime Evidence-to-Action Flow（解释性视图）

> Diagrams are explanatory views of the normative requirements in the specs. If a diagram conflicts with a normative requirement, the spec requirement is authoritative.
> 核心权威约束：所有物理动作（点击目标、为找目标而滚动）均须经 Agent authorization。

```text
 ═════════ 主链：看见 → 接受 → 积累 → 收敛 → 行动 → 回环 ═══════

 External World ──screenshot──► Observation
      │ settling / transient ──► 不 materialize（仅 StabilityEvidence/Trace）
      │ stable
      ▼
 Fast ──► PerceptionCandidate[]（瞬态）
      │
      ▼
 Runtime Acceptance
      ├─ rejected ──────────► Trace / Observability（无 world entity）
      ├─ 无法分类但可靠 ─────► Occurrence · PrimitiveKind = UNKNOWN
      │                        （UNKNOWN != REJECTED，不强行猜）
      ▼ atomic commit
 accepted stable Slice ── accepted visual Occurrence[]（structured 佐证）
      │
      ▼
 Node.LocalModel（evidence 追加 + SliceRelation region 关联）
      │
      ▼
 Incremental Canonicalization（纯函数 × EvidencePolicy[ClaimType]）
      │
      ├─ clear ─────────────┐
      ├─ critical ambiguity ─► Slow typed claims（四类 barrier）─► 确定性 reconcile
      └─ unresolved ─────────► 非阻塞（仅 closure-critical 时升级）
      ▼
 LogicalItem（SemanticResolved · 不含 Agent policy）
      │
 ═══ 权威边界 ① ═══ SEMANTIC_AFFORDANCE != AGENT_ADMISSION
      ▼
 Agent Admission（LogicalItem + Goal + ScenarioPolicy）
      │  ← obligation 可在 not-currently-groundable 时成立
      ▼
 Traversal Obligation ──► _branchProgress（Authorized 证据）
      │
      ▼
 Agent chooses target
      │
      ▼
 GroundableNow?
      │ YES                              │ NO
      │                                  ▼
      │                    SliceRelation / history evidence
      │                                  ▼
      │                          RelocationHint（仅 prior）
      │                                  ▼
      │                        Agent decides relocation
      │                                  ▼
      │              ═══ 权威边界 ② ═══ RELOCATION_HINT != SCROLL_AUTHORIZATION
      │                                  ▼
      │                    Relocation Action Authorization（Agent）
      │                                  ▼
      │                               Driver（滚动）
      │                                  ▼
      │                           External World
      │                                  ▼
      │                          fresh Observation
      │                                  ▼
      │      ┌────────────── fresh Slice ◄┘
      │      ▼
      └──► fresh Occurrence（fresh ScreenBounds）
      │       （历史 bounds / region 坐标 永不 grounding）
      ▼
 ═══ 权威边界 ③ ═══ CURRENTLY_GROUNDABLE != AUTHORIZED
      ▼
 Target Action Authorization（Agent）──► Driver ──► External World
      │
      ▼ fresh Observation
 TransitionOccurrence（observed-only · 期望无投票权）
      ├─► CurrentContainer（NodeRef + CurrentSliceRef + EntryContext）
      └─► ContainerGraph relation evidence（≠ Planner）

 ═══ 辅助链 ═══
 StructuredEvidence ──► corroboration only（无对应 → unmatched auxiliary）
 Slow ──► typed claims ──► deterministic reconciliation
          （不铸造 Occurrence / 不授权 action / 不宣告 completion /
            不变更 Graph / 首帧不自动调用）
 SliceRelation ──► correlation / coverage / relocation evidence（≠ identity）

 ═══ 完成判定（三独立条件） ═══
 ContainerLocalComplete ≈ RegionCoverage 聚合穷尽
                       AND 已 admission 义务全部解决
                       AND 无 closure-critical 未解析语义（有界 resolution policy）

 ═══ 穿透判据 ═══
 所有物理动作（点击目标、为找目标而滚动）均须经 Agent authorization；
 错误可存在、可迁移，但不得无边界穿透
 Action / Graph / Progress / Completion 四权威
```
