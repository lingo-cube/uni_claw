# AGT-014 全链路验证回合 — 2026-10-06

## 结果摘要

本回合把确定性、真实设备、真实 DSH 三个层次分开执行。确定性 Guard 与真实
ADB/感知链通过；真实 DSH 已完成握手，但首次咨询连续两次在 75 秒服务时限内
无响应，未产生 effect。当前不能把三项真实模型任务标为 PASS。

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
emulator，DSH handshake 成功。随后两次 consultation 均为：

- `turn-timeout: consult turn deadline elapsed (75000ms)`
- `status: AgentDecisionFailed (no-response)`
- `delivered: 0`
- 未进入 Runtime Guard dispatch，也未产生 effect receipt。

真实 Settings trace 显示首帧：586 个 proposals，fast perception latency
`63.745s`，hierarchy latency `2.043s`。因此首个分歧点在真实 Agent consultation
之前的感知/上下文压力与 DSH consultation 响应时限之间；不是 ADB 点击、Guard
或 post-action verification。

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

## 后续需要裁决

当前剩余问题集中在真实 DSH consultation：

1. 是否允许为 Settings 真实任务建立更小的首轮上下文/候选投影，以降低 586
   proposals 带来的模型压力；这涉及任务智能性与上下文取舍，需 Owner 决定。
2. 是否允许调整 DSH consultation 服务时限或 provider 运行策略；这属于
   Host/DSH 运行配置边界，不能在本回合静默改变。

在这两项裁决前，AGT-014 保持 persisted，不能关闭为真实模型全链路完成。
