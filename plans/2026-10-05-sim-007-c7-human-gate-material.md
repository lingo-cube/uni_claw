# SIM-007 C7 Human Gate 材料 — kind=none 场景 realization 语义方向裁决（修订版）

> 呈交：所有者（C7 v0.2 修订裁决权持有人）。
> 事实来源：evidence/sim-007/wi-sim007-001/perc-carrier-audit.md（WI-SIM007-001，Leader 抽查复核，
> 含附录 S 逐场景行号与同内容源文件引用）+ changes/SIM-007/state.md Current facts。
> 本材料只压缩事实与路径代价；裁决归 Owner（simulation-baseline v0.1 §7：C1–C9 变更需 Human Gate）。
> 修订记录：2026-10-05 依 Owner 审阅（P1）重写路径框架——原稿把方案 A 表述为"声明与消费闭环、
> CAVEATS 可整体退役"，该表述不成立：A 不构造 GoalEvaluation，evaluation=real 悬空在 A 下依旧。

## 裁决请求

对 8 个 execution.kind=none 场景（SCN-PERC-001..008）的两类悬空声明——
(1) certified expectations 无运行时消费者；(2) goalEvaluationRealization=real 无载体对应物——
选择修正路径（A/B/C 或组合），并裁决 SCENARIO_CAVEATS 文案是否两面拆分。

## 事实矩阵（8/8 同构，已核实；详见审计报告 §0 与附录 S）

| 维度 | 事实 | 关键证据 |
|---|---|---|
| 载体 | 专有 AsyncPerceptionHost 家族；同构缝 ScriptedUniAgent → ConsultAgent | AsyncPerceptionTracer.cs:579-591 |
| certified expectations 消费 | **无**（ScenarioExpectations 在 AsyncPerception* 四文件零引用；全仓 Load 调用仅 GoldenScenarioBundles 9 处等） | 审计 §0.2 |
| GoalEvaluation 构造 | **无**（无 UniAgent 构造；GoalEvaluation 产品面消费点仅在 ScenarioRunner.cs:159/189） | 审计 §0.3 |
| agentDecisionRealization=double | **有**运行时对应物 | AsyncPerceptionTracer.cs:579-581 |
| goalEvaluationRealization=real | **无**运行时对应物（悬空）；执法锚点只覆盖 ScenarioRunner 构成 | ScenarioRealizationAnnotationTests.cs:19-20,78-86 |
| expectations 内容 | 与源场景逐字段相同（digest 一致）：6 个=WIFI-001、003=POLICY-009、008=POLICY-006（复制方向为推断） | 审计附录 S.2 |

## 两类悬空声明 × 路径作用面（修正后框架）

| 路径 | 解决 (1) expectations 无消费 | 解决 (2) evaluation=real 无对应物 | 单独满足 SIM-007 A2？ |
|---|---|---|---|
| A 专有载体消费 expectations 投影 | ✅ | ❌（不构造 GoalEvaluation，evaluation 悬空依旧） | **否** |
| B realization 标注语义/词汇表修正 | ❌（expectations 仍无消费） | ✅（absent/not-applicable 或按 kind 条件化） | **否** |
| C 撤回悬空声明（expectations/认证块降级或移除） | ✅（声明消失） | ✅（evaluation 声明随形态消失或降级） | 可（取决于撤回形态） |
| **A + B** | ✅ | ✅ | **是** |

## 路径与代价

### A. 专有载体消费 expectations 投影（只闭合 expectations 消费）

- 做法：AsyncPerceptionHost 家族增加 ScenarioExpectations 投影消费面（Load/Verify + AcceptancePassed 等价判定）。
- 审计后新增代价：8 组 expectations 是同内容复用（事实矩阵）——真消费会立即暴露不匹配；须先逐场景重写 expectations 反映载体真实断言（**大规模期望迁移，C8 搭乘 SIM-007**）。
- 收益（修正后）：certification 对 kind=none 的 expectations 面获得执行意义；CAVEATS 的 expectations 半边可退役。**evaluation 悬空与 CAVEATS 的 evaluation 半边不变。**

### B. realization 标注词汇表/语义扩展（只修 evaluation 语义）

- 做法：goalEvaluationRealization 增加 legal 值（如 absent/not-applicable）或按 execution.kind 条件化语义；8 个 JSON 改标注；schema.json、ScenarioRealizationAnnotationTests 锚点、scenario-coverage.py legal 值同步。
- 代价：**C7 v0.2 修订本体**（本 Human Gate 即为其授权）。
- 收益（修正后）：evaluation 标注不再说谎；执法面可扩展到 kind=none。**expectations 悬空不变**（除非组合 A 或接受"期望仅为编目"语义）。

### C. 撤回悬空声明

- 做法：移除/降级 8 个场景的 expectations+certification 块（evaluation 声明随认证形态消失或显式降级）。
- 代价：scenario_certify.verify_entry 与 scenario-coverage 现要求 certification 块，工具需适配"无认证条目"新形态；认证账面收缩。
- 收益：最小语义负担；两类悬空同时消失（以撤回而非修复的方式）。

### 附带裁决：SCENARIO_CAVEATS 文案两面拆分

现状文案对 decision=double 半边过宽（该面有载体对应物）。拆分为两面表述（decision: consumed via seam；evaluation: no counterpart）可独立于 A/B/C 先行，或随所选路径一并实施；若选 A+B 并落地，caveat 规则可整体退役。

## Leader 事实性备注（非约束）

- 不引入 GoalEvaluation 的任何组合中，evaluation 面只能靠 B（语义如实）或 C（撤回）处置——"让 PERC 载体真评估"（A 的评估扩展变体）未列入，因其改变专有 harness 行为面，超出当前路径集，如 Owner 需要可作为第四路径显式评估。
- 无论选哪条：ScenarioRealizationAnnotationTests 锚点对 kind=none 的覆盖缺口都应补（属执法面扩展，随 C7 修订授权）。
- A 的期望重写工作量以"8 组从未定制"为前提评估。
