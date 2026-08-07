# Design: Phase 1 — Deterministic Runtime Architecture Proposal

> 对应宪章 §60-A。本文档是 Architecture Proposal；`specs/` 是 Minimum Contracts；`tasks.md` 是实施清单。
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
│   step journal / retry / 结构化 trap 上报（Trap ≠ Exception）   │
└──────────────────────────┬────────────────────────────────────┘
                           │ ObserveAsync / ExecuteAsync
┌──────────────────────────▼────────────────────────────────────┐
│ IEnvironment（UniClaw.Runtime.Environment — port）             │
└──────────────────────────┬────────────────────────────────────┘
                           │
┌──────────────────────────▼────────────────────────────────────┐
│ 外部世界（Phase 1 = ScriptedEnvironment，测试侧 Fake，§33）     │
└───────────────────────────────────────────────────────────────┘

横切（全部组件只依赖不可变模型，只写 trace）:
- Model/        — 纯不可变模型: Observation / ScreenElement / Fingerprint / WorldBelief /
                  RecoveryAnchor / Goal / Plan / DeviceAction / ActionResult / Trap / TraceEvent
- Observability/ — Trace 因果链（RunId / ContainerId / StepId / ActionId），只写不改业务
- Startup/      — §19 启动程序（Agent 在 Initializing 阶段调用）
- World/        — Reconcile（Observation → WorldBelief 更新；belief 由 Agent 代持）
```

**目录调整说明**（相对 Phase 0 骨架新增两个目录）:
- `Startup/` — 宪章 §40 结构清单明确列出 Startup/；启动程序是明确的一次生命周期阶段（§19），不应塞进 Agent。
- `Environment/` — §8 Environment 是外部世界能力边界（观察 + 动作）；port 独立成目录表达 Spine（§56）。
- `Capabilities/{Vision,Device,AI,External}` 保持空目录（.gitkeep），Phase 4 接入真实 Adapter 时再落代码（I-12，不提前建 stub）。

## 2. Ownership Table（每个 mutable state 有且只有一个 owner — I-2）

| 可变状态 | Owner | 宪章依据 |
|---------|-------|---------|
| RunState（全局生命周期） | Agent | §5（Agent 负责 Run-level lifecycle）、§18 |
| WorldBelief | Agent（World/ 提供 reconcile 逻辑，只更新不裁决） | §5、§10 |
| Active Container Stack / 当前 Container | Agent | §5、§6（Container 不得改 Agent 目标） |
| Container 局部状态（observation / candidates / visited / progress / 完成判断） | Container | §6 |
| Traversal 单步状态（selected / retry / step journal） | Traversal | §7 |
| 模拟世界状态（当前 Screen / transition 配置） | ScriptedEnvironment（fake） | §8、§33 |
| Trace 日志 | Observability | §28 |

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
全部 ──→ Observability       （只写 trace）
```

反向依赖不存在（I-1）。低层不得反向拿高层权威（I-8）：Container 不判定全局目标，Traversal 不裁决 Container identity。
**无 FSM**（I-7）：切片 1 的 protocol 用普通方法表达；FSM 是 protocol 表达工具不是目的（§17 条件未满足 → Deferred）。
机械保证：Guard 1（零 ProjectReference）/ Guard 2（零旧 namespace）/ Guard 3（文档导航）。

## 4. Runtime State Model

- **Runtime State**（§11，程序内部执行状态）与 **World Belief**（§10，程序认为现实是什么）**严格分离**。
  Runtime State = RunState + 各 owner 局部状态（见 Ownership Table）；World Belief = ForegroundApplication /
  SemanticPage / ActiveContainer / Confidence / Evidence / Timestamp。
- World Belief 允许 `Unknown / Uncertain / Conflicting`（§10）——证据不足时不得假装确定。
- Observation ≠ Semantic Truth（I-4）：Observation 只是 World Belief 的输入。
- Fingerprint 是 evidence，不是 identity（I-6）：切片 1 的页面匹配用显式语义规则（页面名），不用指纹当身份。
- 核心闭环（§2）: Observe → Reconcile → Decide → Execute → Observe → Verify → Update → Continue。
  每步动作后必须重新 Observe 再推进（§3：Internal Runtime State ≠ External World State）。

## 5. Normal Lifecycle（Phase 1 实现）

```
Run: Idle → Initializing → Running → Completed | Failed

Initializing（Agent 调用 Startup，§19）:
  Attach（fake no-op）→ Launch（ExecuteAsync(LaunchApp)）→ Observe
  → Resolve Initial Semantic World（World/reconcile）
  → Establish Initial Container（Settings Main）
  → Establish RecoveryAnchor（§20: ApplicationIdentity / EntryStrategy /
      ExpectedSemanticEntry / RestoreRecipe / VerificationCriteria）
  → Ready（未 Ready 不得进入 Running）

Running（§34 期望生命周期 — 逐条对应）:
  Bind Settings Container
    → Traverse（Traversal.ExecuteStep: Select Network & Internet → Check →
       Execute Tap → Observe → Verify 导航生效）
  → Navigate（Agent 判定: 新 Observation 匹配 Network Container identity → Switch Active Container）
  → Bind Network Container
    → Traverse（Click WiFi）→ Navigate
  → Bind WiFi Container
    → Traverse（Toggle WiFi Switch）→ Execute → Verify（WiFi Switch = ON）
  → Goal Completed（I-10: 必须有 Goal Evidence，禁止无证据启发式完成）
  → Run Completed
```

