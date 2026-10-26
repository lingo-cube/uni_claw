# 切片 0 接口稿：两个生产只读缝（rev.2，独立审阅处置后）

2026-10-23。审阅对象仅本稿定义的缝形状与不变量；批准后才写 `src/`。
基线 HEAD：56a75fd7。变更：WORLD-VISUALIZATION-001（QV2/QV3 裁决）。
rev.2：采纳独立审阅全部 8 项发现（2 MAJOR / 3 MINOR / 3 NIT），处置见文末。

## 已核对的事实基础

- `WorldModel.DeriveGraphProjection(sourceRevisionId, scope)`（internal，
  WorldModel.cs:229）**已支持任意历史 revision**；公共面
  `UniKernel.ProjectWorldGraph(scope)`（UniKernel.cs:117）只传 current。
- 现投影 `WorldGraphProjection` 只含 **NavigatesTo**（按 run-scope 过滤）边；
  Contains/Overlays 存在于 `revision.Relations`（Scope 为 `capture:{id}`，
  WorldModel.Context.cs:55-56）但不进投影。
- `WorldSemanticKnowledge`（SemanticVerificationObservation.cs:14）随 revision
  携带：ContainerId / PageKey / PageName / PagePurpose / OriginalSemanticEvidenceId /
  VerificationEvidenceId / CorrespondenceEvidenceIds / SourceRevisionId /
  ApplicableRevisionId / Reused。**实现现状**（SemanticKnowledge.cs:130-140）：
  每 revision 经 RefreshSemanticKnowledge 重算、TakeLast(1)，**单 revision 至多
  1 条、仅覆盖当前页容器**；冲突/被撤销解释在发布时已被 VerificationSources
  挡下不产条目。
- revision 链字段：RevisionId / ParentRevisionId / RevisionNumber；时间仅有
  `FreshnessBasis.AsOf`（采集时间高水位，继承制）；**提交墙钟未记录**。
  `revision.Conflicts` 是随 revision 携带的快照（历史安全）；
  `_pageCorrections` 是 Owner 全局当前态日志（需界定）。
- `KernelRuntimeSurfaceWhitelistTests` 冻结全程序集公共类型集合（只执法类型
  集，不执法方法面）；新增公共类型必须随本 Change 显式增集。
- `ProjectWorldGraph(scope)` 现有消费者（Host SettingsCoverage ×3、测试 ×10+）
  全部单参调用 → 可选参数源级向后兼容。

## 缝 1：图投影历史化 + 语义标签 + 结构关系

```csharp
// UniKernel（公共缝扩展）
public WorldGraphProjection? ProjectWorldGraph(string scope, string? sourceRevisionId = null)
// sourceRevisionId == null → current（既有字段语义不变；无 current → null）
// 非 null → 该 revision 的图投影；revision 未知 → 返回空投影，
//           Unknown 含 "source-revision-unknown"（沿用既有 internal 行为，
//           与 ProjectWorldPage 未知即抛不一致——现状保留，Known deviation）
```

`WorldGraphProjection` 增补两个 **additive init 属性**：

```csharp
/// revision-bound、evidence-backed 的结构关系（Contains/Overlays）。
/// 其 Scope 为 capture-scoped（"capture:{id}"），与 NavigatesTo 的 run-scope
/// 属不同域，故不做 run-scope 过滤（DerivePageView ContextRelations 同先例）；
/// graph relation ≠ timeless structural truth。
public IReadOnlyList<ContainerRelation> StructuralRelations { get; init; } = [];

/// Owner 侧 join 的节点稳定语义标签；无条目 = 语义未知（UI 降级显示）
public IReadOnlyList<WorldNodeLabel> NodeLabels { get; init; } = [];
```

```csharp
public sealed record WorldNodeLabel(
    string ContainerId,
    string? PageName,        // 按实现现状实际非空；保留可空作前向兼容
    string? PagePurpose,
    string? PageKey,
    bool Reused,             // Q19：历史验证语义被当前沿用
    bool Conflicted,         // 该语义处于冲突/被界定内纠错撤销 → 不作可信标签
    string? KnowledgeSourceRevisionId,   // 标签来源 revision，可追溯
    IReadOnlyList<string> EvidenceRefs); // 防御拷贝（ToImmutableArray 先例）
```

