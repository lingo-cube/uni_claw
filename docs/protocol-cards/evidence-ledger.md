# 协议卡片 · evidence-ledger（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P3 | edge | keep | evidence-ledger → world-model | record UniClaw.Kernel.Evidence.EvidenceRecord | ### P3 Accepted Evidence Record（Evidence Ledger → World Model） |
| P12 | edge | keep | evidence-ledger → assurance | record UniClaw.Kernel.Evidence.EvidenceRecord | ### P12 Canonical Records Lookup（Evidence Ledger → Assurance） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P2 | edge | keep | capability-plane → evidence-ledger | record UniClaw.Kernel.Evidence.ObservationProposal | ### P2 Observation Ingress（观察生产者 → Evidence Ledger） |
