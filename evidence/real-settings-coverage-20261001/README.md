# Real Android Settings coverage traversal — 可配置有界覆盖遍历（2026-10-01）

## 这次做了什么

在专用 Android 模拟器 `emulator-5556`（API 35，wm 1080x1920）上，经专用
DSH 测试服务 `127.0.0.1:3081` 执行了一次配置驱动的 Settings 菜单覆盖遍历。
共用服务 `3080` 全程未被启动、重启、停止或修改（仅存活探测）。

执行参数（全部来自 `.dsh/profiles/settings-coverage.yaml`，未写死在逻辑中）：

- 任务命名：`遍历设置菜单覆盖测试`（DSH session title）
- workspace：`UniClaw_Product_Tasks`，`workspaceReuse=true`，`autoCloseTurn=false`
- 模型：`zai-coding-cn/glm-5.3-flash`
- 边界：maxSteps=24、maxConsultRounds=24、maxScrolls=4、连续失败上限=3、指令纠正重试=1
- 目标页（7 个可见一级入口）：Network & internet / Connected devices / Apps /
  Notifications / Battery / Storage / Sound & vibration
- 覆盖要求：根页 + 全部可见一级入口 + ≥1 滚动发现入口 + ≥2 二级页 +
  返回导航 + 重复进入
- DSH session：`session-c6d36c6b-fcc8-4b2a-bd9b-766c0c344a62`（单一 session 复用）

## 实际效果（逐项覆盖清单）

| 覆盖项 | 要求 | 实测 | 结果 |
|---|---|---|---|
| 根页 | 观察到 Settings 根页路由 | `android.settings\|route:Settings` 多轮观察 | ✅ |
| 一级入口（全可见） | 7/7 进入 | 7/7（含 Storage、Sound & vibration 在滚动后进入） | ✅ |
| 滚动发现入口 | ≥1 | `Search settings`（仅滚动后可见并进入） | ✅ |
| 二级页面 | ≥2 | 8 个不同二级路由 | ✅ |
| 返回导航 | 有验证的返回 | 9 次 `Navigate up` 点击全部路由回到根页 | ✅ |
| 重复进入 | 无错误步骤/重复 effect | `Apps` 二次进入，新步骤+新回执 | ✅ |

- 状态：`Completed (terminal-emitted)`，结果 `Completion`，进程退出码 0
- 步骤：19/19 已验证（成功率 100%），覆盖率 100%，分歧点：无（NONE）
- 滚动：1 次 `swipe-up`（命令 `input swipe 540 1638 540 907 300` 由滚动容器
  自身 bounds 派生），验证 = 路由未变 ∧ 可见内容变化
- 咨询：20 轮（19 轮真实模型 turn + 1 轮 director 终局 NoAction），
  同一 DSH session；DSH 侧记录 19 turns / 38 steps / llmMs 199611
- 每步 trace（`run-final/coverage-steps.json`）含：DecisionId、DSH session、
  动作前目标唯一性、ADB effect receipt（id/outcome/命令）、动作后 capture id、
  route 前后指纹、Grounding/Assurance/Verification checks——19 步全部齐备，
  无缺失字段

## 原来的问题 → 这次修改

AGT-004 只证明了 Settings→Internet→Wi-Fi 两步闭环；Gate disposition 明确
「全树覆盖需另立覆盖边界清晰的任务」。本次（AGT-005）：

- 新增 `settings-coverage.yaml` 配置 + fail-closed 加载校验（范围/步数/
  滚动/目标/终止条件全部配置化）
- 新增覆盖账本（发现/进入/滚动/返回/重复记账 + 可见性约束的指令引擎 +
  终止评估 + 覆盖率报告 + 首个分歧点）
- 新增 director（ConsultAgent 包装缝：指令注入、有界纠正、终局）
- Kernel 最小扩展：swipe effect、scrollable occurrence、滚动验证语义
  （路由未变 ∧ 内容变化，否则 fail closed）
- Host.Dsh `--settings-coverage` 入口

## 验证结果（确定性）

- solution build 0 errors；全量 1161 自动测试通过
  （Kernel 712 / Simulation 184 / Host 94 / Agent.Dsh 132 / Core 14 /
  Agent 17 / FileSystem 9）
- 29 个场景经 AGT-005 重认证，`scenario_certify --check` PASS，
  coverage 真值链验证 PASS
- 新增聚焦测试：滚动验证 6、swipe 命令 6、scrollable 投影 3、配置 21、
  账本 12、确定性场景闭环 5（全量使命 19 步 / maxSteps 有界停止 /
  偏离有界纠正 / 持续偏离 fail closed / 非法 desiredState 拒绝）、
  DSH session 复用 1；容量重试沿用既有回归
  （`Consult_RetriesSelectedModelCapacity_OnSameRequest`）

## 新发现

- 首次执行尝试因专用 3081 服务被管道 `| head` SIGPIPE 中断而只完成 1 步
  （该次证据在 `/tmp/real-settings-coverage-ag005/run-20261001-134817-590`，
  如实保留为失败尝试记录）；无管道重启后完整重跑成功。
- kernel 终态与覆盖判定分离：kernel 的 end-on-root 义务证明「回到根页」，
  覆盖完成由 director/账本证明；进程退出码取覆盖判定（部分覆盖=3，
  不伪装成功）。
- 滚动后发现的真实「滚动才能看到」入口是 `Search settings`（搜索栏随
  滚动进入内容区）；在 1080x1920 视口下 `Storage`、`Sound & vibration`
  首屏不可见，账本按滚动后可见才指挥进入（步骤 11/13 在滚动步骤 10 之后），
  `Battery` 首屏底部可见（步骤 8，滚动前进入）——「可见一级入口」与
  「滚动发现」的实际边界由观察证据逐项判定，不预设。

## 下一步（可选）

- 更大小视口/更多目标页的压力配置只需改 YAML，无需改代码。
- 若要穷举 Settings 全树（如每页所有子页），需在配置中显式扩大目标清单，
  并预期更大的 maxSteps。

## 证据文件

- `run-final/coverage-steps.json`：19 步逐步 trace（本次核心证据）。
- `run-final/coverage-report.json`：覆盖率/成功率/未覆盖项/分歧点。
- `run-final/facts.json`：终态、回执清单、咨询摘要、完成步骤。
- `run-final/exec.journal`：19 次 ADB dispatch 与 DeliveryCompleted 回执。
- `run-final/settings-trace.json`：每观察周期的 Fast/hierarchy 可用性与路由指纹。
- `run-final/trace.json`：Kernel evidence admit/reconcile 链。
- `run-final/console.log`：进程输出（含配置回显）。
- `run-final/uniagent-prod-3081.yaml`：本次使用的 3081 端点配置副本。
- `dsh/session-c6d36c6b-….json`：DSH session 记录（标题/模型/轮次）。
