# 18 — ExpectedBehavior 预期结果定义系统设计

> 状态: Draft (2026-07-09)
> 上下文: → `docs/system/layers/simulation-baseline.md` §2 七类规则
> 上下文: → `docs/system/constitution/constraints.md` C-11 基线 E2E 回归门槛
> 上下文: → Python `src/simulation/expected_behavior.py` + `tests/v6/settings/expected_behavior.yaml`

---

## 0. 问题背景

基线测试验收标准不明确，导致 NodeId 碰撞 bug（Bluetooth 开关被跳过，18 visited vs 应有 19）未被 Phase B 范围断言发现。当前基线验证依赖硬编码数值和隐式文档，缺乏结构化的预期结果定义——给定 fixture + TraversalPlan，遍历应该产生什么结果。

Python 有三层验证体系：
- 数据层: trace 日志
- 预期层: `expected_behavior.yaml`（7 类规则 + 数值）
- 比较层: `validate_expected_behavior_rules()` 函数

C# 缺中间层——预期结果定义。本设计补齐此层。

---

## 1. 设计决策

### D-E1: 载体形态 = record + JSON

**选择**: C# sealed record class 定义 schema + JSON 文件存放具体场景数值实例

**理由**:
- record 编译期保障字段名/类型/必填（和 StateFixture 设计模式一致）
- JSON 提供可读性和可修改性（改数值不改代码）
- 两者结合：record 是 schema 契约，JSON 是数据实例

**替代**:
- 纯 JSON: 需要 runtime schema 校验，和项目 sealed-record + fail-fast 风格不匹配
- 纯 C# 代码: 改数值需改代码，不灵活

### D-E2: 验证行为 = 返回 VerificationReport

**选择**: `ExpectedBehavior.Verify(TraversalResult)` 返回 `VerificationReport`，测试代码 `Assert.True(report.AllPassed, report.Summary)`

**理由**:
- 失败时能看到具体哪条规则 fail + 实际值（不是只看到异常消息）
- 验证逻辑和测试框架解耦（report 是纯数据，不依赖 Xunit）
- 和 Python `validate_expected_behavior_rules()` 返回 `{total, passed, failed, warnings}` 一致

**替代**:
- 内部直接 Assert: 和测试框架耦合，调试困难
- 返回 bool: 信息量不足

### D-E3: 预期值来源 = 结构性推导 + 数值锚定

**选择**: 结构性预期从 fixture 推导（required_pages, required_elements, collision_proof），数值性预期由运行时锚定（JSON 手写）

**理由**:
- fixture 变了，结构性预期自动跟着变（不用手动同步）
- 步数/节点数无法从 fixture 推导，必须运行一次后锚定

**JSON 中的 `auto_derive` sentinel**: 字段值 `"auto_derive"` 表示此字段从 fixture 推导填充，不手写。Verify 前调用 `expected.WithFixtureDerivation(fixture)` 补充。

### D-E4: 规则映射 = 当前可验证子集先行 (5类)

**选择**: 只定义当前可验证的 5 类规则，2 类标记 TODO

| 当前可验证 | 对照数据源 | TODO (待 Trace 补齐) |
|-----------|-----------|---------------------|
| completion | TraversalResult | operation_rules (restore_ops, skip_dangerous) |
| page_coverage | VisitedPages | trace_integrity (span_types, page_transitions) |
| element_coverage | ActionHistory | |
| collision_proof | VisitedPages | |
| dfs_properties | VisitedPages + Trace | |
| numeric_anchor | TraversalResult | |

### D-E5: 标识体系 = 语义标识，不用 NodeId

**选择**: 预期定义用 fixture 页面名/元素名（如 "Wi-Fi", "bluetooth_switch"），Verify 内部做语义→NodeId 映射

**理由**: NodeId 是实现细节（`dyn_menu_container_Wi-Fi_root`），预期定义应面向人能读懂的语义。改 NodeId 公式不影响预期文件。

---

## 2. 数据模型

### ExpectedBehavior record 体系

