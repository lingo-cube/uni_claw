# Plan — FRS-007 Freshness Enforcement

> PlanType: frs-007-freshness-enforcement / Status: ADOPTED /
> References: changes/FRS-007/state.md · ADR-0010 · 协议基线 P4/P10/P13 ·
> commit 9374f6ae

## 垂直切片：表达与判定分离，单一执法点

```text
World Model（表达，rename）
  FreshnessBasis(AsOf)          // revision + Slice 机械 rename；无判定
        ↓
Assurance（消费时判定，新增缝）
  FreshnessEvaluationInput
  = FreshnessBasis + RevisionIdentity + ConsumptionRequirement
        ↓  IFreshnessEvaluator（ctor 注入；null fail-fast）
  FreshnessJudgment：Sufficient / Insufficient / Unknown
        ↓
  Judge checks + 'freshness-sufficiency'（Passed = Sufficient）
  AssuranceJudgment.Freshness（first-class 载体）
        ↓
Gate：既有 not-authorized 执法（零新词汇）
```

正交不变量：freshness 拒绝 = authorization denied ≠ binding
invalidation；validity 面（currency / consumption）零 freshness。

## Before / After

### Before

- `Freshness(DateTimeOffset AsOf)`：名字占了协议维度，实质是
  provenance 聚合；全仓零消费者
- validity / authorization 全靠 revision currentness（5 处检查）；
  "freshness 不满足"不可构造（Scenario 14 无锚）
- 注释词汇混用：BasisRevisionId 被称"freshness 判定输入"、revision
  取代被称"freshness loss"、协议场景 2 "intent freshness 判定"

### After

| 文件 | 变化 | Owner |
|---|---|---|
| `World/WorldBeliefRevision.cs` | `Freshness` record → `FreshnessBasis(DateTimeOffset AsOf)`；doc：World Model 表达非裁决；不保存消费时派生 age | World Model |
| `World/Slice.cs` | 字段类型 rename；注释诚实化：有效性 = revision currency 派生（World Model 侧），freshness 充分性属消费侧 | World Model |
| `World/WorldModel.cs` | `new FreshnessBasis(...)` ×2（Reconcile / DeriveSlice）；`IsSliceValid` 零行为改动（注释修正） | World Model |
| `Assurance/Freshness.cs`（新） | `ConsumptionRequirement`（当前 TargetSubject + EffectClass；注释：字段不锁）· `FreshnessEvaluationInput`（Basis + RevisionIdentity + Requirement）· `IFreshnessEvaluator` · `FreshnessSufficiency { Sufficient, Insufficient, Unknown }` · `FreshnessJudgment(Sufficiency, Reason)` | Assurance |
| `Assurance/RuntimeAssurance.cs` | ctor + `IFreshnessEvaluator`（null → throw，composition error fail-fast）；`Judge` 构造 input → evaluator → checks 增 `freshness-sufficiency`（位置：currentness 两查之后、no-blind-retry 之前；首失败项惯例不变） | Assurance |
| `Assurance/AssuranceJudgment.cs` | + `FreshnessJudgment Freshness` 字段；doc 去除"P4 deferred"注记（语义已落地） | Assurance |
| `Control/ControlIntent.cs` | 注释：BasisRevisionId 是 currentness 判定输入 | Control |
| `Effects/EffectBoundary.cs` | 注释：去 "freshness loss" 误称（→ 被新 revision 取代） | Effect Boundary |
| tests | C2E/OUT helper：RuntimeAssurance 注入默认 Satisfying evaluator（happy path 机械适配）；`ControlToEffectTests:212` 措辞净化；新增 FreshnessEnforcementTests（下表） | —— |

## 关键语义（固化，防漂移）

1. **单一执法点**：freshness 只在 Judge 内判定与执法；Bind/Gate/
   IsSliceValid/IsBindingValid 不出现 freshness（防止实现时手痒往
   validity API 塞）。
2. **三态不折叠**：Unknown（输入不足）与 Insufficient（判定不满足）
   在 FreshnessJudgment.Reason 词汇可区分；动态 check 名被拒
   （CBA-005 D5 惯例保持）。
