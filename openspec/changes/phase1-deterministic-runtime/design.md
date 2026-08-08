# Design: Phase 1 — Deterministic Runtime Architecture Proposal

> 对应宪章 §60-A。本文档是 Architecture Proposal；`specs/` 是 Minimum Contracts；
> `scenarios/catalog.md` 是 Phase 1 正式执行契约（SC-P1-001..005）；`tasks.md` 是实施清单。
> 设计原则: 宪章 §54（先设计后编码）、I-12（无需求不提前实现）、§60-F（避免 God Object / 重复 authority）。

## 1. Runtime Component Model（Phase 1 切片）

```
┌───────────────────────────────────────────────────────────────┐
│ Agent（UniClaw.Runtime.Agent）                                 │
│   RunState（Idle/Initializing/Running/Completed/Failed）      │
│   WorldBelief（Agent 代持，World/ 提供 reconcile 逻辑）         │
│   Goal + Plan（Plan 是 hypothesis, 不是 reality — I-5）        │
│   Active Container Stack / Container 切换 / 完成判定           │
└──────────────────────────┬────────────────────────────────────┘
                           │ bind / switch / step goal / verdict
┌──────────────────────────▼────────────────────────────────────┐
│ Container（UniClaw.Runtime.Container）                         │
│   Semantic Identity（Phase 1 = 显式规则注入，算法推迟 Phase 5） │
│   当前 Observation / candidates / visited / local progress     │
│   局部完成判断 / still-mine 判断（"观测是否仍属于自己"）        │
└──────────────────────────┬────────────────────────────────────┘
                           │ step goal（Select 候选）
┌──────────────────────────▼────────────────────────────────────┐
│ Traversal（UniClaw.Runtime.Traversal）                         │
│   Select → Check → Execute → Observe → Verify → Branch         │
│   step journal / retry / 语义失败上报（Trap 模型 Phase 2 引入）  │
└──────────────────────────┬────────────────────────────────────┘
                           │ ObserveAsync / ExecuteAsync
┌──────────────────────────▼────────────────────────────────────┐
│ IEnvironment（UniClaw.Runtime.Environment — port）             │
└──────────────────────────┬────────────────────────────────────┘
                           │
┌──────────────────────────▼────────────────────────────────────┐
│ 外部世界（Phase 1 = ScriptedEnvironment，测试侧 Fake，§33）     │
└───────────────────────────────────────────────────────────────┘

横切（全部组件只依赖不可变模型；TraceEvent 是不可变值，只上报不改写 — 裁决 5）:
- Model/        — 纯不可变模型: Observation（Elements / ForegroundApplication / SequenceNumber）/
                  ObservedElement（Text / SwitchState? / Index — SC-P1-005）/ WorldBelief（SemanticPage +
                  SourceObservationSequence，无场景字段）/ RecoveryAnchor（ApplicationIdentity /
                  ExpectedSemanticEntry / VerificationCriteria）/ Goal（含 evidence evaluator 注入点）/
                  GoalEvidence（Satisfied / Reason / SourceObservationSequence — SC-P1-003）/
                  Plan（target/action 由调用侧注入）/ DeviceAction（LaunchApp | Tap | SetSwitch，
                  Tap/SetSwitch 携带 TargetElementIndex — SC-P1-005）/ ActionResult（Dispatched /
                  TimedOut / Rejected）/ StartupResult（Ready(anchor) | NotReady(reason) — SC-P1-002）/
                  TraversalStepResult（Succeeded | Failed(reason) — SC-P1-004）/
                  TraceEvent（RunId/ContainerId/StepId/ActionId + Action?/Reason?/RunState?）/
                  RunState
                  （Fingerprint 不在本切片 — 裁决 2，DEFER 到 Scroll Identity Scenario；
                   Trap 模型不在本切片 — 裁决 4）
- TraceEvent    — 不可变值模型（RunId / ContainerId / StepId / ActionId 因果链）；Phase 1 由 Agent
                  持有 `List<TraceEvent>`，只追加不改写（I-2：唯一可变 owner 是 Agent）；
                  不建独立 Trace / Observability behavioral component
                  （persistence / export / metrics / spans DEFER）
- Startup/      — §19 启动程序（Agent 在 Initializing 阶段调用）
- World/        — Reconcile（Observation → WorldBelief 更新；belief 由 Agent 代持）
```

