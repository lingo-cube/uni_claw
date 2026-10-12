# 协议卡片 · effect-boundary（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P10 | edge | keep | effect-boundary → assurance | record UniClaw.Kernel.Effects.CanonicalBinding | ### P10 Canonical Binding（Effect Boundary → Assurance） |
| P14 | edge | keep | effect-boundary → capability-plane | record UniClaw.Kernel.Effects.DispatchRequest | ### P14 Dispatch Request（Effect Boundary → Capability Plane） |
| P22 | edge | keep | effect-boundary → world-model | record UniClaw.Kernel.World.TransitionContext | ### P22 Runtime Transition Context（Runtime Effect Flow → World Model） |
| P23 | edge | keep | effect-boundary → world-model | record UniClaw.Kernel.World.UiRealization.CurrentGroundingView | ### P23 Continuity Request Seam（Effect / Obligation path ↔ World Model） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P9 | edge | keep | capability-plane → effect-boundary | record UniClaw.Kernel.Effects.CandidateBinding | ### P9 Candidate Binding（Grounding Provider → Effect Boundary） |
| P13 | edge | keep | assurance → effect-boundary | record UniClaw.Kernel.Assurance.AssuranceJudgment | ### P13 Assurance Judgment（Assurance → Effect Boundary） |
| P15 | edge | keep | capability-plane → effect-boundary | record UniClaw.Kernel.Effects.DispatchResult | ### P15 Dispatch Result（Capability Plane → Effect Boundary） |

## 转移合法性 guard 摘要（引用本组件参与边的转移）

- **P10**
  - binding.B-T01: revision current ∧ 无歧义 ∧ target 存在于 current WorldBelief（否则走 B-T02；不变量 24：candidate 无 dispatch 权威）
- **P13**
  - judgment.J-T01: 针对三元组 (IntentId + BindingId + RevisionId)；immutable（冻结语义）——issued 后不可改写
- **P14**
  - attempt.A-T01: EB lowering 产出 DeliveryTarget（locator 唯一载体）；capability 对授权态零感知
  - binding.B-T03: judgment（P13）通过 ∧ Gate 允许；同 binding 单次消费（P10 Forbidden：dispatch 后不得二次 dispatch）
- **P15**
  - attempt.A-T02: outcome = DeliveryCompleted——下游确认投递（≠ Effect ≠ Verified Effect，不变量 33/34）
  - attempt.A-T03: outcome = DeliveryFailed——下游确定拒绝 / 传输确定否定，effect 未送达
  - attempt.A-T04: outcome = UnknownOutcome——effect 可能已发生；唯一合法后继 = re-observe，NEVER blind redispatch（DSE-001 invariant）
  - run.R-T07: dispatch 已发出（P14）且下游确认时序门可查
