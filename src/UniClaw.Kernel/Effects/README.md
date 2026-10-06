# Effect Boundary 代码索引（L2 · 基线 §16）

Sole **Canonical Binding Authority 与 Effect Delivery Authority**：
bounded target binding、Effect Gate、Dispatch、Effect Receipt、canonical
external effect delivery boundary。不拥有 strategy、replan、recovery 或
Proof judgment（权威表见基线 §10）。terminal 后不存在绕过本边界的
effect path（基线 §7）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `EffectBoundary.cs` | binding/gate/dispatch 权威本体 |
| `TargetBinding.cs` | canonical bounded target binding（含 BindingRejectionReason） |
| `EffectDispatch.cs` | DeliveryTarget——driver-executable 目标地址（EB lowering seam） |
| `DeviceViewportResolver.cs` | 设备视口解析（delivery 前置） |
| `AdbEffectDriver.cs` / `AdbLiveEffectDriver.cs` / `AdbProcess.cs` | ADB 设备 driver（真件 realization） |
| `EgoBrowserEffectDriver.cs` | ego-browser driver（仿真/浏览器 realization） |

## ExecutionSource/ 子目录

可靠执行源（ADR-0023）：effect boundary 自有的执行边界——
`ReliableExecutionSource` + `FileExecutionJournal`（append-only journal，
replay/recovery 的事实源）。

## 变更规则

binding/gate/dispatch/receipt 语义进本目录；新设备/浏览器 realization
= 新 driver（经缝注入，替换不改核心）；执行可靠性机制进
`ExecutionSource/`。driver 不得自行授权（授权来自 Assurance 判定 +
Control intent）。

## 指向

- 基线 §16；ADR-0017（pre-dispatch 安全）、ADR-0023（可靠执行源）；
- 定位锚来自 `../World/`（SpatialLocator/NativeLocator）。
