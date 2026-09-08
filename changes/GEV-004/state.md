# GEV-004 — Goal Evaluation Slice（UniAgent 首个监督面）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 0fa12742

## Intent

**WHAT**: 让 UniAgent 作为 L1 peer 首次落地，成为 Runtime Outcome 的第一个
消费者：新建 `src/UniClaw.Agent` assembly（ADR-0008，依赖单向
Agent→Kernel），以 Primary Goal（携带语义身份 satisfaction criteria）消费
immutable RuntimeOutcome envelope，产出纯确定性幂等的 Goal Evaluation 记录
（GoalSatisfaction × EvaluationDisposition 正交两维）。不回写任何 Runtime
事实；`src/UniClaw.Kernel` byte-level 零改动。

**WHY**: Target v0.1（§1/§3.1/§3.9/§6/§17/§18/§19/§20.4）的监督弧在
OUT-003 收盘后断在 Kernel 出口——envelope 已发出而零消费者，「最终评价
Primary Goal satisfaction」（§1 第四能力、不变量 40）是唯一未闭合的 L0
环。本切片证明不变量 40/41 的 UniAgent 侧，并为 Memory slice 提供第一批
真实语料（Goal / Evaluation 记录）。

## Scope

- UniAgent（L1 peer 首次实现）：sole Goal Evaluation Authority；ctor 注入
  PrimaryGoal（1:1，无变更面）；纯确定性 `Evaluate(outcome, context)`；
  零存储
- Primary Goal 最小模型：GoalId / Statement / Required / Preferred；
  构造期 vacuous guard（required 空拒绝，preferred 可空）
- Goal Criterion 语义身份：`ClassificationIs(TerminalClassification)` /
  `ObligationFulfilled(obligation id)`；不耦合 envelope 字段布局或
  Kernel object graph
- Goal Evaluation 记录：EvaluationId（由 GoalId + RunId + OutcomeProofId
  确定性派生）+ satisfaction × disposition + 逐 criterion 结果 +
  rationale（机械拼装）
- Goal Evaluation Context：opaque 契约（仅 Empty），保住 §3.9 三元输入的
  canonical API 语义
- 两档判定格 + GEV-004 disposition policy（长期只锁
  Undetermined ⇒ NeedsFollowUp）

## Out of Scope

- Product Session correlation root —— 等 Session / multi-goal buyer
- Goal revision（显式澄清链）—— 等澄清需求出现
- user/supervisory context 业务语义 —— `GoalEvaluationContext` 保持
  opaque；等真实 context buyer
- Execution Contract authoring 面 —— contract 仍由测试脚本构造；等
  authoring buyer
- follow-up 机制 / multi-run / continuation —— 等显式新 change
- Memory System、F2 架构重构、legacy cutover —— 维持挂起（Q1 优先序为
  当前偏好，非承诺）

## Decisions

