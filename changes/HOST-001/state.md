# HOST-001 — Product Host 最小 composition root v0

lifecycle_state: resolving · disposition: none · depth: decision-heavy · base: 74bd6a92

## Intent

建立 Product Host 最小 composition root：把已存在的 Product Runtime
模块（UniKernel、感知 provider、effect driver、可靠执行 journal）装配成
可运行的产品入口。它是多项已登记 deferral 的解锁钥匙（CORE-014 Q1 恢复
消费落点、Q4 journal 组合根默认、RFS-001「完整 Product Host dependency
closure 待验证」），也是双 Host 方向（roadmap G23，Simulation Host 一半
已由 RFS-001 完成）的另一半。

## Prior art

- RUN-002 live closed loop（ENVIRONMENT 门控测试——产品接线的现成参照）
- RFS-001 SimulationHost（组合根先例：装配同一 Product Runtime）
- CORE-013 `FileExecutionJournal`（journal 注入缝已存在）
- CORE-014 Q4 裁决：journal 生命周期归组合根；必注入与否、路径/retention
  显式留给本 Change 裁决

## Scope（grill round 1 定稿，2026-09-20）

- 新产品程序集 `src/UniClaw.Host`（console；Program.cs = 组合根）
- 单次 headless run：一个目标 → 完整最小闭环（观察→决策→effect→
  Outcome）→ 退出
- journal 必注入 `FileExecutionJournal`，路径默认 `./runs/<runid>/exec.journal`
- Product Host 依赖闭包执法测试进 v0（不含 ScenarioStimulus consumer /
  Replay / Oracle / Importer）
- 验收场景用确定性 driver（scripted 感知 + 确定性 effect，循 RUN-002/
  RFS-001 先例）；真机保持 ENVIRONMENT 门控可选，不进 v0 必选验收

## Out of Scope（显式）

- 恢复编排（DiscoverPending/GetAttempt 消费——下一个 change）
- journal retention / 清理策略
- Grant/授权体系（RFS-001 D10 方向，未裁决载荷）
- 多 run / session 生命周期
- 真机验收（沿用 RUN-002 的 DSH_TEST_PERCEPTION_LIVE 门控，可选不进 v0）

## Decisions（grill round 1，全按建议）

| # | 决策 | 依据 |
|---|---|---|
| D1 | v0 = 单次 headless run（不引入 session/并发/长驻语义） | grill Q1 |
| D2 | 闭包 = 完整最小闭环（观察→决策→effect→Outcome） | grill Q2；检验主张本体 |
| D3 | journal 必注入 + run-scoped 路径默认；retention 不做 | grill Q3；CORE-014 Q4 移交裁决落定 |
| D4 | 入口 = 正式产品程序集（非测试侧组合根） | grill Q4；G23 双 Host 方向 |
| D5 | 闭包执法进 v0（还 RFS-001 债） | grill Q5 |

## Acceptance（grill 定稿）

1. `src/UniClaw.Host` 可独立启动，单次 run 产出 Runtime Outcome 后退出
2. 闭环全链有证据：观察入 Evidence、决策过 Assurance/Gate、effect 有
   Receipt、journal 有 pre-dispatch 记录
3. journal 在产品路径上无「未注入」默认
4. Product Host 依赖闭包测试 GREEN（模拟四件零进入）
5. 既有全量回归零回归（579/579 基线）
6. Out of Scope 零涉入：无 DiscoverPending 消费、无 retention、无 Grant、
   无多 run

## Verification

（实现后按四元组落；level 预告：闭包/组合 DETERMINISTIC，端到端 SCENARIO）

## Status log

- 2026-09-20 · created · 方向经 2026-09-20 分叉裁决（人选 Product Host
  最小 v0）；grill round 1 frontier 已预分类进 shadow 台账（事件 #5）。
- 2026-09-20 · grill-round-1-done · 人裁决 5/5 全按建议、零 override
  （台账事件 #5 已回填）；D1–D5 + Acceptance 六条定稿。决策级 frontier
  空；下一步 to-spec：第一手核实装配面事实（RUN-002 wiring、RFS-001
  SimulationHost 形状、UniKernel/感知/driver 组合缝、UniAgent 决策面）
  后起草 spec，评审为下一 Gate。
