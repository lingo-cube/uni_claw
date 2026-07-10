## Context

基线测试的验收标准不明确，导致 NodeId 碰撞 bug（Bluetooth 开关被跳过，18 visited vs 应有 19）未被 Phase B 范围断言发现。当前基线验证依赖 `SimulationBaselineTests.cs` 中的硬编码数值断言（`VisitedPages == 19`, `Contains("Bluetooth") && Contains("ON")`）和 `simulation-baseline.md` 中的散列数值描述，缺乏结构化的"预期结果定义"层。

Python 有三层验证体系：数据层（trace 日志）→ 预期层（`expected_behavior.yaml` 7 类规则 + 数值）→ 比较层（`validate_expected_behavior_rules()` 函数）。C# 只有数据层（TraversalResult）和隐式比较层（内联 Assert），缺中间层——预期结果定义。本设计补齐此层。

当前 `SimulationBaselineTests.cs` 中 2 个基线场景各有 ~8 条内联断言，验证逻辑散在测试代码中不可复用、不可序列化、不可从 fixture 推导。

## Goals / Non-Goals

**Goals:**
- 建立 ExpectedBehavior record 体系作为结构化预期结果定义的 schema 契约
- 实现 5 类可验证维度（completion, page_coverage, element_coverage, collision_proof, dfs_properties）+ numeric_anchor 参考锚点
- 提供 `Verify(TraversalResult)` 方法返回 VerificationReport，使测试代码从"硬编码断言"升级为"契约驱动验证"
- 支持 `auto_derive` sentinel 从 fixture 自动推导结构性预期，减少手写维护
- 生成 2 个基线场景的 JSON 预期定义实例（settings-full-traversal.json + settings-target-search.json）
- 实现 C-11 schema 锁定：ExpectedBehavior record 结构变更走 C-11 变更流程

**Non-Goals:**
- 不实现 2 类 TODO 维度（operation_rules, trace_integrity）— 待 Trace 补齐后再扩展，走 C-11 变更流程
- 不改变 TraversalEngine 核心行为 — Verify 是纯对照逻辑，不影响引擎运行
- 不替代 TraversalResult — ExpectedBehavior 是预期定义，不是结果容器
- 不引入 YAML — C# 项目统一 JSON 序列化（DomainJsonOptions 约定）
- 不对 numeric_anchor 做硬断言 — numeric_anchor 是参考锚点（±5% tolerance），CI 不 blocking

## Decisions

### D-E1: 载体形态 = sealed record class + JSON 文件

**选择**: C# sealed record class 定义 schema + JSON 文件存放具体场景数值实例

**理由**:
- record 编译期保障字段名/类型/必填（和 StateFixture / TraversalResult 设计模式一致）
- JSON 提供可读性和可修改性（改数值不改代码、不改测试）
- 两者结合：record 是 schema 契约，JSON 是数据实例，和 Domain 层 sealed-record + fail-fast 风格一致

**替代方案**:
- 纯 JSON schema: 需要 runtime schema 校验，和项目 sealed-record + DomainValidationException fail-fast 风格不匹配
- 纯 C# 代码: 改数值需改代码和测试，不灵活，且无法做 fixture 推导

### D-E2: 验证行为 = 返回 VerificationReport

**选择**: `ExpectedBehavior.Verify(TraversalResult)` 返回 `VerificationReport`，测试代码 `Assert.True(report.AllPassed, report.Summary)`

**理由**:
- 失败时能看到具体哪条规则 fail + 实际值（不是只看到异常消息或 bool）
- 验证逻辑和测试框架解耦（report 是纯数据 record，不依赖 Xunit）
- 和 Python `validate_expected_behavior_rules()` 返回 `{total, passed, failed, warnings}` 一致

**替代方案**:
- 内部直接 Assert: 和测试框架耦合，调试困难，无法复用
- 返回 bool: 信息量不足，失败时无法定位哪条规则

### D-E3: 预期值来源 = 结构性推导 + 数值锚定

**选择**: 结构性预期从 fixture 推导（`auto_derive` sentinel），数值性预期由运行时锚定（JSON 手写）

**理由**:
- fixture 变了（加页面/加元素），结构性预期自动跟着变，不用手动同步 JSON
- 步数/节点数无法从 fixture 推导（依赖引擎行为），必须运行一次后锚定

**`auto_derive` sentinel 规范**: JSON 中字段值 `"auto_derive"` 表示此字段从 fixture 推导填充。Verify 前调用 `expected.WithFixtureDerivation(fixture)` 补充。