| # | 决策 | 来源 |
|---|---|---|
| D1 | UniAgent 落独立 assembly（`src/UniClaw.Agent` + Agent.Tests；ProjectReference 单向 Agent→Kernel）——「Kernel 永不引用 UniAgent 类型」从纪律升级为 build 层强制 | grill Q4；ADR-0008 |
| D2 | satisfaction × disposition 正交拆分：`GoalSatisfaction { Satisfied, PartiallySatisfied, Unsatisfied, Undetermined }` + `EvaluationDisposition { Final, NeedsFollowUp }`。baseline §3.9 字面四标签中的 Needs Follow-up 拆出为处置维度、satisfaction 增补 Undetermined——偏离字面列表，enum 名属 §22 L4 开放项（D3 先例） | grill Q6 修正 |
| D3 | criteria-as-data + 语义身份载荷：criterion 只携带 TerminalClassification 词汇 / obligation id string；`GoalCriterion` 对象图不得出现 `RuntimeOutcome` / `ObligationStatus`；复用 Kernel 公开 enum 是词汇复用而非结构耦合 | grill Q5 修正 / Q9 |
| D4 | 两档判定格（required / preferred；required 支配）；required 空 → `PrimaryGoal` 构造拒绝（authoring 点失败优于评价点降级）；preferred 空合法 | grill Q10 / Q11 |
| D5 | criterion 边界：obligation id 缺席 = Unverifiable（≠ false）→ `(Undetermined, NeedsFollowUp)`；id 在而 `Satisfied=false` = Unmet | grill Q7 修正 |
| D6 | disposition 长期不变量仅 `Undetermined ⇒ NeedsFollowUp`；`Satisfied→Final / PartiallySatisfied→NeedsFollowUp / Unsatisfied→Final` 是 GEV-004 Empty-context policy，非 exhaustive type constraint；记录类型允许受约束组合积，未来 context / follow-up slice 可产出 `PartiallySatisfied+Final`、`Unsatisfied+NeedsFollowUp` 而词汇不变 | grill Q12 修正 |
| D7 | `GoalEvaluationContext` opaque 入契约（仅 Empty、零成员）：context 进 API 签名但不扩能力——比删除 §3.9 第三输入（NO_REAL_BUYER）更稳，未来加 context 不改 canonical API 语义 | grill Q3 修正 |
| D8 | `Evaluate` 纯确定性幂等、零存储、不购买 evaluation history；`EvaluationId = f(GoalId, RunId, OutcomeProofId)`（无钟 / 无计数器 / 无 GUID）；同输入重评 = 同 id 同记录 | grill Q14 修正 / Q19 |
| D9 | 不回写由结构证明：单向引用 + Kernel byte-level 零改动 + 输入签名只有 Goal + Outcome + Context；不以测试摸 RunState / WorldBelief 内部对象的方式证明 | grill Q17-③ 修正 |
| D10 | Kernel byte-level 零改动——UniAgent 是纯消费者的最硬结构证据：Agent 只读 Kernel 公开词汇，Kernel 对 Agent 存在零感知 | grill Q15 |

## Acceptance（12 条）

1. **UniAgent 独占 Goal Evaluation 产出**：Agent 程序集唯一产出
   GoalEvaluation 的路径 = `UniAgent.Evaluate`；Kernel / Run Model /
   Assurance 无第二产出点（§3.9；不变量 40）
2. **评价输入仅 Goal + Outcome + Context**：对象图无 Evidence /
   WorldBelief / Run State 类型
3. **对 RuntimeOutcome 只读**：Agent 无写入 RunState / WorldBelief /
   OutcomeProof 的类型或调用路径
4. **Kernel 零改动 + 单向引用**：`src/UniClaw.Kernel` byte-level diff
   empty；项目引用仅 Agent → Kernel
5. **criterion 语义身份边界**：`GoalCriterion` 对象图不含
   RuntimeOutcome / ObligationStatus 结构类型
6. **vacuous guard**：required 为空的 `PrimaryGoal` 构造拒绝
7. **criterion 边界线**：obligation id 缺席 → Unverifiable →
   `(Undetermined, NeedsFollowUp)`；id 存在但 `Satisfied=false` → Unmet
   （不得把不可验证伪装成不满足）
8. **判定格四档穷举**：Satisfied / PartiallySatisfied / Unsatisfied /
   Undetermined 各有确定性用例（两档制）
9. **反镜像双例**：`Failure` outcome + obligation 级 criteria 全满足 →
   Satisfied；`Completion` + preferred 缺 → PartiallySatisfied（无
   classification→satisfaction 硬编码映射）
10. **disposition**：当前 GEV-004 policy 的四种输出组合各有用例；长期
    结构约束只强制 `Undetermined ⇒ NeedsFollowUp`
11. **幂等**：同输入（goal + outcome + Empty context）重复 Evaluate →
    同 EvaluationId、等值记录，不产生重复事实
