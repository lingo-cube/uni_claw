# PER-012 — PER-009 → PER-010/011 Semantic Migration Decision

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 2f633b04
design_status: FROZEN

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
- typed `semantic.checked` / `rendered.*` 是唯一前向正式语义。
- legacy `*.state` 只允许 typed→legacy→old consumer 的 egress-only 投影；禁止
  legacy 回流 typed/P2/P3/Fusion/WorldModel。
- `Unknown` / `Partial` / `Conflicted` 不得有损回写 ON/OFF；无法无损投影时为
  `unavailable / degraded`。
- consumer 逐个切换且禁止 dual-read；顺序为 observer/read-only → 普通非 effect-critical
  → Control/verification/effect-critical。
- rollback 只允许 routing rollback，回滚后的 consumer/run 标记 `legacy/degraded`；
  禁止新增 legacy consumer。
- 删除 legacy `*.state` 需同时满足 zero production readers、migration fixture PASS、
  shadow/cutover acceptance PASS、rollback observation window PASS。

## Focused Grill scope

1. typed-only forward authority 是否保持；
2. egress-only/no-loss/逐 consumer routing 是否可执行；
3. fixture matrix 是否足以阻断 PER-011 implementation 直到 contract 与 matrix FROZEN。

## Acceptance

1. mapping table、compatibility boundary、lineage admission 和 temporal semantics
  全部可判定。
2. rollout、rollback、stop conditions 和 fixture matrix 已记录。
3. PER-009 未重开；PER-010/011 保持 `FROZEN/closed`。
4. Product code、WorldModel authority、Grounding authority 均未改变。
5. focused grill G1–G7 全部 `PASS`；PER-012 contract 与 fixture matrix `FROZEN`。

## Verification

```yaml
level: CONTRACT
method: 当前文档与 PER-009/010/011 边界的 exact-path audit；git diff --check。
expected: Human Gate accepted；focused grill G1–G7 PASS；design_status 为 `FROZEN`；
PER-011 implementation 可另立 change，未在本 change 内执行。
actual: PASS；Human Gate accepted；focused grill G1–G7 PASS；无 Product code 或 PER-009 改动。
evidence: changes/PER-012/{spec,plan,state}.md; plans/2026-09-26-per-012-semantic-migration-fixture-matrix.md。
```

## Status log

- 2026-09-26 · created→resolving · Sol 选择亲自裁决 migration boundary；Luna 暂不修改文档。
- 2026-09-26 · resolving · 已完成 checked mapping、legacy `*.state`、lineage admission、temporal compatibility 与 Human Gate 准备。
- 2026-09-26 · resolving→accepted · Human Gate 裁决 typed-only forward semantics、egress-only legacy surface、no-loss projection、逐 consumer cutover、routing rollback、删除条件与 PER-011 implementation gate；PER-009 不重开。
- 2026-09-26 · accepted · fixture matrix 已补齐；进入 focused grill。
- 2026-09-26 · resolving→verified→closed · focused grill G1–G7 全 PASS；PER-012
  contract 与 fixture matrix FROZEN；PER-011 implementation 保持另立 change，未在本 change 执行。
