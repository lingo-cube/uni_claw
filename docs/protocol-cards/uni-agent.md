# 协议卡片 · uni-agent（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G5 | gap | track | uni-agent → run-model | — | 契约 Authoring（交互模型前奏 0e：UniAgent 形成，P1 上游，无编号边） |
| P1 | edge | keep | uni-agent → run-model | record UniClaw.Kernel.Run.ExecutionContract | ### P1 Execution Contract Admission（UniAgent → Run Model） |
| P19 | edge | track | uni-agent → user-session | — | ### P19 Goal Evaluation（UniAgent → user/session，未来） |
| P20 | edge | track | uni-agent → memory-system | — | ### P20 Memory Persist（placeholder，→ Memory System） |
| P24 | edge | keep | uni-agent → uni-kernel | record UniClaw.Kernel.Runtime.ActivationResult | #### P24 Legal Run Activation（Runtime activation path → Uni Kernel internal run driver） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P18 | edge | keep | uni-kernel → uni-agent | record UniClaw.Kernel.Outcome.RuntimeOutcome | ### P18 Runtime Outcome（Uni Kernel → UniAgent） |
| P25 | edge | keep | uni-kernel → uni-agent | record UniClaw.Kernel.Runtime.AgentDecision | #### P25 Agent Decision Consultation（Uni Kernel internal driver → UniAgent realization，外部 seam） |

## 转移合法性 guard 摘要（引用本组件参与边的转移）

- **P1**
  - run.R-T01: proposal valid（同 version 重复 admit 幂等复用；基线 P1 Validity）
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