```
ExpectedBehavior (顶层 record)
│
├── CompletionExpectation
│     Success: bool
│     Reason: string                    ← "all_visited" / "target_found"
│     FinalState: string?               ← FSM 终态名 (可选)
│
├── PageCoverageExpectation
│     Required: ImmutableArray<string>  ← fixture 页面名或 "auto_derive"
│     Forbidden: ImmutableArray<string> ← 预期不访问的页面名 (目标搜索)
│
├── ElementCoverageExpectation
│     Required: ImmutableArray<string>  ← fixture 元素 ID 或 "auto_derive"
│     RequiredRatio: double             ← 覆盖率阈值 (default 0.95)
│
├── ImmutableArray<CollisionProof>
│     Text: string                      ← 元素显示文本 ("ON")
│     ExpectedDistinct: int             ← 同文本应有几个不同节点
│     ParentPages: ImmutableArray<string>? ← 限定在哪些页面下 (可选)
│
├── DfsPropertiesExpectation
│     RootFirst: bool                   ← VisitedPages[0] == "root"
│     ParentBeforeChild: bool           ← 父节点先于子节点被访问
│     BackAfterForward: bool            ← 每个 forward 后都有 back
│
├── NumericAnchor                        ← 参考锚点，非硬断言
│     TotalSteps: int
│     VisitedPagesCount: int
│     ActionHistoryCount: int
│     ElapsedSecondsMax: double
│
└── VerificationReport                  ← Verify() 返回值
      AllPassed: bool
      Summary: string                   ← 人类可读汇总
      Details: ImmutableArray<RuleResult>
        RuleId: string                  ← "completion" / "collision_proof:ON"
        Passed: bool
        Message: string                 ← "Wi-Fi: PASS / Bluetooth: PASS"
        Actual: string?                 ← 实际值摘要
```

### 与 TraversalResult/Trace 对照表

| 子 record | 对照 TraversalResult | 对照 Trace | 验证方法 |
|-----------|---------------------|-----------|---------|
| CompletionExpectation | `Success`, `CompletionReason`, `FinalState` | Trace last `ToState` | 直接字段比较 |
| PageCoverageExpectation | `VisitedPages` Contains 检查 | Trace `CurrentPageId` | 语义名→NodeId 映射后 Contains |
| ElementCoverageExpectation | `ActionHistory` element_id 检查 | Trace `ActionExecuted` | element_id 直接匹配 |
| CollisionProof | `VisitedPages` 同文本 distinct count | Trace `CurrentNodeId` | 按语义名分组统计 |
| DfsPropertiesExpectation | `VisitedPages` 顺序检查 | Trace FSM 顺序 | 按语义名验证位置关系 |
| NumericAnchor | `TotalSteps`, `VisitedPages.Length` 等 | Trace.Length | 数值比较（参考级，允许 ±5%） |

---

## 3. 存放与组织

### 文件位置

```
tests/UniClaw.Core.Tests/
  Baseline/
    SimulationBaselineTests.cs               ← 测试入口
    Fixtures/
      expected/
        settings-full-traversal.json         ← 场景1 预期定义
        settings-target-search.json          ← 场景2 预期定义
```

`Fixtures/expected/` 子目录存放预期输出，和 `Fixtures/` 的输入数据（fixture 定义）分开。

### JSON 文件实例 — 场景1 全量遍历

```json
{
  "scenario": "settings-full-traversal",
  "description": "7页 Settings App 全量遍历 — CompletionPolicy=null",
  "completion": {
    "success": true,
    "reason": "all_visited"
  },
  "page_coverage": {
    "required": "auto_derive",
    "forbidden": []
  },
  "element_coverage": {
    "required": "auto_derive",
    "required_ratio": 0.95
  },
  "collision_proof": "auto_derive",
  "dfs_properties": {
    "root_first": true,
    "parent_before_child": true,
    "back_after_forward": true
  },
  "numeric_anchor": {
    "total_steps": 145,
    "visited_pages_count": 19,
    "action_history_count": 38,
    "elapsed_seconds_max": 1.0
  }
}
```

### JSON 文件实例 — 场景2 目标搜索

```json
{
  "scenario": "settings-target-search",
  "description": "目标搜索 Dark mode Exact MarkAndStop",
  "completion": {
    "success": true,
    "reason": "target_found"
  },
  "page_coverage": {
    "required": ["Wi-Fi", "Bluetooth", "Display"],
    "forbidden": ["Storage", "Internal Storage", "SD Card"]
  },
  "element_coverage": {
    "required": "auto_derive",
    "required_ratio": 0.95
  },
  "collision_proof": "auto_derive",
  "dfs_properties": {
    "root_first": true,
    "parent_before_child": true,
    "back_after_forward": true
  },
  "numeric_anchor": {
    "total_steps": 92,
    "visited_pages_count": 14,
    "action_history_count": 26,
    "elapsed_seconds_max": 1.0
  }
}
```

### `auto_derive` sentinel 规范

| 字段 | `"auto_derive"` 时推导逻辑 |
|------|--------------------------|
| `page_coverage.required` | `fixture.Pages.Keys`（排除 initialPage） |
| `element_coverage.required` | `fixture.Pages.Values` 所有非-readonly 元素的 Id |
| `collision_proof` | 找 fixture 中**同 Text 不同 PageId** 的元素组合，每组生成一条 CollisionProof(Text=该text, ExpectedDistinct=涉及页面数) |

目标搜索场景的 `page_coverage.required` 手写（只含已访问页面），不用 auto_derive，因为只有部分页面被访问。

---

## 4. 契约体系

### 三层纵切对接