| 字段 | `"auto_derive"` 推导逻辑 |
|------|--------------------------|
| `page_coverage.required` | `fixture.Pages.Keys`（排除 initialPage） |
| `element_coverage.required` | `fixture.Pages.Values` 所有非-readonly 元素的 Id |
| `collision_proof` | 找 fixture 中同 Text 不同 PageId 的元素组合 |

### D-E4: 规则映射 = 当前可验证子集先行 (5类)

**选择**: 只定义当前可验证的 5 类规则 + numeric_anchor，2 类标记 TODO

| 当前可验证 | 对照数据源 | TODO (待 Trace 补齐) |
|-----------|-----------|---------------------|
| completion | TraversalResult | operation_rules (restore_ops, skip_dangerous) |
| page_coverage | VisitedPages | trace_integrity (span_types, page_transitions) |
| element_coverage | ActionHistory | |
| collision_proof | VisitedPages | |
| dfs_properties | VisitedPages + Trace | |
| numeric_anchor | TraversalResult | |

**理由**: 当前 Trace 不包含 SpanType/PageTransition 专用字段，operation_rules 的 restore_ops/skip_dangerous 无法验证。先行定义 5 类可覆盖 Python 7 类中的核心验证维度。

### D-E5: 标识体系 = 语义标识，不用 NodeId

**选择**: 预期定义用 fixture 页面名/元素名（如 "Wi-Fi", "bluetooth_switch"），Verify 内部做语义→NodeId 映射

**理由**: NodeId 是实现细节（`dyn_menu_container_Wi-Fi_root`），预期定义应面向人能读懂的语义。改 NodeId 公式不影响预期文件。

### D-E6: 存放位置 = tests/Baseline/Fixtures/expected/

**选择**: JSON 预期定义文件放在 `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/`

**理由**: 和 fixture 输入数据（StateFixtureBuilder 内联构建）分开，预期输出有独立目录。baseline 测试入口 `SimulationBaselineTests.cs` 直接消费这些 JSON 文件。

### D-E7: 代码层 = Simulation 命名空间，独立文件

**选择**: ExpectedBehavior.cs + 子 records + VerificationReport.cs 放在 `src/UniClaw.Core/Simulation/`，不放 Traversal 命名空间

**理由**: ExpectedBehavior 是测试基础设施（验证预期 vs 实际），不是引擎核心逻辑。Simulation 命名空间已有 StateFixture/StatefulMockVisionService 等测试构建基础设施，ExpectedBehavior 是同一性质的扩展。Traversal 命名空间保持纯引擎逻辑。

## Risks / Trade-offs

- **[Risk] auto_derive 推导逻辑与 fixture 格式耦合** → Mitigation: 推导逻辑只读 StateFixture 公开属性（Pages, Elements），不依赖 builder 内部结构。fixture 格式变更时推导逻辑跟着更新，但 JSON 中的 auto_derive 字段无需手动修改。

- **[Risk] numeric_anchor 过期 — 引擎行为变化导致步数/节点数漂移** → Mitigation: numeric_anchor 是参考锚点（±5% tolerance），不是 CI-blocking 硬断言。引擎优化/bug 修复后只需更新 JSON 数值，不改代码。三路同步规则确保数值权威一致。

- **[Risk] 语义→NodeId 映射失败 — fixture 元素名不匹配实际 NodeId** → Mitigation: 映射用 Contains 匹配（`VisitedPages.Any(p => p.Contains("Wi-Fi"))`），不是精确等于。这容忍 NodeId 前缀/后缀变化，只要求语义片段存在。

- **[Risk] 2 类 TODO 维度长期未补齐** → Mitigation: D-E4 明确标记 TODO，record 结构预留扩展空间（新增子 record 走 C-11 变更流程）。Trace 补齐后自然触发扩展。

- **[Trade-off] sealed record vs 灵活 schema**: record 编译期保障强但不可动态扩展字段。选择 record 是因为 C-11 schema 锁定要求变更走正式流程，这恰好是 record 的优势（编译期强制）而非劣势。

- **[Trade-off] JSON 外部文件 vs 内联构建**: D-B1 选择了 fixture 内联构建（StateFixtureBuilder），但 ExpectedBehavior 选择 JSON 外部文件。原因不同：fixture 是输入（构建逻辑复杂），ExpectedBehavior 是预期输出（数值简单、需可读可修改）。
