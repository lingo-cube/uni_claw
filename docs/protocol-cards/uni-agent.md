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
  - run.R-T02: accepted Contract View 存在且 activation latch 未持有（幂等；基线 P24 / ADR-0019 §12）
- **P25**
  - run.R-T04: 观察已入 belief（每 Run 决策咨询一次；恢复时从当前 phase 继续）
