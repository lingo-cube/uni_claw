# Plan — GEV-004 Goal Evaluation Slice（UniAgent 首个监督面）

> PlanType: gev-004-goal-evaluation-slice / Status: ADOPTED / References:
> changes/GEV-004/state.md · docs/architecture/product-architecture-baseline-l0-l3.md
> （Target v0.1 §1/§3.1/§3.9/§6/§17/§18/§19/§20.4）· changes/OUT-003 ·
> docs/adr/0008-uniagent-as-separate-assembly.md

## 垂直切片：envelope → 语义身份 criteria → 判定格 → 评价记录

终局监督链（一条行为，UniAgent 单 Owner；Kernel 零改动）：

```text
immutable RuntimeOutcome（OUT-003 已发出的 envelope，本片首次被消费）
        ↓  UniAgent.Evaluate(outcome, context = Empty)   （sole Goal Evaluation Authority）
Goal Criterion 语义身份 → envelope 稳定 outcome semantics 解析
（id 缺席 = Unverifiable ≠ false；id 在而 Satisfied=false = Unmet）
        ↓  两档判定格（required / preferred）+ GEV-004 disposition policy
GoalEvaluation（EvaluationId + GoalSatisfaction × EvaluationDisposition
+ criterion results + rationale）
（纯确定性幂等；零存储；不回写任何 Runtime 事实）
```

## Before / After

### Before（OUT-003 收盘面）

- UniAgent 在产品代码中不存在；`TerminalEvaluation.Outcome` 发出后零消费者
- 无 Primary Goal / Goal Criterion / Goal Evaluation / Goal Satisfaction /
  Evaluation Disposition / Goal Evaluation Context 语义
- `src/` 只有 UniClaw.Kernel 一个产品 assembly；slnx 只注册 Kernel 及其测试
- L1 canonical structure 仅 Uni Kernel 一位成员落地

### After

| 文件 | 变化 | Owner |
|---|---|---|
| `src/UniClaw.Agent/UniClaw.Agent.csproj`（新） | Agent assembly；ProjectReference → UniClaw.Kernel（单向，ADR-0008） | UniAgent |
| `src/UniClaw.Agent/Goal/PrimaryGoal.cs`（新） | `PrimaryGoal(GoalId, Statement, Required, Preferred)`；required 空 → 构造拒绝 | UniAgent |
| `src/UniClaw.Agent/Goal/GoalCriterion.cs`（新） | abstract `GoalCriterion` + `ClassificationIs(TerminalClassification)` + `ObligationFulfilled(string)`（语义身份载荷） | UniAgent |
| `src/UniClaw.Agent/Goal/GoalEvaluationContext.cs`（新） | opaque 契约；仅 `static Empty`；零成员 | UniAgent |
| `src/UniClaw.Agent/Evaluation/GoalEvaluation.cs`（新） | immutable 记录：EvaluationId / GoalId / RunId / OutcomeProofId / Satisfaction / Disposition / CriterionResults / Rationale | UniAgent |
| `src/UniClaw.Agent/Evaluation/GoalSatisfaction.cs`（新） | enum：Satisfied / PartiallySatisfied / Unsatisfied / Undetermined（§22 开放项） | UniAgent |
| `src/UniClaw.Agent/Evaluation/EvaluationDisposition.cs`（新） | enum：Final / NeedsFollowUp；结构约束 Undetermined ⇒ NeedsFollowUp | UniAgent |
| `src/UniClaw.Agent/Evaluation/CriterionResult.cs`（新） | `Met / Unmet / Unverifiable` × criterion | UniAgent |
| `src/UniClaw.Agent/UniAgent.cs`（新） | ctor 注入 goal（1:1，无变更面）；纯确定性 `Evaluate(outcome, context)`；零存储；`EvaluationId = f(GoalId, RunId, OutcomeProofId)` | UniAgent |
| `tests/UniClaw.Agent.Tests/GoalEvaluationTests.cs`（新） | 验收 12 + 反例 A-J 的确定性用例（~16） | —— |
| `UniClaw.Kernel.slnx` | 注册 Agent 与 Agent.Tests 两个项目 | —— |
| `src/UniClaw.Kernel/**` | **零改动**（byte-level diff empty） | —— |

## 关键语义（固化，防漂移）

1. **criterion 语义身份**：只携带 TerminalClassification 词汇与 obligation id
   string；`GoalCriterion` 对象图不得出现 `RuntimeOutcome` /
   `ObligationStatus`；解析 envelope 是 evaluator 的私事（grill Q5/Q9）。
