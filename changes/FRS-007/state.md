# FRS-007 — Freshness Enforcement（P4 known gap + Pressure Scenario 14）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 9374f6ae

## Intent

**WHAT**: 落地协议基线 P4 的最小 freshness enforcement 语义：把
`revision currentness ≠ freshness` 变成运行时可执行事实——World Model
表达 `FreshnessBasis`（rename 自现 `Freshness(DateTimeOffset AsOf)`），
Assurance 在 action judgment 消费时经可注入 deterministic evaluator 做
`FreshnessJudgment`（Sufficient / Insufficient / Unknown），Insufficient
与 Unknown fail-closed；freshness 拒绝的是**授权**，不是 binding
validity。Pressure Scenario 14（revision current 但 freshness
insufficient → 拒绝）获得真实测试锚点。

**WHY**: 协议基线第二梯队项（P4 known gap）：当前实现全部 validity /
authorization 判定只依赖 revision currentness（5 处检查实测），
`Freshness.AsOf` 全仓零消费者；"freshness 不满足"在现有类型系统不可
构造，Scenario 14 无锚点。同时清除全仓 currentness/freshness 词汇混用。

## Scope

- `Freshness` record → `FreshnessBasis`（`WorldBeliefRevision` + `Slice`
  机械 rename，字段 `AsOf` 保留）
- 新增 Assurance 侧 freshness 语义类型：`IFreshnessEvaluator`、
  `FreshnessEvaluationInput`（FreshnessBasis + RevisionIdentity +
  ConsumptionRequirement）、`FreshnessJudgment`（三态 + reason）
- `RuntimeAssurance` ctor 注入 evaluator（null → composition error
  fail-fast）；`Judge` 增单一 `freshness-sufficiency` 检查；
  `AssuranceJudgment` 携带 FreshnessJudgment
- Pressure Scenario 14 用例集（Insufficient 主锚 / Unknown / 双消费
  对照 / fail-fast / 无全局 IsFresh 面）
- 全仓词汇净化（src 注释 + 测试措辞 + 协议台账）与 P4/P10/P13 状态
  标注精化

## Out of Scope（红线）

- freshness policy 算法 / 阈值 / 来源 owner（Deferred ④）；canonical
  clock（Deferred ⑪）
- per-screen / per-element freshness 策略；Session；Memory；F2；legacy
- P5 known leak / P11 potential overexposure 曝射面
- DispatchResult / EffectReceipt outcome 词汇（P15 / Deferred ⑤/⑬）
- Control 对 freshness 拒绝的反应接线（重观察编排 = Observation
  Control，deferred；不加通知 API）
