# AGT-014 任务二：Wi‑Fi 首次切换与状态复用

## 结论

同一台 API 35 emulator、同一份 `zai-coding-cn/glm-5.3-flash`、同一条
7890 代理线路上，任务二已经完成两次真实回合：

| 回合 | 预期 | 实际 | 结果 |
|---|---|---|---|
| 首次执行 | Wi‑Fi=0 时找到目标并切到 checked | 2 次导航 + 1 次 `switch/Wi‑Fi/desiredState=checked`，3 个 ADB receipt，设备变为 `wifi_on=1` | PASS |
| 复用执行 | Wi‑Fi 已 checked 时只确认，不重复点击 | 2 次导航 + `noAction`，2 个 ADB receipt，无 toggle Guard、无第三次 effect，设备保持 `wifi_on=1` | PASS |

## 首次回合

运行目录：`task2-toggle-first-final2-20261006/run-20261006-034418-669/`

- `facts.json`：`Completed / Completion`、`delivered=3`、3 个
  `DeliveryCompleted`，`completedSteps` 为 decision 1/2/3。
- `consultations.json`：
  `Network & internet` → `Internet` → `switch Wi‑Fi desiredState=checked` →
  `noAction`。
- `facts.policyGuard`：前两次为 `navigate/Allow`，第三次为
  `toggle:Wi‑Fi/Allow`，始终使用同一 policy digest。
- `exec.journal`：3 个 prepare、3 个 submission、3 个 receipt；最后一次真实
  命令为 `adb -s emulator-5556 shell input tap 969 835`。
- `environment-preflight.json`：`succeeded=true`，预热耗时约 63.5 秒，且
  `formalFlowStartedAfterPreflight=true`。
- `settings-trace.json`：预热之后正式首帧快感知约 0.96 秒，hierarchy 约 2.24 秒；
  后续周期继续有独立耗时记录。

## 复用回合

运行目录：`task2-toggle-reuse-final-20261006/run-20261006-034642-214/`

- `facts.json`：`Completed / Completion`、`delivered=2`、2 个
  `DeliveryCompleted`。
- `consultations.json`：`Network & internet` → `Internet` → `noAction`。
- `facts.policyGuard`：只有两次 `navigate/Allow`，没有 `toggle:Wi‑Fi`。
- `exec.journal`：只有 2 个 prepare/receipt，证明没有重复点击开关。
- 设备复核：`adb -s emulator-5556 shell settings get global wifi_on` 仍为 `1`。

## 这次暴露并修复的问题

首次真实切换前的回合曾经在 Guard 放行后没有 effect。逐层对照后定位为：
UiAutomator 的 exact two-state 证据已经产生 `unchecked`，但
`UiHierarchyOccurrenceStrategy` 只把 `checked` 投影为 occurrence state，Control
因而把 `unchecked` 当作 Unknown 并安全地不派发。修复后完整保留
`checked/unchecked/partial` 三种 typed 值；新增
`UiHierarchyOccurrenceStateTests.ExactUncheckedSwitch_RemainsAvailableToControlPolicy`
回归测试。该修复没有放宽 Guard，也没有修改架构、模型或共享协议。

## 初级程序员查错顺序

1. 先看 `facts.json` 的 `status/reason/delivered/policyGuard`。
2. 再按 `decisionId` 看 `consultations.json` 的上下文、模型决策和 Guard 结果。
3. 用 `completedSteps` 的 receipt 去 `exec.journal` 找 prepare/submission/receipt。
4. 最后对照 `settings-trace.json` 和 `evidence/capture-*.xml/png`，确认动作前后页面
   和开关状态是否真的变化。
