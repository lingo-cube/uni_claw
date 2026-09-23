# Simulation Architecture Baseline v0.2 — C7 Amendment

> DocumentType: `SIMULATION_ARCHITECTURE_BASELINE_V0_2_C7_AMENDMENT`
>
> Status: `CLOSED`（SIM-002 G4 落地即生效）
>
> Authority: `COMPONENT`（同 v0.1；冲突时以产品基线为准）
>
> Date: 2026-09-23 · 谱系：simulation-baseline-v0.1 → 本修订
> 授权：SIM-002 核心裁决（用户 2026-09-22：G4 = C7 v0.2 能准确描述
> 当前 hybrid Agent realization）——满足 v0.1 §7「C1–C9 变更需 Human
> Gate」，仅 C7 走 v0.2，其余条款一字不动。

---

## 修订范围

本文件是 **delta 修订**：v0.1 全文继续有效，唯 C7 由下文取代。目的、
约束 C1–C6 / C8–C9、阶段、非目标、修订规则均见
`docs/architecture/simulation-baseline-v0.1.md`。

## C7（v0.2）Agent 侧装配

```text
C7  Agent 侧装配（v0.2 拆分标注）：
    Agent 侧有两个独立 realization 面，场景必须分别标注：
    a) agentDecisionRealization —— 决策面（ConsultAgent seam 的实现）：
       仿真中可为 deterministic double 或真件；
    b) goalEvaluationRealization —— 评估面（PrimaryGoal → GoalEvaluation）：
       仿真中可为真件或 double。

    断言分层（R2 语义保持，按面细化）：
    - 对 double 输出的断言是 double 自检（sanity check），
      不是产品行为回归断言；
    - 产品决策语义的回归只能由 agentDecisionRealization=真件 的
      场景承载；
    - 产品 Goal Evaluation 语义的回归只能由 goalEvaluationRealization
      =真件 的场景承载。

    当前仓库基线构成（SIM-002 G4 记录，hybrid）：
    - decision = double：ScriptedUniAgent（tests/UniClaw.Simulation.Tests，
      无 live model）；
    - evaluation = 真件：UniClaw.Agent.UniAgent（ScenarioRunner 经
      BuildGoal 构造真实 PrimaryGoal 后 Evaluate）。

    Grant 在仿真中由显式的预录制/预评审静态授权资产提供，
    不得由仿真代码动态签发。
    double 必须实现与真件相同的对外 seam 契约。
```

## 机械执法（SIM-002 G4 落地面）

- 场景库标注：`scenarios/SCN-*.json` 每条目携带
  `agentDecisionRealization` + `goalEvaluationRealization`（legal:
  `real|double`）。
- C# 执法：`ScenarioRealizationAnnotationTests`——标注存在/合法，且
  **与实际构成一致**（构成锚点：ScriptedUniAgent 在组合面上 + 评估走
  真件）；构成翻转时锚点同步改，强制场景库重标注。
- 工具执法：`tools/scenario-coverage.py` 将两字段纳入 schema 违规
  （缺失/非法值 → exit 1）。
- 场景 schema 文件（`scenarios/schema.json`）的 v2 重建（含本两字段）
  归 SIM-002 S5，不在本修订内。