- 被拒 binding re-judgment 许可（Deferred ③，不偷解）
- 产品基线文档改动（§12/§20.4 张力由 ADR-0010 显式 supersede 承接，
  基线不动）

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | Freshness 不是 WorldBelief/Slice/Binding 自身属性，无全局 IsFresh 真相；= Freshness Basis × Consumption Requirement 的消费相对判断（ADR-0010） | 立项 Q1 修正 |
| D2 | `Freshness(DateTimeOffset AsOf)` rename `FreshnessBasis`（不是 `LatestCaptureTime`——不绑定 max-CaptureTime 算法）。当前含 AsOf；未来可扩 source timestamps / observation context / volatility metadata 等 basis，但**不保存消费时计算出的 age**（age 需当前时刻，属消费侧派生） | 立项 Q3 修正 + 终轮修正 |
| D3 | FreshnessJudgment 三态 Sufficient / Insufficient / Unknown（判"是否足够支撑当前消费"，非世界对象绝对 Fresh/Stale）；Insufficient 与 Unknown 都 fail-closed 且词汇可区分不折叠；freshness 输入不足 → Unknown（runtime uncertainty）；evaluator 未配置 → composition/configuration error，fail-fast——两者不混 | 立项 Q2 修正 |
| D4 | 执法点单一：`RuntimeAssurance.Judge` 内 freshness sufficiency 检查；Bind / Gate / `IsSliceValid` / `IsBindingValid` 全部不加 freshness 维（IsSliceValid 零行为改动，仅修正注释）；Gate 靠既有 `not-authorized`；freshness 拒绝 = authorization denied ≠ binding invalidation（§20.4 旧字面由 ADR-0010 显式 supersede，不得按其实现） | 立项 Q5 + 终轮 Q9 修正 |
| D5 | evaluator seam：注入 `RuntimeAssurance` ctor（null fail-fast，镜像 IControlPolicy/IEffectDriver）；输入 `FreshnessEvaluationInput` = FreshnessBasis + RevisionIdentity + **ConsumptionRequirement**（当前 realization 用 target + effect 语义；协议不锁字段）；输出 first-class `FreshnessJudgment` 挂 `AssuranceJudgment` 字段 + Checks 单条 `freshness-sufficiency`（Passed = Sufficient）；`RejectionReason` 保持"首个失败 check 名"惯例（CBA-005 D3），三态区分在 FreshnessJudgment 自身 reason，不搞动态 check 名 | 立项 Q6 修正 |
| D6 | 同输入 deterministic evaluator 同结果；re-judgment 保持 Deferred ③ 不偷解；consumption-relative 的证明 = **同 revision 两种消费**（低 / 高 requirement → 不同 judgment），不用"同 binding 先拒后放" | 立项 Q7 修正 |
| D7 | Scenario 14 锚点：current revision + Insufficient → judgment 拒绝（三态词汇正确）、Gate 拒绝零 dispatch/receipt/回流、非 terminal、无 recovery/blind-retry 触发；**freshness 拒绝本身不使 binding invalid；在 revision 仍 current 且 binding 未消费的前提下 binding validity 仍成立**；Unknown 独立用例；D6 双消费对照用例 | 立项 Q7 + 终轮修正 |
| D8 | Control 零接线：无 freshness-rejection 通知 API；不进 `_pendingRecovery`、不触发 no-blind-retry、不产生 terminal；Observe kind 已在，重观察编排 deferred | 立项 Q8 |
| D9 | 词汇净化入 scope：`ControlIntent.cs`（BasisRevisionId 是 currentness 输入）、`EffectBoundary.cs`（revision 取代 ≠ freshness loss）、`ControlToEffectTests:212`、`WorldModel.cs`/`AssuranceJudgment.cs` doc、协议场景表第 2 行（intent freshness 判定 → currentness）；CBA-005 验收 9"不使用 freshness 措辞"约束由 ADR-0010 取代注记 | 立项 Q4 |
| D10 | 本 change 仅实现 action-local freshness enforcement；JudgeOutcome / EvaluateObligations 本 change 不引入 freshness；是否存在 outcome-level freshness buyer 未来另行裁决（不永久锁 action-local only；不变量 36 / TerminalOutcomeTests 禁用字保护面不动） | 终轮修正 |
| D11 | Route=Direct（单一语义切片：rename + evaluator seam + Judge 执法 + 场景锚高内聚，拆散会把表达与执法割裂）；depth=decision-heavy；TDD RED 先行 | 与 ING-006 D8 / CBA-005 D8 同构 |

## Acceptance（8 条）

1. **Rename 落地**：全仓无 `Freshness` 类型名残留（编译 + grep 双证，
   排除 docs 历史台账引用）；`FreshnessBasis.AsOf` 语义不变（max
   accepted evidence CaptureTime，Reconcile 计算路径零改动）
2. **表达面**：FreshnessBasis 由 World Model 唯一表达（revision +
   Slice）；belief/binding 对象图无 freshness 判定状态成员（反射负向
   断言：WorldBeliefRevision / Slice / CanonicalBinding 无 Fresh /
   IsFresh / Stale 状态成员；FreshnessBasis 输入聚合允许）；不保存
   消费时派生 age
3. **evaluator seam**：RuntimeAssurance ctor 注入；null → fail-fast；
   输入显式携带 ConsumptionRequirement（D5）；evaluator 不消费全量
   belief（窄输入）
4. **Judge 执法**：新增单条 `freshness-sufficiency` 检查（其余检查集
   与 CBA-005 D3 一致零改动）；AssuranceJudgment 携带
   FreshnessJudgment；Insufficient 与 Unknown 都 fail-closed 且
   reason 词汇可区分（不折叠）