**目录调整说明**（相对 Phase 0 骨架新增两个目录）:
- `Startup/` — 宪章 §40 结构清单明确列出 Startup/；启动程序是明确的一次生命周期阶段（§19），不应塞进 Agent。
- `Environment/` — §8 Environment 是外部世界能力边界（观察 + 动作）；port 独立成目录表达 Spine（§56）。
- `Capabilities/{Vision,Device,AI,External}` 保持空目录（.gitkeep），Phase 4 接入真实 Adapter 时再落代码（I-12，不提前建 stub）。
- `Observability/` 目录本切片**不创建**：TraceEvent 是不可变值模型，Phase 1 由 Agent 持有（裁决 5）。

## 2. Ownership Table（每个 mutable state 有且只有一个 owner — I-2）

| 可变状态 | Owner | 宪章依据 |
|---------|-------|---------|
| RunState（全局生命周期） | Agent | §5（Agent 负责 Run-level lifecycle）、§18 |
| WorldBelief | Agent（World/ 提供 reconcile 逻辑，只更新不裁决） | §5、§10 |
| Active Container Stack / 当前 Container | Agent | §5、§6（Container 不得改 Agent 目标） |
| Container 局部状态（observation / candidates / visited / progress / 完成判断） | Container | §6 |
| Traversal 单步状态（selected / retry / step journal） | Traversal | §7 |
| 模拟世界状态（当前 Screen / transition 配置） | ScriptedEnvironment（fake） | §8、§33 |
| TraceEvent 列表 | Agent | §28 |

跨 owner 边界只传**不可变快照**（Model 层）或结果消息。禁止共享可变对象（I-2）。
旧 TraversalRuntimeContext 把 runtime state 与 belief 混成一个 God Context —— 本设计禁止（I-11）。

## 3. Dependency Diagram

```
Agent ──→ Startup            （Initializing 阶段调用启动程序）
Agent ──→ Container          （bind / switch / verdict）
Container ──→ Traversal      （下发 step goal）
Traversal ──→ IEnvironment   （observe / execute）
Agent ──→ World              （reconcile 更新 belief，Agent 代持）
全部 ──→ Model               （不可变，跨 owner 唯一载体）
TraceEvent = 各组件产生的不可变值 → Agent 持有追加（I-2：可变状态唯一 owner 是 Agent，裁决 5）
```

反向依赖不存在（I-1）。低层不得反向拿高层权威（I-8）：Container 不判定全局目标，Traversal 不裁决 Container identity。
**无 FSM**（I-7）：切片 1 的 protocol 用普通方法表达；FSM 是 protocol 表达工具不是目的（§17 条件未满足 → Deferred）。
机械保证：Guard 1（零 ProjectReference）/ Guard 2（零旧 namespace）/ Guard 3（文档导航）。

## 4. Runtime State Model

- **Runtime State**（§11，程序内部执行状态）与 **World Belief**（§10，程序认为现实是什么）**严格分离**。
  Runtime State = RunState + 各 owner 局部状态（见 Ownership Table）；World Belief = SemanticPage /
  Confidence / Evidence / SourceObservationSequence（对支撑观测序列的引用，裁决 2）。
  WorldBelief 不复制场景特定语义字段（如 WiFi Switch 状态）——Goal 完成判定直接基于 Observation evidence。
  ForegroundApplication 是 Observation 字段（Startup 消费，裁决 7）；ActiveContainer 是 Agent 的
  Runtime State（Container Stack），两者均不进入 WorldBelief。
- World Belief 允许 `Unknown / Uncertain / Conflicting`（§10）——证据不足时不得假装确定。
- Observation ≠ Semantic Truth（I-4）：Observation 只是 World Belief 的输入。
- Fingerprint 是 evidence，不是 identity（I-6）：Phase 1 Observation 不包含 Fingerprint 字段（裁决 2）—
  页面匹配用显式语义规则（页面名）。Fingerprint 字段与机制 DEFER 到 Scroll Identity Scenario
  （届时证明 FingerprintChanged ≠ ContainerChanged）。
- 元素引用（SC-P1-005）: ObservedElement.Index（观测内稳定序位，非坐标）是 grounding 结果与
  动作目标的引用载体；DeviceAction.Tap / SetSwitch 携带 TargetElementIndex（grounding 解析后的
  具体元素）。同文本多元素消歧（Text + SwitchState? 证据，state-bearing 优先）是 Runtime 侧
  Traversal.Select 行为；Environment 按元素身份应用物理效果，不替 Runtime 消歧（裁决 3：
  不引入 coordinate / hierarchy 模型）。
