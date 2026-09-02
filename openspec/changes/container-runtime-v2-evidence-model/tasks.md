# Tasks: Container Runtime V2 — Evidence Model & Semantic Convergence Refactor

> 排序原则：以 repo dependency 为准（模型 → reducer seam → 消费端 → shadow 切换 → 真机验收），非机械照搬阶段名。每任务组完成须 `dotnet build src/UniClaw.Runtime.sln` + `dotnet test` + ArchitectureGuard 全绿。authority/source 迁移一律 shadow → divergence → cutover，禁止 big-bang。

## 1. Stage B1 — Spatial Foundation（只产 evidence，零行为变化）

- [x] 1.1 新增 `SpatialRegion` 模型（`src/UniClaw.Runtime/Model/`：RegionRef / RegionKind(V1 candidate) / Bounds / participatesInScroll·Coverage·Grounding 三独立 flag），单元测试覆盖三 flag 独立性（fixed chrome Back：scroll× coverage× grounding✓）
- [x] 1.2 新增 `OccurrenceRegionBinding`（OccurrenceRef / PrimarySpatialRegionRef? / OverlapRatio / Ambiguous）与 V1 max-overlap 判定纯函数；ambiguous 时 ScreenBounds 有效、region 坐标不作权威 correlation 证据的测试
- [x] 1.3 新增 `SliceRelation { FromSliceRef, ToSliceRef, RegionRelations[] }` 模型（V1 Count=1、translation-only、量化不确定度 + 派生档位）；`UniClaw.Runtime.Adapters` 侧 SliceRelation 计算能力端口（anchor matching / registration / consensus / scroll prior 四通道来源标记）
- [x] 1.4 gap 证据测试：大位移低 overlap → coverage 不得宣布覆盖（本阶段仅产出证据，不接 coverage）
- [x] 1.5 Stage B1 观测埋点：boundary 误判捕获率（P5）与 confidently-wrong（R2'）的 Observability span（EXTEND `RuntimeObservability`，不建 domain entity）—— 词汇/组件契约已交付，emitter 随 B2 capability 边界接线（见 evidence §5）

## 2. Stage B2 — Accepted Evidence Model

- [x] 2.1 `ContainerSlice` EXTEND（ViewportBounds / SpatialRegionRefs / OccurrenceRefs / FastAssessmentRefs / acceptance StabilityEvidence ref）；Slice = accepted stable fresh viewport，1 accepted Observation : 1 Slice（分屏 = 多 Region 单 Slice 测试）
- [x] 2.2 新增 `Occurrence` 模型（visual-only 字段骨架 + RegionBinding + RegionRelativeBounds? + CorroborationRefs + StabilizerHint? + EdgeClipped?）；structured 仅 correspondence 佐证（IoU+text 确定性函数），unmatched → auxiliary evidence 三场景测试（佐证 / 无对应 / 冲突）
- [x] 2.3 Runtime Acceptance 三纯函数（`Capabilities/Perception/`：SliceAcceptancePolicy / SourceCorrespondence / OccurrenceMaterializer）+ `RuntimeAcceptance` facade；`ContainerRuntimeV2Reducer` 延伸：Slice+Occurrence[]+FastAssessment[] 一次 atomic commit（无 dangling refs；复用 `SemanticEvidenceRevision` stale 拒绝）
- [x] 2.4 拒绝/降级候选诊断：EXTEND `RuntimeObservability` span/event（ObservationRef + candidate summary + reject reason + validator decision）；transient/settling Observation 不 materialize Slice 测试
- [x] 2.5 `FastAssessment` 模型（StructuralHypothesis 落成，仅 hint）；`ObservedElement` 剥 identity（StableKey → StabilizerHint）
- [ ] 2.6 StableKey 退役 shadow：LocalModel correlation 出正式判断前，旧 StableKey 签名路径旁跑分歧率（复用 `RowIdentityContext` 对账）；记录阈值与 kill criteria
  - 依赖裁决（Human Gate，2026-09）：**延后至 3.3 之后** —— divergence 计算需要 LocalModel correlation/reconciler（3.1/3.3）先建立；不得为完成 B2 提前创建 correlation 权威。见 `evidence/STAGE-B2-ACCEPTED-EVIDENCE-PARTIAL-RESULT.md` §SPEC_BLOCKER。
- [ ] 2.7 taxonomy 映射 shadow：`NormalizeType` 旧 label → PrimitiveKind+组合映射函数旁跑比对；分歧收敛清单
  - 依赖裁决（Human Gate，2026-09）：保持未完成 —— shadow divergence threshold、kill criteria 与收敛证据尚未获明确裁决；mapper 已随 B2 存在，测量与切换等待裁决。
- [ ] 2.8 Container page-local state 处置（Repository Mapping #1 表逐项）：`_viewportExplorationObservations`/`_localPageBeliefState` MOVE、`_executedSteps` MOVE/DERIVE、`_observation`/`_isLocalComplete`/`_objectStateBeliefs` DERIVE、`_objectBindings` DELETE/REPLACE —— 每项独立测试 + ArchitectureGuard 验证 Container 不再持 world-state 字段（shadow 期仅比对）
  - 依赖裁决（Human Gate，2026-09）：**按目标 owner 分解并延后** —— 各字段迁移分别延后至 3.1（LocalModel 聚合）、4.4（coverage 消费边界）、4.5（三条件 closure）对应能力建立后执行；不得提前删除已购买行为或提前创建 destination owner。

