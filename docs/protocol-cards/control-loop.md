# 协议卡片 · control-loop（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G1 | gap | track | control-loop → capability-plane | — | 观察致动（无编号边 · 审计 D5；交互模型步骤 3） |
| P8 | edge | keep | control-loop → assurance | record UniClaw.Kernel.Control.ControlIntent | ### P8 Control Intent（Control Loop → Assurance / Effect Boundary） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P4 | edge | keep | world-model → control-loop | record UniClaw.Kernel.World.Slice | ### P4 Slice（World Model → Control Loop） |
| P5 | edge | track | run-model → control-loop | record UniClaw.Kernel.Run.RunState | ### P5 Run Snapshot（Run Model → Control Loop） |
| P6 | edge | keep | run-model → control-loop | record UniClaw.Kernel.Run.ExecutionContractView | ### P6 Execution Contract View（Run Model → Control Loop / Assurance） |
| P21 | edge | track | memory-system → control-loop | — | ### P21 Memory Recall（placeholder，Memory System → Agent / Control / World Model） |
