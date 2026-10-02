# AGT-009 Evidence — Kernel 抽象计划契约（Leader 侧）

> worktree：../uni_claw-agt009（branch agt009-integration；快照基线 a4d3b391 =
> PER-017/018 + Settings Traversal + 确认改动；Plan 契约 c0dbc63e；执行面 0adebbb1）
> 方法等级：DETERMINISTIC（单测/性质/转移）。

## 1. Plan 契约与 V7 校验（Acceptance 1）

- method：`dotnet test tests/UniClaw.Kernel.Tests --filter FullyQualifiedName~KernelRunDriverPlanTests`
  的 6 个 malformed 分支 + correlation + budget 用例（`EmptyPlan_…` /
  `OversizedPlan_…` / `UnknownPlanItemDerivative_…` /
  `PlanActItem_EffectClassOutsideContract_…` /
  `PlanActItem_UnsupportedDesiredState_…` / `PlanActItem_MissingTargetRole_…` /
  `PlanDecisionIdMismatch_…` / `OverBudgetPlan_…`）。
- expected：全部 `AgentDecisionFailed` 且 `EffectReceipts == 0`；reason 分别为
  `plan:empty` / `plan:too-many-items` / `plan:unknown-item` /
  `plan:effect-class-not-allowed` / `plan:unsupported-desired-state` /
  `plan:missing-target-role` / `correlation-mismatch` / `budget-exceeded:steps`。
- actual：16/16 PASS（含上述全部分支；2026-10-02 run）。
- evidence：tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverPlanTests.cs；
  src/UniClaw.Kernel/Runtime/KernelRunDriver.cs（ValidatePlanProposal / ValidateDecision）。

## 2. 多 ActItem 逐个执行、逐个重新验证（Acceptance 2）

- method：`MultiActPlan_ExecutesOneActAtATime_EachActVerified_PlanExhaustionReconsults`
  ——两 ActItem 计划；第一 Act dispatch 后扣留 post-action 证据（WaitingForInput）
  再放行。
- expected：屏障内 receipts 恒 1（第二 Act 不 dispatch）；证据放行后第二 Act
  dispatch+verified；计划耗尽触发第二次咨询（NoAction）→ Completed；
  步锚 (1,0)/(1,1) 按计划项序归档。
- actual：PASS（WaitingForInput@receipts=1 → Completed@receipts=2、consults=2、
  CompletedSteps=[1.0, 1.1]）。
- evidence：同测试文件；不变量 43 屏障由 StepVerify 既有顺序执法（零新执行器：
  ActItem 物化单步复用 StepAct→StepVerify）。

## 3. Observe 零 Effect、不单独宣告完成（Acceptance 3）

- method：`ObserveOnlyPlan_ZeroEffects_DoesNotCompleteAlone`。
- expected：external 拉取 = initial + plan-observe（观察经 P2 入证）；
  `EffectReceipts == 0`；计划耗尽后 NoAction → TerminalNotProven（非 Completed）。
- actual：PASS（externalPulls=2、receipts=0、TerminalNotProven、非终态）。
- evidence：同测试文件；completion proof 仍由 NoAction+Completion /
  TerminalEvaluation 既有语义独占。

## 4. 失效条件（Acceptance 5 Kernel 侧：冲突/验证失败有界废弃）

- method：`VerificationFailure_VoidsRemainingPlan_NoSecondDispatch` +
  `WorldConflict_VoidsPlanBeforeAct_ZeroEffects`。
- expected：验证失败 → 第二 ActItem 永不 dispatch（receipts=1）、再咨询携带
  VerificationFailed 相位与原因；与 ActItem 目标相交的悬案 → 零 dispatch、
  再咨询携带 StepRejected + `plan:world-conflict`。
- actual：PASS（receipts=1/receipts=0；相位与原因在第二次咨询上下文断言）。
- evidence：同测试文件；相交判定 `PlanTargetConflicted` 与
  AgentPlanPolicy 聚焦复查同律（"role[:desc]" 相等/前缀）。

## 5. 有界控制转移（Acceptance 5 ControlItem 侧）

- method：`ReplanLoop_ConsumesGlobalBudget_BoundedFailClosed` /
  `PlanStop_AfterVerifiedAct_VoidsRemaining_ZeroNewEffects_GoesTerminalEvaluation` /
  `PlanReobserve_PullsExternalObservation_ThenReconsults`。
- expected：持续 Replan 在 MaxConsultations 耗尽后 `consult-budget-exhausted`
  （3 次咨询、零 Effect、无预算重置）；Stop 废弃剩余项、零新 Effect、不再咨询、
  直接终局评估；Reobserve 恰拉取一轮 external 观察后回决策边界。
- actual：PASS（3/3 用例；Stop 场景层1 如实判 Completed——typed 证据已满足义务，
  receipts=1、consults=1）。
- expected（补充）：E4——ActItem 目标已满足 → 零 dispatch 消费该项。
- actual：PASS（`ActItemTargetAlreadySatisfied_AdvancesWithoutDispatch`：receipts=0）。

## 6. 公开面 / 联合 / 场景认证回归

- method：`dotnet test tests/UniClaw.Kernel.Tests --filter FullyQualifiedName~KernelRuntimeSurfaceWhitelist`
  + `Policy_RemainsMemberOfClosedAgentDecisionUnion` +
  `python3 tools/scenario_certify.py --check` + 全量 `dotnet test UniClaw.Kernel.slnx`。
- expected：白名单增集（AgentDecision+Plan / AgentPlanProposal / PlanItem(±3) /
  PlanControlKind）后精确匹配；closed union 恰五成员；29 场景 seal 与当前源码
  哈希一致；全量回归绿。
- actual：白名单 PASS；union PASS；场景 29/29 重认证（change=AGT-009，行为测试
  全绿后经唯一 sanction 工具刷新）；全量 1177 通过、唯一失败
  `DocsMetadataTests.Architecture_DeclaresFrozenAuthority` 为基线预存
  （在快照 a4d3b391 上同样失败；PNL-003 未确认文档缺 FROZEN 声明，非本 change
  范围）。
- evidence：tests/UniClaw.Kernel.Tests/Runtime/KernelRuntimeSurfaceWhitelistTests.cs；
  tests/UniClaw.Kernel.Tests/Runtime/PolicyProtocolTests.cs；
  tests/UniClaw.Simulation.Tests/AgentScriptTaxonomyTripwireTests.cs；
  scenarios/*.json certification 块（change=AGT-009）。

## 7. 不绕过既有链路（结构性论证）

- ActItem 只经 `CurrentSteps()` 物化为单步，进入既有 StepAct 链：
  Control（SelectIntent）→ Grounding（ActViaCurrentGrounding）→ Binding →
  Assurance → Effect Gate → dispatch → StepVerify（post-action 证据 +
  VerifyPostActionEffect）；Plan 无任何 dispatch 直通路径（`AgentDecision.Plan`
  载荷无坐标/occurrence/selector/授权字段——类型形状执法）。
- 每轮最多一个 Act：PlanExpand 每轮恰消费一项；ActItem 物化为长度 1 的步列表。
- 预算：计划采纳消耗 1 咨询轮；ActItem 总数 ≤ StepsRemaining（V7）+ 逐项复检；
  Replan 回 NeedDecision 走既有 consult 预算门（无预算重置代码路径）。
