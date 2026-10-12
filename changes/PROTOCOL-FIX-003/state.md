# PROTOCOL-FIX-003 — S3 预算两层顶 + 续授（ADR-0050 落地）
lifecycle_state: closed · disposition: none · depth: standard · base: 14987fe

## Intent（WHAT/WHY）

D1/D7/ADR-0050：终止测度从"View 缺省承重"变为"policy 硬顶的结构保证"；
续授经 P25 通道（大脑提议、规则封顶），轻记账。

## Scope / Out of Scope

In：`Run/ExecutionContract(View)` admission 留痕 valueOrigin；policy 参数面
（HardCap 步数/轮次 + RenewalThreshold）；`Runtime/KernelRunDriver` 续授窗口
检查与终问携带；`Runtime/AgentDecision`/consultation 载荷扩展（续授提案）；
锚定测试；协议基线 P1/P25 窄注记。
Out：契约不可变不动（续授不是 contract mutation）；崩溃后额度恢复（随 ⑰）；
S1/S2/S4 内容。

## Decisions

- D1：两层顶——契约预算=初始额度（admission 记 valueOrigin ∈ {configured,
  default}，挂 View 侧留痕字段）；policy 声明 HardCap（有限、各自独立）。
- D2：触发（规则侧）——driver 进决策边界检查 `剩余 ≤ RenewalThreshold` →
  该次咨询标记续授窗口；决策上下文已带 BudgetRemaining（既有）。
- D3：决定（大脑侧）——UniAgent 提案附续授量；policy 校验
  `已授 + 提案 ≤ HardCap`；**DeferRoundsExhausted 终问可携带续授提案**
  （防"有步数没轮次开口"死锁角）。
- D4：轻记账——driver 消耗态 + decision 留痕 + trace；不进 canonical
  Run State（不变量 36 精神）；拒续/触顶 → 既有 budget-exhausted 诚实终局。
- D5：`AgentPlanPolicy`（FROZEN v0.3.1）参数面扩展属窄修订，随本 change。

## Acceptance

| # | 判据 |
|---|---|
| A1 | admission 产出预算 valueOrigin；View 沿载（同内容 View 字节不变） |
| A2 | 阈值窗口正确开启/关闭；终问可携带续授 |
| A3 | **liveness 锚定**：无限续授请求全部被 HardCap 拒绝 → 必达诚实终局 |
| A4 | 续授全程 P25 留痕（决策 id、量、依据）；契约 View 零变化 |
| A5 | 步数/轮次同机制、各自硬顶；缺省路径行为不变（无 policy 时） |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet build（全仓）+ dotnet test tests/UniClaw.Kernel.Tests 全量 + liveness/幂等/留痕锚定测试"
  expected: "0 错误；A1-A5 探针/测试全过"
  actual: "dotnet build 0 错误；dotnet test 872/872 通过（基线 866 + 新增 6：A1 origin 1 / A2 窗口 1 / D3 终问 1 / A3 liveness 1 / A5 缺省 1 / A4 View 不变 1）；白名单同步通过"
  evidence: "tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverBudgetRenewalTests.cs（A1-A5 锚定）；KernelRuntimeSurfaceWhitelistTests（+BudgetValueOrigin/BudgetRenewalRequest/BudgetRenewalRecord/RenewalWindow）；docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md P1/P25 注记"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；grill 裁决 Q1/Q7-Q11
  全量映射入 Decisions；体量最大，建议独立排期。
- 2026-10-12 · IMPLEMENT/VERIFY（S3 落地） · worker=glm-5.3-flash ·
  ADR-0050 D1-D5 全量实现：Run 侧 `BudgetValueOrigin`（View 沿载，
  RunModel admission 确定性派生）；policy 参数面 `AgentPlanPolicy`
  HardCap×2 + RenewalThreshold×2（全缺省 = 通道关闭，A5 回归锚定）；
  P25 续授载荷 `BudgetRenewalRequest`（Act/NoAction/Defer 可附，
  DeferRoundsExhausted 终问可携带闭合死锁角）+ 上下文 `RenewalWindow`
  投影；driver 续授窗口重算（进决策边界时）、规则侧封顶校验
  （已授+提案 ≤ HardCap，拒续仅留痕按无续授继续）、轻记账
  `BudgetRenewalRecord` 留账 + `RenewalsGranted` 只读审计投影——
  不进 canonical Run State，契约 View 零变化。liveness：HardCap 有限
  ⇒ 授出有界 ⇒ 必达 consult-budget-exhausted 诚实终局（A3 测试证明）。
  协议基线 P1/P25 窄注记已落。 forbidden 未触碰；未 commit。
- 2026-10-12 · REVIEW→VERIFY→CLOSED（Leader） · 独立复跑 dotnet test Kernel
  872/872 零回归；diff 复核：两层顶（View valueOrigin 留痕 + HardCap 结构
  保证）、窗口重算进决策边界、ValidateRenewal 封顶、终问携带已授续授闭合
  死锁角、轻记账不进 canonical Run State——D1-D5 逐条对位，A1-A5 锚定在
  KernelRunDriverBudgetRenewalTests。trace 接线缺口（driver 不持 IRunTrace）
  如 worker 所报留后续切片。
