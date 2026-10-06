# AGT-014 全链路验证回合 — 2026-10-06

## 结果摘要

本回合把确定性、真实设备、真实 DSH 三个层次分开执行。确定性 Guard、故障矩阵与
真实 ADB/感知链通过；代理配置、运行时动作词汇和 typed 开关状态投影修复后，真实
DSH 的 Wi-Fi 首次切换/复用回合和有界 Settings 覆盖回合均已完成。三项任务证据已
汇总，AGT-014 可闭合。

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
emulator，DSH handshake 成功。最初的独立运行 consultation 均为：

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
`delivered=0`，说明当时剩余故障在 Agent consultation 或其服务边界，不能再归因于
快感知首帧 63 秒；7890 代理修复后已另行完成正式回合，见“Runtime-token 修复与
正式回合”。

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

当时的 DSH 进程没有 proxy 环境。无凭据网络探针复现：Node 直连同一智谱地址
10.551s 后报 `UND_ERR_CONNECT_TIMEOUT`；走授权的 7890 线路 0.159s 收到
HTTP 401（仅证明网络可达，不证明模型请求成功）。直接故障位于 provider
网络线路；75s 是重试累积后撞上的外层截止时间。

已按 7890 配置并重启 DSH；同一 provider/model 的专用 3081 最小真实咨询通过，
DSH 日志为 `consult captured`、`kind=act`、`durationMs=11177`，测试耗时
12.172s。随后正式 Settings 回合发现第二个分歧：DSH 返回了合法 AgentDecision，
但 `effectClass=navigate` 是策略语义词，不是本轮 `allowedEffects=[tap]` 中的
运行时 token，Host Guard 因此在 dispatch 前安全拒绝。修复 prompt/projection 后，
正式回合 3 次 consultation、2 个 `tap` effect 和 1 个 `noAction` 均通过。
详见 `dsh-provider-timeout-diagnosis-2026-10-06.md`。

## Runtime-token 修复与正式回合

首次代理修复后的正式回合把 `navigate` 误用暴露为首个非网络分歧点。修复内容是：

- DSH prompt 明确 `effectClass` 必须逐字复制 `context.allowedEffects`，并给出
  `tap`/`swipe-up` 到 Settings 语义动作的映射。
- Host policy projection 同步显示这条边界；没有放宽 Host 的 allowed effects，
  也没有把策略文件中的 `navigate` 改成运行时 token。
- Node plugin 全量 22/22 PASS；SettingsActionPolicyTests 8/8 PASS；Host.Dsh
  构建 0 errors。

正式运行目录：
`evidence/agt-014/dsh-task2-proxy-runtime-token-fix-20261006/run-20261006-020702-678/`

- facts：`Completed / Completion / delivered=2`，两个 Guard `Allow / navigate`。
- DSH decisions：`tap Network & internet` → `tap Internet` → `noAction Wi-Fi=checked`。
- exec journal：两个 `DeliveryCompleted`，没有 Wi-Fi 开关点击。
- preflight：视觉预热 `63.556s`，正式首帧快路径 `2.595s`，后续约 `0.9s/0.74s`。

本回合已完成的局部修复：SettingsTraversalLiveFeed 构造阶段显式预热视觉服务，
并在 settings-trace 中记录预热耗时/结果；首轮 Analyze 只记录当前截图的解码和
推理耗时。`start-test-device.sh` 通过独立 supervisor 托管 emulator，并用
`environment-preflight.json` 标记正式流程是在环境预检之后启动。上述修复不改变
模型、协议或架构。

## 任务二：真实首次切换与同设备复用

