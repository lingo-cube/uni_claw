# CORE-008 — Attempt/Trace 边界与跨领域 Slice 语义修订

lifecycle_state: planned · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent
parent_change: CORE-007
phase: absorbed-by-successors · execution_record_contract resolved (CORE-012 裁决 / CORE-013 实现 / ADR-0023)

## Intent

吸收审阅意见，修订 Attempt、Trace 和 Slice 的语义边界；本 Change 只改指南和证据，
不把语义修订误写成 Runtime/Trace 实现完成。

## Decisions

- Attempt 的产品语义保留；是否独立持久化与如何从可靠执行源生成另行验证。
- 普通诊断 Trace 不自动具备完整执行依据，不得单独支撑恢复或重试判断。
- Slice 是跨领域的局部观察/派生表示，不限定视觉；来源对象、范围、观察时间和处理版本
  必须区分。
- Receipt 是来源回执，不是 Attempt 替代品，也不自动证明外部结果。

## Verification

```yaml
review:
  level: CONTRACT
  method: 审阅意见逐条对照 qspec 与 CORE-007 基线
  expected: Attempt/Trace 分层与跨领域 Slice 规则无矛盾
  actual: verified; guide revision reviewed by a fresh read-only Luna pass
  evidence: evidence/2026-09-19-core-008-attempt-trace-slice.md
validation_gate:
  level: SCENARIO
  expected: 关闭普通诊断 Trace 后，仍能从可靠执行记录还原准备/尝试、请求、绑定和未知结果
  actual: not satisfied; current audit found no independent pre-dispatch durable record and
    Trace is explicitly lossy/disableable
  evidence: evidence/2026-09-19-core-008-attempt-trace-slice.md
```

## Status log

2026-09-19 · planned · 根据审阅意见建立语义修订 Change；不修改 Runtime/Trace 实现。
2026-09-19 · planned→verified · 完成指南修订和只读审阅；执行记录审计仍待人工决定范围后进行。
2026-09-19 · verified→resolve_human_gate · 审计确认当前 Trace/Receipt 不能独立证明发送前尝试依据；是否建立/复用可靠执行记录契约需人工决定。
2026-09-20 · gate-absorbed · P-E′ 销账（GATE-001 shadow kickoff）：原门触发器「是否建立/复用可靠执行记录契约」已由 CORE-012 docket 四问裁决（Q1=A / Q2=不复用 / Q3=B / Q4=最小面+五约束）、CORE-013 产品实现（`IReliableExecutionSource` 注入 EffectBoundary + append-only 文件 journal，567/567）与 ADR-0023 accepted 完整吸收；validation_gate 的 SCENARIO 语义由 CORE-011 仿真证明（S1–S12）+ CORE-013 真实进程重启测试承接。残余未决项：无。历史行不改写；lifecycle 关闭随首批批量 closure 由人裁决。
