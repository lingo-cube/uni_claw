# Phase 1 Scenario Catalog（SC-P1-001 — SC-P1-005）

> **Authority**: 本 Catalog 是 Phase 1 的 **authoritative Active Scenario set 与 Phase acceptance evidence**。
> 它不是 normative capability spec，不是 unique semantic truth source，也不是 OpenSpec specs 的替代品。
>
> **Scenario-first, Spec-authoritative.**
>
> - **Architecture Contract**（`docs/system/constitution/runtime-architecture-contract.md`）— invariant truth
> - **OpenSpec capability specs**（`specs/environment` / `run-lifecycle` / `container-traversal` / `normal-wifi-scenario`）— normative semantic / behavioral contract，定义 Runtime **SHALL** 做什么
> - **Scenario Catalog**（本文件）— 定义 Phase contract 如何被 exercise 与 prove；capability Specs 定义 Runtime **SHALL** 做什么
> - **Tasks**（`tasks.md`）— implementation work plan

## Requirement

Phase 1 的 acceptance 由 5 个 Active Scenario 组成。每个 Scenario 正式定义：Goal、
Initial World（ScriptedEnvironment 数据变体）、Observation → Action → Observation 序列、
Goal Evidence、Expected authority、Expected action order 与 Assertions。
5 个 Scenario 共享同一 Runtime implementation slice（裁决 7）——差异只存在于
ScriptedEnvironment 数据变体、注入的 Goal / Plan / evidence evaluator / container identity 规则与断言。

## Motivation

宪章 §42（Scenario Tests 优先级高于大量 Unit Tests：NormalExecution / StartupFailure 等类型）、
§57（第一阶段完成标准）、I-8（Lower scope 可以 escalate，不能偷取 higher-scope authority）、
I-10 / §43（Completion 必须由 Goal Evidence 证明）。
Normal WiFi Golden Contract（`specs/normal-wifi-scenario`）是本 change 的 baseline；
本 Catalog 增加 Scenario pressure，但不回退 Golden Contract 已有模型（裁决 1）。

## SC-P1-001 — Normal WiFi Happy Path

> 详细 SHALL 见 `specs/normal-wifi-scenario`（本场景的 Golden 契约）。

### Goal

Enable WiFi。Completion evidence：post-action Observation 中 WiFi Switch = ON。

### Initial World（数据变体 happy）

```
Screen 1:   Settings Main     — "Network & Internet"（SwitchState=null），Tap → Screen 2
Screen 2:   Network Settings  — "WiFi"（SwitchState=null），Tap → Screen 3
Screen 3:   WiFi Settings     — 标题 "WiFi"（SwitchState=null）；开关 "WiFi"（SwitchState=false），
                                SetSwitch(ON) → Screen 3'
Screen 3':  WiFi Settings     — 开关 "WiFi" SwitchState=true
```

### Sequence（Observe → Action → Observe → Reconcile）

```
Settings Main 可见（ForegroundApplication=Settings, seq=S1）
  → Tap("Network & Internet") → 重新 Observe → Network Settings（seq=S2）
  → Tap("WiFi") → 重新 Observe → WiFi Settings（seq=S3）
  → SetSwitch("WiFi", ON) → 重新 Observe → 开关 SwitchState=true（seq=S4）
  → Goal evaluator（对 S4 评估）→ GoalEvidence Satisfied
  → Completed（Run Completed）
```

### Expected authority

- 导航判定 / Container 切换（Navigate / Rebind / Switch）：Agent
- 单步执行（Select → Check → Execute → Observe → Verify → Branch）：Traversal
- 候选 grounding / 消歧：Container 提供 candidates，Traversal.Select 选择
- 完成判定：Agent，基于注入 evaluator 产生的 GoalEvidence

### Expected action order

`LaunchApp → Tap(Network & Internet) → Tap(WiFi) → SetSwitch(WiFi switch, ON)`

### Assertions

1. 生命周期顺序（Trace 中 RunState 转移）：Idle → Initializing → Running → Completed
2. Startup 建立 RecoveryAnchor 且 Verify ForegroundApplication 通过
3. 每个动作后有 post-action Observation（SequenceNumber 单调递增）
4. GoalEvidence.Satisfied == true 且 SourceObservationSequence == 最终 post-action Observation 序号
5. Completed 事件在 dispatch 事件与 post-action Observation 评估之后（dispatch ≠ completed — SC-P1-003）
6. Trace 因果链完整（RunId / ContainerId / StepId / ActionId + 动作载荷 + 原因），可解释每步
7. 同一输入重复运行产生完全相同的事件序列（确定性、可重放）