首次回合的第一次修复验证暴露了一个比 Guard 更靠后的局部问题：模型已经返回
`targetRole=switch`、`targetDescriptor=Wi‑Fi`、`desiredState=checked`，Guard 也返回
`toggle:Wi‑Fi / Allow`，但 Control 没有派发 effect。原因是 exact two-state typed
证据中的 `unchecked` 在 `UiHierarchyOccurrenceStrategy` 投影时被丢弃，
`DescriptorTargetPolicy` 只能看到 `State=null`，按 fail-closed 规则选择观察而不是动作。
现已改为保留合法 typed 值 `checked/unchecked/partial`，并由
`UiHierarchyOccurrenceStateTests` 固定回归。

修复后的首次运行目录：
`evidence/agt-014/task2-toggle-first-final2-20261006/run-20261006-034418-669/`

- `facts.json`：`Completed / Completion`、`delivered=3`、3 个
  `DeliveryCompleted`，completed steps 为 decision 1/2/3。
- `consultations.json`：两次导航、一次 typed Wi‑Fi toggle、一次 `noAction`；Guard
  只允许 safe navigation 和声明目标 `toggle:Wi‑Fi`。
- `exec.journal`：三次 prepare/submission/receipt；最后一次实际命令为
  `adb -s emulator-5556 shell input tap 969 835`。
- 真实设备：回合前 `wifi_on=0`，回合后 `wifi_on=1`。
- `environment-preflight.json`：视觉服务预热成功，且显式标记正式流程在预热后开始；
  `settings-trace.json` 记录预热、快感知、hierarchy 的分段耗时。

同一设备立即复用的运行目录：
`evidence/agt-014/task2-toggle-reuse-final-20261006/run-20261006-034642-214/`

- `facts.json`：`Completed / Completion`、`delivered=2`、2 个 receipt。
- `consultations.json`：两次导航后 `noAction`，没有 `toggle:Wi‑Fi` Guard 记录。
- `exec.journal`：只有两次 prepare/submission/receipt；没有重复开关 effect。
- 设备复核仍为 `wifi_on=1`。

这两次运行的初级查错入口已统一为：`facts.json` → `consultations.json` →
`policyGuard/completedSteps` → `exec.journal` → `settings-trace.json` 与 XML/截图。

## 真实 DSH 有界覆盖回合

覆盖入口在同一 7890 代理和 `glm-5.3-flash` 配置下重新执行，结果为
`CoverageComplete`、`100%`、`19/19` steps verified、`firstDivergence=NONE`。
20 轮 consultation 复用了单一 DSH session；7/7 一级入口、滚动发现项、8 个二级
路由、9 次返回和重复进入均有逐步证据。动作集合为 `tap=18`、`swipe-up=1`，没有
Wi-Fi、USB debugging 或其他 forbidden/targeted switch effect。

首次真实咨询上下文已经携带安全策略 ref、safe/forbidden 集合和固定 digest
`4186f07dfc6b17b29f59c57d782130cf16245bb503fd66511a7018176801081e`；后续每个
act consultation 的 facts 都回填同一 digest，说明策略是在首次咨询前生成并持续复用。

证据：`evidence/agt-014/dsh-task3-coverage-proxy-20261006/run-20261006-025112-969/`
和 `evidence/agt-014/dsh-task3-coverage-2026-10-06.md`。

## 故障注入与诊断回合

Host Settings 覆盖、Director、Ledger 和 ActionPolicy 的故障矩阵 `46/46` 通过，
覆盖弹窗不消失、未知页、连续验证失败、滚动无变化、步数上限、模型偏离、多步动作、
非法目标状态和 forbidden/unknown action；每项都保持 fail-closed 或 bounded stop。
另外用不存在设备做真实 Host 环境门测试，退出码 `1`，在 ADB 检查处停止，未进入
consultation 或 effect dispatch。证据：`evidence/agt-014/fault-injection-2026-10-06.md`。

Wi-Fi 首次切换/复用回合、有界覆盖回合和故障矩阵已经通过；任务一的独立证据来自
AGT-003/AGT-004，本次覆盖第 1 步也重新证明了 `Network & internet` 的唯一定位和
route transition。三项任务的 method/expected/actual/evidence 已齐全，AGT-014
已进入 CLOSED。