## 3. Stage C1 — Canonical World

- [x] 3.1 `LocalModel` per-Node 不可变聚合入 `ContainerRuntimeV2State`（Evidence active/archived 分层 + Assessments + CanonicalProjection + RegionCoverageProjection→ContainerCoverageProjection）；append-only + 整体替换测试
- [x] 3.2 `LogicalItem` 模型（Structure×Affordance×MemberRole×State 组合、单主 affordance、membership evidence、anchor SliceRefs、SemanticResolved）；孪生文本 / 帧级翻转 / STATIC_CONTENT 三 buyer 测试
- [ ] 3.3 `EvidencePolicy[ClaimType]` 策略表 + 无状态 `SemanticReconciler`（attach/create/unresolved/merge/split/reclassify 显式 delta）；RB-04 反例对抗测试（1 旧 Slice vs 5 新同档可推翻；同目的地不推断同对象；margin/hysteresis 防振荡）
  - 3.2 审核锁定的强制子要求（PASS_WITH_FIXES，2026-09）：① `LogicalMembership.EvidenceRef` 必须可追溯到 deterministic reconciliation decision/assessment（优先 typed ref，不留裸 string 挂随机 raw evidence）；② `SemanticResolved=true` 的 production LogicalItem 只能经 SemanticReconciler/EvidencePolicy producer seam 产生（internal factory / validated input 封死 caller 自宣布）；③ CanonicalProjection 级 invariant：一个 Occurrence 至多归属一个正式 canonical LogicalItem；④ reconcile 幂等测试不依赖 ImmutableArray record 默认 equality（same evidence → reconcile twice → 第二次 NO_CHANGE，防 revision churn）。
- [ ] 3.4 canonical 语义 Goal-independence 测试（同一 LocalModel 在不同 Goal 下语义判定一致）

## 4. Stage C2 — Consumption Boundary

- [ ] 4.1 Agent admission 输入迁到 canonical LogicalItem（语义条件；obligation 可在 not-currently-groundable 时成立）；`SEMANTICALLY_ACTIONABLE != CURRENTLY_GROUNDABLE != AUTHORIZED` 测试
- [ ] 4.2 执行 grounding：GroundableNow? → Relocation → fresh Slice → fresh Occurrence → fresh ScreenBounds → Authorization；历史 bounds / region 坐标拒绝 + EdgeClipped V1 policy + ungroundable → Recovery/incomplete-with-evidence 测试
- [ ] 4.3 CREATE 最小 `LogicalItemSupersession`（Prior/Resulting refs + DeltaKind + evidence/binding refs）+ Agent progress reevaluation 消费（REUSE `Agent.ContainerReconciliation` pattern）；merge-after-completion 不静默改写 `_branchProgress` 测试
- [ ] 4.4 coverage 消费边界：RegionCoverageProjection → ContainerCoverageProjection 聚合（participatesInCoverage）；多 region fixture（Media 穷尽 ≠ Container 完成）+ 快滚 gap + 语义歧义不影响 coverage 独立判定测试
- [ ] 4.5 `ContainerLocalComplete` 三条件判定（coverage 穷尽 + admitted obligations resolved + 无 closure-critical unresolved）；deep Unknown 非阻塞测试

## 5. Stage D — Slow Semantic Repair（seam 已定，payload P1）

- [ ] 5.1 Slow typed claims 接入 reconciliation（binding/revision validation + deterministic apply）；FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION 与 Anchor barrier（critical identity ambiguity only）测试
- [ ] 5.2 Slow 权威隔离测试：不铸造 Occurrence / 不授权 action / 不宣告 completion / 不变更 Graph / false promotion 无权威效果

## 6. Stage E — Coverage Cutover

- [ ] 6.1 region-scoped chain shadow vs 旧 no-new/settle 分歧率量测；达阈值后切正式源（kill criteria 生效前不切）

## 7. Stage F — Fresh Phase 2.6 Acceptance

- [ ] 7.1 冻结 Fast baseline，fresh 真机运行；量测：completed（> 0/19 目标）、假完成率（coverage 独立复核）、Slow invocation rate、membership 翻转率（aggregation/margin 解释）、boundary 误判捕获率
- [ ] 7.2 穿透边界 falsifier 判定：blocker 可迁移但不得无边界穿透 Action / Graph / Progress / Completion；新 blocker 落入明确 owner / seam / recovery path
- [ ] 7.3 graduation decision 证据包（evidence/ 目录）；NOT_GRADUATED 状态直至 Human Gate

## 8. 收尾

- [ ] 8.1 30 条候选不变量的落位决策（OpenSpec spec 内已冻结契约级子集；Contract I-x 提升另行 Human Gate）
- [ ] 8.2 文档同步：`docs/snapshots/latest.md` / Repository Mapping 状态回填 / brainstorm 状态闭环

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| docs/analysis/（全部输入事实源） | `container-runtime-v2-p0-contract-tables.md` · `container-runtime-v2-horizontal-chain-model-refactor-brainstorm.md` · `container-runtime-v2-repository-mapping.md` |
| src/UniClaw.Runtime/ | `../../src/UniClaw.Runtime/AGENTS.md` + `docs/system/constitution/runtime-architecture-contract.md` |
| tests/UniClaw.Runtime.Tests/ | `../../tests/UniClaw.Runtime.Tests/AGENTS.md` |
