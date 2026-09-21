# HOST-001 spec — Product Host 最小 composition root v0.3

> 状态: IMPLEMENT 解锁（前置 A/B/C 全部 closed）；v0.3 增补：仿真方针
> （2026-09-20 用户纠正，台账事件 #11）——外部组件（感知/执行/agent
> 智能）先仿真/模拟实现，先跑通核心模型+能力接口，仿真流程作为正式
> 能力完善（可复现、落盘可离线检视）
> 上游: HOST-001/state.md D1–D8 · ADR-0019 · ADR-0023 · ADR-0026 ·
> CORE-014 Q1/Q4 · RFS-001 · RUN-002 · spec 评审 #2（2026-09-20）

## 0. IMPLEMENT 前置序列（全部落地）

1. ✅ 前置 A = FRS-008（产品 freshness evaluator，closed 2026-09-20）
2. ✅ 前置 B = RUN-003（驱动面公开化 + 白名单 197 项执法，closed）
3. ✅ 前置 C = UIW-005（产品 association realization——D6 回退经人
   裁决 b：不造临时件，直接正式零件；closed）
4. **IMPLEMENT 解锁**。
> 已核实事实（2026-09-20 第一手，含评审复核）:
> `EffectBoundary(IEffectDriver, IReliableExecutionSource?)`、
> `UniKernel` 八依赖、`KernelRunDriver`/`RunDriverInputs` 均 **internal**
> （IVT 不含 Host）、src **无** `IFreshnessEvaluator` 产品实现（仅接口
> + 注入缝）、`SeedContainerAssociationStrategy` 为测试 internal 类、
> `WorldModel(…, IAssociationStrategy? = null)` 可空（产品默认路径存在）、
> `FileExecutionJournal(string)` 公开、`src/UniClaw.Agent` 仅 Goal
> Evaluation。

## 1. 形态与检验主张

```text
src/UniClaw.Host（console，Program.cs = 组合根）
  HostRunner.Compose(goal, profile) → 单次 headless run
    UniKernel（八依赖：产品件 + 前置 change 产物）
    KernelRunDriver（自驱：NextInput + ConsultAgent 两缝，前置 B 后可达）
    FileExecutionJournal（必注入，./runs/<runid>/exec.journal）
  run → Runtime Outcome → 落盘（journal/trace/facts）→ 退出码
```

检验主张：**同一 Product Runtime 可在产品组合根下装配并自驱跑完一次
闭环**（ADR-0019；G23 双 Host 的 Product 半边）。

## 2. v0 组合面（deterministic profile，唯一 profile）

| 缝 | v0 取值 | 性质 |
|---|---|---|
| WorldModel association | **`ProductAssociationStrategy`（UIW-005 产品件——D6 回退经人裁决 b 落正式零件，2026-09-20）** | 产品件 |
| occurrence strategy | Host 内最小确定性帧策略（v0 帧契约：简单 JSON detects 格式） | **Host 内 v0 件**（显式命名） |
| freshness | **前置 change A 产物（D7）**：产品 freshness evaluator | 产品件 |
| effect driver | Host 内 `DeterministicDeliveryDriver`：记录 dispatch、零外部副作用 | **Host 内 v0 件**（ADB live 换入 = 后续 change） |
| journal | `FileExecutionJournal(./runs/<runid>/exec.journal)` **必注入** | 产品件（CORE-013） |
| trace | `RunTraceFactory.BeginRun`，终局 `FinalizeArtifact` 落 `./runs/<runid>/trace.json` | 产品件 |
| 决策 | `ControlLoop(AgentPlanPolicy)` + `ConsultAgent` = Host 内最小确定性 consult（P25 形状、run-correlated 单动作 proposal） | **Host 内 v0 件** |
| 输入 | `NextInput` = Host 内确定性帧源（按 v0 帧契约产 N 帧） | **Host 内 v0 件** |
| 驱动 | `KernelRunDriver(kernel, planPolicy, inputs)`——**前置 change B 后编译可达** | 产品件（可见性由前置 B 裁定） |

「Host 内 v0 件」= 显式命名的产品侧确定性替身（dev profile），非测试
程序集 double，不冒充 live 能力或真实智能；各自换入路径已登记。

## 3. 运行时序（对齐 RUN-002 语义、自驱形态）

