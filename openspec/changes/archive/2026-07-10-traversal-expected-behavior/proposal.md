## Why

基线测试的验收标准不明确，导致 NodeId 碰撞 bug（Bluetooth 开关被跳过）未被 Phase B 范围断言发现。当前基线验证依赖硬编码数值（`VisitedPages == 19`）和隐式文档（simulation-baseline.md 中的散列数值），缺乏结构化的"预期结果定义"——即给定 fixture + TraversalPlan，遍历应该产生什么结果。Python 有 `expected_behavior.yaml`（7 类规则 + 数值）和 `expected_behavior.py`（类定义 + 断言方法），C# 只有内联 Assert 和非序列化文档。需要建立三层验证链条的中间层：ExpectedBehavior 定义，使基线验证从"猜数值"变为"对照预期结构"。

## What Changes

- 新增 `ExpectedBehavior` sealed record class — 结构化的预期结果定义，可序列化（JSON）、可对照实际 TraversalResult/Trace 进行验证
- 新增 7 类验证维度映射（completion, page_coverage, element_coverage, collision_proof, dfs_properties, operation_rules, exit_strategy）— 从 Python 7 类规则体系提取，适配 C# 数据模型
- 新增 2 个基线场景的 ExpectedBehavior 实例（Settings 全量遍历 + 目标搜索）— 从当前 C# 运行时基线值和 fixture 定义生成
- 替换 SimulationBaselineTests.cs 中的硬编码断言 — 从 ExpectedBehavior 实例生成验证逻辑
- 更新 simulation-baseline.md §2 — 七类规则从文档描述升级为引用 ExpectedBehavior 结构定义

## Capabilities

### New Capabilities
- `expected-behavior`: 预期结果定义结构 — ExpectedBehavior record 类、7 类验证维度 schema、序列化格式、2 个基线场景实例

### Modified Capabilities
- `traversal-engine`: TraversalResult 验证接口 — 新增 `VerifyAgainst(ExpectedBehavior)` 便利方法（可选，不改变核心行为）
