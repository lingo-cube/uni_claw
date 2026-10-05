# SIM-007 — kind=none 场景的声明消费面与 realization 执法

lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: 12c5763f2e3ba1bda9db4e3ba8b4c78d2ac12c76

## Intent（WHAT/WHY）

SIM-006 审计核实：SCN-PERC-001（execution.kind=none，专有 AsyncPerceptionHost）的 certified expectations 六字段与 goalEvaluationRealization=real 标注均无运行时消费者——悬空声明；ScenarioRealizationAnnotationTests 构成锚点只覆盖 ScenarioRunner 构成。SIM-006 已用 SCENARIO_CAVEATS 从 kind=none 机械推导并输出"载体 PASS ≠ 声明成立"，但这是如实标注缺口，不是消除缺口。

全量盘点（2026-10-05）：kind=none 场景共 8 个——SCN-PERC-001..008。其中仅 001 经逐载体审计；002–008 的专有载体是否消费 certified expectations / 是否构造 GoalEvaluation **未核实**，SIM-006 的通用 caveat 推导对它们是待证假设而非事实。

目标：逐载体核实 8 个 kind=none 场景的声明消费现状；裁决修正路径（专有载体消费 expectations 投影 / realization 标注词汇表扩展 / 撤回悬空声明）；扩展 C7 执法面使标注与实际构成的一致性对 kind=none 载体也可机械检查。

## Scope / Out of scope

### 范围

- 逐载体审计 SCN-PERC-001..008：是否调用 ScenarioExpectations.Verify/Load、是否构造 UniAgent/GoalEvaluation、断言与 JSON expectations 的对应（沿用 WI-SIM006-001 的只读审计方法）。
- 修正路径裁决：a) 专有 harness 消费 expectations 投影（执行面真消费）；b) 标注词汇表/语义扩展（如实表达"该面不存在"）；c) 撤回无消费者的 certified expectations 声明。三选一或组合，须给出被拒方案理由。
- 最小实现 + 执法同步：ScenarioRealizationAnnotationTests 锚点、scenarios/schema.json（如涉 legal 值）、tools/scenario-coverage.py / verify-change 的 SCENARIO_CAVEATS 推导（若事实变化则同步）。
- 认证变更（如期望/执行绑定变动）按 C8 搭乘本 change。

### 不在本 Change 内

- 不批量修改 golden 让现状"变绿"；不新增第二套期望系统或第二 realization 标注。
- 不改 AGT-012 / Android Settings 范围。
- 不改 21 个 golden-bundle 场景的构成与断言。

## Decisions（待实施时裁决；PERSIST 不冻结路径）

1. 逐载体事实先于路径裁决：002–008 未核实前不得假设与 001 同构（终审提醒：避免通用规则过度概括）。
2. C7 v0.2 执法面扩展或 schema legal 值变更属 C1–C9 约束变更——按 simulation-baseline v0.1 §7 **须 Human Gate**；本 Change 实施前必须取得，并按 v0.2 先例落 delta 修订文档。
3. SCENARIO_CAVEATS 推导规则（kind=none → caveat）在事实核实后同步收窄或维持；工具输出不得比事实更宽或更窄。

## Acceptance（后续实现完成的判据）

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | 8 个 kind=none 场景各有逐载体核实结论 | 消费/不消费、构造/不构造，附 文件:行号 证据；无未经核实的概括 |
| A2 | 选定路径实施后声明与事实一致 | expectations 与 realization 标注要么被载体真实消费，要么语义如实；悬空声明消除或显式改写 |
| A3 | C7 执法面对 kind=none 可机械检查 | 标注与实际构成不一致时测试/工具 fail；锚点覆盖专有载体或路径裁决后等价强度 |
| A4 | 认证与场景库纪律保持 | 期望/绑定变更搭乘本 change（C8）；scenario_certify/coverage 全绿；无 bulk-update |

## Constraints / Owner-Authority impact

- C7 realization 语义归 simulation baseline authority；修订经 Human Gate + 版本化 delta（v0.2 先例）。
- 本 Change 不取得 WorldBelief/Assurance/Effect 等 canonical authority；专有 harness 仍为测试基础设施。
- 场景库 status 真值链（trait→FQN→TRX）不因标注语义变化而弱化。

## Current facts（2026-10-05 WI-SIM007-001 只读审计核实）