- 核心闭环（§2）: Observe → Reconcile → Decide → Execute → Observe → Verify → Update → Continue。
  每步动作后必须重新 Observe 再推进（§3：Internal Runtime State ≠ External World State）。
- 观测有序性用 SequenceNumber（确定性、单调递增）表达，不依赖真实时间（裁决 6）。
- ActionResult 只表达 dispatch outcome（Dispatched / TimedOut / Rejected），任何 dispatch 结果都
  不证明世界状态或 Goal 完成（裁决 10）。
- Goal 完成判定（SC-P1-003）: 每次 post-action Observation 后由 Goal evidence evaluator 评估；
  evaluator 对 Observation 产生 GoalEvidence（Satisfied / Reason / SourceObservationSequence）。
  Plan 步数耗尽 ≠ Completed；动作 dispatch 结果 ≠ Completed；仅 Satisfied 的 GoalEvidence →
  Agent 判定 Completed；否则 Plan 耗尽 / 证据不满足 → Failed（显式原因记录于 Trace）。
- 失败表达（SC-P1-004）: Traversal 无法推进 → `TraversalStepResult.Failed(原因)`（结构化结果，
  非异常、非静默）→ Container 只读转交 → Agent（最终 failure authority）判定 Run Failed。
  Phase 1 无 Trap 模型、无任何恢复执行（裁决 4）。

## 5. Normal Lifecycle（Phase 1 实现）

```
Run: Idle → Initializing → Running → Completed | Failed

Initializing（Agent 调用 Startup，§19）:
  Attach（fake no-op）→ Launch（ExecuteAsync(LaunchApp)）→ Observe
  → Verify ForegroundApplication（Observation 字段，确认已进入目标应用 — 裁决 7 的消费者，
      也是语义入口解析的依据）
  → Resolve Initial Semantic World（World/reconcile）
  → Establish Initial Container（Settings Main）
  → Establish RecoveryAnchor（§20 可信入口数据: ApplicationIdentity / ExpectedSemanticEntry /
      VerificationCriteria；EntryStrategy / RestoreRecipe 属恢复规划数据，Phase 2 消费时引入 — 裁决 8）
  → Ready（未 Ready 不得进入 Running）

Running（§34 期望生命周期 — 逐条对应）:
  Bind Settings Container
    → Traverse（Traversal.ExecuteStep: Select Network & Internet → Check →
       Execute Tap → Observe → Verify 导航生效）
  → Navigate（Agent 判定: 新 Observation 匹配 Network Container identity → Switch Active Container）
  → Bind Network Container
    → Traverse（Click WiFi）→ Navigate
  → Bind WiFi Container
    → Traverse（SetSwitch(WiFi Switch, ON)）→ Execute → Verify（Observation evidence: WiFi Switch = ON）
  → Goal Completed（I-10: evidence evaluator（调用侧注入）对最终 Observation evidence 判定成立
     并记录原因；dispatch 结果不构成完成证据）
  → Run Completed
```

Traverse 与 Navigate 的职责切分（I-3 单一 authority）:
- **Traverse** = Container 局部：在"已知当前语义页面"内可靠执行一小步（§7）。
- **Navigate** = Agent 全局：页面切换后的容器判定与切换（§5 Container 切换 authority 在 Agent）。
- 上述 Settings / Network / WiFi 名称与动作序列是 Scenario 输入（Plan / target 数据），
  生产 Runtime 不硬编码（裁决 3 / 11）。

失败分支（Phase 1 契约 — SC-P1-002 / SC-P1-003 / SC-P1-004）:
```
Initializing 失败（SC-P1-002）:
  Startup 报告 NotReady(原因) → Agent 判定 RunState: Initializing → Failed
  （从未进入 Running；RecoveryAnchor 未建立；无恢复动作 — action history 只含 Launch + Observe）

Running 失败（SC-P1-004）:
  Traversal 无法推进 → TraversalStepResult.Failed(原因) → Container 只读转交 →
  Agent（最终 failure authority）判定 RunState → Failed（显式原因；无恢复动作）

证据不满足（SC-P1-003 负向）:
  Plan 耗尽 / 最终 post-action Observation 后 evaluator 产出 NotSatisfied →
  RunState → Failed（显式原因记录于 Trace）
```
三者的共同点: Run 终止 authority 永远在 Agent（RunState 唯一 owner）；低层只上报结构化结果（I-8）。

## 6. Trap / Recovery（宪章概念；Phase 1 只购买 SC-P1-004 的最小 escalate 表面 — 裁决 4）

