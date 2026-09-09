# Plan — DSE-002 DeliveryTarget（EB lowering seam + 首个 ADB driver）

> PlanType: dse-002-delivery-target / Status: ADOPTED /
> References: changes/DSE-002/state.md（Human 裁决全文）· P14 · P-UW-16 ·
> DSE-001（Residual 兑现）· legacy CoordinateMapper（归一化→pixel 数学，DIRECT IDEA）

## 切片链

```text
ProposedOccurrence.Locator（SpatialLocator：归一化 bounds + frame）
    ↓ WorldModel 铸造 / DeriveSlice
OccurrenceFact.Locator → BindingView.TargetOccurrenceLocator（owner fact）
    ↓ EB Bind
CanonicalBinding.TargetLocator
    ↓ EB lowering（ToDispatchRequest）
DeliveryTarget(OccurrenceReference, Spatial)
    ↓
IEffectDriver.Deliver(DispatchRequest)
    ↓ AdbEffectDriver（dry-run）：bounds center × viewport → pixel
receipt.Report == "adb shell input tap 540 164"
```

## Before / After

| 文件 | 变化 |
|---|---|
| `World/SpatialLocator.cs`（新） | `SpatialLocator(double X1,double Y1,double X2,double Y2,string SpatialFrameId)`；构造执法规则 A（frame 非空、X1≤X2、Y1≤Y2、[0,1] 域）；`CenterX/CenterY` 派生 |
| `World/UiEntityModel.cs` | ProposedOccurrence / OccurrenceBelief 增 `SpatialLocator? Locator = null` |
| `World/Slice.cs` | OccurrenceFact 增 `SpatialLocator? Locator = null` |
| `World/WorldModel.cs` | 铸造（:243 一带）`Locator: proposed.Locator`；DeriveSlice（:770 一带）直通；`DeriveBindingView` 增 TargetOccurrenceLocator（匹配 occurrence 的 locator；无匹配/无 locator → null） |
| `World/ConsumerViews.cs` | BindingView 增 `SpatialLocator? TargetOccurrenceLocator = null` |
| `Effects/TargetBinding.cs` | CanonicalBinding 增 `SpatialLocator? TargetLocator = null` |
| `Effects/EffectDispatch.cs` | 新 `DeliveryTarget(string OccurrenceReference, SpatialLocator? Spatial = null)`（doc：Reference 仅溯源永不执行）；`DispatchRequest.Target` 类型 string → DeliveryTarget |
| `Effects/EffectBoundary.cs` | Bind UI 分支：locator = view.TargetOccurrenceLocator → CanonicalBinding.TargetLocator；ToDispatchRequest：Target = DeliveryTarget(binding.TargetOccurrenceId ?? TargetSubject, binding.TargetLocator)（字符串通道 locatorless 包装） |
| `Effects/AdbEffectDriver.cs`（新） | 首个产品 driver（dry-run）：ctor(viewportW, viewportH, clock?)；支持集 {tap,set-switch}×device-viewport；投影 clamp；Report = 完整 adb 命令串；规则 B 拒绝三 reason |
| 协议基线 P14 / CONTEXT.md | DeliveryTarget 语义 + 三规则 + invariant 词条 |
| `tests/.../DeliveryTargetAdbTests.cs`（新） | E1–E6 |

不触：UniKernel.cs、GroundingSeam（CandidateOccurrenceFact 不加——
grounding buyer 不需要）、doubles（Deliver 签名不变）。

## 关键语义（防漂移）

1. 规则 A 在 SpatialLocator 构造；规则 B 在 driver；规则 C 是结构事实
   （driver 无 World 输入）——三层各归其位，不互相代执法。
2. OccurrenceReference 永不参与执行（invariant）；E4 结构断言。
3. 归一化协议值（corpus 像素在测试侧按 1080×1920 归一化——真实数字）。
4. driver fallback 禁止：locator 失效 = DeliveryFailed/Unknown → 上游
   re-observe → re-ground → 新 binding → 新 DispatchRequest（E3 语义）。

## 验收映射
state.md E1–E7 → 测试 E1 端到端（540,164 断言）/ E2 构造拒绝 /
E3 三 reason / E4 结构 / E5 多 viewport / E6 字符串通道回归 / E7 全量。