审计载荷 `workitems/WI-SIM007-001.json`；报告 `evidence/sim-007/wi-sim007-001/perc-carrier-audit.md`；Leader 已抽查复核（trait 行号、零 ScenarioExpectations 消费、零 UniAgent/GoalEvaluation 构造、digest 值均证实）。

- **8/8 kind=none 场景同构悬空**：SCN-PERC-001..008 均经专有 AsyncPerceptionHost 家族载体（同构缝：ScriptedUniAgent → RunDriverInputs.ConsultAgent，AsyncPerceptionTracer.cs:579-591），**均不消费 certified expectations**（四个 AsyncPerception* 文件对 ScenarioExpectations 零引用）、**均不构造 GoalEvaluation**（无 UniAgent 构造）。
- realization 两面分离：`agentDecisionRealization=double` 在 8/8 载体**有**运行时对应物；`goalEvaluationRealization=real` 在 8/8 载体**无**对应物（悬空）。
- PERC-001 与 SIM-006 既有审计逐条一致（仅 fixtures 行号 50-53→51-53 微移）。
- expectations 同内容实证（修订措辞 2026-10-05：digest 一致证明六字段逐字节相同；"复制自"为方向性推断，源文件行号见审计附录 S.2）：PERC-001/002/004/005/006/007 的 expectationsDigest=7de541f2…（同内容源 scenarios/SCN-WIFI-001.json:18-25）；PERC-003=400b1755…（同内容源 scenarios/SCN-POLICY-009.json:20-27）；PERC-008=4b272fd7…（同内容源 scenarios/SCN-POLICY-006.json:20-27）。逐场景 JSON 声明行号见审计附录 S.1。
- 对 tools/verify-change SCENARIO_CAVEATS 规则的含义：核心断言（专有载体不消费 certified expectations）经 8/8 核实成立（由待证假设升级为事实）；但文案 "realization annotations are NOT consumed" 对 decision 半边过宽（decision=double 有载体对应物）——两面拆分候选已登记，随路径裁决一并处置。
- 旧 TRX 引用为既有记录；本轮无新鲜执行、无 PASS 声明。

## Verification（本轮 PERSIST）

```yaml
level: CONTRACT
method: python3 tools/gen-open-changes.py; 结构/引用检查（8 个 SCN-PERC JSON 存在、SIM-006 证据引用有效）; git diff --check
expected: SIM-007 为 persisted；索引含本 change；无实现、无 Human Gate 前的 C7 修改
actual: PASS（仅 PERSIST 文档检查；含审计回合后的结构复核输出，见 evidence/sim-007/persist-structure-check.md）
evidence: 本 state、changes/INDEX.md、evidence/sim-007/persist-structure-check.md
```

## Status log

- 2026-10-05 · UNDERSTAND → RESOLVE → PERSIST · 依据 SIM-006 终审（所有者）指令创建；kind=none 全量盘点 8 个 PERC 场景、仅 001 已核实为切入事实；C7 修订的 Human Gate 为实施前置条件。
- 2026-10-05 · PERSIST（保持；审计回合） · WI-SIM007-001 只读审计完成（固定 Worker + Leader 抽查复核）：8/8 同构悬空（expectations 无消费者、evaluation=real 无对应物、decision=double 有对应物）；expectations 复制来源实证（7de541f2/400b1755/4b272fd7）；CAVEATS 规则核心断言 8/8 成立、文案对 decision 面过宽（候选）。Acceptance A1 的逐载体事实已就绪；路径裁决与 C7 Human Gate 材料见 plans/2026-10-05-sim-007-c7-human-gate-material.md，等 Owner 裁决后进入 PLAN。
- 2026-10-05 · PERSIST（保持；审阅回补） · Owner 审阅指出：P1——Gate 材料原稿把方案 A 表述为"完整闭环/CAVEATS 可整体退役"不成立（A 不构造 GoalEvaluation，evaluation 悬空依旧），已重写为 A/B/C×两类悬空的作用面矩阵（A+B 或 C 才满足 A2；另列"载体真评估"为未评估的第四路径）；P2——补齐审计附录 S（逐场景 JSON 声明行号 S.1 + 同内容源文件行号 S.2，"复制"降级为方向性推断）；P2/P3——审计报告时间状态注记、GoldenScenarioBundles Load 调用计数 8→9（行号列表本正确）、新增 persist-structure-check.md 结构复核输出。SIM-007 维持 persisted，等 Owner 裁决。