5. **validity 面零 freshness**：IsSliceValid / IsBindingValid / Bind /
   Dispatch Gate 行为零改动；freshness 拒绝本身不使 binding
   invalid——revision 仍 current 且未消费时 validity 仍成立（D7 条件
   化措辞）
6. **Scenario 14 真实锚点**：current revision + Insufficient → 拒绝
   （judgment + Gate `not-authorized`）+ 零 dispatch/receipt/回流 +
   非 terminal + 无 recovery 触发；Unknown 独立 fail-closed 用例；
   **双消费对照**：同 revision 低 requirement → Sufficient 放行、高
   requirement → Insufficient 拒绝（D6，不偷解 ③）
7. **回归 GREEN**：E2B 8 断言语义零漂移（Freshness/AsOf 零测试引用，
   预期零断言触碰）；C2E/OUT/GEV 仅构造面机械适配（helper 注入
   evaluator），行为断言不变；全量 ≥63 GREEN，无静默删除
8. **词汇净化 + 台账**：D9 清单全落地；协议 P4（Minimal Payload /
   Validity / Status known gap 消除 + 精化）/ P10 / P13 状态标注；
   场景表 14 标注已锚定；CONTEXT.md 词条与 ADR-0010 已同步（立项时
   落档，VERIFY 核对存在性与一致性）

## Constraints

- ADR-0010 与协议基线 P4/P10/P13（精化后）为直接权威
- Target v0.1 不变量 8-27、32-34、42 不可违反；不变量 18 按本 change
  语义理解（WorldBelief 表达 freshness = 表达 FreshnessBasis）
- E2B 冻结基线 = 8 条断言语义零漂移（ING-006 D1 延续；本 change 预期
  零触碰，若触碰须显式解冻授权）
- Deferred ③/④/⑪/⑬ 不偷解；测试验证行为不验证实现细节

## Verification

```yaml
level: DETERMINISTIC   # 纯内存，无 IO / 真机 / 时钟依赖
method: >
  RED（目标类型/签名 stub + 新增用例先行：Scenario 14 主锚 / Unknown /
  双消费对照 / fail-fast / 反射负向断言）→ GREEN（最小实现：rename /
  evaluator seam / Judge 检查 / FreshnessJudgment 载体）→ REVIEW
  （fresh SubAgent）→ VERIFY（验收 8 条逐条 + 全量 dotnet test + grep
  残留 + 台账同步核对）
expected: >
  验收 8 条全 GREEN；全量测试 GREEN（总数 ≥63，迁移不删除用例）；
  grep -r "Freshness" 类型名零残留（排除 docs 台账）；E2B 断言零改动
actual: >
  2026-09-09 dotnet test（UniClaw.Kernel.slnx；SDK 10.0.400；net10.0）：
  RED 失败 5 / 通过 48（N1/N2/N3/N6/N7 = 执法缺失可见失败；N4/N5 结构项
  即过；既有 63 零回归）→ GREEN 失败 0 / 通过 70（Kernel 53 = 既有 46 +
  N1-N7；Agent 17）→ Review（fresh SubAgent 六轴 APPROVE，F1 台账修正 /
  F2 F3 nit 处置）→ 终验复跑 70/70。grep `Freshness` 类型名零残留；
  EvidenceToBeliefTests 零 diff（E2B 断言零触碰最强形式）；改动面 = plan
  After 表 + 3 新文件 + F1 修正后台账一致。验收 1-8 逐条证明映射见
  evidence
evidence: evidence/2026-09-09-frs-007-deterministic.md
```

## Assumptions

- 单线程内存模型（§22 开放项延续）
- 既有测试构造面适配限于实例化 RuntimeAssurance 的 helper（C2E/OUT；
  grep 实测 Freshness/AsOf 零测试引用、check 名零断言）
