# PER-011 — Visual + Hierarchy Fusion Architecture

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
  XML-first、visual-first 或 confidence voting。
- semantic text 与 rendered text 分轴；bounds 是 frame-bound evidence；resource-id
  不是 canonical identity。
- 同 correlation/cycle、bounded delta、无 mutation 且 frame 可转换才可对齐；
  缺 correlation、跨 mutation、跨 revision 均不融合。
- omission、offscreen、virtualization、clip、overlay、source unavailable 都不产生
  element absence；coverage 不完整时输出 Unknown/coverage limitation。
- conflict 保留双方 evidence；authority 可产生 `Supported + overruled-source`，无
  authority 时 `Conflicted`，required property fail-closed。
- fusion 只能输出带 provenance 的 derived ObservationProposal，经 P2→WorldModel；
  不直连 target、control、effect、Agent 或 identity。
- escalation 由 evidence insufficiency/conflict 触发，按 buyer + bounded budget
  限制 focused/deep 次数；confidence 低不是单独触发条件。

## Acceptance

1. Source Authority Matrix 完整。
2. Temporal Alignment Diagram 与 stale/mutation/correlation 规则完整。
3. Conflict Matrix、Failure Matrix、coverage/absence 语义完整。
4. Association granularity 覆盖 Unique/ManyToOne/OneToMany/Ambiguous/Unassociated。
5. provenance 字段和 bounded escalation 可追溯、可停机。
6. 12 项 Grill 全 PASS；无 upstream design conflict。
7. `design_status: FROZEN`；PER-010 保持 FROZEN；无 Product code 改动。

## Verification

```yaml
level: CONTRACT
method: >-
  required-section lint + PER-010 prerequisite + exact-path git status +
  git diff --check；逐项核对 Product/UWorld/PER-009 authority boundary。
expected: Acceptance 1–7 满足；工作区既有 dirty 文件保持不变。
actual: PASS（矩阵、时间图、冲突/失败表、场景、provenance 与 bounded escalation
均已写入 spec；PER-010 FROZEN 前置成立；无 Product code 或 baseline 改动）。
evidence: changes/PER-011/spec.md; changes/PER-011/plan.md; changes/PER-010/state.md。
```

## Grill findings disposition

- 第一轮：将 authority、temporal alignment、coverage、association、conflict、
  provenance、escalation 分开检查；发现点已收敛到 `spec.md` 的独立矩阵。
- Focused re-grill：12 项全部 PASS；无 `UPSTREAM_DESIGN_CONFLICT`。
- Freeze：PER-011 design FROZEN；后续只按 implementation slices 另立 change。

## Status log

- 2026-09-26 · understanding→persisted · 验证 PER-010 contract 可作为前置；读取
  PER-009 mechanism、Product/UWorld baseline 和 existing perception boundary。
- 2026-09-26 · persisted→planned · 完成 authority matrix、temporal diagram、
  conflict/failure/coverage、association/provenance/escalation 草案。
- 2026-09-26 · planned→verified · required-section lint、前置检查、exact-path
  audit 和 focused re-grill 通过；design_status 置为 FROZEN。
- 2026-09-26 · verified→closed · 设计 acceptance 已证明；只新增 PER-011 文档与计划，既有 Product dirty 文件未触碰。