Traverse 与 Navigate 的职责切分（I-3 单一 authority）:
- **Traverse** = Container 局部：在"已知当前语义页面"内可靠执行一小步（§7）。
- **Navigate** = Agent 全局：页面切换后的容器判定与切换（§5 Container 切换 authority 在 Agent）。

## 6. Trap / Recovery Lifecycle（设计定义；切片 1 只实现结果类型）

- **Trap**（§21）是一等 Runtime 模型，Trap ≠ Exception。Trap = "当前执行所依赖的状态假设可能失效"。
  切片 1 定义 `TrapKind { TargetNotFound / ActionFailed / UnexpectedPage }` + Source + Scope +
  Expected / Observed + LastAction（§22/§45 语义结果）。
- **上报路径**（§56 异常路径）: Traversal / Container 检测 → 结构化上报 → Agent 判定 Scope（I-8 escalate 不偷权）。
- **Recovery**（§23/§35）: act → observe → verify → reconcile → resume；不是单个 PressBack（I-9）。
- **Phase 1 实现边界**（I-12，诚实最小化）: Normal 场景无 trap。切片 1 只实现 Trap 结果类型 +
  "无法推进 → Run Failed（trace 说明原因）"路径。Recovery 执行机制（RecoveryRequest / RecoveryRuntime /
  recovery verification）= Phase 2 change（§60-E），届时实现 Launcher drift 场景。

## 7. Minimal Project Structure

按宪章 §40 + Phase 0 骨架，本切片落地:

```
src/UniClaw.Runtime/
  Agent/           Agent（Run 控制者：lifecycle + belief + container stack + plan 驱动）
  Startup/         Startup（§19 程序）+ RecoveryAnchor 建立
  Container/       Container（identity 规则注入 / 局部状态 / 完成判断 / still-mine）
  Traversal/       Traversal（Select→Check→Execute→Observe→Verify→Branch）
  World/           Reconcile（Observation → WorldBelief）
  Environment/     IEnvironment（port: ObserveAsync / ExecuteAsync）
  Model/           Observation / ScreenElement / Fingerprint / WorldBelief / RecoveryAnchor /
                   Goal / Plan / DeviceAction / ActionResult / Trap / TraceEvent
  Observability/   Trace（RunId / ContainerId / StepId / ActionId 因果链）
tests/UniClaw.Runtime.Tests/
  Scenario/        NormalWifiScenarioTests（§34 端到端）+ Fakes/ScriptedEnvironment（§33 确定性模拟）
  Architecture/    既有 Guard（继续守护，不减弱）
```

每个核心类落地前回答 §48 的九个问题；每个接口证明自己有价值（§49），否则不建（I-12）。

## 8. Deferred Decisions（§58 — 不为显得完整猜未来设计）

| 决策 | 推迟到 | 原因 |
|------|--------|------|
| Container / Traversal FSM | §17 条件满足时（Phase 3+） | 切片 1 protocol 简单；FSM 是表达工具不是目的 |
| Recovery 执行机制（RecoveryRequest/Runtime/verification） | Phase 2（§60-E） | I-12，Normal 场景无需求 |
| Semantic Identity 算法（语义页面解析） | Phase 5 | 切片 1 = 注入显式规则（页面名匹配） |
| Plan 合成（从 Goal 自动生成 Plan） | Phase 5/6 | 切片 1 Plan 是测试提供的任务规格；Agent 拥有并驱动它（§5） |
| Memory | Phase 5 | I-12 |
| LLM / VLM | Phase 5 | §57-12：不依赖 LLM 完成确定性测试 |
| DI 容器 | 不引入 | 构造器注入 + 测试侧组合根 |
| 真实设备 / Vision Adapter | Phase 4 | §33 第一阶段不连真实手机 |
| Scroll / Dynamic Grounding / Popup | Phase 3 | §39 |
| 时钟 / 延迟重试策略 | Phase 3 | 切片 1 用确定性序列号替代时间戳 |
| Pause / Shutdown 完整语义 | §46 需求出现时 | 枚举预留 Terminated |

## 9. Phase 1 完成标准对照（§57 — 本切片覆盖项）

| §57 标准 | 覆盖 | 说明 |
|---------|------|------|
| 1. Normal 全 Fake 跑通 | ✓ 本切片 | NormalWifiScenarioTests |
| 6. Startup 建立 RecoveryAnchor | ✓ 本切片 | Startup 契约 |
| 8. Global lifecycle 与 Traversal 分离 | ✓ 本切片 | RunState 无智能；Traversal 无生命周期 authority |
| 9. 单 owner | ✓ 本切片 | Ownership Table + 不可变跨边界 |
| 10. Dependency Guard | ✓ 已有 | ArchitectureGuardTests Guard 1/2/3 |
| 11. Trace 可解释每步 | ✓ 本切片 | 因果链 Trace 契约 |
| 12. 无 LLM 跑完确定性测试 | ✓ 本切片 | 无任何 AI 依赖 |
| 2–5. Recovery / Scroll / Uncertain / Popup | Phase 2/3 | 对应 §60-E 与 §39 后续阶段 |
