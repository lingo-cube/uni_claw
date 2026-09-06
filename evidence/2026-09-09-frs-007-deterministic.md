# FRS-007 Deterministic Evidence — 2026-09-09

level: DETERMINISTIC（纯内存；无 IO / 真机 / 时钟依赖）

## 环境

- SDK 10.0.400 · net10.0 · UniClaw.Kernel.slnx
- base: 9374f6ae（ING-006 closed）；ENTRY 实测基线 63/63 GREEN
  （Kernel 46 = E2B 8 + C2E 13 + OUT 18 + ING 7；Agent 17）

## RED（TDD 先行）

- 类型/签名就位：`FreshnessBasis` rename（WorldBeliefRevision / Slice /
  WorldModel）+ `Assurance/Freshness.cs`（ConsumptionRequirement /
  FreshnessEvaluationInput / FreshnessSufficiency / FreshnessJudgment /
  IFreshnessEvaluator）+ `RuntimeAssurance(IFreshnessEvaluator)` ctor
  （null → ArgumentNullException）+ `AssuranceJudgment.Freshness` 字段
  ——不接执法检查
- 新增 FreshnessEnforcementTests N1-N7 + 既有构造面机械迁移
  （C2E/OUT/ING helper ×4 + AssuranceJudgment 直接构造点 ×7）
- 实测：**Kernel 失败 5 / 通过 48**（N1/N2/N3/N6/N7 失败 = freshness
  执法缺失；N4 fail-fast / N5 反射负向 = 结构项即过）；Agent 17 通过
  ——RED 剖面与 plan 预期完全一致，既有 63 零回归

## GREEN

- 最小实现：`RuntimeAssurance.Judge` 接入单条
  `freshness-sufficiency` 检查（Passed = Sufficient；位置：
  intent-basis-currentness 之后、no-blind-retry 之前）
- 实测：**失败 0 / 通过 70**（Kernel 53 = 既有 46 + N1-N7；Agent 17）

## 验收 1-8 逐条证明

| # | 验收 | 证明 |
|---|---|---|
| 1 | Rename 零残留 | 编译零错 + grep `\bFreshness\b` 类型名零命中（残留命中均为概念注释 "Freshness Basis/Judgment" 与 TerminalOutcomeTests:675 保护用例禁用字串，非类型名）；`FreshnessBasis.AsOf` 计算路径（WorldModel.Reconcile max-capture）零改动（git diff 核对） |
| 2 | 表达面 + 无全局真相 | N5 反射断言：WorldBeliefRevision/Slice/CanonicalBinding 无 Fresh/IsFresh/Stale 判定成员，唯一 FreshnessBasis 成员类型 = FreshnessBasis 输入聚合；判定状态仅存在于 AssuranceJudgment.Freshness |
| 3 | evaluator seam | N4：`new RuntimeAssurance(null!)` → ArgumentNullException（composition error fail-fast，非 runtime Unknown）；N3：ConsumptionRequirement 显式携带（effect class 键控双消费）；窄输入（FreshnessEvaluationInput 三成员，无全量 belief） |
| 4 | Judge 执法 + 三态不折叠 | N7：检查集 = CBA-005 九检查零改动 + 恰新增 `freshness-sufficiency`（顺序锁定 RejectionReason 确定性）；N1/N2：Insufficient 与 Unknown 都 IsAdmissible=false，FreshnessJudgment.Sufficiency 可区分 |
| 5 | validity 面零 freshness | IsSliceValid/IsBindingValid/Bind/Dispatch 行为零改动（git diff：仅注释）；N1：freshness 拒绝后 `IsBindingValid == true`（revision 仍 current ∧ 未消费前提显式断言，D7 条件化） |
| 6 | Scenario 14 锚点 | N1：current revision（rev-1 断言）+ Insufficient → judgment 拒绝 + Gate `not-authorized` + 零 receipt + 非 terminal + binding validity 仍成立；N2：Unknown 独立 fail-closed；N3：同 revision 双消费（tap 低要求 → Sufficient 放行 / swipe 高要求 → Insufficient 拒绝；attempt 回流不改 revision 的前提显式断言）——不偷解 Deferred ③ |
| 7 | 回归 GREEN | EvidenceToBeliefTests **零 diff**（E2B 8 断言零触碰，D2/ING-006 D1 冻结基线最强形式）；C2E/OUT/GEV 改动仅构造面（git diff：4 helper + 7 judgment ctor 参数），行为断言不变；全量 70/70 ≥ 63，无删除 |
| 8 | 词汇净化 + 台账 | ControlIntent/EffectBoundary/ControlToEffectTests:212 currentness 误称已改（grep 核对）；协议 P4/P10/P13 精化 + known gap 消除标注 + 场景表 2/14 更新 + §5 第二梯队完成注记 + 全局规则 9 ADR-0010 精化；CONTEXT.md（立项时落档）与 ADR-0010 存在且与代码命名一致 |

## Review / Verify 终段

- **REVIEW（fresh SubAgent，独立复核含独立 grep + 独立全量测试）**：
  **APPROVE**——A1-A6 全 PASS（单一执法点 / 三态不折叠 / Deferred
  不偷解 / validity 面零 freshness / E2B 零触碰 + 回归语义 / 意外改动
  与文档一致性）
- **F1（minor）已修**：plan 迁移台账改真——实际 5 处构造面适配
  （C2E/OUT NewKernel helper + ING 内联 ctor + GEV ctor + GEV 私有
  替身），另 7 处 AssuranceJudgment 直接构造点补 Freshness 参数
- **F2（nit）已处置**：无关未跟踪目录 `.tmp-hf-intake/` 不随本 change
  提交（提交使用显式路径清单）
- **F3（nit）已确认**：协议台账 diff 归属本 change（验收 8 / D9 台账
  同步，与 REVIEW 并行落盘，内容经审查逐段核读一致）
- **终验**：dotnet test 复跑 **70/70 GREEN**（Kernel 53 = 既有 46 +
  N1-N7；Agent 17）；验收 1-8 逐条全 GREEN
