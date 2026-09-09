# Plan — DSE-003 NativeLocator + ego-browser driver（+EB 级联去重）

> PlanType: dse-003-native-locator-ego-driver / Status: ADOPTED /
> References: changes/DSE-003/state.md · DSE-002（DeliveryTarget 前作）·
> RVR-001 state（级联去重移交凭据）· Human 方向裁决 2026-09-09

## 切片链

```text
ProposedOccurrence.Native（NativeLocator: kind + value）
    ↓ WorldModel 铸造 / DeriveSlice / DeriveBindingView
OccurrenceFact / BindingView.TargetOccurrenceNative
    ↓ EB Bind（级联去重后）
CanonicalBinding.TargetNative
    ↓ EB lowering
DeliveryTarget(OccurrenceReference, Spatial?, Native?)
    ↓ 双 driver 交叉：
       AdbEffectDriver   只消费 Spatial  → "adb shell input tap X Y"
       EgoBrowserEffectDriver 只消费 Native → "ego click node:827"
```

## Before / After

| 文件 | 变化 |
|---|---|
| `World/NativeLocator.cs`（新） | `NativeLocator(string Kind, string Value)`；kind 开放词汇 `<platform>.<key-kind>`；doc：execution anchor ≠ identity（P-UW-24） |
| `World/UiEntityModel.cs` / `Slice.cs` | occurrence 族 + `NativeLocator? Native = null`（末位） |
| `World/WorldModel.cs` | 铸造/DeriveSlice 直通 + DeriveBindingView native fact（匹配 occurrence 的 Native） |
| `World/ConsumerViews.cs` | BindingView + `NativeLocator? TargetOccurrenceNative = null` |
| `Effects/TargetBinding.cs` | CanonicalBinding + `NativeLocator? TargetNative = null` |
| `Effects/EffectDispatch.cs` | DeliveryTarget + `NativeLocator? Native = null`；doc 增「多 locator 并存 = 不同 delivery material；driver 固定消费支持集，永不挑选/fallback」 |
| `Effects/EffectBoundary.cs` | Bind UI 分支携带 native（+级联去重：双分支共享 stale→ambiguous 前置与判定骨架提取，零行为变化）；lowering 产 Native |
| `Effects/EgoBrowserEffectDriver.cs`（新） | 第二产品 driver（dry-run）：支持集 browser.backend-node-id × {click,tap,set-switch} → `ego click node:{id}`；clock 注入 |
| `tests/.../EgoBrowserDeliveryTests.cs`（新） | N1–N4 + N2 双 driver 交叉 |
| `tests/.../RuntimeViewExposureTests.cs` | N6 白名单 +1 |
| 协议基线 / CONTEXT.md | P14 NativeLocator 落地注记；词条更新 |

不触：UniKernel.cs、AdbEffectDriver（支持集不变——adb 只认 spatial）、
并行 LAT-001 会话全部文件。

## 关键语义（防漂移）

1. 双 driver 各自只认一种 locator——这是「delivery form 由支持集声明」
   的实现，不是 fallback 基础设施；N3 是无 fallback 的行为断言。
2. 级联去重边界 = RVR-001 凭据：拒绝序与 reason 词汇零变化，N5 以既有
   Bind 测试全绿证明。
3. backend-node-id 失效的真实闭环（DOM 重建）留待真链接入——本轮
   dry-run 只证协议端。

## 验收映射
state.md N1–N7 → 测试（N5/N7 由既有套件承载）。