```
┌─────────────────────────────────────────────────────┐
│ Tier 1: Constitution (不变约束)                       │
│   C-11: 基线 E2E 回归门槛                              │
│   C-11 补充: ExpectedBehavior schema 锁定               │
│     - record 字段增删走 C-11 变更流程                   │
│     - 5 类验证维度定义不可随意移除                        │
├─────────────────────────────────────────────────────┤
│ Tier 3: Layers (改代码才改文档)                        │
│   simulation-baseline.md:                            │
│     §0 引用 Fixtures/expected/*.json 清单              │
│     §2 七类规则 → 映射到 ExpectedBehavior 子 record     │
│     §4 基线数值更新 → 三路同步规则                       │
├─────────────────────────────────────────────────────┤
│ 测试资产层 (改预期才改文件)                             │
│   Fixtures/expected/*.json: 预期结果定义实例            │
│   SimulationBaselineTests.cs: 消费 ExpectedBehavior    │
├─────────────────────────────────────────────────────┤
│ 代码层 (实现 record + FromJson + Verify)               │
│   ExpectedBehavior.cs + 子 records                    │
│   VerificationReport.cs                               │
│   ExpectedBehavior.FromJson() 反序列化                 │
│   ExpectedBehavior.WithFixtureDerivation() 推导填充     │
│   ExpectedBehavior.Verify(TraversalResult) 对照逻辑     │
└─────────────────────────────────────────────────────┘
```

### 三路同步规则

预期结果定义的**数值权威**是 JSON 文件。三路同步确保一致性：

```
JSON 文件 (数值权威)
  → ExpectedBehavior.FromJson() (代码消费)
  → simulation-baseline.md (文档描述)
  → SimulationBaselineTests.cs (测试入口)
```

| 操作 | 需同步 | 不同步后果 |
|------|--------|----------|
| 改 JSON 预期值 | baseline.md §1 数值 | 文档和实际不一致 |
| 加新验证维度 | baseline.md §2 + record + C-11 | record 不认识新字段 |
| 改 fixture（加页面） | JSON auto_derive 自动 + baseline.md | 预期覆盖不到新页面 |
| 改引擎行为 | JSON numeric_anchor + baseline.md | 数值锚点过期 |

### Schema 锁定 (C-11 补充)

ExpectedBehavior 的 record 结构是**契约 schema**，和 enum 值锁定同一级别：

- 新增验证维度 → 更新 record + baseline.md §2 + constitution C-11
- 删除验证维度 → 同上，不能静默移除
- 改字段名/类型 → 同上

---

## 5. 测试消费模式

### 当前（硬编码断言）

```csharp
Assert.Equal(19, result.VisitedPages.Length);
Assert.Contains(result.VisitedPages, p => p.Contains("Bluetooth") && p.Contains("ON"));
Assert.DoesNotContain(result.VisitedPages, p => p.Contains("Storage"));
```

### 目标（契约驱动验证）

```csharp
var fixture = SettingsAppFixture7Pages();
var expected = ExpectedBehavior.FromJson("Fixtures/expected/settings-full-traversal.json");
expected = expected.WithFixtureDerivation(fixture);  // auto_derive 字段填充

var engine = CreateEngine(fixture, plan);
var result = engine.Run();

var report = expected.Verify(result);
Assert.True(report.AllPassed, report.Summary);
// report.Details 逐条列出每条规则的 PASS/FAIL + 实际值
```

验证逻辑从测试代码中**提取出来**，集中在 ExpectedBehavior.Verify() 中。测试代码只做一件事：加载预期 → 运行引擎 → 对照 → Assert report。

---

## 6. Python 对照与差异

| 对比项 | Python | C# (本设计) |
|--------|--------|------------|
| 预期定义载体 | YAML + Python dataclass | JSON + C# sealed record |
| 规则维度 | 7 类全量 | 5 类先行 + 2 类 TODO |
| 预期值来源 | YAML 手写全量 | auto_derive 推导 + numeric_anchor 手写 |
| 验证方式 | validate_expected_behavior_rules() 函数返回 dict | Verify() 返回 VerificationReport |
| 标识体系 | NodeId / 路径式 ID | 语义名（fixture 页面名/元素名） |
| 契约约束 | 无正式锁定 | C-11 schema 锁定 |

---

## 7. TODO: 待 Trace 补齐后实现

| 维度 | 需要的 Trace 补充 | 预期定义示例 |
|------|-----------------|-------------|
| operation_rules | ElementId, ItemType, MatchRuleId 字段 | `restore_ops_count >= 2`, `skip_dangerous_buttons` |
| trace_integrity | SpanType, PageTransition 专用 span | `span_types: ["step_end", "state_transition", ...]`, `page_transitions >= 10` |

待 Trace 补齐后，新增 `OperationRulesExpectation` 和 `TraceIntegrityExpectation` record，走 C-11 变更流程。
