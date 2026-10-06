# AGT-014 DSH runtime-token 修复回合 — 2026-10-06

## 结论

7890 代理配置后，DSH 已能在完整 Android Settings 上下文中完成真实咨询；原先的
provider `TIMEOUT` 不再出现。新的首个分歧是动作词汇边界：模型把策略语义
`navigate` 当成 `effectClass`，而 Runtime 合同只允许 `tap`。Host Guard 在设备
动作之前拒绝了它，设备没有被误点。

已在 DSH 咨询提示和 Settings 策略投影中明确：`effectClass` 必须逐字复制
`context.allowedEffects`；Android Settings 用 `tap` 表示选择/导航，用
`swipe-up` 表示滚动，Host 再把它们映射到 `navigate/back/scroll` 语义。

## 修复前后的真实行为

| 阶段 | 观察 | 结论 |
|---|---|---|
| 直连 provider | 10.551s `UND_ERR_CONNECT_TIMEOUT`，重试后撞 75s | DSH 没有走通 provider 网络 |
| 7890 代理 + 最小咨询 | DSH `consult captured`，11.177s | provider、handshake、`submit_decision` seam 可用 |
| 代理后的首次完整 Settings | DSH 返回 `effectClass=navigate`，Host Guard `unknown-action` | 提示/投影没有区分策略语义和运行时 token；无 effect |
| 修复后的完整 Settings | 两个 `tap` 被 Guard 允许并落盘，第三次 `noAction` | 真实链路完成，Wi-Fi 已观察为 `checked`，没有重复切换 |

## 最终运行证据

运行目录：`evidence/agt-014/dsh-task2-proxy-runtime-token-fix-20261006/run-20261006-020702-678/`

- `facts.json`：`status=Completed`、`outcome=Completion`、`delivered=2`；两个 Guard
  结果均为 `Allow / navigate`，策略 digest 为
  `4186f07dfc6b17b29f59c57d782130cf16245bb503fd66511a7018176801081e`。
- DSH 三次咨询：`act(Network & internet, tap)`、`act(Internet, tap)`、
  `noAction(Wi-Fi checked)`。
- `exec.journal`：两个 `DeliveryCompleted`，真实 ADB 命令分别点击
  `Network & internet` 和 `Internet`；没有 Wi-Fi 开关点击。
- `settings-trace.json`：预热成功 `63.556s`；首帧快路径 `2.595s`，其中解码
  `0.262s`、推理 `2.314s`；后续快路径约 `0.9s/0.74s`。
- `environment-preflight.json`：`formalFlowStartedAfterPreflight=true`。

## 局部修改

- `dsh/uniclaw-decision-channel/src/index.js`：把 `effectClass` 的允许集合和
  Android Settings 运行时映射写入每次 consultation prompt。
- `src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs`：把同一映射写入
  Host policy projection，避免模型只看到 `safe=[navigate,...]` 而误用语义词。
- `dsh/uniclaw-decision-channel/tests/plugin.test.mjs`：新增提示边界回归测试。
- `tests/UniClaw.Host.Tests/SettingsActionPolicyTests.cs`：新增策略投影回归测试。

## 仍未完成

这只证明“确认 Wi-Fi 并在已开启时复用”的真实 DSH 全链路。AGT-014 还包含
指定菜单项任务、完整设置覆盖任务和故障注入汇总，因此 Change 仍保持
`persisted`，不能据此关闭。

