# environment Specification

## Purpose
TBD - created by archiving change phase1-deterministic-runtime. Update Purpose after archive.

## Requirements

### Requirement: Environment（观察与动作边界）

**SHALL**

外部世界通过统一端口暴露观察与动作能力；第一阶段使用可确定性驱动的 Fake Environment（ScriptedEnvironment）。
动作后必须重新观察确认，不得信任内部记录（Internal Runtime State ≠ External World State）。

**Motivation**

宪章 §8（Environment = Observation capabilities + Action capabilities，不拥有任务决策）、
§3（External World 不可信：CurrentPage 记录与 ActionExecuted 记录都不能证明现实）、
§33（第一阶段创建可模拟环境：Screen A / Click X → Screen B，验证 Runtime 生命周期）、
§57-12（不依赖 LLM 与真实设备完成确定性测试）。

**SHALL**

- **SHALL** 定义 `IEnvironment` 端口：`ObserveAsync()` 与 `ExecuteAsync(action)`，两者都接受取消信号。
- **SHALL** Observation 携带 Elements / ForegroundApplication / SequenceNumber。
  本阶段 Observation 不含 Fingerprint（裁决 2：Fingerprint 字段与机制 DEFER 到 Scroll Identity
  Scenario；I-6 原则「Fingerprint 是 evidence，不是 identity」保留在宪章）。
  SequenceNumber 是确定性、单调递增的观测序列号（裁决 6：不依赖真实时间，不用 Timestamp 当序列号）。
- **SHALL** ObservedElement 携带可空 SwitchState 语义（null = 当前非开关承载元素，非 null = 开关状态可用），
  并携带 Index（元素在当前 Observation 内的稳定序位，供 grounding 结果与动作目标引用；非坐标 — SC-P1-005）。
  本阶段不引入独立 ElementKind 枚举（裁决 9）。
- **SHALL** 具体动作以 `DeviceAction` 表达：`LaunchApp | Tap | SetSwitch`；`Tap` / `SetSwitch` 携带
  `TargetElementIndex`（Runtime 侧 grounding 解析出的具体元素引用 — SC-P1-001 / SC-P1-005；
  Environment 不替 Runtime 做元素选择）。
- **SHALL** 动作结果以 `ActionResult` 表达 dispatch outcome：`Dispatched / TimedOut / Rejected`
  （含动作描述与结果信息）。任何 dispatch 结果都不直接证明世界状态或 Goal 完成
  （dispatch outcome ≠ world success，裁决 10）。
- **SHALL** 同文本多元素的物理效果按元素身份应用：同一动作作用到不同元素产生不同物理效果；
  `SetSwitch` 作用于非开关承载元素（SwitchState=null）→ `Rejected`（物理能力语义，非任务决策 — SC-P1-005）。
- **SHALL** Fake Environment（ScriptedEnvironment）由 Screen 配置驱动：`Screen A + Click X → Screen B`，
  同一动作序列必然产生同一观察序列（确定性、可重放）。
- **SHALL** 执行动作后，Runtime 必须重新 Observe 再推进判断（不信任动作成功记录之外的状态）。
- **SHALL** IEnvironment 不承担任何任务决策（Environment 回答"现在能看到什么 / 请执行这个动作"，不回答"下一步做什么"）。

#### Scenario: Environment（观察与动作边界）

Given ScriptedEnvironment 配置：Screen1（Settings Main，含元素 "Network & Internet"）→
点击 "Network & Internet" → Screen2（Network Settings，含元素 "WiFi"）；
When Runtime 执行 `ExecuteAsync(Tap("Network & Internet", targetElementIndex))`（grounding 已解析出
元素引用）后调用 `ObserveAsync()`；
Then 返回 Screen2 的 Observation；同一输入序列重复运行产生完全相同的观察序列（确定性）。
同一 Text 可出现在多个元素上（如 SC-P1-005 数据变体：标题与开关都是 "WiFi"）——
消歧是 Runtime 侧 grounding 行为（SHALL 见下），Environment 不做任务决策。
