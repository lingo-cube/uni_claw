# EXP-008 Deterministic Evidence — 2026-09-07

level: DETERMINISTIC（纯内存；无 IO / 真机 / 时钟依赖）

## 环境

- SDK 10.0.400 · net10.0 · UniClaw.Kernel.slnx
- base: 79519a43（FRS-007 closed）；ENTRY 实测基线 70/70 GREEN
  （Kernel 53；Agent 17）

## RED（TDD 先行）

- 类型/签名就位（stub）：`World/ConsumerViews.cs`（ScopedClaim /
  BindingView / ActionAssuranceView / OutcomeAssuranceView，字段集 =
  D6/D7/D8 白名单）+ WorldModel 三派生方法 stub（NotImplementedException）
  + 消费者签名收缩（`SelectIntent(view, slice)` / `ControlInputs` 二元组 /
  EB 三方法收 BindingView / Assurance 三方法收 view）+ UniKernel 组合点
  改派生调用——派生行为未接
- 新增 RuntimeViewExposureTests N1-N7 + seam 机械迁移（C2E 直调 24 处 /
  TOT 11 处 / FRS 1 处 / ING 1 处，见迁移台账）
- 实测：**Kernel 失败 38 / 通过 22（总计 60）**——N5/N6 行为失败
  （派生 stub）+ 36 个既有 act 路径用例失败（同因）；N1/N2/N3/N4/N7
  结构项即过（view 类型已就位）；Agent 17 通过（零改动零触碰）——
  RED 剖面与 plan 预期一致（结构项即过、行为项可见失败）

## GREEN

- 最小实现：WorldModel 三派生方法（BindingView：revision 锚 +
  `WorldState.ContainsKey(subject)`；ActionAssuranceView：revision 锚 +
  FreshnessBasis + subject 冲突存在性；OutcomeAssuranceView：空 subject
  过滤 + claims/conflicts 按 obligation subjects scope + 全量 basis +
  `Uncertainty.ConflictingClaimCount`）
- 实测：**失败 0 / 通过 77**（Kernel 60 = 既有 53 + N1-N7；Agent 17），
  `dotnet test` 全量 GREEN

## 结构残留（VERIFY grep / 反射实测）

- `grep RunState src/UniClaw.Kernel/Control/`：零代码引用
- `grep WorldBeliefRevision src/UniClaw.Kernel/Assurance/ Effects/`：
  仅 doc 注释（"非 WorldBeliefRevision 聚合"），签名零引用
- `git diff --stat`：EvidenceToBeliefTests.cs（E2B 8 断言）零 diff；
  src/UniClaw.Agent/ 与 tests/UniClaw.Agent.Tests/ 零 diff；
  RunState.cs / RunModel.cs / WorldBeliefRevision.cs / Slice.cs 零 diff
- 改动面 = plan After 表（src 7 文件：1 新 + 6 改；tests 5 文件：
  1 新 + 4 迁移）+ docs（基线 P5/P11/§5、ADR-0011、CONTEXT.md、
  state.md、plan、evidence）

## 验收 1-10 逐条证明映射

1. **Control 零 RunState**：N3（反射：ControlLoop 公开方法/属性无
   RunState）+ N7（ControlInputs 二元组、SelectIntent(view, slice)）；
   grep Control 命名空间零引用。
2. **EB 零 aggregate**：N3（反射）+ Bind/IsBindingValid/Dispatch 签名
   （EffectBoundary.cs:56/98/113）只收 BindingView；C2E 直调用例迁移
   后 GREEN。
3. **Assurance 零 aggregate**：N3（反射）+ Judge 收 ActionAssuranceView
   （RuntimeAssurance.cs:53）、EvaluateObligations/JudgeOutcome 收
   OutcomeAssuranceView；TOT/ING 直调迁移后 GREEN。
4. **shape allowlist**：N1（三 view + ScopedClaim 属性集/类型精确匹配
   白名单，新增/改名/改类型即失败）+ N2（无 owner-internal aggregate
   型别、无 mutable collection——IList/IDictionary/ISet<> 实现即失败）。
5. **ephemeral**：N5（rev-1 旧 view + rev-2 current intent/binding →
   Bind StaleRevision / Judge binding-revision-currentness / Dispatch
   binding-stale / IsBindingValid false 四面 fail-closed；对照 current
   view 派生下 valid）+ N4（三 consumer 实例字段含 private 零 view
   持有、零 view 集合持有）。
6. **CBA-005 回归**：C2E 全量 GREEN（Bind→Judge→Gate 序 + 三元组
   correlation 断言原样，见迁移台账"断言零变化"）；check 名集合与顺序
   由 FreshnessEnforcementTests.N7 锁定（零改动通过）。
7. **FRS-007 回归**：FreshnessEnforcementTests 7/7 GREEN（Scenario 14
   N1 原样；FreshnessBasis 经 ActionAssuranceView 抵达 evaluator，
   窄输入语义不变——FreshnessEvaluationInput 构造零改动）。
8. **上层回归**：OUT 18 / ING 8 / GEV 17 / E2B 8 全 GREEN（77/77 内）；
   Agent 层 git diff 空；E2B EvidenceToBeliefTests 零 diff。
9. **全量 GREEN**：77/77（Kernel 60 + Agent 17），无删除用例（迁移
   台账逐条留痕：全部为签名机械迁移，行为断言零变化）。
10. **文档同步**：基线 P5 → deferred(no-current-buyer)（Reference
    Realization 改为"无——dependency 暂时消失"）、P11 → verified
    （三 view 为 Reference Realization + Conflict 升格注记 + ephemeral
    语义）、§5 第二梯队 EXP-008 完成注记；ADR-0011 与 CONTEXT.md
    （Consumer View 词条 / Run Snapshot deferred / Slice avoid 修正）
    立项时已落，VERIFY 核对一致。

## 迁移台账（实际 diff 核对）

```text
签名迁移（seam 直调，行为断言零变化；grep 实测 27 处 derive 调用点）：
  ControlToEffectTests（19 处）：IsBindingValid ×3（:149 用 e2. 前缀 +
    :210/:216）；Accepted4 Bind ×1 + Dispatch ×4；Accepted10 Bind+Judge
    ×2；Scenario13 case1/case2 各 Bind+Judge+Dispatch ×6；产者侧
    correlation Bind+Judge+Judge(null) ×3 —— world.Current! →
    world.DeriveBindingView("screen.home") / DeriveActionAssuranceView
  TerminalOutcomeTests（7 处）：DeriveOutcomeAssuranceView ×3
    （EvaluateObligations ×2 :200/:685 + JudgeOutcome ×1 :346）；
    DeriveBindingView ×3（Outcome11 Bind ×1 + Dispatch ×2）；
    SelectIntent 直调 ×1（:435 去 run.State! 实参）
  FreshnessEnforcementTests（1 处）：IsBindingValid ×1（:120）
  ObservationIngressTests（1 处）：EvaluateObligations ×1（:167）
新增：RuntimeViewExposureTests N1-N7（7 用例）
删除：无
零改动：E2B 8 断言（零 diff）、UniKernel 公开 API（SelectIntent(slice)/
  Act/EvaluateTerminal 签名不变）、RunModel/RunState/WorldBeliefRevision/
  Slice/Reconcile/DeriveSlice/IsSliceValid、Agent 层全部
```

（plan 台账草稿估算 C2E 24 处，实测 19 处（Bind×5/Dispatch×6/Judge×5/
IsBindingValid×3）；TOT 7 处；合计 27 处 derive 调用点 + 1 处实参移除。
台账已按实测在 plan 内同步校正。）
