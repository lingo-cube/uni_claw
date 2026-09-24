# RUN-005 — L2 Policy Protocol & Runtime

lifecycle_state: implementing (Slice A complete) · design_state: frozen · disposition: none · depth: decision-heavy · base: 4022ce40

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
- 2026-09-24 · implementing · **Slice A COMPLETE（Policy Protocol &
  Validation）**· RED→GREEN：产品协议 `AgentDecision.Policy`（+DecisionId，
  V6e/D2 与 §2 sketch 合并落形——correlation 字段在 union 成员，Defer
  先例）+ `PolicyProposal`/`PolicyPredicate`（三员 closed AST）/
  `PolicyActionTemplate`（自带 TargetRole）/`PolicyGuard`（单员）/
  `PolicyTruth`（三态）/`PolicyInvalidationReason`（八因词汇）＝公开咨询缝
  词汇（白名单 RUN-005 增集 10 项，显式修订）；internal 机制面
  `PolicyEvaluationView`（§4.1 owner-derived 独立最小投影，FromBelief 派生）
  / `PolicyGuardCursor`（warm-up≠Unknown）/ `PolicyLease(TryDerive)`
  （§4 identity 规则）/`PolicyEvaluation`（§3 推导表+合取）/`PolicyValidation`
  （V6a-d/f 纯函数）；driver 三触点：ConsultAgentV2 correlation case +
  ValidateDecision V6 case（形态→V6f 唯一→V6g lease 绑定）+ NeedDecision
  Policy case（V6 通过 → `policy-execution-not-implemented` 诚实占位——
  adoption/PolicyExpand 归 Slice B，fail closed 零新 Effect 非终局）。
  StepAct/StepVerify 主链零改动。四元组：method = dotnet test
  UniClaw.Kernel.slnx 全量于工作树（Kernel 509 / 全 solution 729）；
  expected = Slice A 边界测试全绿（policy 2 新测试文件 30 例：三态推导表/
  合取序/warm-up≠Unknown/ClaimInSet 正反例/lease 派生表/V6 全 reject 面/
  第四成员/词汇封闭反射执法）+ 既有全绿；actual = 729 通过 / 0 失败，
  RUN-004 行为零回归（场景库 18/18 经 C8 协议重认证
  `--change RUN-005`——expectationsDigest/executionDigest 逐字节不变，
  仅 runtimeSourceHash 随源码位移）；evidence = 本条 + git diff（4 新文件/
  3 修改 + 18 场景哈希钉扎位移）+ 测试运行输出。 DESIGN_CONFLICT: NONE
  （任务表列 PolicyTermination/PolicyBounds 映射为 FROZEN §2 的
  `Termination` 合取列表与 `MaxApplications` 唯一预算字段——未造 wrapper
  类型即未动冻结 schema）。下一步：Slice B（adoption + PolicyExpand 良基
  循环 + `_pendingPolicyOutcome` + PolicyInvalidated 相位）。
