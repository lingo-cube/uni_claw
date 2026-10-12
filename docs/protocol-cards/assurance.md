# 协议卡片 · assurance（生成产物）

> 派生自 `schemas/protocol/state-machine.yaml` 边表（PROTOCOL-MACHINE-001
> D8 第四出口，纯派生视图——权威不搬家）；手改会被 `--check` 抓出。

## 出边（本组件 → 对端）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P13 | edge | keep | assurance → effect-boundary | record UniClaw.Kernel.Assurance.AssuranceJudgment | ### P13 Assurance Judgment（Assurance → Effect Boundary） |
| P16 | edge | keep | assurance → run-model | record UniClaw.Kernel.Assurance.OutcomeProof | ### P16 Outcome Proof（Assurance → Run Model） |

## 入边（对端 → 本组件）

| id | kind | status | 方向 | 载荷 | 基线锚 |
|---|---|---|---|---|---|
| P7 | edge | keep | run-model → assurance | record UniClaw.Kernel.Run.ProofObligationState | ### P7 Obligation View（Run Model → Assurance） |
| P8 | edge | keep | control-loop → assurance | record UniClaw.Kernel.Control.ControlIntent | ### P8 Control Intent（Control Loop → Assurance / Effect Boundary） |
| P10 | edge | keep | effect-boundary → assurance | record UniClaw.Kernel.Effects.CanonicalBinding | ### P10 Canonical Binding（Effect Boundary → Assurance） |
| P11 | edge | keep | world-model → assurance | record UniClaw.Kernel.World.ActionAssuranceView | ### P11 Current WorldBelief View（World Model → Assurance / Effect Boundary） |
| P12 | edge | keep | evidence-ledger → assurance | record UniClaw.Kernel.Evidence.EvidenceRecord | ### P12 Canonical Records Lookup（Evidence Ledger → Assurance） |

## 转移合法性 guard 摘要（引用本组件参与边的转移）

- **P10**
  - binding.B-T01: revision current ∧ 无歧义 ∧ target 存在于 current WorldBelief（否则走 B-T02；不变量 24：candidate 无 dispatch 权威）
- **P13**
  - judgment.J-T01: 针对三元组 (IntentId + BindingId + RevisionId)；immutable（冻结语义）——issued 后不可改写
- **P16**
  - run.R-T16: 完成证明充分 → Completion（证据不足不进 terminal，见 R-T09 的 TerminalNotProven 投影）
