# DSE-002 — DeliveryTarget（EB lowering seam 落地 + 首个 ADB driver）
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 0b2cf4ef

## Intent（WHAT/WHY）
DSE-001 把 `DispatchRequest.Target` 收窄为 occurrence id 字符串时，以「无源
不造」豁免了 executable target（Residual risks 记录在案）。Human 原则
（2026-09-09）推翻该豁免：**driver 可替换是产品前提**（后续后端：ADB →
机械手 / ego-browser / 任意），occurrence id 对一切真实后端不可执行——
替换即断，buyer 从假设性变为现在时。

Human 裁决定义（本轮，2026-09-09）：

```text
DSE-002 不只是载荷补全，而是建立正式的 Effect Boundary lowering seam：
把 Runtime-authorized target 转换为 driver-executable DeliveryTarget。
Driver 只负责物理翻译与投递，不负责重新 grounding、选择语义目标或自行恢复。

WorldModel 认目标，Effect Boundary 出地址，Driver 只送货。
```

五层名词不混（Human 裁决）：TargetDescriptor（找谁）→
ObservationOccurrence（看到谁）→ CanonicalBinding（作用谁）→
DeliveryTarget（去哪执行）→ DispatchRequest（送什么）。

## Scope（第一切片，Human 裁决收窄）
- `World/SpatialLocator.cs`：`SpatialLocator(X1,Y1,X2,Y2, SpatialFrameId)`
  ——归一化 bounds + 开放 frame 词汇（不锁枚举；v0.1 值 =
  "device-viewport"，机械手未来只加词不加结构）。**构造执法规则 A**：
  空间值必带 frame，bounds 有效（X1≤X2 等），违反 = 构造拒绝。
- World occurrence 族（`ProposedOccurrence`/`OccurrenceBelief`/
  `OccurrenceFact`）增 `SpatialLocator? Locator`（末位可选，CDS-001 State
  同款先例）；`WorldModel` 铸造 / `DeriveSlice` 直通。
- `BindingView` 增 `SpatialLocator? TargetOccurrenceLocator`（owner-derived：
  匹配 occurrence 的 locator fact；新白名单成员，UIW-004 先例）。
- `Effects/TargetBinding.cs`：`CanonicalBinding` 增 `SpatialLocator?
  TargetLocator`（EB Bind 时从 view 携带）。
- `Effects/EffectDispatch.cs`：新 `DeliveryTarget(OccurrenceReference,
  SpatialLocator? Spatial)`——OccurrenceReference 仅溯源（receipt/attempt
  关联），**永不参与执行**；`DispatchRequest.Target` 类型 string →
  DeliveryTarget（EB lowering 唯一构造点）。
- `Effects/AdbEffectDriver.cs`：**首个产品 driver**（dry-run：命令构造，
  零进程孵化）。归一化 bounds center × viewport → pixel → `adb shell
  input tap X Y`（legacy CoordinateMapper 数学平移）。**规则 B 执法**：
  locator 缺失 / frame 非 device-viewport / effect 不支持 →
  DeliveryFailed + reason（no-executable-locator / unsupported-frame /
  unsupported-effect）。
- 协议基线 P14 + CONTEXT.md（DeliveryTarget 词条）。
- 测试：corpus 真实 bounds 端到端 + 失败路径族（见 Acceptance）。

## Out of Scope（禁止）
- **NativeLocator 不实现**（Human 裁决：无真实数据链宁可不建——类型都不
  预建，等 ego-browser/stable-key 链路 buyer）。
- driver 侧 locator fallback / priority / scoring / retry（Human 裁决禁止：
  ID 失效改用坐标 = 语义重定位 = stale grounding；正确路径 =
  DeliveryFailed/Unknown → 上游 re-observe → re-ground → 新 binding →
  新 DispatchRequest）。multi-locator delivery buyer 出现前不讨论。
- 机械手 schema（3D pose / 力学参数）——Parameters/family 域，随其立项。
- 非 UI 字符串通道语义（Deferred ⑮ 不动；其 DispatchRequest 以
  locatorless DeliveryTarget 包装，协议构造不强制 locator——driver-supported
  判定归 driver）。
