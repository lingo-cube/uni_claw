# SIM-008 裁决材料 — SCN-SMOKE-001 / SCN-WIFI-001 载体重复三方案

> 用途：SIM-008 Owner 裁决支持。事实来源：SIM-006 审计 §1（evidence/sim-006/wi-sim006-001/scenario-audit.md）、
> tests/UniClaw.Simulation.Tests/SimulationHostSmokeTests.cs、scenarios/*.json、tools/scenario_certify.py。
> 本材料只列事实与代价，不替 Owner 裁决；Leader 倾向见文末（非约束）。

## 事实基础（SIM-006 审计核实）

1. 两场景共用 carrier `wifi-off-to-on`；certification 三个 digest（expectations/execution/runtimeSource）**逐字节相同**。
2. SMOKE 断言 3 条（AcceptancePassed、AgentViolations 空、Metrics.ModelCalls 以 "N/A" 开头——[SimulationHostSmokeTests.cs:20-26](../tests/UniClaw.Simulation.Tests/SimulationHostSmokeTests.cs)），为 WIFI-001 断言面的**严格子集**。
3. WIFI-001 同链路（ScenarioLibrary.Load → ScenarioRunner → SimulationHost）+ decision-id 协议、消费顺序、GoalEvaluation 同 RunId、driver 自驱证明。
4. **SCN-SMOKE-001 是 capability=host-integration 的唯一场景**（11 个能力轴之一）——退役即从场景库删除该能力轴。
5. digest 计算边界（[scenario_certify.py](../tools/scenario_certify.py)）：expectationsDigest 只覆盖期望六字段；executionDigest 只覆盖 kind/carrier/options；runtimeSourceHash 只覆盖 src/Kernel+src/Agent。**测试代码与其余 JSON 字段（name/purpose/components 等）不进任何 digest**。

## 方案对照

### A. 差异化（SMOKE 承担独立断言面）

- 做法：为 SMOKE 增加 WIFI-001 没有的 Type-B 断言（期望模型外的架构不变量）。现有候选轴：`report.FirstActivation`/`host.FirstActivation` 组装事实、Compose 面不变量（SimulationHost 组装冒烟语义）。
- 代价：改测试代码；若只加 Type-B 断言则**三 digest 全不动、无需重认证**（事实 5）；场景库 29 条与 host-integration 轴保留。
- 风险：断言面若仍是子集或语义重叠，冗余只是被掩盖；"独立价值"需要真实断言内容支撑，不是改名。

### B. 退役 SCN-SMOKE-001

- 触点（grep 全量）：scenarios/SCN-SMOKE-001.json、SimulationHostSmokeTests.cs（trait+测试）、testsets/simulation-baseline/manifest.json（task/simulation-baseline/scn-smoke-001）、docs/analysis/sim-006-first-baseline-matrix.md（行）、SIM-006/008 state 与历史 plans/evidence（历史记录不改）。
- 代价：**host-integration 能力轴从场景库消失**（事实 4）；coverage 总数 29→28；首批 manifest 8→7 任务；矩阵行删除。
- C8：剩余 28 场景 digest 不动（删条目不是期望迁移）；但 SIM-006 的"首批 8 场景"表述成为历史快照（state 已 closed，不改写）。
- Host 整装闭环证明由 WIFI-001 继续承担（同一链路，事实 3）。

### C. 维持现状 + 显式子集声明

- 做法：在 SCN-SMOKE-001.json 的 purpose/name 字段写明"WIFI-001 断言子集，非独立能力"（**digest 中性**，事实 5）；矩阵已如实记录。
- 代价：最低；29 条与能力轴保留。
- 风险：冗余条目继续存在；子集关系靠文字维持，无机械执法（除非后续给 schema 加约束——预造接口，暂不做）。

## 各方案与 SIM-008 Acceptance 的对照

| Acceptance | A 差异化 | B 退役 | C 维持+声明 |
|---|---|---|---|
| A1 显式裁决记录（含被拒方案） | 均可满足（裁决记录本身） | 同 | 同 |
| A2 无"同载体同断言子集"冗余 | 满足（前提：断言面真独立） | 满足（条目消失） | **不满足**（冗余保留，仅声明） |
| A3 首批基线语义不降级 | 满足 | 满足（闭环证明由 WIFI-001 承担；host-integration 轴消失需显式接受） | 满足 |

## Leader 倾向（非约束，供裁决参考）

A 或 B 优于 C（C 不满足 A2）。若 host-integration 作为独立能力轴有持续编排价值（coverage by capability 报告的消费路径），选 A 并要求断言面独立可判；若该轴无真实消费者，选 B 并接受轴消失。裁决依据应是 host-integration 轴的真实买家，不是条目感情（ADR-0026）。
