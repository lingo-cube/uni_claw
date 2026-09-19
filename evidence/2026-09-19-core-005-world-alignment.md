# CORE-005 World Evidence 到 Claim 对齐验证

日期：2026-09-19

## 对齐结论

现有 Kernel Evidence/World realization 是 Evidence、Segment continuity、Slice
history 和 Claim revision 的唯一事实 Owner。Core 只接收只读语义投影，不新增 writer，
不回写 Kernel World。

```text
Evidence admission / Provenance → Core Evidence
Container continuity / scoped observation → Core Segment + Slice
World claim / revision history → Core Claim
```

Segment 的持续引用不证明现实身份；Slice 是局部观察，允许重叠、包含、重复和 partial；
Claim 是带依据的判断；Event/因果仍作为 Claim 视图表达。

## 现有证据

| 验收语义 | 证据 |
|---|---|
| admitted Evidence 的来源、时间、处理链和上下文可投影 | `CoreSemanticProjection.ProjectEvidence`；CoreProjectionSeamTests |
| 同一 Segment 的多个 Slice、重叠覆盖、旧 Slice 不被新观察覆盖 | CoreObjectRelationTests；CoreProjectionSeamTests；`FastPerceptionSliceTests` |
| Claim revision 保留旧 Evidence basis，不跟随 latest 静默改写 | CoreObjectRelationTests；`ClaimEvolutionTests`；CoreProjectionSeamTests |
| stale binding、未知投递、冲突和拒绝态 fail-closed | CoreProjectionSeamTests；`ControlReferencePolicyTests`；`DispatchSeamSpecificationTests` |
| omission 不等于 absence，post-action 需要新 Evidence 才能再评价 | CoreProjectionSeamTests；`EvidenceToBeliefTests`；`AsyncPerceptionScenarioTests` |
| Core 投影不产生 World writer 或 second authority | projection 为纯函数；World/Terminal/Simulation 测试保持 canonical owner 约束 |

## 验证结果

- Core.Tests：11/11。
- Core projection seam + ProductHostClosureTests：9/9。
- 全解决方案：527/527（Core 11、Agent 17、Kernel 388、Simulation 112）。
- 本切片未修改 Kernel World/Evidence owner，未新增项目、Adapter 或第二套 writer。
- NU1900 仅为 NuGet 漏洞缓存目录权限警告，不影响执行。

## 保留的 gates

1. 通用 BasisRef 和固定非 Slice 依据仍未定义；依赖文件/API 版本、标定参考系或
   历史复核的用途不能以不透明 LocatorKey 放行。
2. Effect/Attempt 请求快照、执行端、授权、dispatch progress、外部执行和协调三维
   状态仍未完成，不在本切片扩张。
3. Slice 的参考系、单位、覆盖和上下文由领域契约提供，不由 Core 添加通用
   freshness/trust/confidence 字段。

本证据证明 World 责任对齐切片，不证明旧模型源码迁移完成，也不证明最终字段或
继承树已经确定。