- Deterministic evaluator 在测试内构造（per-requirement 脚本），
  生产默认 evaluator 无 buyer、不提供

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| Expression-time（reconcile 冻结 Fresh/Stale 到 revision） | 不可变 revision 上 Fresh 永不降级，Scenario 14 不可构造；制造第二 belief 真相（ADR-0010） |
| 二值折叠（Sufficient/Insufficient） | Unknown（输入不足）与 Insufficient（判定不满足）不同源，折叠违反 unknown ≠ fresh 且伪装判定过 |
| 同 binding 先拒后放证明 consumption-relative | 违反同输入同结果；偷解 Deferred ③ |
| `LatestCaptureTime` 命名 | 绑定当前 max-CaptureTime 算法；basis 扩展即再改名（终轮 Q3） |
| 宽输入（intent/binding/revision 三引用直传 evaluator） | evaluator 变相消费全量 belief，扩大 P11 overexposure；无 buyer |
| Control freshness-rejection 通知 API | 无消费者（Control 拿到后做什么未决），speculative surface |
| evaluator 默认实例（不注入即全 Unknown / 全 Sufficient） | 前者使全部 act 永拒（Unknown fail-closed 连锁），后者把 unknown 折叠成 fresh；显式注入 + fail-fast 最诚实 |

## Owner / Authority Impact

- World Model：职责不变（belief + 表达），FreshnessBasis 是表达面 rename
- Assurance：**新增** action-local freshness sufficiency 判定职责
  （sole Freshness Judgment Authority；evaluator 是其判定缝，不构成
  平行 truth）
- Control / Effect Boundary：零职责变化（仅注释净化）
- 无 Owner 迁移、无第二 WorldBelief Authority

## ADR Refs

- docs/adr/0010-freshness-is-consumption-relative-sufficiency-judgment.md
  （本 change 裁决来源；§20.4 supersede 条款）
- docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md
  P4/P10/P13 + 场景 14 + Deferred ④/⑪
- CBA-005 D3/D5（检查集与 RejectionReason 惯例）、ING-006 D1（E2B
  语义冻结基线）

## Residual Risks

- evaluator 接口/类型命名与 FreshnessJudgment 在 AssuranceJudgment 的
  字段名属 realization，REVIEW 时可能微调（语义不变）
- 协议台账精化的具体字句在实现时定稿（语义边界已由 ADR-0010 锁定）
- outcome-level freshness buyer 出现时需回 RESOLVE（D10 未永久禁止）

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-09 | →resolved | ENTRY 核验（base 9374f6ae、63/63 基线实测、Freshness 零消费者、currentness 检查 5 处、E2B/保护面/冻结面实测）+ 立项 grill 三轮（11 问 + 7 修正）定稿 D1-D11 / acceptance 8；术语即时同步 CONTEXT.md（Freshness 拆分 + Freshness Basis / Freshness Judgment / Consumption Requirement 词条 + Slice / Assurance Judgment / Canonical Binding 修订）+ ADR-0010 即时落档（含 §20.4 supersede） |
| 2026-09-09 | resolved→planned | PLAN 落盘 plans/2026-09-09-frs-007-freshness-enforcement.md；Route: Direct（D11）；停在 implementation 前 |
| 2026-09-09 | planned→implemented | TDD RED（5 失败：N1/N2/N3/N6/N7 = 执法缺失；N4/N5 结构项即过；既有 63 零回归）→ GREEN（70/70：Kernel 53 = 既有 46 + N1-N7；Agent 17）；改动面 = plan After 表 + 3 新文件（Assurance/Freshness.cs / FreshnessDoubles.cs / FreshnessEnforcementTests.cs）；E2B 零 diff；词汇净化落地；协议台账同步（P4/P10/P13 + 场景 2/14 + 规则 9 + §5 注记） |
| 2026-09-09 | implemented→reviewed | fresh SubAgent 六轴 APPROVE（A1-A6 全 PASS，独立 grep + 独立全量测试复核）；F1 迁移台账与实际不符（minor）、F2 无关未跟踪目录不得随 change 提交（nit）、F3 协议台账并行落盘归属确认（nit） |
| 2026-09-09 | reviewed→verified | F1 修复（plan 迁移台账改真：5 处构造面 + 7 处 judgment ctor）；验收 1-8 逐条对照 evidence 全 GREEN；终验复跑 70/70；四元组 actual/evidence 完整 |
| 2026-09-09 | verified→closed | 范围完成 + acceptance 被证明 + 无未授权改动；A9 文档同步已在立项/实现期即时完成（CONTEXT 词条、ADR-0010、协议台账）；F2 遵守（提交显式路径清单排除 .tmp-hf-intake/）；无阻塞 Human Decision |
