# Environment（观察与动作边界）

## Requirement

外部世界通过统一端口暴露观察与动作能力；第一阶段使用可确定性驱动的 Fake Environment（ScriptedEnvironment）。
动作后必须重新观察确认，不得信任内部记录（Internal Runtime State ≠ External World State）。

## Motivation

宪章 §8（Environment = Observation capabilities + Action capabilities，不拥有任务决策）、
§3（External World 不可信：CurrentPage 记录与 ActionExecuted 记录都不能证明现实）、
§33（第一阶段创建可模拟环境：Screen A / Click X → Screen B，验证 Runtime 生命周期）、
§57-12（不依赖 LLM 与真实设备完成确定性测试）。

## Scenario

Given ScriptedEnvironment 配置：Screen1（Settings Main，含元素 "Network & Internet"）→
点击 "Network & Internet" → Screen2（Network Settings，含元素 "WiFi"）；
When Runtime 执行 `ExecuteAsync(Tap("Network & Internet"))` 后调用 `ObserveAsync()`；
Then 返回 Screen2 的 Observation；同一输入序列重复运行产生完全相同的观察序列（确定性）。

## SHALL

- SHALL 定义 `IEnvironment` 端口：`ObserveAsync()` 与 `ExecuteAsync(action)`，两者都接受取消信号。
- SHALL Observation 携带 Elements / Fingerprint / ForegroundApp / Timestamp（切片 1 用确定性序列号）。
- SHALL 动作结果以语义 `ActionResult` 表达（成功 / 失败，含动作描述与结果信息）。
- SHALL Fake Environment（ScriptedEnvironment）由 Screen 配置驱动：`Screen A + Click X → Screen B`，
  同一动作序列必然产生同一观察序列（确定性、可重放）。
- SHALL 执行动作后，Runtime 必须重新 Observe 再推进判断（不信任动作成功记录之外的状态）。
- SHALL IEnvironment 不承担任何任务决策（Environment 回答"现在能看到什么 / 请执行这个动作"，不回答"下一步做什么"）。
