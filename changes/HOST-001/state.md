# HOST-001 — Product Host 最小 composition root v0

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 74bd6a92 · implementation complete 2026-09-20 · Human closure 2026-09-20（批量 closure，GATE-001 台账事件 #12）

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
| D6 | association = WorldModel 产品默认（strategy=null），v0 帧契约设计为兼容；实现期不撑则回 Leader 不质补 | 评审 #2 裁决 A1（2026-09-20） |
| D7 | freshness = 前置小 change 建产品 realization（FRS-007 谱系延续，Kernel/Assurance，真实时间逻辑非恒 Sufficient） | 评审 #2 裁决 A2 |
| D8 | 驱动缝 = 前置小 change 做 Kernel 驱动面最小公开化（internal→public 零行为变化 + 公开面白名单反射测试）；HOST-001 §8 零 Kernel 承诺保持 | 评审 #2 裁决 A3 |

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

- 2026-09-20 · closed·human-closure · 台账事件 #12：人批准关闭（批量，二选二）。
  **组件化终点线达成**（地图 §4 全勾）。后续队列（推导文档 §4）：①恢复
  编排（CORE-014 Q1 落点）②多轮决策形状（F9 证据）③association 滚动
  连续性（F8）。
- 2026-09-20 · created · 方向经 2026-09-20 分叉裁决（人选 Product Host
  最小 v0）；grill round 1 frontier 已预分类进 shadow 台账（事件 #5）。
- 2026-09-20 · grill-round-1-done · 人裁决 5/5 全按建议、零 override
  （台账事件 #5 已回填）；D1–D5 + Acceptance 六条定稿。决策级 frontier
  空；下一步 to-spec：第一手核实装配面事实（RUN-002 wiring、RFS-001
  SimulationHost 形状、UniKernel/感知/driver 组合缝、UniAgent 决策面）
  后起草 spec，评审为下一 Gate。
- 2026-09-20 · to-spec-drafted · 事实核实完成（EffectBoundary 执行源
  注入缝、UniKernel 八依赖、KernelRunDriver 自驱缝、UniAgent 仅 Goal
  Evaluation——决策面走 ConsultAgent + ControlLoop）；`spec.md` v0.1
  落盘：HostRunner 组合面表（三处显式命名的 Host 内 v0 确定性件）、
  运行时序对齐 RUN-002、闭包执法扩展 ProductHostClosureTests、四个
  实现期待核实事实单列 §7。待 spec 评审 Gate。
- 2026-09-20 · spec-review-2·CHANGES_REQUIRED·IMPLEMENT_BLOCKED ·
  台账事件 #6（override=是）。Leader 对源码逐条复核：①`SeedContainer
  AssociationStrategy` 为测试 internal 类——spec「产品件」表述错误，
  坐实；②src 无 `IFreshnessEvaluator` 产品实现（仅接口+注入缝），坐实，
  且非命名问题而是组合缺口；③`KernelRunDriver`/`RunDriverInputs` 均
  internal、IVT 不含 Host——编译不可达，坐实，属 Kernel 边界裁决；
  ④「基线 406/407」驳回——修复后全量 579/579（9+14+17+132+407）刚于
  HEAD 复跑核实，评审树为 d5612615 之前状态。裁决前不建 `src/UniClaw.Host`、
  不改 Kernel、不绕 IVT。新增事实：`WorldModel(…, IAssociationStrategy? =
  null)` 可空——产品默认 association 路径存在，A1 选项空间改变。
  A1/A2/A3 三裁决待人工（见 Decisions 待补 D6–D8）。
- 2026-09-20 · adjudicated·D6–D8 · 人裁决三选三全按建议：A1 产品默认
  null / A2 前置 freshness realization / A3 前置可见性 change（台账事件
  #6 已回填）。IMPLEMENT 前置序列确立：先落两个前置 change（FRS 产品
  freshness realization；Kernel 驱动面最小公开化），HOST-001 保持
  IMPLEMENT_BLOCKED 直至两者 closed。spec 升 v0.2。
- 2026-09-20 · sim-first·v0.3 · 用户纠正（台账事件 #11）：外部组件先
  仿真/模拟，先跑通核心模型+能力接口，仿真流程按正式能力完善。
  spec v0.3：前置 A/B/C 全 closed（FRS-008 / RUN-003 / UIW-005——D6
  回退经人裁决 b 落正式零件，事件 #10）；association 行更新为
  ProductAssociationStrategy 产品件；Acceptance 增第 7 条仿真可复现
  （两次 run digest 一致，RFS 先例）。IMPLEMENT 解锁，装配开始。
  D6 原裁决（产品默认 null）作废存档：其前提（产品默认路径可用）经
  实证不成立。
- 2026-09-20 · implemented·verified · 装配完成：首跑 Completed/Completion
  /EXIT=0，journal 有 pre-dispatch 记录（含 freshness:Sufficient 十项
  检查），trace/facts 落盘，两跑 digest 一致（Acceptance #7），闭包
  {UniClaw.Kernel} + 禁词扫描 GREEN（RFS-001 债清），全量 598/598。
  装配期三发现留痕（占位 obligation / occurrence revision-local 批序 /
  UIW-005 D4）。四元组落 evidence/2026-09-20-host-001-product-host.md。
  待批量 closure。
- 2026-09-20 · closed·human-closure · 台账事件 #12：人批准关闭（批量，
  二选二）。头部 lifecycle 由 HYG-001（2026-09-22）补齐，与 ee790f7 裁决一致。
