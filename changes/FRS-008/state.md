# FRS-008 — 产品 freshness evaluator 最小实现

lifecycle_state: resolving · disposition: none · depth: standard · base: 51dbb6c4

## Intent

给 `IFreshnessEvaluator`（FRS-007 缝，目前零产品实现）落第一个产品实现。
HOST-001 D7 前置：Product Host 组合需要真插头，不用恒 Sufficient 替身。

## 已核实事实

- 缝：`Evaluate(FreshnessEvaluationInput)`；输入 = `FreshnessBasis(AsOf)`
  × `RevisionId` × `ConsumptionRequirement(TargetSubject, EffectClass)`；
  三态 Sufficient/Insufficient/Unknown（fail-closed，不得折叠）；
- 缝文档约束：**接口不携带时间权威**（Deferred ⑪ canonical clock 未决）；
  同输入必须同结果；未配置 = composition error 而非 runtime Unknown；
- `FreshnessBasis` 只有 `AsOf`（无 revision id）；
- 仓库时钟权威先例：`Func<DateTimeOffset>? clock = null` 构造注入
  （AdbEffectDriver / AdbLiveEffectDriver / EgoBrowserEffectDriver /
  AdbScreenshotAcquisition）。

## Open Questions（唯一 frontier）

- freshness 规则语义：锚点存在性 / 注入时钟+窗口 / 扩 Basis 加 revision id

## Out of Scope

- ConsumptionRequirement 或 FreshnessBasis 字段扩展；canonical clock 裁决
  （Deferred ⑪ 不动）；obligation/outcome-level freshness（FRS-007 D10
  维持后续裁决）；Kernel 其他改动。

## Status log

- 2026-09-20 · created · HOST-001 D7 前置开立；grill frontier 仅一问
  （台账事件 #7），余皆可推导。
- 2026-09-20 · implemented·verified · 规则 b 落地（注入时钟 + 窗口；
  人未否决，veto 窗口随批量 closure 触点关闭）：`ProductFreshnessEvaluator
  (Func<DateTimeOffset> clock, TimeSpan window)`——AsOf 缺失→Unknown、
  age≤window（含等号）→Sufficient、超窗→Insufficient、负龄归 Sufficient
  （宽松方向存证）；clock null / window 负 = ctor fail-fast
  （composition error）。Requirement 内容不参与判定（requirement-scoped
  = 未来 buyer）。测试 7/7（三态、边界等号、确定性、ctor fail-fast、
  负龄）；全量见 closure 提交。
