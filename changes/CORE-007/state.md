# CORE-007 — vNext 最小候选基线审阅与 UI realization 实现
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent
parent_change: CORE-004
phase: closed

## Intent

以 vNext.1 为参考设计，区分语义必要性与字段/类型必要性，锁定当前材料支持的最小
候选基线；随后以现有 UI realization 做一版实现，不提前决定其他领域的抽取。

## Review lock

- Core 核心记录：`Clause`、`Segment`、`Evidence`、`Claim`、`Effect`。
- World 内部结构：`Slice`，按用途需要且历史依据必须固定保存或可靠重建。
- Effect 内部结构：`Attempt`、`TargetBinding`。
- Event 发生语义保留，独立 Event 类型待具体表达/演化反例。
- 本锁定不是严格最小性证明，不冻结最终字段、继承、存储或迁移。

## Acceptance

1. vNext.1 字段已按建档必填、用途条件必填、发生后追加、展示选填和暂不进入 Core 分类。
2. 最小候选基线与审阅依据、场景压力和现有测试证据互相可追溯。
3. UI realization 通过现有 Kernel projection seam 表达最小基线。
4. UI 专用细节不成为跨领域 Core 强制字段，缺少保证只局部阻塞。
5. 不新增第二事实权威，不迁移旧 UI/Kernel 类，不冻结其他 realization 的结构。

## Verification

```yaml
review:
  level: CONTRACT + SCENARIO_BACKED
  method: vNext.1 字段等级审阅 + 后续审阅材料对照 + 现有 UI projection/test 对照
  expected: 语义保留但字段按用途裁剪，最小候选基线明确
  actual: review evidence recorded; UI projection seam implements the locked UI minimum
  evidence: evidence/2026-09-19-vnext-field-minimal-review.md
ui_implementation:
  level: DETERMINISTIC + SCENARIO
  expected: existing UI scroll/click seam projects the locked baseline without second authority
  actual: ProjectClauses plus existing World/Effect projections verified by Core and Kernel tests
  evidence: evidence/2026-09-19-core-007-ui-realization.md
closure:
  level: REVIEW + VERIFY
  actual: Luna read-only review found no Core-set counterexample; documentation was narrowed to
    the implemented UI boundary; docs metadata and full solution tests pass
  evidence: evidence/2026-09-19-core-007-ui-realization.md
```

## Status log

2026-09-19 · planned · 根据 vNext.1 和后续审阅要求建立合并 Change；先审阅锁定，再做 UI realization。
2026-09-19 · planned→review_locked · 完成字段等级和最小候选基线审阅；UI implementation 尚未开始。
2026-09-19 · review_locked→verified · 完成 UI Clause 投影和现有滚动—点击 projection seam 验证；未做源码迁移或跨领域扩张。
2026-09-19 · verified→closed · 完成实现审阅、指南边界校准和回归验证；后续非 Slice basis 与 richer Attempt 字段保留为用途条件关口。
