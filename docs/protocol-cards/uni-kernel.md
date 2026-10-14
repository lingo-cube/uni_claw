# 协议卡片 · uni-kernel（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G3 | gap | track | uni-kernel → uni-kernel | — | Task Instance 创建（交互模型前奏 0c：Runtime 铸造，无独立协议边） |
| G4 | gap | track | uni-kernel → uni-kernel | — | Product Session 分配（交互模型前奏 0d：Runtime 铸造 + 显式 Binding，无编号边） |
| O2 | edge | track | uni-kernel → user-session | schema v1 | O2 · task-launch-ack（Runtime 铸造；交互模型前奏 0b/0c） |
| O3 | edge | track | uni-kernel → user-session | schema v1 | O3 · launch-stage-record（阶段记录：valueOrigin + availability 分离） |
| O4 | edge | keep | uni-kernel → user-session | schema v1 | O4 · runtime-run-event（source-native 事件流：DSH / Kernel / 设备 / Runtime） |
| O5 | edge | keep | uni-kernel → user-session | schema v1 | O5 · runtime-run-projection（Runtime Run 查询投影，唯一 authority：Runtime Host） |
| O6 | edge | keep | uni-kernel → user-session | schema v1 | O6 · runtime-run-response（单 Run 查询响应） |
| O7 | edge | keep | uni-kernel → user-session | schema v1 | O7 · runtime-run-events-response（事件列表响应，nextCursor 分页） |
| O8 | edge | keep | uni-kernel → user-session | schema v1 | O8 · runtime-run-list-response（Run 列表摘要投影） |
| O9 | edge | keep | uni-kernel → user-session | schema v1 | O9 · run-report（全链路单 run 报告只读投影，PNL-006） |
| O11 | edge | keep | uni-kernel → user-session | schema v1 | O11 · query-error（结构化查询错误，不返回裸字符串） |
| O12 | edge | keep | uni-kernel → user-session | schema v1 | O12 · record-envelope（跨来源记录公共封套） |
| O13 | edge | keep | uni-kernel → user-session | schema v1 | O13 · local-storage-namespace（不透明逻辑 namespace 引用） |
| P18 | edge | keep | uni-kernel → uni-agent | record UniClaw.Kernel.Outcome.RuntimeOutcome | ### P18 Runtime Outcome（Uni Kernel → UniAgent） |
| P25 | edge | keep | uni-kernel → uni-agent | record UniClaw.Kernel.Runtime.AgentDecision | #### P25 Agent Decision Consultation（Uni Kernel internal driver → UniAgent realization，外部 seam） |
| P26 | edge | keep | uni-kernel → simulation-host | record UniClaw.Kernel.Trace.RunTraceArtifact | #### P26 Sealed Trace → ScenarioStimulus（测试 / 治理侧，非产品内边） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G2 | gap | track | user-session → uni-kernel | — | 任务定义（交互模型前奏 0a：Task Catalog，无编号边） |
| G3 | gap | track | uni-kernel → uni-kernel | — | Task Instance 创建（交互模型前奏 0c：Runtime 铸造，无独立协议边） |
| G4 | gap | track | uni-kernel → uni-kernel | — | Product Session 分配（交互模型前奏 0d：Runtime 铸造 + 显式 Binding，无编号边） |
| O1 | edge | track | user-session → uni-kernel | schema v1 | O1 · task-launch-request（调用方 → Runtime HTTP；交互模型前奏 0b） |
| O10 | edge | keep | user-session → uni-kernel | schema v1 | O10 · capability（五类只读 capability 请求/响应公共封套） |
| P17 | edge | keep | run-model → uni-kernel | record UniClaw.Kernel.Run.OutcomeState | ### P17 Terminal Outcome State（Run Model → Uni Kernel） |
| P24 | edge | keep | uni-agent → uni-kernel | record UniClaw.Kernel.Runtime.ActivationResult | #### P24 Legal Run Activation（Runtime activation path → Uni Kernel internal run driver） |

## 转移合法性 guard 摘要（引用本组件参与边的转移）

- **P18**
  - run.R-T15: terminal 评估证据充分（P16/P17）；分类 ∈ {Completion, Failure, SafeStop, Escalation}
- **P24**
  - activation-record.AR-T01: 初次提交或按原 activation key 显式 resume 后真实 RunId 已绑定；同 key 重放返回原 finalized record
  - activation-record.AR-T02: binding / contract / activation 校验确定失败；零新 Run、零新 Effect
  - activation-record.AR-T03: reserved 中断且无法证明原 owner；只能按原 key 显式 resume，禁止重新 mint
  - contract-view.CV-T00: accepted View 的 activation record 已 finalized 并绑定真实 RunId；同 key 重放复用原记录
  - run.R-T02: accepted Contract View 存在且 activation latch 未持有（幂等；基线 P24 / ADR-0019 §12）
- **P25**
  - consultation-journal.CJ-T01: 同一四元组与 request fingerprint 匹配；结果持久化后同 correlation retry 只回放
  - consultation-journal.CJ-T02: correlation / proposal 校验失败或 fail-closed；本 semantic consultation 计一次 HardCap
  - consultation-journal.CJ-T03: no-response / timeout 持久化为 rejected；不得产生 Effect；本 semantic consultation 计一次 HardCap
  - consultation-journal.CJ-T04: correlation 与 fingerprint 相同；继续等待原结果
  - run.R-T04: 观察已入 belief（每 Run 决策咨询一次；恢复时从当前 phase 继续）