- 真实 ADB 进程执行 / IAdbProcessRunner 移植（dry-run 命令构造即第一验收）。

## Decisions（Human 裁决冻结）
- **规则 A**（协议构造级）：spatial 无 frame = 无效载荷（P-UW-16 执法到
  locator 构造）。
- **规则 B**（driver 级）：DeliveryTarget 必须含至少一个 **driver-supported**
  executable locator——supported 与否由 driver 判定（换 driver = 换支持集，
  协议不预设）。
- **规则 C**（结构级）：locator 只能来自当前授权 Binding 的 lowering；
  driver 不得语义重定位（结构保证：driver 无 World/grounding 输入）。
- **invariant 冻结：Reference identity ≠ executable locator**——
  OccurrenceReference 永不参与执行（防 legacy PhysicalEnvironment
  `_actionHistory` 式第二真相从 adapter 侧回渗）。
- SpatialLocator 归 World 域（P-UW-16/§24 frame 语义 owner 是 UWM-009；
  依赖方向 Effects→World 已存在，不建反向依赖）。
- 归一化坐标（非像素）进协议：与 viewport 解耦、跨设备 replay 稳定
  （legacy CoordinateMapper DIRECT IDEA）；corpus 像素在测试侧按真实设备
  尺寸归一化。

## Alternatives（被拒，Human 2026-09-09）
- 后端特化子类进协议（PixelTarget/DomTarget）——R13 provider 扩散。
- driver 内 locator fallback（ID→坐标）——语义重定位，越权 + stale
  grounding 事故源。
- 万能 robotics pose schema 预建——无 buyer（当前 buyer = ADB）。
- 协议构造强制「至少一 locator」——字符串通道（Deferred ⑮）会被误伤；
  层次归 driver（规则 B）。

## Owner-Authority impact
- 无新 L2 Owner。EB lowering 从「字段改名」升级为正式语义缝
  （authorized target → executable address 的唯一转换点）。
- World：occurrence 载荷 +1 维（UWM-009 §41「frame-bound spatial」语义位
  兑现——冻结语义一直有，realization 缺）。
- Driver 首次获得产品实现（AdbEffectDriver）；doubles 不受影响。

## ADR refs
- ADR-0011（locator 字段 buyer = 可替换性原则）；P14 协议边；P-UW-16；
  DSE-001（Residual 兑现）。

## Residual risks
- NativeLocator 数据链缺失（ego-browser 立项时补）。
- dry-run ≠ 真机（进程层/超时/三态映射随真实执行 buyer）。
- frame 词汇 "device-viewport" 语义 = 归一化相对单设备全屏——多屏/车机
  frame 词汇随其 buyer。

## Acceptance
E1 端到端（corpus 真实数字）：golden-run reset_button 像素 bounds
   (48,140,1032,188) @1080×1920 → 归一化 → occurrence.Locator →
   ResolveCurrent/Bind → CanonicalBinding.TargetLocator → Dispatch →
   DispatchRequest.Target=DeliveryTarget → AdbEffectDriver → receipt.Report
   == `adb shell input tap 540 164`（center=(540,164)，零进程）
E2 规则 A：SpatialLocator 空 frame / 逆 bounds → 构造拒绝
E3 规则 B：locator 缺失 → DeliveryFailed(no-executable-locator)；
   frame="container-viewport:c1" → unsupported-frame；effect="swipe" →
   unsupported-effect（AdbEffectDriver 支持集 = {tap, set-switch} ×
   device-viewport）
E4 规则 C / invariant：DispatchRequest 四字段结构断言延续（Target 现为
   DeliveryTarget；无 IntentId/BindingId/judgment）；OccurrenceReference
   仅出现在 DeliveryTarget 内（receipt 关联），driver 无解析路径
E5 多 viewport 投影：同 locator × 不同 viewport 尺寸 → 不同 pixel 命令
   （归一化跨设备稳定）
