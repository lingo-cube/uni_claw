# RUN-005 — L2 Policy Protocol & Runtime

lifecycle_state: implementing (Slice A+B owner-adjudicated PASS; Slice C in progress) · design_state: frozen (v0.3.1) · disposition: none · depth: decision-heavy · base: 4022ce40

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

- 2026-09-24 · implementing · **v0.3.1 窄幅 amendment（owner 授权——不重开
  设计、不再 Grill）**：Slice A 终裁 PASS，唯一修正项落地——删除
  `PolicyPredicate.ElementExists`（动因：「看见→Satisfied / 没看见→Unknown」
  与 Termination Unknown→PolicyInvalidated 出口矛盾，无法表达「继续执行
  直到目标元素出现」；「元素没出现」只有 coverage 足够时才能合法判 false，
  v1 无 coverage/completeness semantics；禁把 not-observed 改判 Violated
  绕过）。v1 词汇 = ClaimEquals/ClaimInSet + ObservationUnchanged；DEFER
  登记 buyer = coverage-aware traversal / termination（P12 随之 DEFER，
  矩阵 P1-P11 聚焦 A2/A4/C1 claim-driven contingent loop）。同步：
  PolicyProtocol/PolicyRuntime/词汇封闭反射测试/truth-table 测试/白名单
  （-ElementExists）/设计稿 v0.3.1（§2/§3/§10/§11/§16/§17 + §4 两条 Slice B
  实现约束：lease exact-equality / PolicyId validated≠adopted）/spec/plan。
  上述 state.md 早条的「v1 buyers = … + ElementExists termination」被本
  amendment 取代（历史记录保留）。
- 2026-09-24 · implementing · **Slice B COMPLETE（展开运行时）**· adoption
  （V6 通过即采纳：绑定 exact `PolicyLeaseRef` 进 ephemeral
  `PolicyExecutionState`；`_adoptedPolicyIds` 只在实际采纳时记录——
  validated/reserved ≠ adopted，V6f operand = 采纳集）· `DrivePhase.
  PolicyExpand` 良基循环（每轮：fresh External observation[WaitingForInput
  可恢复] → lease current==adopted exact 等值 → Termination 合取 → Guards
  逐个 tri-state → bounds → Match 合取 → 模板物化单步）· 复用
  StepAct→StepVerify 全链零新执行器（`CurrentSteps()` 统一取步；主链
  非-policy 路径逐字节不变）· verified → ApplicationsUsed++/GuardCursor
  更新（首样本只初始化；缺席/冲突不产样本）→ 回 PolicyExpand · E4 映射
  （plain-Observe → 重评 Termination：Satisfied→policy-succeeded / 否则
  control-non-act，F7(b)）· `_pendingPolicyOutcome`（Phase+Reason+Summary，
  活到下次咨询消费即清；Progress.PolicyState 数据源）· `PolicyInvalidated`
  新相位（八 typed reason，reason 原文 M2；invalidation 预算门由 NeedDecision
  既有 consult-budget-exhausted 执法）· 步链失败（grounding/gate/verify/
  no-root）既有转移 + policy 作废 + 摘要并入。公开面 +`PolicyProgressState`
  （白名单 RUN-005 增集）+`AgentDecisionPhase.PolicyInvalidated` +
  `ConsultationProgress.PolicyState` 可选尾参。四元组：method = dotnet test
  UniClaw.Kernel.slnx 全量 + scenario-coverage --run；expected = Slice B
  边界/行为测试全绿（新增 KernelRunDriverPolicyExpandTests 11 例：两轮
  成功链+WaitingForInput 续跑/0-application 即时满足/termination-unprovable/
  no-match/match-unknown/bounds-exhausted/guard-violated（2 applications 后
  trip）/guard-unknown/lease-invalidated（容器身份漂移零新 Effect）/
  control-non-act E4/验证失败既有转移+摘要；validation 套件补 duplicate-id
  采纳占用例）+ 既有全绿；actual = 740 通过 / 0 失败（Kernel 520 ·
  Simulation 162 · Host 18 · Agent 17 · Core 14 · FSRealization 9），
  场景库 18/18 重认证（--change RUN-005，expectationsDigest/executionDigest
  逐字节不变，仅 runtimeSourceHash 位移）+ coverage 18/18=100%；RUN-004
  零回归（E1-E5/Defer 链/cancel/finalization 全绿）。调试记录：4 轮到
  GREEN，缺陷全部在测试脚手架（容器 id 探针 capture-time 错位 / post
  队列误用 External 构造器 / claim 演化同 scope 异值→Conflict 语义——
  Temp 观察改 tick 派生 scope 走 Revise），driver 实现零返工。
  DESIGN_CONFLICT: NONE。下一步：Slice C（ScriptedUniAgent 相位感知重做 +
  Simulation integration + P1-P11 全矩阵 + full regression/certification/
  coverage）。

- 2026-09-24 · implementing · **Slice B owner 裁决：PASS**——PolicyExpand:
  PASS · Semantic lease: PASS · PolicyState boundary: PASS · RUN-004
  execution reuse: PASS · Budget/invalidation: PASS · Architecture
  deviation: NONE。保持 `design_state: frozen` / `lifecycle_state:
  implementing`（不 CLOSED——余 Slice C）。进入 Slice C（ScriptedUniAgent
  相位感知重做 · Simulation integration · P1-P11 全矩阵；禁接 DSH/禁扩
  vocabulary/禁恢复 ElementExists/禁 traversal memory/禁改 FROZEN 设计/
  禁新 Simulation execution path/禁为测试方便改 Product semantics）；
  Slice C 后做 RUN-005 最终实现级验收 → CLOSED（不直接进 AGT-002）。