- **Trap**（§21）是一等 Runtime 模型，Trap ≠ Exception。概念保留在宪章；Trap 结果类型、TrapKind、
  TrapScope、Source / Expected / Observed / Recoverability 等字段**不在 Phase 1 引入**（裁决 4）：
  无 trap 场景需求，无需求不建模型（I-12）。
- **SC-P1-004 购买的最小 escalate 表面**: Traversal 无法推进时必须返回结构化结果
  `TraversalStepResult.Failed(FailureReason)`（非异常、非静默），经 Container 只读转交上报 Agent；
  Agent 是最终 failure authority — Run 终止决策只能由 Agent 发出（I-8 的 escalate 半句；
  recovery 半句属 Phase 2）。Result 不携带 Expected / Observed 世界快照字段
  （当前 assertion 不需要 — 裁决 4）。
- **Phase 1 失败路径**: "无法推进 / 证据不满足 / Startup 失败 → Run Failed（trace 记录显式原因）"。
  语义失败以 Result 表达（结构化原因），不是 Trap 类型。
- **Recovery**（§23/§35，act → observe → verify → reconcile → resume，I-9）与 Recovery 执行机制
  （RecoveryRequest / RecoveryRuntime / recovery verification / RestoreRecipe 消费）全部由 Phase 2
  （§60-E）Failure / Recovery Scenario 引入（届时实现 Launcher drift 场景）。
  Phase 1 任何失败路径都不执行恢复动作。

## 6b. Phase 1 Scenario Catalog（SC-P1-001 — SC-P1-005）

正式执行契约见 `scenarios/catalog.md`。5 个 Scenario 共享单一 Runtime slice（裁决 7）：
差异仅为 ScriptedEnvironment 数据变体（happy / startup-fg-fail / switch-stuck / missing-target /
same-text）+ 注入的 Plan / Goal / evidence evaluator / identity 规则 + 断言。Golden Contract
（SC-P1-001）不得回退（裁决 1）。

| Scenario | 类型 | 验证重点 | 主要消费组件 |
|----------|------|---------|-------------|
| SC-P1-001 Normal WiFi Happy Path | Normal Execution | 完整 Observe→Act→Verify 闭环 + GoalEvidence + Trace 因果链 | Agent / Container / Traversal / Startup / Environment |
| SC-P1-002 Startup Foreground Verification | Startup Failure | 失败时不 Ready / 不 Running / 无 anchor / 无恢复执行 | Startup / Agent |
| SC-P1-003 Goal Evidence Completion | Completion Evidence | Plan 耗尽 ≠ 完成；dispatch ≠ 完成；evidence 才完成（正 / 负向变体） | Agent / Goal evaluator |
| SC-P1-004 Escalation Without Stealing Authority | Escalation / Failure Authority | Traversal 结构化失败结果；Agent 最终 authority；无越权动作 | Traversal / Container / Agent |
| SC-P1-005 Same-text Disambiguation | Grounding | state-bearing 元素优先；无 coordinate / hierarchy 模型 | Traversal.Select / Container / Environment |

## 7. Minimal Project Structure

按宪章 §40 + Phase 0 骨架，本切片落地:

```
src/UniClaw.Runtime/
  Agent/           Agent（Run 控制者：lifecycle + belief + container stack + plan 驱动 +
                   证据评估循环 + 最终 failure authority）
  Startup/         Startup（§19 程序）+ StartupResult + RecoveryAnchor 建立
  Container/       Container（identity 规则注入 / 局部状态 / 完成判断 / still-mine / 步骤结果转交）
  Traversal/       Traversal（Select→Check→Execute→Observe→Verify→Branch + grounding 消歧 +
                   TraversalStepResult）
  World/           Reconcile（Observation → WorldBelief）
  Environment/     IEnvironment（port: ObserveAsync / ExecuteAsync）
  Model/           Observation（ForegroundApplication / SequenceNumber）/ ObservedElement（Text /
                   SwitchState? / Index）/ WorldBelief / RecoveryAnchor / Goal（evidence evaluator
                   注入点）/ GoalEvidence / Plan / DeviceAction（LaunchApp | Tap | SetSwitch +
                   TargetElementIndex）/ ActionResult（Dispatched/TimedOut/Rejected）/ StartupResult /
                   TraversalStepResult / TraceEvent（+ Action?/Reason?/RunState?）/ RunState
                   （Fingerprint 不在本切片 — 裁决 2；Trap 模型不在本切片 — 裁决 4；
                    Observability/ 目录不创建，TraceEvent 由 Agent 持有 — 裁决 5）
tests/UniClaw.Runtime.Tests/
  Scenario/        SC-P1-001..005 场景测试（§42 优先级）+ Fakes/ScriptedEnvironment
                   （§33 确定性模拟，数据变体工厂：happy / startup-fg-fail / switch-stuck /
                   missing-target / same-text）
  Architecture/    既有 Guard（继续守护，不减弱）+ 可选新增架构断言（无 Trap 类型 — SC-P1-004 断言 5）
```

