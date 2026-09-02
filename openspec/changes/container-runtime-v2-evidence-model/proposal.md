# Proposal: Container Runtime V2 — Evidence Model & Semantic Convergence Refactor

## Why

Phase 2.6 真机运行（19 fresh runs / 0 completed）证明当前 Runtime 的主要问题不是某个检测器不够准，而是一条隐含链路：`Fast row ≈ logical item ≈ traversal candidate / obligation`。感知的随机方差经这条链路**无边界穿透**到 Agent traversal semantics，导致 blocker 在不同层之间持续迁移（Z4 StableKey 跨 Container 污染、FRAME_LOCAL_FUSION_INSTABILITY 帧级类别翻转直接污染 inventory、r5 期望与观察世界脱节、menu_item taxonomy 过拟合、semantic ambiguity 与 coverage/completion 耦合造成假完成/假未完成）。

本 Change 完成 V2 的"第二半"：R8 已解决 ownership（谁拥有当前世界），本 Change 解决 **evidence model 与 semantic convergence** —— 让不可靠 perception 的错误**不能无边界穿透**至 Action / Graph / Agent progress / Completion 权威，并能通过 evidence accumulation + canonicalization + bounded semantic correction 逐步收敛。目的不是"让 YOLO 更准"。

## What Changes

- **新增 Runtime Acceptance 边界**：Fast 瞬态 Candidate 与 Runtime accepted evidence 分离；`SliceAcceptancePolicy / SourceCorrespondence / OccurrenceMaterializer` 三纯函数 + atomic commit；拒绝诊断 EXTEND 既有 Observability（不建 Runtime domain diagnostic entity）。
- **新增 accepted evidence 模型**：`Slice`（accepted **stable** fresh viewport，1 accepted Observation : 1 Slice）、`SpatialRegion`（空间分区 + 三参与 flag）、`Occurrence`（accepted **visual** occurrence；structured 仅 corroboration，不得铸造）、`OccurrenceRegionBinding`、`SliceRelation`（region-bound pairwise 空间关系证据）。
- **新增 container-local canonical world**：`Node.LocalModel`（per-Node 不可变聚合，入 `ContainerRuntimeV2State`；NET_NEW_MUTABLE_TRUTH = +1 集中式）、`LogicalItem`（组合 taxonomy，单主 affordance，无跨 run identity，hierarchy DEFERRED）、claim-specific `EvidencePolicy[ClaimType]`、纯函数 `SemanticReconciler`。
- **新增消费边界**：Admission（语义条件）/ Grounding（执行时 fresh Occurrence → fresh ScreenBounds）/ Authorization 三分离；`LogicalItemSupersession`（最小新 payload）驱动显式 Agent progress reevaluation；coverage 改为 SpatialRegion-scoped → Container aggregate，脱离 Fast item 数量。
- **退役两个被定罪的隐含实体**：`StableKey = RowId` 的 identity 权威（降级为 StabilizerHint，双轨 shadow）；`NormalizeType` 的 menu_item 单枚举（组合 taxonomy 映射，双轨 shadow）。
- **R8 全部保留**：`ContainerRuntimeV2State` / `CurrentContainer`（NodeRef + CurrentSliceRef + EntryContext）/ `ContainerGraph` / `TransitionOccurrence` / 纯 Reducer / Agent authority 不变。ordering REUSE `SemanticEvidenceRevision` + `Observation.SequenceNumber`（不新建 global semantic clock）。
- **Slow seam 仅定边界**（Stage D 实现）：typed semantic claims + deterministic reconciliation；`FIRST_SLICE != AUTOMATIC_SLOW_INVOCATION`；不得 create Occurrence / authorize action / declare completion / mutate Graph。

## Capabilities

### New Capabilities

- `container-runtime-v2-evidence-foundation`: P0-A —— Runtime Acceptance 边界（三纯函数 + atomic commit）、accepted stable Slice、SpatialRegion、accepted visual Occurrence（含 RegionBinding / GroundingPolicy）、SliceRelation、FastAssessment 绑定。
- `container-runtime-v2-canonical-world`: P0-B —— LocalModel ownership、LogicalItem、CanonicalMembership、claim-specific EvidencePolicy、canonical reconciliation；taxonomy 只冻结组合模型。
- `container-runtime-v2-consumption-boundary`: P0-C —— Agent admission（语义条件）、execution grounding（fresh 反向链）、LogicalItemSupersession → Agent progress reevaluation、region-scoped coverage 消费边界。

### Modified Capabilities

（无 —— 本 Change 不修改既有 spec 的 requirements；`sibling-branch-progress` / `container-traversal` 等既有能力的行为输入变化由上述新能力的消费边界 spec 界定，progress authority 本身 NOT_REWRITTEN。）

## Impact

- **代码**（实现映射事实源 = Repository Mapping）：
  - `src/UniClaw.Runtime/Model/`：新增 Slice/SpatialRegion/Occurrence/SliceRelation/LocalModel/LogicalItem/EvidencePolicy/LogicalItemSupersession 模型；`ContainerSlice` EXTEND；`ObservedElement` 剥 identity。
  - `src/UniClaw.Runtime/Capabilities/Perception/`：Runtime Acceptance 三纯函数。
  - `src/UniClaw.Runtime/Container/Container.cs`：7 项 page-local world state 按 Repository Mapping 处置表 REUSE/MOVE/DERIVE/DELETE（shadow 双轨后切）。
  - `src/UniClaw.Runtime.Adapters/`：PerceptionCandidate 瞬态化 + StabilizerHint；SliceRelation CV 计算端口；`NormalizeType` 退役。
  - `src/UniClaw.Runtime/Agent/`：admission 输入迁移；supersession 消费（REUSE `Agent.ContainerReconciliation` pattern）。
- **不变量**：本 Change 冻结 brainstorm §5 契约级不变量集（FAST_RESULT != ACCEPTED_RUNTIME_EVIDENCE 等 16 条 authority boundary + region/stable/visual 三定义规则）。
- **不受影响**：`ContainerRuntimeV2Reducer` 语义（ordering 延伸除外）、`ContainerGraphQuery`、GoalEvidence、Recovery。
- **验证**：tests/UniClaw.Runtime.Tests 场景套件 + ArchitectureGuardTests；验收 buyer 覆盖 r5 / Z4 / FRAME_LOCAL_FUSION_INSTABILITY / twin-text grouping / fast-scroll 假完成 / deep Unknown / Slow false promotion / canonical merge-after-admission / ungroundable relocation / 多 region IVI fixture / structured-visual 冲突。

## Evidence / Mapping References

- Architecture baseline: `docs/analysis/container-runtime-v2-horizontal-chain-model-refactor-brainstorm.md`（v3-final，READY_FOR_P0_CONTRACT_FREEZE）
- P0 contracts: `docs/analysis/container-runtime-v2-p0-contract-tables.md`
- Repository mapping: `docs/analysis/container-runtime-v2-repository-mapping.md`
- R8 / buyer evidence: `docs/analysis/container-runtime-v2-architecture-working-draft.md`
- Blocker evidence: `docs/analysis/runtime-debugging-capability-landscape.md`
