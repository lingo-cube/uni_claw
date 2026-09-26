# PER-012 — PER-009 → PER-010/011 Semantic Migration Decision

lifecycle_state: resolving · disposition: none · depth: decision-heavy · base: 2f633b04
design_status: DRAFT_HUMAN_GATE

## Intent（WHAT/WHY）

为 PER-011 implementation 提供一个独立的 semantic migration decision，处理
PER-009 current realization 与 PER-010/011 forward design 的已知差异；不重开
PER-009，不修改 Product code。

## Decisions in scope

- Exact checked 只由 two-state contract + Exact metadata + contract/fixture evidence
  共同证明。
- 无 Exact proof 的 legacy boolean false 保持 `Unknown(partial-unrepresentable)`。
- shared `*.state` 只保留为 legacy compatibility surface；新 semantic/rendered
  分轴不被静默回写旧语义。
- 合法 shared-leaf duplication 做 leaf 去重；非法 lineage 为 `MalformedLineage`
  并在 admission 前拒绝。
- `Aligned` 只表示 eligible for joint fusion；不证明 same-world-instant。

## Unresolved Human Gate

1. legacy `*.state` surface 的终止条件；
2. old consumer 的切换/回滚顺序；
3. implementation entry fixture/scenario matrix。

## Acceptance

1. mapping table、compatibility boundary、lineage admission 和 temporal semantics
   全部可判定。
2. rollout、rollback、stop conditions 和 evidence owner 已记录。
3. PER-009 未重开；PER-010/011 保持 `FROZEN/closed`。
4. Product code、WorldModel authority、Grounding authority 均未改变。

## Verification

```yaml
level: CONTRACT
method: 当前文档与 PER-009/010/011 边界的 exact-path audit；git diff --check。
expected: migration decision 可进入 Human Gate；未进入 implementation。
actual: PENDING_HUMAN_GATE
evidence: changes/PER-012/spec.md; changes/PER-012/plan.md。
```

## Status log

- 2026-09-26 · created→resolving · Sol 选择亲自裁决 migration boundary；Luna 暂不修改文档。
- 2026-09-26 · resolving · 已记录 checked mapping、legacy `*.state`、lineage admission、temporal compatibility 与 Human Gate；等待裁决。

