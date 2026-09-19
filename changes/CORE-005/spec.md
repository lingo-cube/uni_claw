# CORE-005 — World Evidence 到 Claim 的责任对齐切片

Status: `READY FOR IMPLEMENTATION`
Triage: `ready-for-agent`
Publication: repository Change State（本仓库未配置外部 issue tracker）
Parent: CORE-004（步骤子规格）

## Problem Statement

CORE-003 已验证 Core 的 World 对象，CORE-004 已建立现有模型责任矩阵。当前 Kernel
已经维护 Evidence admission、Segment/Occurrence continuity、Slice、World Claim
和 revision history，但这些职责尚未按 Core 语义明确对齐。若直接迁移类型，容易让
projection、cache 或 UI occurrence 成为第二套 World 事实。

需要先完成一条只读、可验证的 World 对齐切片，明确 Evidence、Segment、Slice、Claim
之间的责任关系，并证明历史依据、未知和现实身份边界不被投影改变。

## Solution

以现有 Kernel World/Evidence seam 作为唯一事实来源，建立如下对齐关系：

```text
Kernel Evidence admission / provenance
  → Core Evidence
Kernel container continuity / scoped observation
  → Core Segment + Slice
Kernel World claim / revision history
  → Core Claim
```

这是一条只读语义投影和责任对齐切片，不迁移 Kernel World owner，不创建第二个
Evidence、Segment、Slice 或 Claim writer，也不让 Core 投影反向写回 World。

## User Stories

1. As a World owner, I want Evidence admission and provenance to remain canonical in the current World realization, so that Core does not become a second evidence ledger.
2. As a Core consumer, I want projected Evidence to preserve source, time, processing chain, and context, so that later Claim review can reconstruct its basis.
3. As a World owner, I want Segment continuity to remain separate from occurrence perception, so that a continuous reference does not claim correct real identity.
4. As a mobile traversal realization, I want each observation to create or reference a new Slice, so that scrolling never rewrites an earlier Slice.
5. As a Core consumer, I want overlapping, contained, repeated, and partial Slices preserved, so that coverage is not confused with completeness.
6. As a Claim consumer, I want projected Claims to preserve disposition, conflict, correction, and supporting Evidence, so that judgment does not become reality.
7. As a history reviewer, I want a later revision to retain earlier Evidence basis, so that latest or current canonical views cannot silently rewrite history.
8. As a safety reviewer, I want omission and missing observation to remain Unknown, so that absent data does not become a negative Claim.
9. As a cross-domain consumer, I want UI occurrence identifiers and locator payloads to stay opaque, so that Core does not depend on UI classes.
10. As a migration owner, I want this slice to define ownership and projection only, so that later source migration can be decided from evidence rather than assumed from the projection.

## Implementation Decisions

- Use the existing highest seam: Kernel Evidence/World realization to Core semantic
  projection. Do not introduce a new interface or a second adapter for this slice.
- Keep Kernel Evidence admission, World reconciliation, revision storage, continuity and
  current-view evaluation as the current authority during this Change.
- Map only cross-domain semantics: Evidence provenance, Segment continuity reference, Slice
  scope/history, and Claim disposition/basis. Keep UI occurrence, perception, locator,
  calibration and reconciliation algorithms in the realization.
- Preserve historical basis through explicit immutable references. A later revision may
  create a new Core Claim or Slice but may not rewrite the earlier record's Evidence basis.
- Preserve Unknown and Conflict. Do not derive a negative Claim from an empty Slice,
  omitted occurrence, missing response, or absent visual change.
- Treat Event and causality as Claim views in this slice; do not add an Event type or a
  second causal authority.
- This Change may refine candidate value fields only when a test demonstrates loss of the
  required semantic distinction. It may not freeze final package, storage, serialization,
  or inheritance choices.

## Testing Decisions

- Use the existing Core projection seam and current Evidence/World tests as the single
  external test surface; do not test private mapping helpers or legacy constructor shape.
- Positive paths cover admitted Evidence, continuous Segment, multiple overlapping Slices,
  Claim revision, and post-observation re-evaluation.
- Negative/history paths cover latest rewrite, stale current location, omission-as-absence,
  conflict loss, projection write-back, and UI identity being mistaken for real identity.
- Tests must prove one canonical writer remains: the projection is read-only and no Core
  record can change Kernel World state.
- Test results establish this alignment slice only; they do not prove the entire legacy
  model is aligned or that source migration is complete.

## Out of Scope

- Effect, Attempt, TargetBinding execution-contract expansion.
- Generic BasisRef design for non-Slice fixed resources.
- Moving or deleting Kernel Evidence/World classes.
- Introducing an independent Event type.
- Replacing perception, reconciliation, storage, Runtime, Assurance, Trace, or UI models.
- Declaring Segment identity correctness from a record id or occurrence id.

## Further Notes

The slice is selected because it has the clearest current buyer and highest existing seam.
Effect/Attempt alignment remains gated by the CORE-003 grill findings on request snapshots,
executor/authorization references, dispatch progress, external execution and coordination
state. Any counterexample that cannot be expressed by this World slice must be recorded
before expanding Core.
