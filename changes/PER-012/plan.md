# PER-012 Plan — Semantic Migration Decision

版本：v0.1；decision-only；等待 Human Gate。

## 计划

1. **Current-path inventory**：核对 PER-009 checked/`*.state`、source authority、
   temporal 和 Evidence lineage 的实际路径。
2. **Transition mapping**：冻结 Exact/Collapsed/tri-state、legacy projection、
   semantic/rendered 分轴和 Aligned 兼容规则。
3. **Admission boundary**：为 valid shared-leaf dedup、`MalformedLineage`、
   parent/closure/basis 校验定义 entry/exit 条件。
4. **Cutover decision**：定义旧 consumer 切换顺序、回滚条件、观测证据和停止条件。
5. **Focused migration grill**：攻击 checked false、旧 `*.state` consumer、
   A+B→F1/F1+C→F2、malformed lineage 和 Aligned 后分歧。
6. **Implementation gate**：仅在 Human Gate 通过后另立 implementation slices；
   本 change 不写 Product code。

## Required artifacts

- migration mapping table；
- compatibility/cutover boundary；
- fixture/scenario acceptance matrix；
- lineage admission decision；
- rollback and stop conditions；
- Human Gate disposition。

## Verification

```yaml
level: CONTRACT
method: >-
  逐项核对 PER-009、PER-010、PER-011、current implementation evidence 与本 change
  的 mapping/acceptance；git diff --check；确认 Product code 与 PER-009 路径未改。
expected: A1–A7 可判定；未引入第二 authority owner；implementation gate 明确。
actual: PENDING_HUMAN_GATE
evidence: changes/PER-012/{spec,plan,state}.md；相关 PER-009/010/011 文档与 fixture matrix。
```

