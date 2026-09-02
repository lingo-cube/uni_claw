# Stage B1 — Spatial Foundation 结果（毕业证据）

> Change: container-runtime-v2-evidence-model
> Stage: B1（只产 evidence，零 traversal/completeness 行为变化）
> 日期: 2026-09（apply 会话）
> 结论: **B1 毕业条件满足** —— 新空间证据不构成 Action / Identity / Completion authority。

## 1. Spec Requirement → Implementation Symbol → Test 映射

| Spec Requirement（evidence-foundation） | Symbol | Test | 结果 |
|---|---|---|---|
| SpatialRegion 与 OccurrenceRegionBinding（三参与 flag 独立；max-overlap 判定；ambiguous fail-closed） | `Model/SpatialRegion.cs`（SpatialRegion / SpatialRegionRef / SpatialRegionKind）· `Model/OccurrenceRegionBinding.cs`（OccurrenceRegionBinding / SpatialRegionBinding.Assess / ViewportOccurrenceRef） | `Unit/SpatialRegionTests.cs`（6）· `Unit/OccurrenceRegionBindingTests.cs`（9） | 15/15 PASS |
| SliceRelation 为 region-bound 空间证据（V1 Count=1；多 region seam；gap 证据可推导） | `Model/SliceRelation.cs`（SliceRelation / RegionRelation / SpatialTranslation / SpatialRelationUncertainty / RegionContinuity / SpatialEvidenceChannel） | `Unit/SliceRelationTests.cs`（6，含快滚 gap 与 IVI 多 region） | 6/6 PASS |
| SliceRelation 计算端口（四通道 provenance；Adapter 侧；Runtime 零依赖不破） | `UniClaw.Runtime.Adapters/Perception/SliceRelationComputation.cs`（ISliceRelationSource / SliceRelationComputationInput / SliceRegionGeometry） | 端口为边界声明（实现随 B2 acceptance wiring 到位；无 stub 实现，符合"不提前建 stub"） | 编译 + 依赖方向核验（Adapters → Runtime 不变） |
| 验收拒绝留诊断而非实体（REUSE/EXTEND Observability） | `Observability/RuntimeObservability.cs` EXTEND：`SpatialRegionBinding` / `SpatialSliceRelation` component + `ObservabilityEvidenceEvent` 事件词汇（region_binding.ambiguous / slice_relation.gap / slice_relation.low_confidence） | 词汇契约即 B1 埋点交付；**emitter 在 B2 capability 边界接线**（B1 纯 Model 函数无副作用，boundary 判定逻辑 B2 才存在——repo dependency 排序，非 scope 变更） | 常量编译 + ObservabilityConformance 套件不受影响 |

## 2. 验证结果

- `dotnet build src/UniClaw.Runtime.sln`：0 error。
- B1 新测试：**21/21 PASS**。
- 全套 `dotnet test`：2649 通过 / 5 失败（Runtime）+ Semantic 基准若干失败 —— **全部为既有失败，与 B1 无关**，逐项分类：
  - `Evidence/ScrollStabilityConfirmationTests.TitleOff_...`（DecisionRecord 断言；源自仓库在途未提交工作）
  - `ValidationHarness/HarnessSourceShapeGuardTests.ScenarioKnowledgeTokens_...`（RowIdentityContextDomainTests 含白名单外 token；同上）
  - `Scenario/CapstoneSingleAgentRunTests` / `Scenario/ExternalBoundaryRealDeviceTests`（RealDevice/模拟器 collection，环境不可用即失败——环境依赖，非回归）
  - `UniClaw.Semantic.Tests` 基准（独立项目，关联 platforms/perception 在途修改）
  - 佐证：stash 基线复测（去除在途 tracked 修改后）上述失败依旧/恶化，且 B1 前后唯一因本 change 变化的失败是 `ModelImmutabilityTests`（已按购买机制解决，见 §3）。
- `scripts/check-consistency.sh`：**ALL PASS**（含 C11/C12 active-change 投影同步：本 change 已登记 `docs/work/active/current-gates.md` + `docs/snapshots/latest.md`，ActiveChangeCount 12→13）。

## 3. Guard 交互记录（非放宽，按购买机制登记）

- `ModelImmutabilityTests.NoDeferredTypesOrFields_LeakIntoModel`（裁决 3：Model 禁 coordinate/hierarchy 字段）因 `SpatialRegion.Bounds` 触发。处置：在该 guard **既有 PURCHASED 白名单**（ElementBounds / ObservedElement.Bounds / StructuredElementEvidence.Bounds / CanonicalObservationOccurrence.Bounds）按同一机制追加 `SpatialRegion.Bounds` 豁免，注释引用购买来源（本 change P0-A contract A3 / spec evidence-foundation）。bounds 为空间关联证据，非 coordinate-based grounding authority——与裁决 3 精神一致，非断言放宽。

## 4. B1 毕业条件证明：新空间证据 ≠ 新 authority

- 本 Stage 新增类型全部为**纯证据模型**：无可变状态、无 grounding API、无 scroll 授权路径、无 coverage 判定路径、无 identity 授予路径。
- `SliceRelation.IndicatesUncoveredGap` 为派生证据视图，注释与 spec 均锁定"coverage 决策留在 Stage C2/E 消费边界"。
- 未触碰 Agent admission / Slow / completion semantics / LogicalItem / global clock / diagnostic domain entity（全部为后续 Stage 范围）。
- Runtime 零 ProjectReference guard 保持（新 Runtime 文件仅依赖 Model 内部类型；CV 依赖隔离在 Adapters）。

## 5. 残留与移交

- Task 1.5 emitter 接线随 B2 acceptance capability 边界落地（P5 boundary 误判捕获率、R2' confidently-wrong 观测自 B2 起有数据）。
- 仓库在途未提交工作造成的 5 个既有失败与 Semantic 基准失败不由本 change 修复（owner 归在途工作；已在此留档防止误归因）。
