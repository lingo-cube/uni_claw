# PROTOCOL-FIX-002 — S2 期望载荷 ExpectedEffect（ADR-0049 落地）
lifecycle_state: closed · disposition: none · depth: standard · base: 14987fe

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
  actual: "dotnet build（全解决方案）0 错误；grep 探针 src/ 下旧滚动三布尔字段零残留（剩余命中为 Host SettingsCoverage 既有无关类型）；谓词类型 ExpectedEffect/StateBecomes/ChangePredicate 就位并入白名单；dotnet test tests/UniClaw.Kernel.Tests 全量绿：863 通过 / 0 失败（基线 853 + 新增 10）"
  evidence: "method=dotnet build（0 错误）+ dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj（863/863 通过）+ grep -rn \"PriorRoute\\|CurrentRoute\\|ScrollContentChanged\" src tests（Input 字段零残留）；锚定测试：tests/UniClaw.Kernel.Tests/Assurance/ScrollVerificationTests.cs（6 场景等价迁移，判定断言与迁移前逐条一致）+ tests/UniClaw.Kernel.Tests/Assurance/ExpectedEffectTests.cs（A3 空谓词集构造拒绝 / A4 谓词求值 vs 身份连续 / claim-subject / typed 值域）；白名单同步 tests/UniClaw.Kernel.Tests/Runtime/KernelRuntimeSurfaceWhitelistTests.cs（+ChangePredicate/+ExpectedEffect/+StateBecomes）"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；规格就绪，待排期实施。
- 2026-10-12 · IMPLEMENT→VERIFY 完成 · worker=glm-5.3-flash（Fresh Worker，WI-PFX002-001）：ExpectedEffect 两族封闭谓词落地（A1）；Input 必填 Expected（A3 编译期）；滚动三布尔迁移为 RouteStable ∧ ContentChanged 实例后删除，观测事实收敛进 internal ChangeEvidence（A2 等价锚定 6 场景）；Assurance 判定对照物 = 谓词求值（A4，与 -001 S5 门槛不重开）；白名单 +3。A3 空谓词集运行期拒绝；无期望载荷 visual-path 步骤由 UniKernel 机械实例化 `StateBecomes(target.Role, "present")`（可观察在位；navigation transition 由导航证明豁免）——该保留字值域为本次实现补充语义，建议后续协议变更复核。
- 2026-10-12 · REVIEW→VERIFY→CLOSED（Leader） · 独立复跑 dotnet test Kernel 866/866
  （当时点）零回归；diff 复核：谓词类别封闭、期望/观测分离、effect-class 硬编码
  门删除，与 D1-D3 逐条对位。"present" 保留字经 Leader 裁定属 D2"锁类别不锁
  exhaustive"值域内（D3 无期望=RED 对 visual-path 步骤的必然推论），ADR-0049
  已补实现注记（值域扩展走协议变更复核，留 owner 复核项）。封印随系列统一
  reseal（certifiedByChange=PROTOCOL-FIX-002）。