12. **无 envelope 无评价**：fail-closed，不发明中途 Goal Evaluation

## Verification

```yaml
level: DETERMINISTIC   # 纯内存；无真机 / 真用户 / 真模型依赖
method: >
  RED（桩 + 测试先行，关键监督场景可见失败）→ GREEN（仅本 slice 最小实现，
  新 assembly + slnx 注册）→ REVIEW（fresh SubAgent，六轴：第二 Goal
  Evaluation Authority / criterion 吃 envelope 结构 / 评价回写 Runtime /
  镜像映射 / 幂等破坏 / Kernel 反向引用）→ VERIFY（验收 12 条 + 反例 A-J
  逐项 + E2B/C2E/OUT 回归零改动 + Kernel byte-level no-drift + 验收↔反例↔
  用例映射）
expected: >
  验收 12 条全 GREEN；GEV ~16 用例全 GREEN；E2B 8 + C2E 10 + OUT 18
  零改动全保持 GREEN；src/UniClaw.Kernel byte-level diff empty；
  幂等与反镜像双例有确定性证据；四档 satisfaction 与四组合 disposition
  各有代表性用例
actual: >
  2026-09-06 dotnet test（UniClaw.Kernel.slnx；SDK 10.0.400；net10.0）：
  失败 0 / 通过 53——GEV-004 17 用例全 GREEN，E2B 8 + C2E 10 + OUT 18
  零改动全保持。RED 阶段（桩 + NotImplementedException）Agent.Tests 失败
  13 / 通过 4（通过项为结构断言；GoalEval10 首跑暴露测试自身 walk 缺陷，
  修复后 RED 定格）；GREEN 后失败 1（GoalEval13 record 集合属性按引用
  比较——测试断言改为逐字段 + 序列比较，非语义缺陷）；REVIEW 修复后
  失败 0 / 通过 53。REVIEW（fresh SubAgent 六轴）APPROVE；F1（防御性
  拷贝防别名突变击穿幂等）已修，F2/F3 nit 已修。no-drift：git diff
  src/UniClaw.Kernel EMPTY、既有测试文件 EMPTY。幂等（GoalEval13）、
  反镜像双例（GoalEval5/6）、fail-closed（GoalEval14）、真实 emission
  消费（GoalEval17）均有确定性证据。
evidence: evidence/2026-09-06-gev-004-deterministic.md
```

## Constraints

- Target v0.1 §1/§3.1/§3.9/§6/§17/§18/§19/§20.4 为本切片直接权威；
  不变量 40/41 不可违反
- E2B-001 / C2E-002 / OUT-003 已定型语义零改动；`src/UniClaw.Kernel`
  零文件改动
- Evaluator 只消费 RuntimeOutcome 暴露的稳定 outcome semantics / refs；
  不得 dereference raw runtime evidence / EffectReceipt / WorldBelief
  形成第二套 Runtime judgment（不是不能看到 reference，而是不得越过
  Outcome 回头重审 Runtime）
- 禁止 classification→satisfaction 硬编码映射；不以执行回执代替 Goal
  satisfaction（§3.1 boundary）
- 测试验证行为，不验证实现细节

## Assumptions

- GEV-004 暂不建立 Goal ↔ Run correlation validation。当前 1 Session /
  1 Goal / 1 Run 范围内，RuntimeOutcome 与 PrimaryGoal 的关联由
  composition 保证。这是 slice assumption，不是永久语义——Session /
  Contract authoring / multi-run / correlation buyer 出现时必须重开
- obligation id 唯一性与稳定性由 contract well-formedness 保证
  （OUT-003 F4 admission 检查延续）
- EvaluationId 派生输入在未来 goal revision / 非 Empty context 下需扩展，
  届时另定（确定性派生原则不变）

## Alternatives Considered

