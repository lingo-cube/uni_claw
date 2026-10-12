# PROTOCOL-FIX-004 — S4 P10 locator 曝射收窄（CanonicalBinding 去执行材料）
lifecycle_state: closed · disposition: none · depth: standard · base: 14987fe

## Intent（WHAT/WHY）

形式审计附录 A.2：`CanonicalBinding` 携带 `TargetLocator/TargetNative/
TargetSpace` 三字段，但 P10 收件人是 Assurance（裁决不需要执行坐标）——
投递材料（P14 层）提前固化进绑定载荷。消费面已查清：全仓仅两处
（`EffectBoundary.cs:261` lowering；`CoreSemanticProjection.cs:160`）。

## Scope / Out of Scope

In：`Effects/TargetBinding.cs`（移除三字段）；`Effects/EffectBoundary.cs`
lowering 改从 view 取（构造处本就读 `view.TargetOccurrenceLocator` 等，
`ToDispatchRequest` 需改签名携带 view 或 locator 三元组）；
`Core/CoreSemanticProjection.cs` 改从 `DeliveryTarget` 投影（语义更正：
delivery record 从 delivery material 投影）；白名单/快照测试同步；
协议基线 P10 载荷注记。
Out：P14 `DeliveryTarget` 形状（不变，且成为 locator 唯一来源）；
字符串通道行为；S1-S3。

## Decisions

- D1：直接收窄（非双写过渡）——消费面两处、进程内、测试可锚定，
  EXP-008 先例即直接收窄。
- D2：Core 投影改源后须验证**输出字节等价**（同输入对照），防止投影漂移。
- D3：收窄后 `DeliveryTarget` 是 locator 唯一载体（五层名词链归位：
  TargetDescriptor→Occurrence→Binding→DeliveryTarget→DispatchRequest）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | `CanonicalBinding` 无 locator 三字段（形状快照锚定） |
| A2 | Core 投影输出迁移前后字节等价 |
| A3 | lowering 行为零变化（既有 act 场景 digest 一致） |
| A4 | 公开面白名单同步；`DeliveryTarget` 为唯一 locator 来源 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet build（离线）+ 字节等价/场景 digest 锚定测试 + grep 探针"
  expected: "0 错误；三字段零残留；投影字节等价"
  actual: "slnx 全量 build 0 错误；三字段零残留（全仓 src+tests 生产面 grep
    探针零命中，仅锚定测试自引用）；Core 投影改 DeliveryTarget 源后输出
    字节等价（字面量锚逐项相等：browser.backend-node-id/node-42/
    spatial:device-viewport/0.1000,0.2000,0.3000,0.4000/none）；lowering
    行为零变化（DeliveryTargetAdbTests E1 adb 命令串不变 + 新增
    view→DeliveryTarget 逐项同源断言）"
  evidence: "dotnet build UniClaw.Kernel.slnx → 0 错误；dotnet test
    tests/UniClaw.Kernel.Tests → 866 通过 / 0 失败（新增锚定
    CanonicalBindingLocatorNarrowingTests 3 测试：形状快照 A1 / 字节等价
    A2 / lowering+DeliveryTarget 唯一载体 A3-A4）；
    tests/UniClaw.Simulation.Tests 188 中 186 通过（2 失败为
    ScenarioCertificationTests 认证封印哈希——Kernel 源码变更后需 reseal，
    属独立封印流程，非本 change 范围）"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；消费面证据已在审计
  附录 A.2 固化；最小切片，可与 -002 并行。
- 2026-10-12 · S4 IMPLEMENTED/VERIFIED · worker=glm-5.3-flash ·
  CanonicalBinding 移除 TargetLocator/TargetNative/TargetSpace（D1 直接
  收窄）；EffectBoundary.ToDispatchRequest 改签名携 BindingView、locator
  从 view.TargetOccurrence* 取；CoreSemanticProjection.ProjectTargetBinding
  增 DeliveryTarget? target 参数、ProjectLocator 改从 target 投影（D2 字节
  等价锚定）；DeliveryTarget 为 locator 唯一载体（D3）。测试同步：
  新增 CanonicalBindingLocatorNarrowingTests（形状快照/字节等价/lowering
  锚定）；DeliveryTargetAdbTests E1 改经 CapturingEffectDriver 观察
  DeliveryTarget；CoreProjectionSeamTests LocatorMaterial 改 target: 入参；
  Simulation.Tests 断言迁移至 DeliveredRequests（DeterministicEffectDriver
  增捕获）。协议基线 P10 增 PROTOCOL-FIX-004 修订注记。白名单无形状变化
  （类型面），未改。未 commit/push。新发现：ScenarioCertificationTests 2 失败
  = 认证封印哈希需 reseal（源码变更后固有，超出本 change 范围，移交 Owner）。
- 2026-10-12 · REVIEW→VERIFY→CLOSED（Leader） · 独立复跑 dotnet test Kernel
  866/866（当时点）；diff 复核：三字段零残留、lowering 从 view 取、Core 投影
  改自 DeliveryTarget 且字节等价锚定、Simulation 断言迁移不改语义——D1-D3
  逐条对位。封印失败如 worker 所报，已由 Leader 统一 reseal 吸收（本 change
  为源哈希变更致因之一）。