```text
AdmitContract(goal) → 首帧观察
→ Drive 循环：NextInput 帧 → Process → SelectIntent → grounding
   → Assurance/Gate（前置 A 的真实 freshness）→ dispatch
   （journal pre-dispatch 记录 → driver）
→ 再观察证实 → EvaluateTerminal（如实，不伪造 proof）
→ FinalizeArtifact + facts 落盘 → 按终局退出
```

## 4. 依赖闭包执法（还 RFS-001 债）

- Host csproj ProjectReference ⊆ {UniClaw.Kernel, UniClaw.Agent}；
  **零测试程序集引用**；
- `ProductHostClosureTests` 扩展：Host 程序集 `GetReferencedAssemblies()`
  的 UniClaw.* 集合 ⊆ {UniClaw.Kernel, UniClaw.Agent}；Host 内无
  ScenarioStimulus/Oracle/Importer/Replay 类型引用（类型名扫描）；
- Host 入 solution `UniClaw.Kernel.slnx`。

## 5. 验收映射（state.md Acceptance 1–6）

| # | 承载 |
|---|---|
| 1 独立启动单次 run | console 入口 + `HostRunner`；SCENARIO 测试进程内跑完整 run 并断言退出码与产物存在 |
| 2 闭环证据 | facts 断言：Evidence admission、Judgment 非空、Receipt 非空、journal 含 pre-dispatch 记录（读回文件验证） |
| 3 journal 无未注入默认 | `HostRunner.Compose` 无「null 执行源」路径（结构断言 + 构造必填） |
| 4 闭包测试 GREEN | §4 扩展测试 |
| 5 零回归 | 全量套件——基线以开工时 HEAD 全量第一手复跑为准（2026-09-20 HEAD=fd20f51f 为 592/592：Core 14 + Agent 17 + FSRealization 9 + Simulation 132 + Kernel 420） |
| 6 Out of Scope 零涉入 | 无 DiscoverPending 消费、无 retention、无 Grant、单 run |
| 7 **仿真可复现**（v0.3 新增，事件 #11 方针） | 同输入两次 run：outcome 一致；`./runs/<runid>/` 三件产物齐（journal / trace / facts）；deterministic profile 下两次 run 的 facts digest 一致（RFS digest 先例）——仿真流程是正式能力，不是测试脚手架 |

## 6. 验证 level 预告

闭包/组合/结构断言 = DETERMINISTIC；端到端单次 run = SCENARIO（进程内；
进程级 smoke 可选手工）。真机/live 不在本 change（ENVIRONMENT 门控先例，
属后续 change）。

## 7. 实现期需核实的既存事实

1. P25 `AgentActionProposal` 精确形状与 `ConsultAgent` 委托签名
   （前置 change B 公开化后核对）；
2. 产品默认 association（null）对 v0 帧契约首帧行为（D6 回退条件）。

（v0.1 §7 的 freshness 与 association 两项已升格为前置 change / D6 裁决，
不再属「待核实」。）

## 8. Non-goals / 不可外推

- v0 确定性外部缝不是产品能力声明；v0 通过 ≠ Product Host 完备；
- 不动恢复编排（CORE-014 Q1 落点在下一个 change）；
- 不做 retention/Grant/多 run/session；
- **不改 Kernel/Core 产品代码**（§8 承诺因前置 change A/B 的存在而保持：
  Kernel 侧改动全部发生在前置 change 内，本 change 零 Kernel diff；
  组合若再暴露缺缝，回 Leader 裁决，不本地质补）。

## 9. v0.1 → v0.2 变更溯源（评审 #2 处置）

| 评审问题 | 处置 | 落点 |
|---|---|---|
| #1 `SeedContainerAssociationStrategy` 非产品件（坐实） | D6：产品默认 null + 帧契约兼容设计 | §2 行 1 |
| #2 无产品 freshness evaluator（坐实，组合缺口） | D7：前置 change A（FRS 谱系产品 realization） | §0/§2 |
| #3 驱动缝 internal 不可达（坐实，Kernel 边界） | D8：前置 change B（最小公开化 + 白名单执法） | §0/§2/§7 |
| #4 基线 406/407 | **驳回**：评审树为 d5612615 前状态；HEAD 16f0f190 全量复跑 579/579（Kernel 407/407） | §5 |
