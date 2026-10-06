# Assurance 代码索引（L2 · 基线 §15）

Sole **Runtime Assurance Judgment Authority（含 Outcome Proof
Authority）**：Action Admissibility、Preconditions/Freshness、Safety/
Contract Guard、Effect Verification、Outcome Proof。不拥有 Control
Intent、Run State、target binding 或 dispatch（权威表见基线 §10）。

## 代码归属

| 文件 | 职责 |
|---|---|
| `AssuranceJudgment.cs` | 判定的 typed 形状（四语义门产物，ADR-0004） |
| `Freshness.cs` / `ProductFreshnessEvaluator.cs` | freshness = 消费相对充分性判断（ADR-0010）；FRS-008 首个产品实现 |
| `OutcomeProof.cs` | outcome 证明（本维度独有 judgment 权威） |
| `RuntimeAssurance.cs` | runtime assurance 权威本体（Target §15） |
| `PostActionEffectVerification.cs` | 一次现实 Effect 的 post-action verification judgment（AGT-005，Host 消费面） |

## 变更规则

judgment 语义（可容许性/充分性/安全/证明）进本目录；被判断的事实来自
Evidence/World/Run，effect 的执行与回执归 Effect Boundary；judge 的
canonical 绑定遵循 ADR-0009，非幂等动作安全的 pre-dispatch 定位遵循
ADR-0017。

## 指向

- 基线 §15；四语义门：ADR-0004；ADR-0009/0010/0017；
- 输入：`../Evidence/`、`../World/`、`../Run/`；约束：`../Effects/`。
