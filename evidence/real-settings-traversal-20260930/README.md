# AGT-003 真实 Android 设置遍历报告

## 这次做了什么

在不触碰共用的 3080 服务的前提下，启动了专用 DSH 测试服务 `127.0.0.1:3088`，使用配置的 `zai-coding-cn/glm-5.3-flash`，对 `emulator-5554` 执行了真实 Android Settings 的一条短路径：设置首页 → `Network & internet` → `Internet` → 读取 Wi-Fi 状态。任务复用了同一个 Product task-scoped DSH session，项目和对话标题来自配置，`autoCloseTurn=false` 保持默认不自动回收共享服务中的会话。

## 结果

- 运行状态：`Completed`，终态原因：`terminal-emitted`。
- 目标：Wi-Fi 开关应为开启；最终 typed observation 为 `checked`，设备保持 Wi-Fi 开启。
- 实际动作：2 次真实点击，依次进入 `Network & internet`、`Internet`。
- 咨询轮次：3 次；每轮都绑定当前 `DecisionId` 和同一个物理 session。
- 第 3 轮第一次生成了带不合规完成清单的 `noAction`，插件拒绝该 payload；模型在同一物理 turn 内修正为无清单的 `noAction`，根据当前 typed `checked` 证据结束任务。没有绕过校验，也没有新建会话。
- 采集：3 个 runtime cycle；层级树均可用，快速视觉通道不可用，但 typed hierarchy 已足以完成目标判断。

**范围判定：这证明了真实 DSH Agent × 真实模拟器的一条两步导航路径与最终状态判断；不构成原始《Real Android Settings Traversal — IMPLEMENT / LIVE E2E》任务书的完整 PASS。** 本次没有遍历设置菜单全树，也没有覆盖 Focused/Slow 路由。快速视觉在三轮均为 `fastAvailable=false`。两次点击后的新观察确实发生，但导航目标的 step-level post-action verification 未通过，`completedSteps=[]`；最终 `Completion` 来自 Wi-Fi 当前 typed `checked` 证据，而不是两步导航历史均已验证。

## 证据位置

原始运行目录：`/private/tmp/run-final-state6/run-20260929-173231-800`。以下四份证据已复制到本报告所在目录，便于长期复核。

- `facts.json`：终态、回执和完成状态。
- `settings-trace.json`：3 个采集周期及候选数量（583、633、686）。
- `exec.journal`：实际动作和执行回执。
- `trace.json`：完整运行 trace、DecisionId、Product Run/Session 关联。
- `/private/tmp/latest-final.jsonl`：本地 DSH session transcript，包含拒绝与同轮修正；此文件保留在本机，不随仓库证据分发。

本次专用服务使用 3088；仓库默认配置已恢复为 3080，未启动、重启或关闭 3080 上的共用服务。

## 仍可改进的地方

快速视觉通道在本次运行中不可用；随后 FSV-002 已修复 provider 启动配置并单独证明非空推理，但尚未重跑整条 Android 路径来证明 `fastAvailable=true`。当前导航动作的 step-level post-action verification 因目标在跳页后不再唯一而失败，不能把两个 `DeliveryCompleted` 回执称作“已验证的步骤”。下一轮完整 E2E 需在真实路径上复测 Fast/Hierarchy 协同，补齐每轮紧凑 trace、Grounding/Assurance 明细、每个 effect 的 post-action 证明及原始任务书的 Final Gate；没有真实 buyer 时不应为测试而强行调用 Focused 或 Slow。

## AGT-004 修复后的验证状态（2026-09-30）

代码已经修正了上面暴露的首个分歧点：导航后页面路由改变时，原点击目标消失不再被直接当作 grounding 失败；只有动作前目标唯一、动作后产生新的 `ui.screen.route` fingerprint 才会通过 navigation transition 检查。Agent 生成的自由文本 `desiredState` 也会在 effect 前拒绝，导航目标明确要求省略该字段。

对应的 Kernel/Host 回归、完整 solution build/test、DSH plugin test 和 29 个场景认证均通过（见 `changes/AGT-004/state.md`）。这轮尝试启动隔离 Android 模拟器时，环境在启动前退出，日志为：

```text
Incompatible processor. This Qt build requires the following features:
    neon
```

因此本目录中的真实设备证据仍只有上面的两步短路径；AGT-004 的真实复跑状态是 `ENVIRONMENT_BLOCKED`，没有把确定性回归结果或旧的短路径升级成完整遍历 PASS。

2026-10-01 的再次探测仍未进入 Android 启动阶段：`tools/verify-live` 创建的隔离目录为 `/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-android-20261001-174046-43467`，同样在 emulator 进程初始化时报告 `neon` 不兼容；`adb devices` 仍无设备。没有启动、重启或关闭 3080，也没有留下运行中的专用服务或模拟器。

## Final escalated rerun supersedes the environment block (2026-10-01)

确认前一次 `neon` 错误来自沙箱的 CPU 特性/子进程生命周期限制。用保持存活的专用执行会话后，API 35 `emulator-5556` 正常运行，专用 DSH 3081 也使用了正确的 `zai-coding-cn/glm-5.3-flash` provider 路由。

最终证据已移到 [`evidence/real-settings-traversal-20261001/`](../real-settings-traversal-20261001/)。最终运行完成两步真实导航并通过两步验证；原来的 `ENVIRONMENT_BLOCKED` 结论只适用于未跳过沙箱的那次尝试。
