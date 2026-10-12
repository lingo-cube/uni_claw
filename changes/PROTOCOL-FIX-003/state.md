# PROTOCOL-FIX-003 — S3 预算两层顶 + 续授（ADR-0050 落地）
lifecycle_state: persisted · disposition: none · depth: standard · base: 14987fe

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
  method: "dotnet build（离线）+ liveness/幂等/留痕锚定测试（运行待网络）+ grep 探针"
  expected: "0 错误；A1-A5 探针/测试全过"
  actual: "待开工"
  evidence: "待开工"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；grill 裁决 Q1/Q7-Q11
  全量映射入 Decisions；体量最大，建议独立排期。
