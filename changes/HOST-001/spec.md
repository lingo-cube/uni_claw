# HOST-001 spec — Product Host 最小 composition root v0.1

> 状态: DRAFT — 待评审（本 Change 下一 Gate）
> 上游: HOST-001/state.md D1–D5（grill round 1 全按建议，2026-09-20）·
> ADR-0019（Primary Run 自驱于 UniKernel）· ADR-0023（执行源归
> EffectBoundary）· CORE-014 Q1/Q4 裁决 · RFS-001（SimulationHost 范本、
> closure 债）· RUN-002（live 接线蓝图）
> 已核实事实（本 spec 的代码级依据，2026-09-20 第一手）:
> `EffectBoundary(IEffectDriver, IReliableExecutionSource?)` 注入缝、
> `UniKernel(EvidenceLedger, WorldModel, IRunTrace, RunModel, ControlLoop,
> RuntimeAssurance, EffectBoundary, RuntimeStageMetrics)` 八依赖、
> `KernelRunDriver(kernel, planPolicy, RunDriverInputs{NextInput,
> ConsultAgent})` 自驱缝、`RunTraceFactory.BeginRun/FinalizeArtifact`、
> `src/UniClaw.Agent` 仅有 Goal Evaluation（无决策面）。

## 1. 形态与检验主张

```text
src/UniClaw.Host（console，Program.cs = 组合根）
  HostRunner.Compose(goal, profile) → 单次 headless run
    UniKernel（八依赖全真实产品件）
    KernelRunDriver（自驱：NextInput + ConsultAgent 两缝）
    FileExecutionJournal（必注入，./runs/<runid>/exec.journal）
  run → Runtime Outcome → 落盘（journal/trace/facts）→ 退出码
```

检验主张：**同一 Product Runtime 可在产品组合根下装配并自驱跑完一次
闭环**（ADR-0019；G23 双 Host 的 Product 半边）。与 SimulationHost 的
区别：产品程序集、journal 必注入、无 ScenarioStimulus/Oracle/Importer/
Replay 机制、有真实进程入口与退出语义。

## 2. v0 组合面（deterministic profile，唯一 profile）

| 缝 | v0 取值 | 性质 |
|---|---|---|
| WorldModel association | `SeedContainerAssociationStrategy`（RUN-002 同款；产品位置实现期核实） | 产品件 |
| occurrence strategy | Host 内最小确定性帧策略（v0 帧契约：简单 JSON detects 格式，Host 自定义输入源配套） | **Host 内 v0 件**（诚实命名，非测试 double） |
| freshness | FRS-007 真实 evaluator（实现期核实产品面；**禁止恒 Satisfying double**） | 产品件 |
| effect driver | Host 内 `DeterministicDeliveryDriver`：记录 dispatch、零外部副作用 | **Host 内 v0 件**（ADB live driver 换入 = 下一 change） |
| journal | `FileExecutionJournal(./runs/<runid>/exec.journal)` **必注入** | 产品件（CORE-013） |
| trace | `RunTraceFactory.BeginRun`，run 终局 `FinalizeArtifact` 落 `./runs/<runid>/trace.json` | 产品件 |
| 决策 | `ControlLoop(AgentPlanPolicy)` + `ConsultAgent` = Host 内最小确定性 consult（产 P25 形状、run-correlated 的单动作 proposal） | **Host 内 v0 件**（智能升级 = 后续 change） |
| 输入 | `NextInput` = Host 内确定性帧源（按 v0 帧契约产 N 帧） | **Host 内 v0 件** |

「Host 内 v0 件」= 显式命名的产品侧确定性替身（dev profile），非测试
程序集 double；它们的替换路径（live 感知/ADB/真 agent）各自是已登记的
后续 change。

## 3. 运行时序（对齐 RUN-002 语义、自驱形态）

```text
AdmitContract(goal) → seed 观察（铸 root container）
→ Drive 循环：NextInput 帧 → Process → SelectIntent → grounding
   → Assurance/Gate → dispatch（journal pre-dispatch 记录 → driver）
→ 再观察证实 → EvaluateTerminal（如实，不伪造 proof）
→ FinalizeArtifact + facts 落盘 → 按终局退出
```

## 4. 依赖闭包执法（还 RFS-001 债）

- Host csproj ProjectReference ⊆ {UniClaw.Kernel, UniClaw.Agent}；
  **零测试程序集引用**；
- `ProductHostClosureTests`（Kernel.Tests 内，RFS-001 建立）扩展：
  Host 程序集 `GetReferencedAssemblies()` 的 UniClaw.* 集合 ⊆
  {UniClaw.Kernel, UniClaw.Agent}；且 Host 内无
  ScenarioStimulus/Oracle/Importer/Replay 类型引用（类型名扫描）；
- Host 入 solution `UniClaw.Kernel.slnx`（CORE-016 F1 教训：不入 = 套件盲区）。

## 5. 验收映射（state.md Acceptance 1–6）

| # | 承载 |
|---|---|
| 1 独立启动单次 run | console 入口 + `HostRunner`；SCENARIO 测试进程内跑完整 run 并断言退出码与产物存在 |
| 2 闭环证据 | facts 断言：Evidence admission、Judgment 非空、Receipt 非空、journal 含 pre-dispatch 记录（读回文件验证） |
| 3 journal 无未注入默认 | `HostRunner.Compose` 无「null 执行源」路径（结构断言 + 构造必填） |
| 4 闭包测试 GREEN | §4 扩展测试 |
| 5 零回归 | 全量套件（579/579 基线 + 新增） |
| 6 Out of Scope 零涉入 | 无 DiscoverPending 消费、无 retention、无 Grant、单 run（grep/结构断言） |

## 6. 验证 level 预告

闭包/组合/结构断言 = DETERMINISTIC；端到端单次 run = SCENARIO
（进程内；进程级 smoke 可选手工）。真机/live = 不在本 change（沿用
RUN-002 ENVIRONMENT 门控先例，属下一 change）。

## 7. 实现期需核实的既存事实（不阻塞评审，阻塞 IMPLEMENT 开工核对）

1. `SeedContainerAssociationStrategy` 的产品命名空间位置；
2. FRS-007 freshness 真实 evaluator 的产品面与构造；
3. P25 `AgentActionProposal` 精确形状（ConsultAgent 委托签名）；
4. `FileExecutionJournal` 构造签名与命名空间。

## 8. Non-goals / 不可外推

- v0 确定性外部缝不是产品能力的声明（live 感知/ADB/智能 agent 全部
  后续 change）；v0 通过 ≠ Product Host 完备；
- 不动恢复编排（CORE-014 Q1 落点在下一个 change 消费 DiscoverPending）；
- 不做 retention/Grant/多 run/session；
- 不改 Kernel/Core 产品代码（如组合暴露缺缝，回 Leader 裁决，不本地质补）。