3. **不偷解 Deferred**：无阈值 / clock / 算法（④/⑪）；无 re-judgment
   语义（③）；evaluator 是测试内构造的 deterministic 脚本，无生产
   默认实现。
4. **D7 条件化**：binding validity 断言必须带"revision 仍 current 且
   未消费"前提；核心句 = authorization denied ≠ binding invalidation。
5. **E2B 零触碰预期**：Freshness/AsOf 零测试引用（ENTRY grep 实测）；
   若实现中发现触碰 → 回 Leader 显式解冻，不得静默改断言。

## TDD 次序（RED → GREEN → REVIEW → VERIFY）

1. RED（FreshnessEnforcementTests 先行，目标类型 stub）：
   - N1 Scenario 14 主锚：current revision + evaluator→Insufficient →
     judgment 拒绝（`freshness-sufficiency` 失败 + 三态正确）+ Gate
     `not-authorized` + 零 receipt/回流 + 非 terminal + binding
     validity 仍成立（current ∧ 未消费）
   - N2 Unknown fail-closed：拒绝且 reason 词汇与 N1 可区分
   - N3 双消费对照（D6）：同 revision，低 requirement 消费 → Sufficient
     放行 dispatch；高 requirement 消费 → Insufficient 拒绝（AttemptReport
     回流不改 revision，双消费构造合法）
   - N4 fail-fast：ctor null evaluator → throw（composition error）
   - N5 反射负向：WorldBeliefRevision/Slice/CanonicalBinding 无
     Fresh/IsFresh/Stale 状态成员（FreshnessBasis 允许）
   - N6 freshness 拒绝后无 recovery 强制：下一 SelectIntent 非强制
     Recovery intent（pendingRecovery 未触发）
   - N7 检查集核对：CBA-005 9 检查名零改动 + 恰新增
     `freshness-sufficiency`
2. GREEN：按 After 表最小实现 + 机械迁移（rename / helper 注入）
3. REVIEW：fresh SubAgent（轴：单一执法点 / 三态不折叠 / Deferred 不
   偷解 / validity 面零 freshness / E2B 零触碰 / 词汇净化完整 / 意外
   改动）
4. VERIFY：全量 dotnet test（≥63）；grep 类型名 `Freshness` 零残留
   （排除 docs 台账）；D9 词汇清单核对；协议 P4/P10/P13 + 场景表
   2/14 标注；CONTEXT/ADR 一致性核对；evidence 落盘

## 验收 ↔ 用例映射

| 验收 | 用例 |
|---|---|
| 1 rename 零残留 | grep + 编译双证（VERIFY） |
| 2 表达面 + 无全局真相 | N5；Reconcile 路径零改动（既有 E2B GREEN 即证） |
| 3 evaluator seam | N4；N3（requirement 显式携带） |
| 4 Judge 执法 + 三态 | N1/N2/N7 |
| 5 validity 面零 freshness | N1（binding validity 断言）；既有 C2E Accepted3/4 迁移后 GREEN |
| 6 Scenario 14 锚点 | N1/N2/N3 |
| 7 回归 | E2B 8 零改动；C2E/OUT/GEV helper 适配后 GREEN |
| 8 词汇净化 + 台账 | VERIFY 文档核对 |

## 迁移台账（草稿，VERIFY 时核对）

```text
rename：src 3 文件（WorldBeliefRevision / Slice / WorldModel）+ 测试零触碰（grep 实测）
迁移（Review F1 修正：实际 5 处构造面，非草稿的 2 处）：
  C2E/OUT NewKernel helper（ControlToEffectTests:72 / TerminalOutcomeTests:95）
  + ING 内联 ctor（ObservationIngressTests:167）
  + GEV ctor（GoalEvaluationTests:438）+ GEV 私有替身 SatisfyingFreshness
  另 7 处 AssuranceJudgment 直接构造点补 Freshness 参数（C2E×5 / TOT×2）
新增：FreshnessEnforcementTests N1-N7
删除：无（Freshness 类型 rename 非删除）
零改动：E2B 8 断言（EvidenceToBeliefTests 零 diff）、JudgeOutcome/
EvaluateObligations、Gate/Bind/validity API 行为、GEV 行为断言
```