## SC-P1-002 — Startup Foreground Verification 失败

### Goal

验证失败路径：可信 Runtime 尚未建立时不得进入正式执行（§19）——Startup 失败时：
Startup not Ready、Run never enters Running、RecoveryAnchor not established、
Run Failed with explicit reason、no recovery execution。

### Initial World（数据变体 startup-fg-fail）

ScriptedEnvironment：LaunchApp 后 ForegroundApplication != 目标应用（如 "Launcher"）。

### Sequence

```
Run 开始（Initializing）
  → Startup: Attach → ExecuteAsync(LaunchApp) → Observe
  → Verify ForegroundApplication：观测 foreground != 期望应用 → 验证失败
  → Startup 报告 NotReady(原因)
  → Agent 判定: RunState Initializing → Failed（记录显式原因）；从未进入 Running
  → RecoveryAnchor 未建立；无任何恢复动作
```

### Expected authority

- Startup 报告 Ready / NotReady：Startup 程序（§19）
- Run 终止 authority：Agent（RunState 唯一 owner）

### Expected action order

`LaunchApp`（之后无任何动作 — 证明无恢复执行）

### Assertions

1. RunState 从未进入 Running（Trace 中无 Running 转移事件）
2. StartupResult == NotReady(显式原因)，原因记录于 Trace
3. RecoveryAnchor 未建立（Agent 无 anchor）
4. RunState 最终 == Failed
5. Environment action history 仅含 [LaunchApp]（无 recovery 动作）
6. 无 Container 绑定、无 Traversal 执行（Trace 中无 Container / Step 事件）

## SC-P1-003 — Goal Evidence Completion

### Goal

锁定 I-10 / §43：Plan exhausted ≠ Completed；Action Dispatched ≠ Completed；
只有 Goal evaluator 从 post-action Observation 产生 explicit Goal Evidence 才能 Completed。

### 正向变体（happy world，同 SC-P1-001）

注入 evaluator：仅当 Observation 中开关 SwitchState == true 时 Satisfied。

### 负向变体（数据变体 switch-stuck）

ScriptedEnvironment：SetSwitch(ON) 不改变开关状态（开关物理卡住，SwitchState 保持 false）；
注入诚实 evaluator（要求 SwitchState == true）。

### Sequence（正向）

```
... → SetSwitch("WiFi", ON) → dispatch 结果 Dispatched
  → （dispatch 后 Run 未进入 Completed — dispatch ≠ completed）
  → 重新 Observe → 开关 SwitchState=true（seq=S4）
  → evaluator 对 S4 评估 → GoalEvidence Satisfied（Reason, SourceObservationSequence=S4）
  → Completed
```

### Sequence（负向）

```
... → SetSwitch("WiFi", ON) → 重新 Observe → 开关 SwitchState 仍为 false
  → Plan 步数耗尽 → evaluator 对最终 Observation 评估 → NotSatisfied
  → RunState → Failed（显式原因记录于 Trace；不是 Completed）
```

### Expected authority

- 完成 / 失败判定：Agent（基于 evaluator 的 GoalEvidence）；evaluator 只报告证据，不判定 Run 去向

### Assertions

1. SetSwitch dispatch（Dispatched）发生后 Run 未进入 Completed（Trace 中 Completed 事件必须位于
   dispatch 事件与 post-action Observation 评估之后）
2. GoalEvidence.SourceObservationSequence == 该 post-action Observation 的序号
   （测试侧在 ObserveAsync 外层捕获 sequence — 证据来自观察，不是 dispatch 结果）
3. 完成原因记录于 Trace（GoalEvidence.Reason）
4. 负向：Plan 步数耗尽 + 证据不满足 → RunState 最终 == Failed（不是 Completed）
5. 负向：失败原因显式记录；无任何恢复动作（action history 无额外动作）

## SC-P1-004 — Escalation Without Stealing Authority

### Goal

验证 I-8 的 escalate 半句（recovery 半句属 Phase 2）：Traversal 无法推进时必须返回明确的
structured execution result，Agent 是最终 failure authority。本场景只购买该最小 escalate 表面；
不引入 Trap / TrapKind / TrapScope / RecoveryRequest / Recovery Runtime（裁决 4）。

### Initial World（数据变体 missing-target）

ScriptedEnvironment：Screen 2（Network Settings）只含 "Bluetooth"（无 "WiFi"）。

### Sequence

```
... → Tap("Network & Internet") → 重新 Observe → Network Settings（无 "WiFi" 候选）
  → Traversal.Select：目标 "WiFi" 在当前 Observation 无候选 → Check 失败
  → TraversalStepResult.Failed(原因)（结构化结果，非异常、非静默）
  → Container 转交上报 Agent（不自行恢复、不判定 Run 失败）
  → Agent（最终 failure authority）：RunState → Failed（记录显式原因）
```

