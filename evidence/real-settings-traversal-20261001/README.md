# Real Android Settings traversal — final live rerun (2026-10-01)

## 这次做了什么

在跳过沙箱限制的专用执行会话中启动 API 35 Android 模拟器 `emulator-5556`，再启动专用 DSH 测试服务 `127.0.0.1:3081`。生产共用服务 `3080` 没有被启动、重启或关闭。

实际运行使用：

- 模型：`zai-coding-cn/glm-5.3-flash`
- Product session title：`遍历设置菜单`
- workspace：`UniClaw_Product_Tasks`
- `workspaceReuse=true`
- `autoCloseTurn=false`
- DSH session：`session-04de3df7-3221-4dc6-875a-c12f6360ce60`

## 实际效果

- 状态：`Completed`，原因：`terminal-emitted`
- 结果：`Completion`
- 真实点击：2 次，均为 `DeliveryCompleted`
- 已验证步骤：2/2，`completedSteps` 不再为空
- Agent consultation：3 轮，同一个 task-scoped DSH session
- 每轮都使用配置模型，session 记录中可核验 provider/model/title
- Fast perception：3/3 可用
- Android hierarchy：3/3 可用
- 路由指纹：`Settings → Internet → T-Mobile`
- 最后一轮判断 Wi-Fi 已为 `checked`，因此没有重复切换

## 这证明了什么

之前的首个分歧是“点击跳页后原目标消失”被误判成 grounding 失败。修复后，动作前目标唯一、动作后路由改变即可通过 navigation transition 验证；本次真实运行已经产生两条已验证步骤和两条完成回执。

这次仍然是到 Wi-Fi 状态的短路径验证，不是遍历 Settings 全部菜单。若“遍历”要求全树覆盖，还需要另一个有明确目录覆盖边界的任务；当前证据证明的是从 Settings 根页进入网络设置并验证 Wi-Fi 状态的真实闭环。

## 证据文件

- `run-final/facts.json`：终态、回执、完成步骤。
- `run-final/settings-trace.json`：3 个真实采集周期、Fast/Hierarchy 状态和路由指纹。
- `run-final/exec.journal`：两次 ADB tap 与 `DeliveryCompleted` 回执。
- `run-final/trace.json`：Evidence admit/reconcile 链和 revision。
- `run-final/metadata.json`：设备、模型、项目、session、路由和运行摘要。
- `dsh/session-04de3df7-3221-4dc6-875a-c12f6360ce60.json`：DSH session 的模型、标题和三轮摘要。