**标签 join 规则（Owner=World，投影时执行）**：
1. 来源仅限**选定 revision 自身**的 `SemanticKnowledge` 列表——禁止当前知识
   回填历史视图。
2. 同 Container 多条：取最后一条非冲突条目，否则最后一条并 `Conflicted=true`
   （按实现现状当前不可达——单 revision ≤1 条；规则保留作前向兼容）。
3. **冲突判定（历史界定，审阅发现 1 处置）**：
   a. `Conflicts`：只用选定 revision 自身快照 `revision.Conflicts`（随
      revision 携带，天然历史安全）。
   b. 纠错撤销：只计入 `SourceRevisionId` 的 RevisionNumber ≤ 选定 revision
      的 Applied 纠错（沿 DerivePageView 的 revisionIds 界定先例，
      WorldModel.History.cs:36-38）；其后发生的纠错不影响本投影。
4. 无任何条目 → 不产生 NodeLabel 记录（UI 按"语义未知"降级；按实现现状
   图投影至多标注当前页 1 个节点，其余降级——诚实呈现，不伪造）。

## 缝 2：revision 时间线投影

```csharp
public WorldRevisionTimeline ProjectWorldTimeline()

public sealed record WorldRevisionTimeline(
    IReadOnlyList<WorldRevisionSummary> Revisions)   // 按 RevisionNumber 升序全链
{
    public string? CurrentRevisionId { get; init; }
}

public sealed record WorldRevisionSummary(
    string RevisionId,
    string? ParentRevisionId,
    int RevisionNumber,
    DateTimeOffset? LatestCaptureTime,   // = FreshnessBasis.AsOf（采集时间高水位）
    WorldRevisionChangeSummary Changes);

public sealed record WorldRevisionChangeSummary(
    bool PageObservation,
    IReadOnlyList<string> ObservedContainerIds,      // 归属冲突 → 空
    bool NavigationVerified,
    bool CorrectionApplied,                          // 本 revision 发布 Applied 纠错
    bool CorrectionRejected,                         // 本 revision 发布被拒纠错
    IReadOnlyList<string> CorrectedCaptureEvidenceIds, // 仅 Applied
    bool SemanticKnowledgeUpdated,                   // 按固定比较键相对父有增/删
    bool SemanticKnowledgeReused,                    // 新在条目含 Reused=true
    IReadOnlyList<string> StructuralRelationChanges, // 差分键=(Kind,Src,Tgt)
    IReadOnlyList<string> NewEvidenceIds);
```

**派生规则（全部可从快照机械推导，无启发式）**：
- `SemanticKnowledge` 比较键（审阅发现 4 处置，排除每 revision 重算字段）：
  `(ContainerId, OriginalSemanticEvidenceId, VerificationEvidenceId, PageKey,
  PageName, PagePurpose, Reused)`——**排除** ApplicableRevisionId /
  CurrentCaptureEvidenceId / CorrespondenceEvidenceIds（每 revision 皆变，纳入
  则标记恒真）。Updated = 键集合相对父非空差（增或删）；Reused = 新增键中含
  Reused=true。
- `StructuralRelationChanges` 差分键 = `(Kind, SourceContainerId,
  TargetContainerId)`；格式 `+contains:a>b` / `-overlays:a>b`（EvidenceBasis
  变化不入差分）；**首 revision（无父）基线 = 空集**，现存全部记 "+"。
- 纠错标记：`_pageCorrections` 中 SourceRevisionId == 本 revision：Applied →
  CorrectionApplied=true；!Applied → CorrectionRejected=true（被拒纠错也发布
  revision，须有专属标记，审阅发现 5 处置）。
- NavigationVerified：**本 revision 相对父新增 NavigatesTo 关系**（快照一致语义，
  实现时收敛：关系只存在于其发布 revision 及之后，"TargetRevisionId==本rev" 在
  历史快照下永不成立；到达锚仍由关系携带的 TargetRevisionId 表达）。

