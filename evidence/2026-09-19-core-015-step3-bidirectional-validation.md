# Evidence — CORE-015 Step 3 双向验证（7 场景 × 两方向）

> Change: `changes/CORE-015/state.md`
> 契约: `docs/design/uiworld-realization-contract-v0.1.md`（已评审通过，
> 含调整自由度条款）
> 方法：不迁移、不改产品代码；建立 realization→Core projection 语义
> 保留 + Core 约束→realization 满足的双向验证（Human 四步路线 Step 3）。

## 1. 验证矩阵（7 场景 × 两方向 → 测试落点）

| # | 场景 | 方向 A：realization→投影语义保留 | 方向 B：Core 约束→realization 满足 |
|---|---|---|---|
| 1 | 滚动后页面局部观察 | `CoreProjectionSeamTests.ScrollThenTap_RealizationProjectsOntoCore_*`（S1/S2、Coverage、ObservedAt 语义） | 同测试：Slice 依据 = revision basis、观察时间与处理版本分离 |
| 2 | 多 Slice 重叠和历史保留 | 同上（同 Segment 多 Slice、claim 依据链跨 revision 保留，P1 断言组） | `CoreObjectRelationTests.Relation_chain_*`（Core 侧 Slice 不改写）+ `Update_then_use_*` |
| 3 | Occurrence revision-local | P1（`ObservedRecordIds` 随 revision 重铸断言） | 契约 §3「occurrence 引用只在源 revision 作用域内有效」由 realization 测试族承担 |
| 4 | LogicalItem 跨 revision 连续 | **新增** `CoreProjectionSeamTests.LogicalItemContinuityAcrossRevisions_*`（连续性成立时世界语义经投影保留：claim 值与依据链） | 同测试方向 B 断言：li-* 不泄漏为 Core 身份（无对应主题 claim、谓词不制造 identity 命题）；realization 侧 19 个事实见 `UIWorldContinuityTests` |
| 5 | 旧 Binding 不自动换目标 | P1/P3（Stale disposition、basis 不漂移、`IsHistoricalBasisStable` 定锚） | P3/DeniedGate（realization 拒绝 ↔ Core disposition 一致） |
| 6 | Unknown 不变成失败或成功 | P2（`MapDelivery` 三态原样、非终态、无 World Result） | `CoreInvariants.IsTerminalDelivery` 只认 Completed/Failed（Core.Tests） |
| 7 | 非 Slice 固定资源版本不被伪装成 Slice | （未来非 UI realization 的投影义务——无 realization 可验证，契约 §5 已归属） | **新增** `CoreObjectRelationTests.FixedBasisReference_without_slice_*`：BasisReferences 成立固定依据、可投递、不漂移、无 Slice 不得经 Slice 路径判稳定、无依据不放行、混合路径并存 |

方向 B 的结构面（Core 不自动生成 UI identity / 无授权导出通路 /
程序集自足）：既有 `CoreObjectRelationTests.Core_assembly_stands_alone_*`
与 `CoreProjectionSeamTests.LocatorMaterial_*`（程序集引用方向 + 记录面
类型 allowlist + 无 Clause 导出 API 反射证明），本步零新增即已覆盖。

## 2. 本步新增（仅测试，零产品代码）

| 文件 | 测试 | 覆盖 |
|---|---|---|
| `tests/UniClaw.Core.Tests/CoreObjectRelationTests.cs` | `FixedBasisReference_without_slice_is_dispatchable_and_never_poses_as_slice` | 场景 7 方向 B |
| `tests/UniClaw.Kernel.Tests/CoreProjectionSeamTests.cs` | `LogicalItemContinuityAcrossRevisions_WorldSemanticsProject_WithoutIdentityLeakage` | 场景 4 双向 |

## 3. 验证声明

```yaml
level: DETERMINISTIC（单测/性质；场景级语义由所引测试族承担）
method: dotnet test UniClaw.Kernel.slnx 全量
expected: 新增 2 测试通过；既有基线零回归；git diff --check 通过
actual: >
  569/569 全绿（Core 14 + Agent 17 + Simulation 132 + Kernel 406）；
  567 基线零回归；git diff --check 通过；产品代码零改动
evidence: 本文件 + §1 矩阵所引全部测试
```

## 4. 边界

- 未迁移任何类型；未新增 Core 对象；未冻结 Attempt 三轴映射与
  `Predicate` 词汇（契约 §8 保持 deferred，属 Step 4 后或投影增强轮）。
- 场景 7 的方向 A（真实文件/API realization 投影）无可验证对象，
  归属已记录（对齐表 §5）；Core 侧约束行为已钉住。
