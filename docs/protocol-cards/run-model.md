# 协议卡片 · run-model（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P5 | edge | track | run-model → control-loop | record UniClaw.Kernel.Run.RunState | ### P5 Run Snapshot（Run Model → Control Loop） |
| P6 | edge | keep | run-model → control-loop | record UniClaw.Kernel.Run.ExecutionContractView | ### P6 Execution Contract View（Run Model → Control Loop / Assurance） |
| P7 | edge | keep | run-model → assurance | record UniClaw.Kernel.Run.ProofObligationState | ### P7 Obligation View（Run Model → Assurance） |
| P17 | edge | keep | run-model → uni-kernel | record UniClaw.Kernel.Run.OutcomeState | ### P17 Terminal Outcome State（Run Model → Uni Kernel） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G5 | gap | track | uni-agent → run-model | — | 契约 Authoring（交互模型前奏 0e：UniAgent 形成，P1 上游，无编号边） |
| P1 | edge | keep | uni-agent → run-model | record UniClaw.Kernel.Run.ExecutionContract | ### P1 Execution Contract Admission（UniAgent → Run Model） |
| P16 | edge | keep | assurance → run-model | record UniClaw.Kernel.Assurance.OutcomeProof | ### P16 Outcome Proof（Assurance → Run Model） |

## 转移合法性 guard 摘要（引用本组件参与边的转移）

- **P1**
  - run.R-T01: proposal valid（同 version 重复 admit 幂等复用；基线 P1 Validity）
- **P16**
  - run.R-T16: 完成证明充分 → Completion（证据不足不进 terminal，见 R-T09 的 TerminalNotProven 投影）
