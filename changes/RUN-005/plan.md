# RUN-005 — Plan

> 前置：设计稿 **v0.3 FROZEN**（owner 终裁 PASS_WITH_ONE_NARROW_AMENDMENT；
> Further Grill NOT REQUIRED）。实现以 FROZEN 设计为权威。

## 实现顺序（owner 指定 Slice A→B→C）

**Slice A — 契约与校验面**
Policy records（PolicyProposal/Predicate 三员/Guard/自带目标模板）·
`PolicyTruth` · `PolicyEvaluationView`（§4.1 契约）· `AgentDecision.Policy`
第四员 · V6 validation（V6a-f）· **lease binding validation**（adoption 绑定
active execution lease）。RED 先行：P9/P10/P11。

**Slice B — 展开运行时**
Policy adoption · ephemeral PolicyState（含 GuardCursor，warm-up ≠ Unknown）·
`PolicyExpand` 良基循环（每轮：fresh observe → lease 校验 → termination →
guard → bounds → match → 物化单步）· `_pendingPolicyOutcome` ·
`PolicyInvalidated`（typed cause，含 LeaseInvalidated；带预算门）·
**复用现有 Act path（零新执行器）**· E4 映射。P1-P8。

**Slice C — 仿真与回归**
`ScriptedUniAgent` 相位感知改造 · Simulation integration · P1-P12 全矩阵 ·
full regression / certification / coverage（场景库随动按 C8 搭乘）。

## 禁令（实现期持续有效）

不再开完整 grill · 不修改 AGT-001 · 不接 DSH/DeepSeek · 不新增 Policy
executor · 不新增 canonical owner · 不扩大 v1 vocabulary。

## 验证策略

- 每实现裁决指回 FROZEN 设计条款（§2-§12）；
- 全 deterministic（ScriptedUniAgent；禁 live model）；
- 权威检查：driver 无 Policy mutation API；「Kernel never invents/repairs/
  extends」无违例路径（模板自带目标后「猜 target」结构性不存在）；
  PolicyTruth 三态在 conflicted/absent 数据上的 fail-closed 行为逐场景可证。
