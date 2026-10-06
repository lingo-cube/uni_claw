# AGT-014 全链路验证回合 — 2026-10-06

## 结果摘要

本回合把确定性、真实设备、真实 DSH 三个层次分开执行。确定性 Guard 与真实
ADB/感知链通过；真实 DSH 已完成握手，但独立运行中的首次咨询在 75 秒服务时限内
无响应，未产生 effect。最终预热回合只有一次 consultation；重复诊断行不代表
两次请求（见下文 provider 诊断）。当前不能把三项真实模型任务标为 PASS。

## 真实设备结果

| 链路 | 结果 | 证据 |
|---|---|---|
| API 35 emulator + XML → typed → P2 → WorldModel | PASS，1/1 | `live/typed-live-20261006b.log` |
| Host 实屏感知 → ADB tap → 重新观察 → Wi-Fi 状态 | PASS，1/1，耗时 1m24s | `live/host-full-20261006.log` |
| 坐标空间匹配/不匹配 | PASS，2/2；不匹配为零 effect | `live/coordinate-live-20261006.log` |
| ADB 真点击 → uiautomator 重读确认 | PASS，1/1，耗时 12s | `live/adb-environment-20261006.log` |
| Settings 有界覆盖（测试侧指令跟随器） | PASS，1/1，耗时 4m33s；终局 BoundedStop，33/33 steps verified，coverage 66.7% | `live/settings-coverage-live-20261006b.log`、`live/coverage-run-20261005-172851-691/coverage-report.json` |

覆盖测试的 BoundedStop 是诚实终局，未覆盖项为 `scroll-discovered-entry` 和
`repeated-entry`，不是假装完成全树遍历。

## 真实 DSH 结果

真实入口使用 profile 中的 `zai-coding-cn/glm-5.3-flash`，设备为 API 35
emulator，DSH handshake 成功。独立运行的 consultation 均为：

- `turn-timeout: consult turn deadline elapsed (75000ms)`
- `status: AgentDecisionFailed (no-response)`
- `delivered: 0`
- 未进入 Runtime Guard dispatch，也未产生 effect receipt。

真实 Settings trace 显示首帧：586 个 proposals，原始快路径总耗时
`63.745s`，hierarchy latency `2.043s`。该数字不能解释为当前截图的视觉推理
耗时：对同一真实截图的冷启动复测拆分为服务启动 `63.314s`、PNG 解码
`0.056s`、当前截图推理 `0.619s`（详见 `fast-perception-timing-2026-10-06.md`）。
因此首个已证实分歧点是 Host 在第一次观察中懒启动视觉服务；原先把它归因为
586 proposals 带来的上下文压力是不成立的。此前两个独立 DSH 运行均出现
75s 无响应，但是否与 586 proposals 有关目前没有证据。问题不在 ADB 点击、
Guard 或 post-action verification。

### 预热复验

最终构建的真实 DSH 回合首帧记录为：预热 `63.233s`、快路径总耗时
`0.721s`、PNG 解码 `0.055s`、当前截图推理 `0.664s`、hierarchy
`2.011s`，`fastInferenceSucceeded=true`，proposal 数量为 586。首帧不再携带
视觉服务冷启动。
这次回合仍出现 `turn-timeout: consult turn deadline elapsed (75000ms)`，
`delivered=0`，说明剩余故障在 Agent consultation 或其服务边界，不能再归因于
快感知首帧 63 秒。

证据：`dsh-task2-prewarm-final/run-20261006-013701-765/environment-preflight.json`、
同目录 `settings-trace.json`、`facts.json` 和 `trace.json`。

证据：

- `dsh-task2-first/console.log`
- `dsh-task2-first/run-20261005-173509-345/facts.json`
- `dsh-task2-first/run-20261005-173509-345/settings-trace.json`
- `dsh-task2-first/run-20261005-173509-345/trace.json`
- `dsh-task2-first/run-20261005-173509-345/exec.journal`

## 首轮环境失败与局部修复

首轮出现 emulator offline、同一 AVD 多实例锁冲突、uiautomator 尚未就绪和
ADB 启动超时。已完成不改变架构/协议/模型的局部修复：

- emulator 使用 `-read-only` 并脱离父 shell 存活，避免克隆 AVD 竞争和 shell
  退出导致设备消失。
- uiautomator dump 增加 4 次、每次 750ms 的有界重试。
- Host.Dsh ADB 启动超时会终止整个进程树，并输出带 device 的明确环境错误。
- Host/coverage 早期异常会落 `failure.json`，facts 提供初级程序员的查找顺序。
- Host 配置测试中的进程级环境变量覆盖已归入同一串行集合，避免全量回归时
  临时 profile 污染默认 Settings profile。
- emulator 生命周期复验：启动脚本退出 3 秒后，`emulator-5558` 仍为
  `device`、API 35、`sys.boot_completed=1`，`.state` 中 supervisor PID 存活；
  `stop-test-device.sh` 可回收该实例。

## Provider 诊断与下一步

后续读取最终 DSH 会话原始 journal，确认：首次请求 + 5 次重试，每次约 10.5s
均为 `TIMEOUT`；实际 consultation prompt 为 6,445 UTF-8 bytes、8 个元素、
4 条 claim、1 个 obligation，586 个感知 proposals 没有全部下发给模型。
最终回合只有一次 consultation；adapter 对同一 null response 重复记录了
no-decision，撤回此前根据重复诊断行写的“本回合两次 consultation”。

当前 DSH 进程没有 proxy 环境。无凭据网络探针复现：Node 直连同一智谱地址
10.551s 后报 `UND_ERR_CONNECT_TIMEOUT`；走授权的 7890 线路 0.159s 收到
HTTP 401（仅证明网络可达，不证明模型请求成功）。直接故障位于 provider
网络线路；75s 是重试累积后撞上的外层截止时间。

已按 7890 配置并重启 DSH；同一 provider/model 的专用 3081 最小真实咨询通过，
DSH 日志为 `consult captured`、`kind=act`、`durationMs=11177`，测试耗时
12.172s。下一步重跑正式 Settings 流程，确认完整上下文和设备链也能通过。
无需先改模型、协议、缩小上下文或提高外层时限。另需验证重复诊断与超时后取消
生命周期。
详见 `dsh-provider-timeout-diagnosis-2026-10-06.md`。

本回合已完成的局部修复：SettingsTraversalLiveFeed 构造阶段显式预热视觉服务，
并在 settings-trace 中记录预热耗时/结果；首轮 Analyze 只记录当前截图的解码和
推理耗时。`start-test-device.sh` 通过独立 supervisor 托管 emulator，并用
`environment-preflight.json` 标记正式流程是在环境预检之后启动。上述修复不改变
模型、协议或架构。

在预热后的真实 DSH 复验已完成但 consultation 仍失败的情况下，AGT-014 保持 persisted，不能关闭为真实模型
全链路完成。
