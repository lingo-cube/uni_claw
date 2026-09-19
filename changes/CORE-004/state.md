# CORE-004 — Core 与现有模型的责任对齐
lifecycle_state: verified · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent
children: CORE-005, CORE-006, CORE-007

## Intent

在 CORE-003 Core 对象基线之上，建立现有模型的责任对齐矩阵和选择规则；先做语义
分类与验证，不进行源码迁移，不冻结继承树或字段。

## Acceptance

1. 现有候选模型按责任分解，明确 Core 语义、下层职责、事实权威和历史义务。
2. 每个对齐决定明确选择上提、删除、合并、拆分、替换、继承、组合、引用或保留。
3. 没有旧类与 Core 的默认一一对应，没有第二套 World/Evidence/Binding 事实。
4. 继承、组合、拆分和删除规则均有场景或测试证据，不由字段相似度决定。
5. 缺少的字段/使用保证形成局部 gate，只阻塞依赖它的用途。
6. 本 Change 不改产品源码，不宣称迁移完成，不冻结最终字段/API/存储。

## Verification

```yaml
level: DETERMINISTIC + DOCUMENTED
primary_seam: responsibility-matrix review + existing semantic test seams
expected: every aligned candidate has an owner, relation choice, evidence, and unresolved gate
actual: responsibility matrix created and reviewed against current Kernel/Agent model clusters,
  CORE-001/003, vNext.1, scenario evidence, and existing semantic test seams; no source
  migration or final inheritance decision made.
evidence: docs/design/core-model-responsibility-matrix-v0.1.md
```

## Status log

2026-09-19 · planned · 根据 CORE-003 grill-with-doc 结果进入旧模型责任对齐规格化；未开始源码迁移。
2026-09-19 · planned→verified · 完成第一版责任对齐矩阵；确认 Core 对象集不因现有类结构扩张，字段/使用契约缺口形成局部 gates；未修改产品源码。
2026-09-19 · verified · 结构调整：CORE-005（World）和 CORE-006（Effect）标记为本 Change 的步骤子规格；CORE-004 保留统一规则、术语、Owner/Authority 和最终关闭条件。
2026-09-19 · verified · 双向语义验证记录于 evidence/2026-09-19-core-bidirectional-validation.md；UI/Slice 语义通过，非 UI 资源版本与 Attempt 丰富字段确认是下层 projection gates，未扩张 Core 对象或开始迁移。