每个核心类落地前回答 §48 的九个问题；每个接口证明自己有价值（§49），否则不建（I-12）。

## 7b. 新购类型 §48 九问（Contract Reconciliation）

本轮（SC-P1-002/003/004/005 纳入契约）新购 3 个结果值类型 + 若干字段，全部回答 §48 九问：

### StartupResult（SC-P1-002 / SC-P1-001 消费）

| 问题 | 答案 |
|------|------|
| Purpose | 结构化表达 Startup 阶段结果：Ready(RecoveryAnchor) 或 NotReady(显式原因)，使 Running 门控与 Startup 失败路径可断言 |
| Owns | 无可变状态（不可变值） |
| Does Not Own | 不拥有 RecoveryAnchor（Ready 携带的是不可变值）、不决定 Run 去向（Agent 决定） |
| Inputs | Startup 程序执行结果（§19 顺序） |
| Outputs | Ready(RecoveryAnchor) \| NotReady(reason) |
| Authority | 报告 Ready/NotReady 的 authority 在 Startup 程序；判定 Run 去向的 authority 在 Agent |
| Lifecycle | 每次 Startup 调用产生一个值；Agent 消费后丢弃 |
| Failure | 不抛异常 — NotReady(reason) 即失败表达（§45 Result 语义） |
| Dependencies | RecoveryAnchor |

### GoalEvidence（SC-P1-001 / SC-P1-003 消费）

| 问题 | 答案 |
|------|------|
| Purpose | 完成判定的证据值（I-10）：evaluator 对 Observation 的判定（满足/不满足 + 原因 + 证据来源观测序号） |
| Owns | 无可变状态（不可变值） |
| Does Not Own | 不承担完成判定决策（Agent 判定）；不是 GoalEvidenceSpec 层级（spec 层级仍 DEFER — 裁决 3） |
| Inputs | post-action Observation + Goal 条件（调用侧注入） |
| Outputs | Satisfied / NotSatisfied + Reason + SourceObservationSequence |
| Authority | evaluator 只有报告证据的 authority；「是否 Completed」的 authority 在 Agent |
| Lifecycle | 每次评估产生；Agent 记录于 Trace 后丢弃 |
| Failure | 不抛异常 — NotSatisfied 是合法结果（不构成完成） |
| Dependencies | Observation |

### TraversalStepResult（SC-P1-001 / SC-P1-004 消费）

| 问题 | 答案 |
|------|------|
| Purpose | 单步执行的结构化结果：Succeeded 或 Failed(原因)；是 SC-P1-004 的 escalate 表面（§45） |
| Owns | 无可变状态（不可变值） |
| Does Not Own | 不决定 Run 失败（Agent 决定）、不携带恢复意图（Trap 模型 Phase 2） |
| Inputs | Traversal 单步执行状态（Select/Check/Execute/Observe/Verify） |
| Outputs | Succeeded \| Failed(FailureReason) |
| Authority | 报告步骤结果的 authority 在 Traversal；「Run 终止」authority 在 Agent |
| Lifecycle | 每步产生一次；Container 只读转交 Agent |
| Failure | 不抛异常 — Failed(reason) 是结构化失败表达 |
| Dependencies | 无（纯值） |

### 新购字段（全部有断言消费，裁决 9）

