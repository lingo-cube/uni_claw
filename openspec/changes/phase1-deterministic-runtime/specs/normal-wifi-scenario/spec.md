# Normal WiFi Scenario（第一条必须通过的场景）

> 本场景 = Scenario Catalog 的 **SC-P1-001**。正式 Scenario 契约（Goal / Initial World / Sequence /
> Expected authority / Assertions）见 `scenarios/catalog.md`；本文件是 SC-P1-001 的详细 SHALL。

## ADDED Requirements

### Requirement: Normal WiFi Scenario（第一条必须通过的场景）

**SHALL**

"Enable WiFi" 场景必须在 Fake Environment 中完整跑通，产生符合宪章 §34 的生命周期顺序，
完成必须由 Goal Evidence 证明，Trace 必须能解释系统为什么做每一步。

**Motivation**

宪章 §34（第一条必须通过的 Normal Scenario：Run Initialize → Startup → RecoveryAnchor established →
Bind Settings Container → Traverse → Navigate → Bind Network Container → Traverse →
Bind WiFi Container → Execute → Verify → Goal Completed → Run Completed）、
§57-1 / §57-11 / §57-12（Fake 全跑通 / Trace 可解释 / 无 LLM）、
I-10（Completion 必须由 Goal Evidence 证明，禁止无证据启发式完成）。

**SHALL**

- **SHALL** 场景产生 §34 的完整生命周期顺序：
  `Run Initialize → Startup → RecoveryAnchor established → Bind Settings Container → Traverse →
  Navigate → Bind Network Container → Traverse → Bind WiFi Container → Execute → Verify →
  Goal Completed → Run Completed`。
- **SHALL** "Enable WiFi" 的完成判定由 Goal evidence evaluator（调用侧注入，裁决 3）对最终
  post-action Observation evidence 判定，产出显式 `GoalEvidence`（Satisfied / Reason /
  SourceObservationSequence — SC-P1-003），判定结果与原因被记录（I-10）。
- **SHALL** 完成判定逻辑与 Plan 内容不得硬编码 "WiFi" / "Network & Internet" 等场景字符串
  —— target / action 数据来自 Scenario 输入（裁决 3 / 11）。
- **SHALL** 开关动作以 `SetSwitch(target, desiredState)` 表达期望状态（本场景 `SetSwitch(WiFi Switch, ON)`），
  不定义机械翻转语义 `Toggle`（裁决 1）。
- **SHALL** 场景测试断言 Startup 在 Launch → Observe 后验证 Observation.ForegroundApplication
  （裁决 7 的消费者），并以其为解析语义入口的依据。
- **SHALL** 场景全程不依赖真实设备、不依赖 LLM（§33、§57-12）。
- **SHALL** Trace 记录每个 Container / Step / Action 的因果链（RunId / ContainerId / StepId / ActionId），
  可以解释系统为什么做每一步（§57-11）。
- **SHALL** 场景测试断言：生命周期顺序、Goal Evidence、Trace 因果链完整；重复运行结果一致（确定性、可重放）。

#### Scenario: Normal WiFi Scenario（第一条必须通过的场景）

Given ScriptedEnvironment（§33 确定性模拟）：

```
Screen 1:  Settings Main        — 元素 "Network & Internet"，点击 → Screen 2
Screen 2:  Network Settings     — 元素 "WiFi"，点击 → Screen 3
Screen 3:  WiFi Settings        — WiFi Switch = OFF，SetSwitch(ON) → Screen 3'
Screen 3': WiFi Settings        — WiFi Switch = ON
```

When Agent 以 Goal "Enable WiFi" 执行完整 Run；
Then 生命周期按 §34 顺序完整产生，最终 Observation evidence 中 WiFi Switch = ON
（WorldBelief 不复制该场景字段 — 裁决 2），Goal evidence evaluator 判定成立，Run 进入 Completed；
Trace 记录每个 Container / Step / Action 的因果链。