| 备选 | 被拒原因 |
|---|---|
| Kernel 内 `UniClaw.Kernel.Agent` namespace | 合法且省事，但反向依赖只剩纪律约束；ADR-0008 |
| NeedsFollowUp 作 GoalSatisfaction 成员 | 语义混层（处置 ≠ 程度）；无法表达 Unsatisfied+Final、PartiallySatisfied+NeedsFollowUp |
| `Evaluate(goal, outcome)` 双参签名 | 破坏 §3.9 三元输入 canonical API 语义，未来加 context 即 breaking；改 opaque context 入契约 |
| 空 required 留到评价时降级 | authoring 错误应在构造点失败，不在判断点降级 |
| append-only evaluation history（agent 持列表） | goal / outcome / context / evaluator 全确定，重复评价会制造重复事实；本 slice 不购买 history，Session 落地时再议归属 |
| criterion 用 predicate delegate / DSL | 不可构造期校验、不可机械拼 rationale；DSL 是 YAGNI |
| 不回写靠测试摸 RunState / WorldBelief | 它们根本不是 Evaluate 输入；拉进测试反而扩大 slice；边界由结构保证 |

## Owner / Authority Impact

- UniAgent：新增（L1 peer 首次落地）——Primary Goal 与 Goal Evaluation
  Authority（§3.1/§3.9；不变量 40）
- Uni Kernel 六 L2：零改动、零感知（envelope 出口早已满足消费需要）
- Memory：不在本 slice；GEV-004 为其备好第一批真实语料

## ADR Refs

- docs/adr/0008-uniagent-as-separate-assembly.md（assembly 分离与单向依赖）
- 产品架构基线：docs/architecture/product-architecture-baseline-l0-l3.md
  （§1/§3.1/§3.9/§6/§17/§18/§19/§20.4 直接权威；原 docs/analysis/ 路径
  由 ARCH-DOC-013 relocation 收口）
- 既有：changes/E2B-001 · changes/C2E-002 · changes/OUT-003

## Residual Risks

- disposition 派生规则是 Empty-context policy；context 语义到位后需重审
  （词汇已稳定，重审只动 policy）
- EvaluationId 派生输入在 goal revision / 非 Empty context 下需扩展
- Goal ↔ Run correlation assumption 需在 Session / multi-run buyer
  出现时重开

## Status Log

| 日期 | from→to | 依据 |
|---|---|---|
| 2026-09-06 | →planned | Pre-UniFlow grill（grill-with-docs）三轮达成共享理解；CONTEXT.md 术语六条与 ADR-0008 已先行落盘；plan = plans/2026-09-06-gev-004-goal-evaluation-slice.md |
| 2026-09-06 | planned→implemented | ENTRY 复验（HEAD=base 0fa12742、基线 36/36 GREEN、工作树仅 grill 产物）→ TDD RED（Agent.Tests 失败 13 / 通过 4，关键监督场景可见失败）→ GREEN（最小实现仅限新 assembly：PrimaryGoal 校验 / GoalEvaluation 结构不变量 / UniAgent.Evaluate 判定格；Kernel 不动） |
| 2026-09-06 | implemented→reviewed | fresh SubAgent 六轴 APPROVE（轴1-6 全 PASS）；F1（IReadOnlyList 别名突变可击穿幂等）已修（防御性拷贝），F2/F3 nit 已修 |
| 2026-09-06 | reviewed→verified | 验收 12 条逐条对照 evidence 全 GREEN；反例 A-J 逐项 GREEN；E2B/C2E/OUT 36 用例零改动；git diff src/UniClaw.Kernel EMPTY；四元组映射落 evidence |
| 2026-09-06 | verified→closed | 范围完成 + acceptance 被证明；无第二 Goal Evaluation Authority / 无 Kernel 反向引用 / 无 classification→satisfaction 镜像；working tree 仅预期新增（Agent 产物 + grill 文档 + .tmp-hf-intake/ 已知无关项） |
