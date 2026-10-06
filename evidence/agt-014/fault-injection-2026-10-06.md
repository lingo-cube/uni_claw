# AGT-014 故障注入与安全停止回合 — 2026-10-06

## 结果

这次回合覆盖了设置遍历链的确定性故障面，以及 Host 在设备不可用时的环境门：

- Host Settings 测试集 `44/44` 通过，0 失败、0 跳过。
- 设备不存在时，真实 DSH Host 在第一次 ADB 环境检查处以非零码退出，未创建
  Agent consultation，也没有产生 Kernel effect。
- 所有故障都保持安全停止或有界终止，没有把未完成覆盖伪装成完成。

## 已注入的故障与期望

| 故障 | 期望 | 实际 |
|---|---|---|
| 弹窗持续存在 | 到达重试上限后 `bounded-stop:popup-not-cleared` | 通过 |
| 页面身份持续未知 | Defer 配额耗尽后 `bounded-stop:unknown-page` | 通过 |
| 连续 post-action 验证失败 | 达到连续失败上限并停止 | 通过 |
| 滚动到底但内容不变 | 当前步骤验证失败，留下 first divergence，再有界停止 | 通过 |
| 最大步数耗尽 | 保留未覆盖项并 `BoundedStop` | 通过 |
| 模型一次偏离 | 有界纠正咨询后继续 | 通过 |
| 模型持续偏离 | fail closed，零绕过 effect | 通过 |
| 多步 Act / 多 Act Plan | dispatch 前拒绝 | 通过 |
| 非法 desiredState | Director 拒绝，不把语义字段带入导航/滚动 | 通过 |
| forbidden、unknown 或缺失策略 | Guard 在 Kernel dispatch 前拒绝或 fail closed | 通过 |

确定性证据：
`evidence/agt-014/fault-injection-20261006/agt-014-fault-injection.trx`。
对应源码入口是 `tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs`、
`SettingsCoverageDirectorPlanTests.cs`、`SettingsCoverageLedgerTests.cs` 和
`SettingsActionPolicyTests.cs`。

## 真实环境门

使用不存在的设备 `emulator-__fault_injected__` 启动真实 Host：

- 期望：环境不可用时在 ADB 入口停止，不能进入 consultation 或 effect dispatch。
- 实际：退出码 `1`，输出
  `ENVIRONMENT_UNAVAILABLE: adb failed: adb: device 'emulator-__fault_injected__' not found`。

证据：`invalid-device-console.log`、`invalid-device-result.json`。

## 诊断查找顺序

失败时先看运行目录的 `facts.json` 中 `firstDivergence`、`reason` 和
`diagnostics.guidance`；再用 `DecisionId` 对照 `coverage-steps.json`、`trace.json`
和 `exec.journal`。如果失败发生在环境启动前，则看本目录的环境日志；如果进入
了覆盖 runner，则看 `failure.json` 和 `environment-preflight.json`。

本回合没有修改架构、模型或协议；没有发现需要 Owner 裁决的故障语义问题。