2. **两档判定格**：任一 criterion 不可验证 → `(Undetermined, NeedsFollowUp)`；
   否则 Required 全满足 + Preferred 全满足 → Satisfied；Required 全满足 +
   ≥1 Preferred 未满足 → PartiallySatisfied；任一 Required 未满足 →
   Unsatisfied（required 支配）。preferred 可空（纯二值 goal 合法）；
   required 空 → 构造拒绝（grill Q10/Q11）。
3. **criterion 边界线（Q7）**：obligation id 在 envelope 存在但
   `Satisfied=false` → Unmet；id 缺席 → Unverifiable（≠ false）。
4. **disposition**：长期不变量仅 `Undetermined ⇒ NeedsFollowUp`；
   `Satisfied→Final / PartiallySatisfied→NeedsFollowUp / Unsatisfied→Final`
   是 GEV-004 Empty-context policy，不写成 exhaustive type constraint
   （grill Q12 修正）。
5. **反镜像**：无硬编码 classification→satisfaction 映射；无 classification
   criterion 时不得因 classification 评出 Satisfied。`Failure` outcome +
   obligation 级 criteria 全满足 → Satisfied、`Completion` + preferred 缺 →
   PartiallySatisfied 双例均须有用例（§3.1 boundary：不以执行回执代替
   Goal satisfaction）。
6. **消费面（反例 D 语义）**：Evaluator 只消费 RuntimeOutcome 暴露的稳定
   outcome semantics / refs；不得 dereference raw runtime evidence /
   EffectReceipt / WorldBelief 形成第二套 Runtime judgment。不是不能看到
   reference，而是不得越过 Outcome 回头重审 Runtime。
7. **Evaluate 纯确定性幂等**：同输入（goal + outcome + Empty context）→
   同 EvaluationId、同记录；零存储，不购买 evaluation history（grill Q14
   修正 / Q19）。
8. **不回写由结构证明**：Agent→Kernel 单向引用 + Kernel byte-level 零改动 +
   Evaluate 输入只有 Goal + Outcome + Context；不以测试摸内部对象的方式
   证明（grill Q17-③ 修正）。

## TDD 次序（RED → GREEN → REVIEW → VERIFY）

1. RED：`GoalEvaluationTests.cs` 先行（验收 12 + 反例 A-J），行为方法桩
   （NotImplementedException），运行可见关键监督场景失败
2. GREEN：按 After 表实现最小代码（新 assembly + slnx 注册；Kernel 不动）
3. REVIEW：fresh SubAgent 六轴（第二 Goal Evaluation Authority / criterion
   吃 envelope 结构 / 评价回写 Runtime / 镜像映射 / 幂等破坏 / Kernel
   反向引用）
4. VERIFY：全量 `dotnet test`（E2B 8 + C2E 10 + OUT 18 + GEV ~16 全 GREEN）；
   no-drift（既有 36 用例零改动 + `src/UniClaw.Kernel` byte-level diff
   empty）；evidence 落盘；CONTEXT.md 术语已收编（grill 期间先行完成）

## 验收 ↔ 反例 ↔ 用例映射（草案）

| 验收 | 覆盖 | 用例（GoalEvaluationTests） |
|---|---|---|
| ① UniAgent 独占 Goal Evaluation 产出 | —— | GoalEval9 |
| ② 评价输入仅 Goal + Outcome + Context | D | GoalEval11 |
| ③ 对 RuntimeOutcome 只读；无写入路径 | F | GoalEval15 |
| ④ Kernel byte-level 零改动 + 单向引用 | G | GoalEval16 + VERIFY diff |
| ⑤ criterion 对象图无 envelope 结构类型 | A | GoalEval10 |
| ⑥ required 空 → 构造拒绝 | B | GoalEval12 |
| ⑦ id 缺席 = Unverifiable ≠ Unmet | C | GoalEval4（对照 GoalEval3） |
| ⑧ 判定格四档穷举 | —— | GoalEval1/2/3/4 |
| ⑨ 反镜像双例（Failure+Satisfied / Completion+Partially） | E | GoalEval5/6 |
| ⑩ policy 四组合各一用例；结构只锁 Undetermined⇒NeedsFollowUp | I | GoalEval1/2/3/4 |
| ⑪ 幂等：同输入同 EvaluationId 同记录 | H | GoalEval13 |
| ⑫ 无 envelope 无评价（fail-closed） | J | GoalEval14 |
| 附加：SafeStop / Escalation 可评价 | —— | GoalEval7/8 |

## 停止条件（本 Plan 完成后不自动启动）

Memory slice / F2 架构重构 / legacy cutover / Product Session / Goal
revision / Contract authoring 面 / follow-up 机制 / multi-run
continuation —— 一律不做，只输出推荐下一 change。Q1 优先序
（Goal Evaluation > Memory > F2 hardening > legacy cutover）记为当前
偏好，不作为对未来的承诺；收盘推荐届时按实际局面给出。
