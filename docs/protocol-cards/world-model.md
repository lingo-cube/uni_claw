# 协议卡片 · world-model（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P4 | edge | keep | world-model → control-loop | record UniClaw.Kernel.World.Slice | ### P4 Slice（World Model → Control Loop） |
| P11 | edge | keep | world-model → assurance | record UniClaw.Kernel.World.ActionAssuranceView | ### P11 Current WorldBelief View（World Model → Assurance / Effect Boundary） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P3 | edge | keep | evidence-ledger → world-model | record UniClaw.Kernel.Evidence.EvidenceRecord | ### P3 Accepted Evidence Record（Evidence Ledger → World Model） |
| P22 | edge | keep | effect-boundary → world-model | record UniClaw.Kernel.World.TransitionContext | ### P22 Runtime Transition Context（Runtime Effect Flow → World Model） |
| P23 | edge | keep | effect-boundary → world-model | record UniClaw.Kernel.World.UiRealization.CurrentGroundingView | ### P23 Continuity Request Seam（Effect / Obligation path ↔ World Model） |
