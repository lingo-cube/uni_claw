# CORE-010 — 可靠执行记录首个受控实现切片（规划）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree · Human closure 2026-09-20（首批批量 closure，GATE-001 台账事件 #1）
triage_label: ready-for-review
parent_change: CORE-009
phase: implementation_authorization_required · implementation_forbidden

## Intent

在 CORE-009 契约通过后，形成首个受控实现切片的垂直计划；优先复用现有
EffectBoundary / Runtime 记录，不进入实现。

## Frozen Constraints

- Attempt 产品语义保留，不新增 Core 顶层事实模型。
- 普通诊断 Trace 不是恢复唯一来源。
- 发送前准备提交成功/失败/未知必须分开；未知不允许换身份发送。
- 同一逻辑干预的外部重试是新 Attempt；补偿是新 Effect。
- 本轮只规划，不修改产品代码、测试、Trace、存储或运行配置。

## Verification

```yaml
plan:
  level: CONTRACT
  method: 现有 seam 复核 + CORE-009 契约逐条映射 + 垂直切片计划
  expected: 实施入口、Owner、阻塞条件、验收顺序和禁止范围明确
  actual: pending review
  evidence: plans/2026-09-19-core-010-reliable-execution-record.md
implementation:
  level: SCENARIO
  expected: first slice proves reliable pre-dispatch registration and unresolved discovery
  actual: not run; implementation forbidden
  evidence: plans/2026-09-19-core-010-reliable-execution-record.md
```

## Status log

2026-09-19 · planned · CORE-009 契约通过后建立实现规划 Change；不自动进入实现。
2026-09-19 · plan refinement · 加入四个确定性仿真 crash cut point；明确仿真重启不等于 Trace writer 故障或同一对象内 phase resume。
2026-09-19 · read-only simulation audit · 确认 `SimulationHost.Compose` 可重建 Host A/B，
但当前没有跨 Host 恢复注入；后续若获授权，先在测试组合层设计独立于 Trace 的
`ReliableExecutionSourceFixture`，不修改产品 Runtime、Trace 或存储实现。
2026-09-19 · human-confirmed · 用户确认仿真范围、Owner 边界、测试侧 fixture 与四个
crash cut point；已记录仿真设计与验收契约，保持实现授权为 NONE。
2026-09-19 · approved · 用户确认 CORE-010 仿真设计与验收契约通过；下一阶段仅等待
单独的实现授权，当前仍禁止修改产品 Runtime、Trace、存储、Core 模型和测试实现。
2026-09-19 · implementation authorized · 用户单独授权测试侧
`ReliableExecutionSourceFixture` 完整首切片；实现落子 Change CORE-011
（`changes/CORE-011/state.md`），本 Change 保持规划记录不变。CORE-011 已
closed：S1–S12 全绿、全量回归 544/544、src/ 零改动。产品侧
（EffectBoundary.Dispatch 内执行源接入）仍是后续独立授权 Gate。
2026-09-20 · closed·human-closure · 首批批量 closure（GATE-001 台账事件 #1）：人批准关闭。规划职责已交付——产品侧接入由 CORE-013（closed，567/567）按 CORE-012 计划 §5 完成，本 Change 无残余授权义务。
