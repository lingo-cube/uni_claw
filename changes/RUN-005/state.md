# RUN-005 — L2 Policy Protocol & Runtime

lifecycle_state: implementing · design_state: frozen · disposition: none · depth: decision-heavy · base: 4022ce40

## Status

**DESIGN FROZEN → Implementation READY**（owner 终裁
PASS_WITH_ONE_NARROW_AMENDMENT · Further Grill NOT REQUIRED）。

设计稿：`plans/2026-09-24-run-005-l2-policy-runtime.md`（v0.3 · FROZEN）

## Owner 终裁记录（2026-09-24）

**Verdict: PASS_WITH_ONE_NARROW_AMENDMENT**。核心架构成立（Agent authors
Policy · Kernel 只做机械展开 · 每轮 fresh observation · 复用 RUN-004 Act
链 · 无第二 Runtime/owner/planner）。

**必补 1（已落 §4）**：Semantic Lease 规则冻结——adoption 绑定 active
execution lease；每次 PolicyExpand 校验；失效 →
PolicyInvalidated(LeaseInvalidated) → NeedDecision → 零新 Effect；
content revision/scroll/可见元素变化 ≠ 失效；detection vocabulary /
preemption detection / cross-process recovery 维持 DEFER。

**Owner 决策 1（已落 §7）**：采用 `PolicyInvalidated(reason)`，不用
`PolicyGuardTripped`；统一承载一切 policy 级 invalidation；只是 NeedDecision
的 typed cause，不建新状态机。

**Owner 决策 2（已落 §4.1）**：独立最小 `PolicyEvaluationView`
（owner-derived · read-only · ephemeral · fresh-derived · not persisted ·
not recovery state · not authority）；不绑定 AgentDecisionContext。

**GuardCursor 澄清（已落 §8）**：Warm-up ≠ PolicyTruth.Unknown（首样本只
初始化 cursor；样本不足属 warm-up；Unknown 只表示当前证据冲突/不足）。

**接受不变**：MaxRounds 删除 · PolicyFallback 删除 · PolicyTruth 三态 ·
ClaimInSet · TargetRole · remainingApplications 良基递减 ·
_pendingPolicyOutcome · E4 映射 · invalidation budget gate ·
B2/B6/A1/B4 DEFER（v1 buyers = A2/A4/C1 + ElementExists termination）。

## Verification

```yaml
verification:
  level: CONTRACT
  method: 双审（owner 预审 + 零泄漏盲审）→ v0.2 合并修订 → owner 终裁窄修 → v0.3 FROZEN
  expected: findings 全闭合 + lease 冻结 + 两项 Owner 决策落形 + warm-up 澄清
  actual: DESIGN FROZEN / Implementation READY（Baseline reopen: NO）
  evidence: plans/2026-09-24-run-005-l2-policy-runtime.md v0.3（§4/§4.1/§7/§8/§13/§16/§17）
```

## Baseline Impact

Product baseline reopened? **NO** · RUN-004 reopened? **NO** ·
Simulation baseline reopened? **NO** · AGT-001 修改：**NO**（禁令维持）。

## Status log

- 2026-09-24 · understand→designing · v0.1 → ready-for-grill（1739bca2）。
- 2026-09-24 · designing · Owner 预审（PASS_WITH_FINDINGS）+ 盲审（REOPEN）
  → v0.2 dual-grill revision（01a518ab）。
- 2026-09-24 · designing→**implementing（design_state: frozen）** · Owner
  终裁 PASS_WITH_ONE_NARROW_AMENDMENT：lease 规则冻结 + 决策 1/2 落形 +
  warm-up 澄清 → v0.3 **FROZEN**。实现按 plan Slice A→B→C 展开；禁令：
  不再开完整 grill / 不改 AGT-001 / 不接 DSH / 不新增 executor 或
  canonical owner / 不扩大 v1 vocabulary。
