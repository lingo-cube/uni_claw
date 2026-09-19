# Evidence — CORE-002 阶段 2：手机/UI 滚动—点击 realization 接到 Core seam

- Date: 2026-09-19
- Scope: `UniClaw.CoreAdapter` 投影缝 + 现有手机/UI 滚动—点击 realization（UniClaw.Kernel）→ Core 候选语义记录
- Level: DETERMINISTIC
- Status: VERIFIED_WITH_SCOPE

## 实施范围

- 新增 `src/UniClaw.CoreAdapter`（独立 adapter 程序集，ProjectReference = Kernel + Core，
  依赖方向固定 realization/adapter → Core）。独立成程序集的原因：`UniClaw.Kernel.csproj`
  受 `ProductHostClosureTests`（RFS-001）执法保持零 ProjectReference，本 Change 不改写该边界；
  `UniClaw.Core` 保持零 ProjectReference（CORE-002 acceptance 1）。
- 新增 `CoreSemanticProjection`：Kernel realization 产物（EvidenceRecord、container、
  DeriveSlice、WorldState claim、CanonicalBinding、EffectReceipt）→ Core 候选记录
  （Evidence、Segment、Slice、Claim、Effect、Attempt、TargetBinding）的纯函数投影。
  UI occurrence id、spatial/native locator、scope 等域载荷只以不透明字符串值进入 Core；
  Runtime/Trace/Harness/设备 SDK 不进投影。
- Core 候选协议随 realization 接触精化两处（候选，非冻结）：
  1. `Slice.ObservationEvidenceId` → `ObservationEvidenceIds`（realization 的 slice 依据是
     revision EvidenceBasis 集合，无单一触发 evidence 可诚实选取）；
  2. Attempt↔Binding 方向改为 `Attempt.BindingId`（attempt→binding），与已验证 realization
     的 receipt↔BindingId 方向一致（阶段 1 review 留痕 ② 的 realization 证据）。
- 新增 `tests/UniClaw.Kernel.Tests/CoreProjectionSeamTests.cs`（6 tests）；slnx 增补
  adapter 条目；Kernel.Tests 显式引用 adapter + Core。未改 Kernel/Agent 产品源码，
  未删除旧类，未做源码迁移。

## 验证命令

```text
dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --logger 'console;verbosity=minimal'
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter "FullyQualifiedName~CoreProjectionSeamTests" --logger 'console;verbosity=minimal'
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter "FullyQualifiedName~RealAssetEntityModelTests|FullyQualifiedName~FastPerceptionSliceTests|FullyQualifiedName~ControlReferencePolicyTests|FullyQualifiedName~DispatchSeamSpecificationTests" --logger 'console;verbosity=minimal'
dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --logger 'console;verbosity=minimal'
dotnet test UniClaw.Kernel.slnx --logger 'console;verbosity=minimal'
```

## 预期

- Core.Tests 3/3（候选形状精化后语义不变量仍成立）。
- CoreProjectionSeamTests 6/6：全 tracer 投影、Unknown fail-closed、Stale/拒绝/授权、
  locator 不透明化 + Core 纯度结构证明。
- 四个指定 Kernel 行为套件不回归；Kernel 全量不回归；solution 全量
  （基线 514 + 新增 6 = 520）通过。

## 实际结果

```text
Core.Tests:                      3/3 通过
CoreProjectionSeamTests:         6/6 通过
四套件（Real/FastPerception/ControlReference/DispatchSeam）: 30/30 通过
UniClaw.Kernel.Tests 全量:       388/388 通过
UniClaw.Kernel.slnx 全量:        520/520 通过（Agent 17 + Core 3 + Simulation 112 + Kernel 388）
```

覆盖（CoreProjectionSeamTests）：

1. 滚动—点击全 tracer（真实 corpus：scroll01-v1 → 滚动 act → scroll01-v2 → 元素 Claim →
   tap 接地 → 点击后新 Evidence）：Evidence/Segment/Slice/Claim/Effect/Attempt/TargetBinding
   全链投影；同一 Segment 多 Slice；Claim Revise 保留旧依据（痕迹链）；binding basis 固定
   不漂移；Attempt 时间（act 常量时间）严格晚于 Slice 观察时间（FreshnessBasis.AsOf）。
2. Unknown 投递：投影为 Core `DeliveryOutcome.Unknown`，非终态、不产生 World Result、
   不改写 belief（attempt reflux 无 revision、无 attempt.* World claim）；三态映射不升级。
3. canonical binding 在 revision 推进后重新投影 → Stale fail-closed，basis slice 不变。
4. Bind 四态拒绝（stale/ambiguous/unknown-target）在 realization 不产生 canonical binding
   → 无 Core TargetBinding 可产（fail-closed）。
5. gate 授权拒绝（freshness Insufficient → "not-authorized"）→ Core `Unauthorized`。
6. locator 材料 → 不透明 (kind, key)；无材料 → ("none", "")（reference identity 不冒充
   locator）；Core 程序集无 realization 依赖、候选记录属性类型只允许
   string/CoreId/DateTimeOffset/枚举/CoreId、string 列表（提取纪律的结构证明）。

环境说明：测试运行有 `NU1900` 警告（NuGet 漏洞缓存路径无权限），未影响执行（既有现象）。

## 边界结论

本证据只证明：现有手机/UI 滚动—点击 realization 的已验证语义可经投影缝无损表达为
Core 候选记录，且 Core 不变量在投影产物上成立；Core 程序集保持零 ProjectReference、
零 realization 类型泄漏。不证明：源码迁移完成、Kernel 重复职责已删除、第二种非 UI
realization 通过、最终字段/继承树/公共 API 冻结。

## 剩余缺口

1. `CoreInvariants.ProducesWorldResult` 仍是常量谓词（阶段 1 review ① 未解）；本阶段以
   realization 侧行为断言（reflux 无 revision / 无 attempt.* claim）补充证据，Core 侧
   结构性证明仍待后续。
2. Core 候选 `BindingDisposition.Ambiguous/Unverified` 在本 realization 无产生路径
   （四态拒绝不产生 canonical binding）；语义由候选侧表达，realization 映射留待
   qspec §9 旧模型最终映射验证。
3. 防重发（binding-already-dispatched / no-blind-retry）执法留在 realization
   EffectBoundary（DispatchSeamSpecificationTests S3 已验证）；Core 候选 `CanDispatch`
   只覆盖 disposition 维度，不重复拥有该执法（所有权记录，非缺陷）。
4. `ClaimDisposition.Conflict` 映射存在但未经真实 corpus 路径出现（post-CLE-001 语料
   零 conflict 行）。
5. 多 Slice 历史固定的证据含行为断言（RevisionHistory + SupersededEvidenceIds 痕迹链），
   完整端到端 reconciliation 回放仍属 qspec §9 缺口。
6. 第二种非 UI realization 反向验证未开始（公共 API 冻结门槛不变）。
