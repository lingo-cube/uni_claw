# PROTOCOL-FIX-002 — S2 期望载荷 ExpectedEffect（ADR-0049 落地）
lifecycle_state: persisted · disposition: none · depth: standard · base: 14987fe

## Intent（WHAT/WHY）

D8/ADR-0049：验证的对照物成为显式载荷。`PostActionEffectVerificationInput`
增加两族封闭谓词期望（StateBecomes 状态型 / ContentChanged·RouteStable 变化型），
滚动三布尔（PriorRoute/CurrentRoute/ScrollContentChanged）迁移为
`RouteStable ∧ ContentChanged` 实例后删除。

## Scope / Out of Scope

In：`Assurance/PostActionEffectVerification.cs`（载荷类型 + Input 扩展）、
Kernel 编排侧构造点（UniKernel/KernelRunDriver 中 Input 的组装处）、滚动语义
迁移、锚定测试、CONTEXT 词条核（已落，无需改）。
Out：S2 不改 MaterialEffect 判定门槛（S5 已在 -001 落地）；谓词词汇扩展面
（封闭起步，扩展走协议变更）。

## Decisions

- D1：期望载荷挂 **验证输入侧**（authoring/意图投影），不进 CanonicalBinding、
  不进 ExecutionContract、不进 P14——避免第二套 effect 权威（ADR-0049）。
- D2：两族谓词封闭起步：`StateBecomes(subject, value)`；
  `ChangePredicate ∈ {ContentChanged, RouteStable}`（通则 4：锁类别不锁
  exhaustive）。滚动迁移 = `RouteStable ∧ ContentChanged` 复合。
- D3：**无期望载荷的验证输入 = RED**（编译期必填 + 测试双保险）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | Input 携带必填 ExpectedEffect；两族谓词类型就位 |
| A2 | 滚动三布尔删除；等价迁移（既有滚动验证场景 digest 前后一致） |
| A3 | 无期望载荷构造 = 编译失败；空谓词集 = 测试 RED |
| A4 | S5 门槛与期望谓词联动（MaterialEffect 对照物 = 谓词求值，非身份连续） |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet build（离线）+ 滚动场景等价锚定测试（运行待网络）+ grep 探针"
  expected: "0 错误；三布尔零残留；谓词类型就位"
  actual: "待开工"
  evidence: "待开工"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；规格就绪，待排期实施。
