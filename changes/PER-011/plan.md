# PER-011 Plan — Fusion Explore → FROZEN

版本：v0.1.1（narrow amendment；design-only；保持 FROZEN）

## 前置

只有 `changes/PER-010/state.md` 的 `design_status: FROZEN` 成立后才进入本 change；PER-010 未冻结时不得开始正式 fusion design。PER-011 implementation 另有硬前置：必须先完成 dedicated migration decision，处理当前 PER-009 realization 与本前向 contract 的 semantic migration mismatch；该决策不重开 PER-009。

## 垂直切片

1. **Source Authority Matrix**：按 checked/enabled/selected/focused/text/bounds/visibility/icon/state/role/resource-id 固定结构 authority、辅助 source、冲突处理和有效性条件。
2. **Temporal Alignment**：固定 CaptureId、ObservationCycleId、correlation、bounded window、mutation marker、frame conversion；`Aligned` 只表示 eligible for joint fusion，不证明 same-world-instant。
3. **Association/Fusion**：固定 hierarchy occurrence ↔ visual occurrence ↔ OCR token 粒度，覆盖 Unique/ManyToOne/OneToMany/Ambiguous/Unassociated；derived proposal 携带 `DerivedFromEvidenceIds` 与去重后的 `TransitiveEvidenceBasis`，并区分合法 shared-leaf duplication 与 `MalformedLineage`。
4. **Conflict/Coverage**：固定 Supported/Conflicted/Unknown/Unsupported/Unaligned、absence/coverage、fail-closed 语义；Aligned + disagreement 仍可 Conflicted。
5. **Escalation/Provenance**：固定 fast→hierarchy→focused→deep 的 buyer/budget/attempt 边界和完整 provenance。
6. **Grill/disposition**：本次 v0.1.1 只对 F1/F2/F3 做 focused re-grill；结果保持 `design_status: FROZEN`。
7. **后续 implementation slices（未授权）**：dedicated migration decision → source evidence fixtures → association fixtures → derived proposal ingress → bounded escalation telemetry → WorldModel scenario verification。migration decision 未完成前不得执行其后的 implementation slices。

## Required artifacts

- Source Authority Matrix
- Temporal Alignment Diagram
- Conflict Matrix
- Failure Matrix
- Fusion Scenarios
- `plans/2026-09-26-per-011-visual-hierarchy-fusion.md`
- `changes/PER-011/{spec,plan,state}.md`

## Acceptance

- A1 明确字段级 source authority，不采用 XML/Visual 永远优先或 confidence voting；checked authority 要求 capability 足以表达当前 domain。
- A2 明确同周期/跨周期/mutation/stale/缺 correlation 的 temporal semantics；Aligned 只表示 eligible for joint fusion，禁止把它解释为 same-world-instant 或因而自动选源。
- A3 明确 hierarchy occurrence、visual occurrence、OCR token 的 association 粒度及多对一/一对多/歧义处理。
- A4 明确 absence/coverage，不把 omission 变成 false/absent。
- A5 明确 conflict fail-closed、完整 provenance、`DerivedFromEvidenceIds`、`TransitiveEvidenceBasis` 和 bounded escalation；合法 shared-leaf duplication 只做 leaf 去重，self-reference/cycle/missing parent/basis mismatch 必须为 `MalformedLineage` 并在 P2/P3 admission 前拒绝，derived evidence 不得自我 corroborate。
- A6 明确 fusion 不是第二 WorldModel，fused result 不直接变 target，Agent 不绕过 WorldModel。
- A7 F1/F2/F3 focused re-grill 全 PASS、PER-010 前置保持 FROZEN、本 change design_status = FROZEN。
- A8 已记录 PER-009 semantic migration mismatch；PER-011 implementation 明确以 dedicated migration decision 为前置；PER-009 未重开。

## Verification（设计 change）

```yaml
level: CONTRACT
method: >-
  required-section lint（Authority Matrix/Temporal Diagram/Conflict Matrix/
  Failure Matrix/Scenarios）+ PER-010 prerequisite check + exact-path git status
  + git diff --check；复读 Product/UWorld/PER-009 authority boundary。
expected: A1–A8 满足；无 Product implementation 或 upstream boundary 改动。
actual: PASS，PER-010 FROZEN 前置成立；v0.1.1 三项窄修订、矩阵、场景和 focused re-grill 完成；migration mismatch 已记录且 implementation gate 已冻结为前置条件。
evidence: changes/PER-011/spec.md; changes/PER-011/plan.md; changes/PER-010/state.md。
```