| 字段 | 消费 Scenario | 验证断言 | 删除后谁错 |
|------|--------------|---------|-----------|
| ObservedElement.Index | SC-P1-005 / SC-P1-001 | 005 断言 1（目标元素 == 开关元素 Index） | SC-P1-005（同文本元素无法区分） |
| DeviceAction.TargetElementIndex | SC-P1-001 / SC-P1-004 / SC-P1-005 | 005 断言 1 + 004 无越权动作（action history） | SC-P1-005（Environment 被迫替 Runtime 消歧 = 泄漏任务决策） |
| TraceEvent.Action? | SC-P1-001 / SC-P1-005 | 001 断言 6（因果链）+ 005 断言 1 | SC-P1-005（无法从 Trace 证明动作作用于哪个元素） |
| TraceEvent.Reason? | SC-P1-001 / 002 / 003 / 004 | 001 断言 4-5 + 002 断言 2 + 003 断言 3/5 + 004 断言 2（显式原因） | SC-P1-002/003/004（「显式原因」断言无观察面） |
| TraceEvent.RunState? | SC-P1-001 / SC-P1-002 | 001 断言 1（生命周期顺序）+ 002 断言 1（从未 Running） | SC-P1-002（「never enters Running」无法确定性断言） |

## 8. Deferred Decisions（§58 — 不为显得完整猜未来设计）

| 决策 | 推迟到 | 原因 |
|------|--------|------|
| Container / Traversal FSM | §17 条件满足时（Phase 3+） | 切片 1 protocol 简单；FSM 是表达工具不是目的 |
| Recovery 执行机制（RecoveryRequest/Runtime/verification） | Phase 2（§60-E） | I-12，Normal 场景无需求 |
| Semantic Identity 算法（语义页面解析） | Phase 5 | 切片 1 = 注入显式规则（页面名匹配） |
| Plan 合成（从 Goal 自动生成 Plan） | Phase 5/6 | 切片 1 Plan 是测试提供的任务规格；Agent 拥有并驱动它（§5）；target/action 数据由调用侧注入，Runtime 不硬编码场景字符串（裁决 11） |
| Memory | Phase 5 | I-12 |
| LLM / VLM | Phase 5 | §57-12：不依赖 LLM 完成确定性测试 |
| DI 容器 | 不引入 | 构造器注入 + 测试侧组合根 |
| 真实设备 / Vision Adapter | Phase 4 | §33 第一阶段不连真实手机 |
| Scroll / Dynamic Grounding / Popup | Phase 3 | §39 |
| 时钟 / 延迟重试策略 | Phase 3 | 切片 1 用确定性序列号替代时间戳 | 
| Trap 结果模型（TrapKind/Scope/Expected/Observed） / Recovery 执行机制 | Phase 2（§60-E） | 裁决 4：SC-P1-004 只购买 TraversalStepResult 最小 escalate 表面，无 trap 模型 |
| Fingerprint 字段与机制 | Scroll Identity Scenario（裁决 2） | Observation 无 Fingerprint；I-6 原则保留 |
| Coordinate / Hierarchy-based grounding | 未来场景购买时（裁决 3） | SC-P1-005 只使用 Text + SwitchState? 证据 |
| RunResult / ContainerResult 结果类型 | 无断言消费（裁决 9） | Trace + RunState 已覆盖失败原因与生命周期断言 |
| Expected / Observed 世界快照字段（result 内） | Phase 2 Trap（裁决 4） | SC-P1-004 断言不需要 |
| Trace 持久化 / export / metrics / spans | Phase 2+ | 裁决 5：Phase 1 TraceEvent 仅内存列表（Agent 持有） |
| Pause / Shutdown 完整语义 | §46 需求出现时 | 枚举预留 Terminated |

## 9. Phase 1 完成标准对照（§57 — 本切片覆盖项）

| §57 标准 | 覆盖 | 说明 |
|---------|------|------|
| 1. Normal 全 Fake 跑通 | ✓ 本切片 | NormalWifiScenarioTests |
| 6. Startup 建立 RecoveryAnchor | ✓ 本切片 | Startup 契约 |
| 8. Global lifecycle 与 Traversal 分离 | ✓ 本切片 | RunState 无智能；Traversal 无生命周期 authority |
| 9. 单 owner | ✓ 本切片 | Ownership Table + 不可变跨边界 |
| 10. Dependency Guard | ✓ 已有 | ArchitectureGuardTests Guard 1/2/3 |
| 11. Trace 可解释每步 | ✓ 本切片 | TraceEvent 因果链（Agent 持有 List<TraceEvent>，裁决 5） |
| 12. 无 LLM 跑完确定性测试 | ✓ 本切片 | 无任何 AI 依赖 |
| 2–5. Recovery / Scroll / Uncertain / Popup | Phase 2/3 | 对应 §60-E 与 §39 后续阶段 |
| SC-P1-002..005（Catalog） | ✓ 本切片 | Startup 失败路径 / evidence 完成判定 / escalate 不偷权 / 同文本消歧 — Golden Contract 之上新增 Scenario pressure（裁决 1，不回退） |
