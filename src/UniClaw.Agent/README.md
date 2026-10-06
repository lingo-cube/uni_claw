# UniAgent 代码索引（L1 · 系统的大脑）

UniAgent 是**整个系统的大脑**（cognition core）——所有者裁决 2026-10-07。
基线 §3.1 的「面向用户的完整智能主体」是表述偏差，待基线措辞修订收口
（见文末已知偏差）；本 README 以所有者裁决为准，L0 权威结构不变。

## Sole Owner（基线 §3.1/§6，权威不改述）

Primary Goal · Goal interpretation · global strategy · Execution Contract
authoring · Goal Evaluation。

四不（§3.1 Boundary）：不拥有 Current WorldBelief；不产生现实动作授权；
不写 Run State / Effect judgment / Outcome Proof；不以模型输出或执行回执
代替 goal satisfaction。

## 大脑的工作方式

- **主循环**：理解 Primary Goal → 形成全局策略 → 创建 Execution
  Contract → 消费 Runtime Outcome envelope → 评价 Goal Satisfaction；
- **自己定义的 tools**：tool 词汇归 UniAgent 所有，可调用；执行经
  realization（Host adapter）投影——DSH Tool 是 Binding/Exposure，不是
  能力类型（ADR-0038）；
- **realization 可替换**：当前用 DSH 实现（`../UniClaw.Agent.Dsh/`），
  可以在其他宿主用其他方式实现。宿主既有能力（模型、工具、记忆等）
  经 adapter **复用**映射进系统，而不必自研（ADR-0022：codex 与 DSH
  是 dual full UniAgent realizations 的先例）。替换 realization 只动
  adapter，不动本目录。

## 与 Kernel 的缝（单向 typed）

决策缝进（Kernel `Runtime/` 的 AgentDecision / ConsultationTypes）；
RuntimeOutcome envelope 出（Kernel `Outcome/`）。除此之外零依赖。

## 代码归属

| 文件 | 职责 |
|---|---|
| `UniAgent.cs` | L1 peer 本体（§3.1/§3.9；ADR-0008 独立程序集） |
| `Goal/PrimaryGoal.cs` | 用户希望现实世界达到的结果（§3.2；GEV-004 最小落地） |
| `Goal/GoalCriterion.cs` | 结构性 satisfaction 判据（GEV-004） |
| `Goal/GoalEvaluationContext.cs` | §3.9 第三输入（user supervisory input） |
| `Evaluation/GoalEvaluation.cs` | 消费 Runtime Outcome envelope 后的评估 |
| `Evaluation/GoalSatisfaction.cs` | satisfaction 维度（GEV-004 D2） |
| `Evaluation/CriterionResult.cs` | criterion 级三值判定 Met/Unmet/Indeterminate（D5） |
| `Evaluation/EvaluationDisposition.cs` | 处置维度（D2/D6） |

`../UniClaw.Agent.Dsh/` 是 DSH realization/adapter（profile/yaml/模型
解析），**不进本维度归属表**（L3 不混入 L2 索引，同 Capability↔
Perception 模式）。

## 记忆

UniAgent 的记忆由 Memory System 组件提供（L1 · §9）：管理常用规则与
常用知识。当前为声明占位（零落地），见
`docs/design/memory-system-component-declaration-v0.1.md`。

## 变更规则

goal/策略/contract/evaluation 语义与 tool 词汇进本目录；宿主特有实现
进对应 realization 目录；新增与 Kernel 的缝 = 架构级变更，先走 ADR。

## 已知偏差

- 基线 §3.1 定位措辞（「面向用户的完整智能主体」）与所有者裁决
  （「系统的大脑」）不一致——权威语义（owner/边界）不受影响；基线
  措辞修订待独立 change（L0-L3 重开条件见基线 §23）。

## 指向

- 基线 §3.1/§3.2/§3.9/§6/§9；ADR-0008（独立程序集）、ADR-0022（dual
  realizations）、ADR-0038（tool=exposure）；GEV-004 谱系。
