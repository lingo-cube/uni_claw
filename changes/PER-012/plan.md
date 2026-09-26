# PER-012 Plan — Semantic Migration Decision

版本：v0.1.1；decision-only；Human Gate 已通过，focused grill PASS，FROZEN。

## 计划

1. **Current-path inventory**：核对 PER-009 checked/`*.state`、source authority、
   temporal 和 Evidence lineage 的实际路径。
2. **Transition mapping**：冻结 Exact/Collapsed/tri-state、legacy projection、
   semantic/rendered 分轴和 Aligned 兼容规则。
3. **Admission boundary**：为 valid shared-leaf dedup、`MalformedLineage`、
   parent/closure/basis 校验定义 entry/exit 条件。
4. **Cutover decision**：记录旧 consumer 切换顺序、routing rollback、观测证据和
   停止条件；禁止 dual-read 与新增 legacy consumer。
5. **Fixture matrix**：覆盖 checked proof、egress-only/no-loss、consumer routing、
   deletion gate、lineage 和 temporal 兼容场景。
6. **Focused migration grill**：攻击 checked false、旧 `*.state` consumer、
   A+B→F1/F1+C→F2、malformed lineage 和 Aligned 后分歧。
7. **Implementation gate**：focused grill 通过且 contract/matrix FROZEN 后，另立
   implementation slices；
   本 change 不写 Product code。

## Required artifacts

- migration mapping table；
- compatibility/cutover boundary；
- fixture/scenario acceptance matrix；
- lineage admission decision；
- rollback and stop conditions；
- Human Gate disposition。
- `plans/2026-09-26-per-012-semantic-migration-fixture-matrix.md`。

## Verification

```yaml
level: CONTRACT
method: >-
  逐项核对 PER-009、PER-010、PER-011、current implementation evidence 与本 change
  的 mapping/acceptance；git diff --check；确认 Product code 与 PER-009 路径未改。
expected: A1–A8 可判定；未引入第二 authority owner；implementation gate 明确。
actual: Human Gate accepted; focused grill PASS；PER-012 contract 与 fixture matrix FROZEN；未写 Product code。
evidence: changes/PER-012/{spec,plan,state}.md；相关 PER-009/010/011 文档与 fixture matrix。
```