E6 字符串通道回归：locatorless DeliveryTarget 经 doubles 全链不破
E7 既有 192 全量零回归

## Constraints
- 新代码仅限 World occurrence 载荷 + SpatialLocator + BindingView/派生 +
  Effects（TargetBinding/EffectDispatch/EffectBoundary lowering/AdbEffectDriver）
  + 协议基线/CONTEXT.md + 新测试。
- 确定性（driver clock 注入；零 wall-clock/random）；不触 UniKernel.cs
  （ActViaCurrentGrounding 面不变）。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案，两次独立运行）
  expected: E1–E7 GREEN；全量零回归（并行会话半成品除外，见 actual）
  actual: >
    199 总测试：197 通过 + 2 失败（两次独立运行一致）。2 失败 =
    RunTraceBulletTests.Catalog_Totality（11≠16）与 Artifact_Shape（Int64
    payload）——经查证均属并行会话（Latency 基线）工作区半成品（其已改
    src/Trace/SpanDefinition·TraceReference·UniKernel·FastPerception +
    新增 RuntimeStageMetrics.cs，守卫未更——该会话自身债务，非本 change
    波及；证据：失败面在 Trace 域，本 change 零触碰 Trace）。本 change 面
    全绿：新增 DeliveryTargetAdbTests 13 用例（E1 端到端断言 receipt.Report
    == "adb shell input tap 540 164"——golden-run 真实像素 48,140,1032,188
    @1080×1920 归一化投影；E2 规则 A 构造拒绝；E3 三 reason fail-closed；
    E4 invariant/四字段结构；E5 三 viewport 投影稳定；E6 字符串通道回归）
    + N1 allowlist 迁移（BindingView +TargetOccurrenceLocator 成员，UIW-004
    先例）+ 既有全量。变更面 = World/（SpatialLocator 新增；UiEntityModel/
    Slice +Locator；WorldModel 三处直通/派生；ConsumerViews +fact）+
    Effects/（TargetBinding +TargetLocator；EffectDispatch +DeliveryTarget +
    Target 改型；EffectBoundary Bind 携带 + lowering 产 DeliveryTarget；
    AdbEffectDriver 新增——首个产品 driver）+ 协议基线 P14 + CONTEXT.md +
    测试。UniKernel.cs 零触碰（其工作区改动属并行会话）。
  evidence: dotnet test 输出（2026-09-09，两次独立运行；并行 in-flight
    文件移开-验证-复原，内容零修改 mtime 一致确认）
```

## Status log
2026-09-09 · understanding→resolved→planned · Human 原则（driver 可替换：
ADB→机械手/ego-browser/任意）推翻 DSE-001「无源不造」豁免；裁决冻结
  DeliveryTarget/SpatialLocator 形状、NativeLocator 不建、driver 禁
  fallback、三规则 + Reference≠locator invariant；to-spec + PLAN 同会话
  （plans/2026-09-09-dse-002-delivery-target.md）

2026-09-09 · planned→implemented · Direct 实施。SpatialLocator（World 域，
  P-UW-16 owner）/ DeliveryTarget / TargetLocator 载荷链 / AdbEffectDriver
  （dry-run，规则 B 三 reason）
2026-09-09 · implemented→reviewed · REVIEW 偏离 2 条：①并行会话
  （LatencyBaseline）实施中途出现并改 Trace/UniKernel/FastPerception——
  RunTrace 两守卫失败经查证属其半成品（Trace 域，本 change 零触碰），
  不修不碰，验证口径如实记录；②N1 allowlist 迁移 = BindingView 新字段的
  显式解锁面（计划内，UIW-004 先例）。产品面与 PLAN Before/After 一致
2026-09-09 · reviewed→verified→closed · 两次独立运行（197 通过 + 2 并行
  半成品失败）+ diff 审阅合规 → DELIVERY_TARGET_LOWERING_SEAM_ESTABLISHED。
  「可替换 driver」从口号变为 executable boundary：WorldModel 认目标，
  Effect Boundary 出地址，Driver 只送货
