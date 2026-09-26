# PER-011 Plan — Fusion Explore → FROZEN

## 前置

只有 `changes/PER-010/state.md` 的 `design_status: FROZEN` 成立后才进入本 change；PER-010 未冻结时不得开始正式 fusion design。

## 垂直切片

1. **Source Authority Matrix**：按 checked/enabled/selected/focused/text/bounds/visibility/icon/state/role/resource-id 固定结构 authority、辅助 source、冲突处理和有效性条件。
2. **Temporal Alignment**：固定 CaptureId、ObservationCycleId、correlation、bounded window、mutation marker、frame conversion 和跨 revision 禁止项。
3. **Association/Fusion**：固定 hierarchy occurrence ↔ visual occurrence ↔ OCR token 粒度，覆盖 Unique/ManyToOne/OneToMany/Ambiguous/Unassociated。
4. **Conflict/Coverage**：固定 Supported/Conflicted/Unknown/Unsupported/Unaligned、absence/coverage、fail-closed 语义。
5. **Escalation/Provenance**：固定 fast→hierarchy→focused→deep 的 buyer/budget/attempt 边界和完整 provenance。
6. **Grill/disposition**：逐项攻击用户给出的 12 个问题；focused re-grill 后置 `design_status: FROZEN`。
7. **后续 implementation slices（未授权）**：source evidence fixtures → association fixtures → derived proposal ingress → bounded escalation telemetry → WorldModel scenario verification。

## Required artifacts

- Source Authority Matrix
- Temporal Alignment Diagram
- Conflict Matrix
- Failure Matrix
- Fusion Scenarios
- `plans/2026-09-26-per-011-visual-hierarchy-fusion.md`
- `changes/PER-011/{spec,plan,state}.md`

## Acceptance

- A1 明确字段级 source authority，不采用 XML/Visual 永远优先或 confidence voting。
- A2 明确同周期/跨周期/mutation/stale/缺 correlation 的 temporal semantics，禁止跨 revision 强行融合。
- A3 明确 hierarchy occurrence、visual occurrence、OCR token 的 association 粒度及多对一/一对多/歧义处理。
- A4 明确 absence/coverage，不把 omission 变成 false/absent。
- A5 明确 conflict fail-closed、完整 provenance 和 bounded escalation。
- A6 明确 fusion 不是第二 WorldModel，fused result 不直接变 target，Agent 不绕过 WorldModel。
- A7 12 项 Grill 全 PASS、PER-010 前置保持 FROZEN、本 change design_status = FROZEN。

## Verification（设计 change）

```yaml
level: CONTRACT
method: >-
  required-section lint（Authority Matrix/Temporal Diagram/Conflict Matrix/
  Failure Matrix/Scenarios）+ PER-010 prerequisite check + exact-path git status
  + git diff --check；复读 Product/UWorld/PER-009 authority boundary。
expected: A1–A7 满足；无 Product implementation 或 upstream boundary 改动。
actual: PASS，PER-010 FROZEN 前置成立；设计稿、矩阵、场景和 focused re-grill 完成。
evidence: changes/PER-011/spec.md; changes/PER-011/plan.md; changes/PER-010/state.md。
```
