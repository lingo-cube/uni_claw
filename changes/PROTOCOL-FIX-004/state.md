# PROTOCOL-FIX-004 — S4 P10 locator 曝射收窄（CanonicalBinding 去执行材料）
lifecycle_state: persisted · disposition: none · depth: standard · base: 14987fe

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
  method: "dotnet build（离线）+ 字节等价/场景 digest 锚定测试（运行待网络）+ grep 探针"
  expected: "0 错误；三字段零残留；投影字节等价"
  actual: "待开工"
  evidence: "待开工"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；消费面证据已在审计
  附录 A.2 固化；最小切片，可与 -002 并行。
