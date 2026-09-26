# PER-011 — Visual + Hierarchy Fusion Architecture

版本：v0.1.1（narrow amendment；design-only）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 2f633b04
design_status: FROZEN

## Intent（WHAT/WHY）

在 PER-010 compatibility contract FROZEN 后，冻结 hierarchy、screenshot、OCR 等
observation source 如何按字段 authority、temporal alignment、coverage、association
和 provenance 形成 WorldModel 可消费的 evidence/proposal；fusion 不成为第二个
WorldModel 或新的 authority owner。

## Scope / Out of Scope

范围和排除项见 `spec.md`。本 change 只产生设计、matrix、failure/scenario、Grill
findings 和后续 implementation slices，不写 Product implementation。

## Frozen decisions

- Source authority 是 field/capability/validity/association/temporal 的合取，不是
  XML-first、visual-first 或 confidence voting；checked authority 必须足以表达
  当前 checked claim domain。
- semantic text 与 rendered text 分轴；bounds 是 frame-bound evidence；resource-id
  不是 canonical identity。
- 同 correlation/cycle、bounded delta、无已知 mutation 且 frame 可转换才具备
  `Aligned` 联合融合资格；Aligned 不证明 same-world-instant，field disagreement
  仍可 Conflicted。缺 correlation、跨 mutation、跨 revision 均不融合。
- omission、offscreen、virtualization、clip、overlay、source unavailable 都不产生
  element absence；coverage 不完整时输出 Unknown/coverage limitation。
- conflict 保留双方 evidence；authority 可产生 `Supported + overruled-source`，无
  authority 时 `Conflicted`，required property fail-closed。
- derived proposal 必须携带 `DerivedFromEvidenceIds` 与去重后的
  `TransitiveEvidenceBasis`；合法 shared leaf 通过多条 ancestry path 到达时只做
  leaf 去重并保持 valid，传递链 `A+B→F1`、`F1+C→F2` 的独立 basis 分别为
  `{A,B}`、`{A,B,C}`。self-reference、ancestry cycle、missing parent `EvidenceId`
  或 basis 与实际 parent ancestry closure 不一致均为 `MalformedLineage`，必须在
  P2/P3 admission 前拒绝、零新 belief contribution、不修复、不猜测、不降级为独立
  evidence，并保留原始/direct source evidence。
- fusion 只能输出带 provenance 的 derived ObservationProposal，经 P2→WorldModel；
  不直连 target、control、effect、Agent 或 identity。
- escalation 由 evidence insufficiency/conflict 触发，按 buyer + bounded budget
  限制 focused/deep 次数；confidence 低不是单独触发条件。
- PER-009 不重开；其 current realization 与本前向 contract 的 semantic migration
  mismatch 已记录。任何 PER-011 implementation 必须先通过 dedicated migration
  decision，明确 transition mapping、compatibility boundary 和 acceptance。

## Acceptance

1. Source Authority Matrix 完整。
2. Temporal Alignment Diagram 与 stale/mutation/correlation 规则完整。
3. Conflict Matrix、Failure Matrix、coverage/absence 语义完整。
4. Association granularity 覆盖 Unique/ManyToOne/OneToMany/Ambiguous/Unassociated。
5. provenance 字段和 bounded escalation 可追溯、可停机。
6. v0.1.1 focused re-grill 的 F1/F2/F3 全 PASS；没有需要重开 PER-009 的 upstream design conflict。
7. `design_status: FROZEN`；PER-010 保持 FROZEN；无 Product code 改动。
8. Q11 已完成：semantic migration mismatch 已记录；dedicated migration decision 是 PER-011 implementation 前置；PER-009 不重开。

## Verification

```yaml
level: CONTRACT
method: >-
  required-section lint + PER-010 prerequisite + exact-path git status +
  git diff --check；逐项核对 Product/UWorld/PER-009 authority boundary。
expected: Acceptance 1–8 满足；工作区既有 dirty 文件保持不变。
actual: PASS（矩阵、时间图、冲突/失败表、场景、provenance 与 bounded escalation
均已写入 spec；PER-010 FROZEN 前置成立；无 Product code 或 baseline 改动）。
evidence: changes/PER-011/spec.md; changes/PER-011/plan.md; changes/PER-010/state.md。
```

## Grill findings disposition

- 第一轮：将 authority、temporal alignment、coverage、association、conflict、
  provenance、escalation 分开检查；发现点已收敛到 `spec.md` 的独立矩阵。
- 原冻结设计的完整 Grill 结论保持不变；本次只对 F1/F2/F3 做 focused re-grill。
- F1/F2/F3 均 `PASS`；无需重开 PER-009 的 `UPSTREAM_DESIGN_CONFLICT`；后续只按 implementation slices 另立 change。
- Q11 `PASS`：PER-010/011 forward design 保持 FROZEN，PER-009 保持现状；semantic
  mismatch 进入 dedicated migration decision gate，未进行迁移实现。

## Status log

- 2026-09-26 · understanding→persisted · 验证 PER-010 contract 可作为前置；读取
  PER-009 mechanism、Product/UWorld baseline 和 existing perception boundary。
- 2026-09-26 · persisted→planned · 完成 authority matrix、temporal diagram、
  conflict/failure/coverage、association/provenance/escalation 草案。
- 2026-09-26 · planned→verified · required-section lint、前置检查、exact-path
  audit 和 v0.1.1 focused re-grill（F1/F2/F3）通过；design_status 置为 FROZEN。
- 2026-09-26 · verified→closed · v0.1.1 只修改三项设计语义；PER-010、PER-011
  均继续 FROZEN，Product baseline、PER-009、WorldModel、Grounding 均未重开。
- 2026-09-26 · grill→closed · Q11 对齐完成；迁移缺口已持久化，PER-011 implementation
  在 dedicated migration decision 完成前保持阻塞。