**诚实性规则**：
- `LatestCaptureTime` = FreshnessBasis.AsOf（采集时间高水位），**不是提交
  时间**；提交墙钟未记录，UI 显示"提交时间未记录"。纠错/沿用类 revision 无
  新采集 → AsOf 与父相同，靠 Changes 标记区分，不伪造单调时间。
- Changes 是多标记摘要，不是单值 cause；空 World → Revisions 空、
  CurrentRevisionId=null。

## 不变量清单（审阅逐项核对）

1. 两缝纯只读：不修改 World 状态、不产生 revision、无副作用、无时钟读取。
2. 无执行授权：投影不产生 binding/permission/执行资格；结构关系与标签均
   revision-bound。
3. 历史不可变：历史投影只消费选定 revision 快照 + **以 RevisionNumber 界定
   的 Owner 日志**（纠错/语义验证），其后事件不回填。
4. 标签只用选定 revision 自身知识 + 界定内纠错状态。
5. Unknown 不折叠：无语义 → 无标签记录；未知 revision → 空投影 + unknown
   标记；均不伪装成已确认事实。
6. 兼容：单参调用**既有字段语义不变**（增补字段默认空；真实缝返回值新增
   非空字段，整记录相等断言的既有测试随增集显式更新——实现时以全量测试
   验证，不逐字节承诺）。
7. 白名单增集 4 类型：WorldRevisionTimeline / WorldRevisionSummary /
   WorldRevisionChangeSummary / WorldNodeLabel（该测试只执法类型集合，
   不豁免方法面审查）。
8. 返回集合全部不可变副本（EvidenceRefs 等防御拷贝）。

## 独立审阅处置记录（rev.1 → rev.2）

| 发现 | 级别 | 处置 |
|---|---|---|
| 1 纠错冲突判定未按 revision 界定，历史标签可被其后纠错翻转 | MAJOR | 规则 3b 改为 RevisionNumber 界定（DerivePageView 先例）；Conflicts 只用快照 |
| 2 同 Container 多条/全冲突取最后不可达（实现 ≤1 条/仅当前页） | MINOR | 事实基础与规则 4 写明实现现状；选择规则保留作前向兼容 |
| 3 "逐字节等价"过强，整记录相等断言可能 RED | MINOR | 改述"既有字段语义不变"+ 全量测试验证，实现时更新受影响断言 |
| 4 SemanticKnowledge 比较键未定，标记恒真噪声 | MAJOR | 固定比较键（排除每 revision 重算字段），规则写明 |
| 5 被拒纠错无标记；差分键与首基线未定 | MINOR | 增 CorrectionApplied/Rejected；差分键=(Kind,Src,Tgt)、首基线=空集 |
| 6 StructuralRelations "非 run-scoped" 措辞不准 | NIT | 改 capture-scoped 并注明与 NavigatesTo run-scope 不同域 |
| 7 未知 revision 不抛 vs ProjectWorldPage 抛；EvidenceRefs 拷贝 | NIT | 现状保留记 Known deviation；EvidenceRefs 防御拷贝写入 |
| 8 白名单只执法类型集 | NIT | 不变量 7 注明，不作方法面豁免 |

## 实现落点（批准后）

- `src/UniClaw.Kernel/UniKernel.cs`：ProjectWorldGraph 扩参 + ProjectWorldTimeline。
- `src/UniClaw.Kernel/World/WorldModel.cs`：DeriveGraphProjection 增补两属性；
  新增 internal DeriveRevisionTimeline()。
- `src/UniClaw.Kernel/World/WorldRevisionTimeline.cs`（新）：三个 timeline
  record + WorldNodeLabel。
- `tests/UniClaw.Kernel.Tests/Runtime/KernelRuntimeSurfaceWhitelistTests.cs`：
  增集 4 项（注明 WORLD-VISUALIZATION-001）。
- 新测试：历史投影稳定（含纠错后历史不变——发现 1 回归）、标签 join 正/
  降级/界定冲突、结构关系快照与差分、时间线标记（含被拒纠错、语义沿用
  比较键）、未知 revision 行为、单参兼容、EvidenceRefs 防御拷贝。
