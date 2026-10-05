# Simulation Architecture Baseline v0.2 — C7 kind=none Delta

> DocumentType: `SIMULATION_ARCHITECTURE_BASELINE_V0_2_C7_KIND_NONE_DELTA`
> Status: `CLOSED`（SIM-007 Owner 决策）
> Authority: `COMPONENT`
> Date: 2026-10-05 · 致因 Change：SIM-007

本文件是 `simulation-baseline-v0.2-c7-amendment.md` 的局部 delta；其余
C7 语义保持不变。

## kind=none 专有载体

- `agentDecisionRealization` 仍描述 ConsultAgent seam 的实际实现；当前
  PERC 载体为 `double`，对应 `ScriptedUniAgent`。
- `goalEvaluationRealization` 在专有载体不构造 `GoalEvaluation` 时使用
  `not-applicable`。该值表示评估面不属于该载体的证明范围，不等价于
  `real` 或 `double`。
- `execution.kind=none` 场景可以保留 `expectations` 作为描述性记录，
  但不得携带 `certification`。这些字段不进入 executable expectation
  projection，也不计入 golden certification 数量。
- 专有载体的行为断言仍然有效，但载体 PASS 只证明其局部行为；不能推导
  GoalEvaluation 或 certified golden claim 已成立。

## 机械执法

- schema 允许 `goalEvaluationRealization=not-applicable`，并要求
  `execution.kind=none` 使用该值。
- schema 禁止 `execution.kind=none` 携带 `certification`；golden-bundle
  仍必须认证。
- `ScenarioRealizationAnnotationTests`、`scenario-coverage.py` 和
  `scenario_certify.py --check` 对上述分层执行 fail-closed 检查。