### Expected authority

- 步骤失败结果：Traversal（TraversalStepResult.Failed + 原因）
- 上报路径：Container（只读转交，不裁决）
- Run 终止 authority：Agent（唯一可以终结 Run 的组件 — RunState 唯一 owner）

### Expected action order

`LaunchApp → Tap(Network & Internet)`（之后无任何动作）

### Assertions

1. Traversal 返回结构化失败结果（Trace 中 StepId 关联 Failed 结果 + 非空原因）
2. RunState 最终 == Failed，显式原因记录于 Trace
3. 无恢复动作（action history 长度 == 2；无 PressBack / 重新 Launch / 重试动作）
4. Run 终止由 Agent 判定（RunState 唯一 owner 是架构保证）；行为面由断言 3（无越权动作）覆盖
5. 架构断言：生产 Model 层不出现 Trap / TrapKind / TrapScope / RecoveryRequest 类型
   （可加入 ArchitectureGuardTests）

## SC-P1-005 — Same-text Element Disambiguation

### Goal

同文本元素消歧：Text="WiFi" 同时以 SwitchState=null（标题）和 SwitchState=false（开关）出现时，
grounding 必须选择 state-bearing 开关元素。不新增 coordinate / hierarchy model（裁决 3）；
coordinate-based 与 hierarchy-based grounding 均 DEFER 到未来场景购买。

### Initial World（数据变体 same-text）

```
Screen 3:  WiFi Settings — 标题元素: Text="WiFi", SwitchState=null
                          — 开关元素: Text="WiFi", SwitchState=false, SetSwitch(ON) → true
物理世界语义: SetSwitch 作用于非开关元素 → ActionResult.Rejected（物理能力，非任务决策）
```

### Sequence

```
... → WiFi Settings（两个 "WiFi" 元素，各有稳定 Index）
  → Traversal.Select：目标 "WiFi"（SetSwitch）→ 消歧规则：非 null SwitchState 优先
    → 选中开关元素（Index = 开关元素 Index）
  → Execute: SetSwitch("WiFi", TargetElementIndex=开关元素, ON)（动作携带 grounding 解析后的元素引用）
  → 重新 Observe → 开关 SwitchState=true，标题元素 SwitchState 仍为 null
  → evaluator Satisfied → Completed
```

### Expected authority

- 消歧 / grounding：Traversal.Select（仅使用 Text + SwitchState? 证据）
- 物理效果应用：Environment（按元素身份应用效果，不替 Runtime 决定选哪个元素）

### Assertions

1. Trace 中 SetSwitch 动作的 TargetElementIndex == 开关元素在观测中的 Index（≠ 标题元素 Index）
2. post-action Observation：开关 SwitchState=true，标题元素 SwitchState 仍为 null
3. 错误路径对照：若动作作用于标题元素 → ActionResult.Rejected（环境不替 Runtime 消歧）
4. 架构断言：生产 Model / 行为中无 coordinate / hierarchy 字段或模型

## 共享契约与共享 Runtime Slice（裁决 7）

- 5 个 Scenario 共享同一 Runtime implementation slice（Agent / Container / Traversal / Startup /
  Environment port / Model）。Scenario 间差异仅为：ScriptedEnvironment 数据变体
  （happy / startup-fg-fail / switch-stuck / missing-target / same-text）、注入的
  Goal / Plan / evidence evaluator / container identity 规则、断言。
- 不得为任一 Scenario 创建独立 Runner / framework / production subsystem。

## SHALL（Catalog 级）

- SHALL 每个 Scenario 保留完整的 Act → Observe → Reconcile 闭环；动作后必须重新 Observe 再推进（§3）。
- SHALL 完成判定只由 Goal Evidence 触发（I-10）：Plan 耗尽、Action Dispatched 均不构成完成（SC-P1-003）。
- SHALL 低层组件（Traversal / Container）无法推进时以结构化结果上报，不得自行终结 Run 或执行恢复动作（I-8 — SC-P1-004）。
- SHALL 生产 Runtime 不硬编码场景字符串（裁决 3 / 11）：target / action / 页面名 / identity 规则均为注入数据。
- SHALL ScriptedEnvironment 不做任务决策（§8）：grounding / 消歧是 Runtime 侧行为（SC-P1-005）。
- SHALL 测试断言通过 Trace 因果链 + ScriptedEnvironment action history + 注入 evaluator 行为完成，
  不需要真实设备与 LLM（§57-12）。
