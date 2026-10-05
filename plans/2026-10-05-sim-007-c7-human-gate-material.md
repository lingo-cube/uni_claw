# SIM-007 C7 Human Gate 材料 — kind=none 场景 realization 语义方向裁决

> 呈交：所有者（C7 v0.2 修订裁决权持有人）。
> 事实来源：evidence/sim-007/wi-sim007-001/perc-carrier-audit.md（WI-SIM007-001，Leader 抽查复核）+
> changes/SIM-007/state.md Current facts。
> 本材料只压缩事实与路径代价；裁决归 Owner（simulation-baseline v0.1 §7：C1–C9 变更需 Human Gate）。

## 裁决请求

对 8 个 execution.kind=none 场景（SCN-PERC-001..008）的 certified expectations 与
goalEvaluationRealization=real 悬空声明，选择修正路径（A/B/C 或组合），并裁决
SCENARIO_CAVEATS 文案是否两面拆分。

## 事实矩阵（8/8 同构，已核实）

| 维度 | 事实 |
|---|---|
| 载体 | 专有 AsyncPerceptionHost 家族；同构缝 ScriptedUniAgent → ConsultAgent |
| certified expectations 消费 | **无**（ScenarioExpectations 在 AsyncPerception* 四文件零引用） |
| GoalEvaluation 构造 | **无**（无 UniAgent 构造） |
| agentDecisionRealization=double | **有**运行时对应物 |
| goalEvaluationRealization=real | **无**运行时对应物（悬空） |
| expectations 来源 | 复制自其他场景：002/004/005/006/007=WIFI-001（7de541f2）、003=POLICY-009（400b1755）、008=POLICY-006（4b272fd7）、001=WIFI-001 |
| 执法现状 | ScenarioRealizationAnnotationTests 锚点只覆盖 ScenarioRunner 构成，kind=none 不受构成一致性检查 |

## 路径与代价（按审计后事实更新）

### A. 专有载体消费 expectations 投影（声明变真）

- 做法：AsyncPerceptionHost 家族增加 ScenarioExpectations 投影消费面（Load/Verify + AcceptancePassed 等价判定）。
- **审计后新增代价**：8 个场景的 expectations 是复制来的（矩阵如上）——真消费会立即暴露不匹配。必须先为每个场景重写 expectations 使其反映载体真实断言（等待/覆盖并集/恰 N effect/终态），即**大规模期望迁移，C8 搭乘 SIM-007**；语义上等于给 8 个专有载体补"可认证期望面"。
- 收益：声明与消费闭环；certification 对 kind=none 获得真实意义；CAVEATS 规则可整体退役。

### B. realization 标注词汇表/语义扩展（声明变如实）

- 做法：goalEvaluationRealization 增加 legal 值（如 absent/not-applicable）或按 execution.kind 条件化语义；8 个 JSON 改标注；schema.json、ScenarioRealizationAnnotationTests 锚点、scenario-coverage.py legal 值同步。
- 代价：**C7 v0.2 修订本体**（本 Human Gate 即为其授权）；expectations 悬空问题不解决（除非组合 C 或接受"期望仅为编目"语义）。
- 收益：标注不再说谎；执法面可扩展到 kind=none。

### C. 撤回悬空声明（声明消失）

- 做法：移除 8 个场景的 expectations/certification 块（或降级为非认证编目字段）。
- 代价：scenario_certify.verify_entry 与 scenario-coverage 现要求 certification 块——工具需适配"无认证条目"新形态（工具语义变更）；场景库从 29 certified 变混合形态；SIM-002/SIM-003 建立的认证账面出现收缩。
- 收益：最小语义负担；无复制期望的误导。

### 附带裁决：SCENARIO_CAVEATS 文案两面拆分

现状文案 "realization annotations are NOT consumed by the carrier" 对 decision=double 半边过宽（该面有载体对应物）。拆分为两面表述（decision: consumed via seam；evaluation: no counterpart）是小改，可随任一路径一并实施，也可独立先行。

## Leader 事实性备注（非约束）

- 路径组合可行：B（标注如实）+ expectations 降级为编目（C 的软形态）是不动载体行为的组合；A 是唯一让 certification 对该家族获得执行意义的路径，但代价是 8 组期望重写。
- 无论选哪条：ScenarioRealizationAnnotationTests 锚点对 kind=none 的覆盖缺口都应补（属执法面扩展，随 C7 修订授权）。
- 6+1+1 的 digest 复制分布说明 expectations 从未为这些场景定制过——重写（A）的工作量评估应以此为前提。
