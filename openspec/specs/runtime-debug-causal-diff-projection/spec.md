# runtime-debug-causal-diff-projection Specification

## Purpose
定义 Evidence Packet 内 causal chain、EvidenceRef 参与位置和已存 Good/Bad 边界的确定性只读投影，用于机械阅读证据而不计算 FDP、Owner 或 repair eligibility。

## Requirements

### Requirement: Causal/evidence tree projection
The Toolchain SHALL project the packet `EvidenceChain` as a causal/evidence tree with the closed stage vocabulary `raw → normalized → fused → canonical → semanticAdmission → affordance → runtimeState`, each stage carrying its stored status, summary, and sorted input/decision/output refs. Stage-type pruning (`--prune`) SHALL hide stages from the projection only (never mutate the packet); `--only-decisions` and `--only-evidence` SHALL filter to decision-bearing and evidence-bearing stages respectively. Absent `EvidenceChain` SHALL fail closed with `INSUFFICIENT_TRACE_COVERAGE`.

#### Scenario: Causal tree by type pruning
- **WHEN** a user prunes `raw,fused` from the causal tree
- **THEN** the projection SHALL omit those stage types and SHALL report the pruned stage names, while the underlying packet stays byte-identical

#### Scenario: Decision-only causal view
- **WHEN** a user requests `--only-decisions`
- **THEN** every projected stage SHALL carry at least one decisionRef

### Requirement: Evidence-chain query
The Toolchain SHALL trace one EvidenceRef across the chain: for each stage where the ref participates as input, decision, or output, the projection SHALL report the stage, role, and stored stage status, plus the ref metadata (kind, uri, digest, mediaType, integrity, selector). An unknown ref SHALL fail closed with `EVIDENCE_UNAVAILABLE`; a stored `IDENTITY_MISMATCH` integrity SHALL surface as `IDENTITY_MISMATCH`. The tool SHALL NOT dereference the ref URI.

#### Scenario: Ref traced across stages
- **WHEN** a ref participates in `normalized` and `fused` stages
- **THEN** the chain projection SHALL include those stage/role pairs in chain order

### Requirement: Packet-scoped differential projection
The Toolchain SHALL project the stored `GoodComparison` and `BadComparison` (status/label/summary/axes/evidenceRefs) together with stored `LastGood` and `FirstBad`. The projection SHALL carry stored facts only and SHALL NOT compute FDP, Owner, GapKind, or repair eligibility. Absent comparison facts SHALL fail closed with `INSUFFICIENT_TRACE_COVERAGE`.

#### Scenario: Diff surfaces the stored first divergence boundary
- **WHEN** a packet stores `LastGood.stage=canonical` and `FirstBad.stage=semanticAdmission`
- **THEN** the diff projection SHALL reproduce both stored facts verbatim with their evidence refs
