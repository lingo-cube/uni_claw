# CORE-009 — 可靠执行记录契约设计

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-review
parent_change: CORE-008
phase: closed

## Intent

优先审计现有 EffectBoundary / Runtime 记录能否复用，并提交可靠执行记录契约与验收计划。
只做只读审计、契约设计和文档更新，不修改产品 Runtime、Trace 或存储实现。

## Decisions

- Attempt 的产品语义保留；不预设独立类、独立存储或新 Core 记录。
- 普通诊断 Trace 不得成为恢复依据的唯一来源。
- 发送前登记准备不等于请求已发送；无 Receipt 时保持外部执行未知。
- Receipt 是来源回执；AttemptReport 是回流语义；Evidence/Claim 才承担 World 判断。
- Runtime 可以直接保存 Attempt，也可以从满足契约的可靠执行源生成 Attempt view。

## Verification

```yaml
audit:
  level: CONTRACT
  method: EffectBoundary / Runtime / Trace 只读代码审计
  expected: 复用能力、缺口和单一事实来源边界明确
  actual: verified; BindingLog/ReceiptLog 可复用部分关联，但缺少可证明的发送前完整执行记录
  evidence: docs/design/core-execution-record-contract-v0.1.md
design:
  level: CONTRACT
  method: Attempt 时序、Owner/Authority、缺失语义和恢复场景逐条审阅
  expected: 契约覆盖准备、提交、无 Receipt、未知协调、迟到反馈和重试
  actual: verified; 设计稿提交评审
  evidence: docs/design/core-execution-record-contract-v0.1.md
implementation:
  level: SCENARIO
  expected: 关闭 Trace 且无 Receipt 时仍可恢复已可靠登记的尝试信息
  actual: not run; implementation explicitly forbidden in this Change
  evidence: docs/design/core-execution-record-contract-v0.1.md
```

## Status log

2026-09-19 · planned→reviewed · 完成现有记录只读审计、契约设计和验收计划；评审结论为方向通过、契约 CHANGES_REQUIRED。
2026-09-19 · reviewed→closed · 用户确认契约修订通过；实现授权仍为 NONE，受控实现规划转入 CORE-010。
