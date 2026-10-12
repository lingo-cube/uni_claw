# 协议卡片 · capability-plane（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P2 | edge | keep | capability-plane → evidence-ledger | record UniClaw.Kernel.Evidence.ObservationProposal | ### P2 Observation Ingress（观察生产者 → Evidence Ledger） |
| P9 | edge | keep | capability-plane → effect-boundary | record UniClaw.Kernel.Effects.CandidateBinding | ### P9 Candidate Binding（Grounding Provider → Effect Boundary） |
| P15 | edge | keep | capability-plane → effect-boundary | record UniClaw.Kernel.Effects.DispatchResult | ### P15 Dispatch Result（Capability Plane → Effect Boundary） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| G1 | gap | track | control-loop → capability-plane | — | 观察致动（无编号边 · 审计 D5；交互模型步骤 3） |
| P14 | edge | keep | effect-boundary → capability-plane | record UniClaw.Kernel.Effects.DispatchRequest | ### P14 Dispatch Request（Effect Boundary → Capability Plane） |

## 转移合法性 guard 摘要（引用本组件参与边的转移）

- **P14**
  - attempt.A-T01: EB lowering 产出 DeliveryTarget（locator 唯一载体）；capability 对授权态零感知
  - binding.B-T03: judgment（P13）通过 ∧ Gate 允许；同 binding 单次消费（P10 Forbidden：dispatch 后不得二次 dispatch）
- **P15**
  - attempt.A-T02: outcome = DeliveryCompleted——下游确认投递（≠ Effect ≠ Verified Effect，不变量 33/34）
  - attempt.A-T03: outcome = DeliveryFailed——下游确定拒绝 / 传输确定否定，effect 未送达
  - attempt.A-T04: outcome = UnknownOutcome——effect 可能已发生；唯一合法后继 = re-observe，NEVER blind redispatch（DSE-001 invariant）
  - run.R-T07: dispatch 已发出（P14）且下游确认时序门可查
