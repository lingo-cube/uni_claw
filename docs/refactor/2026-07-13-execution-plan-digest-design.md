# Design: ExecutionPlanDigest — operation_rules + trace_integrity 验证维度

> **创建时间**: 2026-07-13
> **状态**: 设计阶段
> **依赖**: Phase22 Trace Minimal (SpanType + PageTransition, 已完成)
> **解锁**: D-E4 的 2 个 TODO 验证维度

## 1. 背景

### 现状

- **ExpectedBehavior 验证维度**: 5 类可验证 + 1 informational（D-E4）
- **TODO 维度**: `operation_rules`（4 条规则）、`trace_integrity`（2 条规则）
- **数据基础设施**: Phase22 已添加 `SpanType` enum（11 值锁定，D-E8）+ `PageTransition` record
- **数据就绪度**: Trace 层完整（ITraceRecorder / ITraceService / ITraceStorage + TraversalResult.Trace）

### 为什么不是 C2 ExecutionPlanDigest

路线图（`docs/refactor/20-b-refactoring-roadmap-design.md`）将 ExecutionPlanDigest 标记为 P3，作为 D-E4 的前置解锁。但实际检查发现，6 条规则中的 4 条可以直接从 `TraversalResult.ActionHistory` 和 `TraversalResult.Trace` 通过简单 LINQ 查询完成，不需要独立的 Digest 服务。

**决策**: Path A — 直接在 ExpectedBehavior.Verify 里读现有数据，不建新服务。如果以后需要跨 run 分析 / CI artifact 上传 / 趋势对比，再把 static 方法抽成 `IExecutionPlanDigest`（纯机械重构）。

### 引擎数据现状

SpanType enum 11 值中，只有 5 个被引擎实际 emit：

| SpanType | 引擎是否 emit | 本期可用 |
|---|---|---|
| StateDecision | ✅ | ✓ |
| PageAnalysis | ✅ | ✓ |
| DfsForward | ✅ | ✓ |
| AICall | ✅ | ✓ |
| ErrorHandling | ✅ | ✓ |
| RestoreOp | ❌ 无恢复操作逻辑 | ✗ defer Phase 3 |
| SkipDangerous | ❌ 无危险按钮检测 | ✗ defer Phase 3 |
| DfsBacktrack | ❌ | ✗ |
| PopupHandling | ❌ | ✗ |
| ContainerHandling | ❌ | ✗ |
| CacheOp | ❌ | ✗ |

`TraceRecord.PageFrom` / `PageTo` / `PageTransitionType` 永远为 null（创建 TraceRecord 时未填充）。

## 2. 设计目标

### Goals

1. 实现 `operation_rules` 维度中**数据就绪**的 2 条规则：`depth_first_order`、`no_duplicate_actions`
2. 实现 `trace_integrity` 维度中**本期可修**的 2 条规则：`span_types_present`（已有 5 个 SpanType）、`page_transitions_recorded`（修 1 行引擎代码后可用）
3. 保持与现有 ExpectedBehavior 5 维度一致的模式（sealed record class + JSON optional）
4. 向后兼容：现有 JSON 文件不加新 key 也能正常工作和反序列化

### Non-Goals

- `restore_operations_count` — 引擎没有 toggle 后恢复操作逻辑（defer Phase 3）
- `skip_dangerous_buttons` — 引擎没有危险按钮检测（defer Phase 3）
- 新增 `IExecutionPlanDigest` 服务（YAGNI，需要时再抽）
- 改现有 5 个维度的行为

## 3. 数据结构

### 3.1 OperationRulesExpectation

```csharp
// 新文件: src/UniClaw.Core/Simulation/ExpectedBehavior/OperationRulesExpectation.cs

public sealed record class OperationRulesExpectation(
    bool DepthFirstOrder = false,       // depth_first_order: skip if false; checks stack discipline (depth≥0 + ≥1 back), NOT redundant with dfs_properties:back_after_forward
    int NoDuplicateActionsMax = 0       // no_duplicate_actions: skip if 0
);
```

**规则详情**:

| 字段 | 逻辑 | 数据源 | 状态 |
|---|---|---|---|
| `DepthFirstOrder` | 遍历 ActionHistory, tap = push(+1) / back = pop(-1), 深度永不负数 + 至少一次回退 → DFS 栈规程正确 | `result.ActionHistory` | ✅ 本期实现 |
| `NoDuplicateActionsMax` | 同 element_id 在 ActionHistory 中连续出现 ≤ N 次 | `result.ActionHistory` | ✅ 本期实现 |

### 3.2 TraceIntegrityExpectation

```csharp
// 新文件: src/UniClaw.Core/Simulation/ExpectedBehavior/TraceIntegrityExpectation.cs

public sealed record class TraceIntegrityExpectation(
    ImmutableArray<SpanType> RequiredSpanTypes = default,  // span_types_present: skip if empty
    int MinPageTransitions = 0                             // page_transitions_recorded: skip if 0
);
```

**规则详情**:

| 字段 | 逻辑 | 数据源 | 状态 |
|---|---|---|---|
| `RequiredSpanTypes` | Trace 中所有 SpanTypes 的并集是否包含指定类型 | `result.Trace[].SpanTypes` | ✅ 本期实现（5/11 可用） |
| `MinPageTransitions` | Trace 中 PageTransitionType != null 的记录数 ≥ N | `result.Trace[].PageTransitionType` | ⚠️ 需修 1 行引擎代码 |

### 3.3 ExpectedBehavior 主 record 扩展

```csharp
// 修改: src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.cs

public sealed partial record class ExpectedBehavior(
    string Scenario,
    string Description,
    CompletionExpectation Completion,
    PageCoverageExpectation PageCoverage,
    ElementCoverageExpectation ElementCoverage,
    ImmutableArray<CollisionProof> CollisionProof,
    DfsPropertiesExpectation DfsProperties,
    NumericAnchor NumericAnchor,
    OperationRulesExpectation OperationRules,      // ← NEW
    TraceIntegrityExpectation TraceIntegrity)       // ← NEW
```

**默认值策略**: 缺失 key → `OperationRulesExpectation()` / `TraceIntegrityExpectation()`（全 `false`/0/empty → 不产出 RuleResult）

## 4. 验证逻辑

### 4.1 主调度变更

```csharp
// 在 ExpectedBehavior.Verify.cs 的 Verify() 方法中，numeric_anchor 之后追加:

// 3.7 operation_rules (blocking)
details.AddRange(VerifyOperationRules(result));

// 3.8 trace_integrity (blocking)
details.AddRange(VerifyTraceIntegrity(result));
```

AllPassed 计算不改动。新维度的 RuleId 不以 `numeric_anchor` 开头 → 自动纳入 blocking。

### 4.2 VerifyOperationRules

```
RuleId 格式: "operation_rules:<rule_name>"

depth_first_order:
  检查 DFS 栈规程 —— 与 dfs_properties:back_after_forward（仅检查 forward/back 是否存在）互补，
  本规则检查栈操作的正确性:
  1. 遍历 ActionHistory, tap（非 back 元素）= push（+1）, back = pop（-1）
  2. 若深度在任何时刻 < 0 → FAIL（stack underflow: back before forward）
  3. 若从未 back（深度从未 -1）→ FAIL（engine never returns from any branch）
  4. 无 forward 操作 → FAIL（no forward movement）
  PASS when: 深度始终 ≥ 0 AND 至少一次 back
  与 dfs_properties:back_after_forward 的关系: 后者只检查 "两者都存在"，
  本规则进一步检查栈操作序列本身无 underflow + 确有一致回退。
  Data: result.ActionHistory → Action + Parameters["element_id"]

no_duplicate_actions:
  PASS when: ActionHistory 中同一 element_id 最大连续重复 ≤ NoDuplicateActionsMax
  FAIL when: 某节点连续重复超过限制
  Data: result.ActionHistory → Parameters["element_id"] 连续相同计数
```

### 4.3 VerifyTraceIntegrity

```
RuleId 格式: "trace_integrity:<rule_name>" 或 "trace_integrity:span_type:<SpanType名>"

span_types_present:
  为 RequiredSpanTypes 中每个类型产出一条 RuleResult
  PASS when: 该 SpanType 在 result.Trace 任意记录中存在
  Data: result.Trace[].SpanTypes 并集

page_transitions:
  PASS when: result.Trace 中 PageTransitionType != null 的记录数 ≥ MinPageTransitions
  Data: result.Trace[].PageTransitionType
```

## 5. 引擎埋点改动

### 5.1 TraceRecord 填 PageFrom/PageTo/PageTransitionType

**文件**: `src/UniClaw.Core/Traversal/TraversalEngine.cs`

**现状** (line 214):
```csharp
traceRecords.Add(new TraceRecord(
    StepNumber: i + 1,
    FromState: fromState,
    ToState: stepResult.NextState,
    CurrentNodeId: _ctx.CurrentFrame?.NodeId,
    CurrentPageId: GetCurrentPageId(),
    ActionExecuted: GetLastAction(),
    ActionSuccess: GetLastActionSuccess(),
    ChildPushed: stepResult.ChildPushed,
    FrameCompleted: stepResult.FrameCompleted,
    SpanTypes: _stepCtx.Trace.GetStepSnapshot()));
```

**改为**: 加 3 行 — 跟踪 `_lastPageId`，在页面变化时记录新旧页面。

```csharp
// 在 Run() 方法开头加:
string? _lastPageId = null;

// 在 TraceRecord 创建处，SpanTypes 行之后加:
PageFrom: _lastPageId,
PageTo: _lastPageId != GetCurrentPageId() ? GetCurrentPageId() : null,
PageTransitionType: _lastPageId != null && _lastPageId != GetCurrentPageId() ? "navigation" : null,

// RecordPageVisit 调用前更新:
_lastPageId = GetCurrentPageId();
```

## 6. JSON Schema 扩展

### 6.1 格式

两个新顶层 key，都是可选：

```json
{
  "scenario": "settings-full-traversal",
  "completion": { ... },
  "pageCoverage": { ... },
  "elementCoverage": { ... },
  "collisionProof": "auto_derive",
  "dfsProperties": { ... },
  "numericAnchor": { ... },

  "operationRules": {
    "depthFirstOrder": true,
    "noDuplicateActionsMax": 3
  },

  "traceIntegrity": {
    "requiredSpanTypes": [
      "StateDecision",
      "PageAnalysis",
      "DfsForward",
      "AICall",
      "ErrorHandling"
    ],
    "minPageTransitions": 10
  }
}
```

### 6.2 向后兼容

- 两个 key 都不存在 → DTO 属性为 null → `FromJson` 使用 default 值
- `requiredSpanTypes` 为空数组 → 不检查 span_types_present
- `minPageTransitions: 0` → 不检查 page_transitions

## 7. DTO 反序列化扩展

### 7.1 新增 DTO 类

```csharp
// 在 ExpectedBehavior.cs 的 DTO 区域:

internal sealed class OperationRulesExpectationDto
{
    public bool DepthFirstOrder { get; set; }
    public int NoDuplicateActionsMax { get; set; }
}

internal sealed class TraceIntegrityExpectationDto
{
    public List<string> RequiredSpanTypes { get; set; } = new();
    public int MinPageTransitions { get; set; }
}
```

### 7.2 FromJson 扩展

```csharp
// 在 FromJson() 中 NumericAnchor 构造之后加:

var opRulesDto = dto.OperationRules;
var operationRules = opRulesDto != null
    ? new OperationRulesExpectation(
        DepthFirstOrder: opRulesDto.DepthFirstOrder,
        NoDuplicateActionsMax: opRulesDto.NoDuplicateActionsMax)
    : new OperationRulesExpectation();

var tiDto = dto.TraceIntegrity;
var traceIntegrity = tiDto != null
    ? new TraceIntegrityExpectation(
        RequiredSpanTypes: tiDto.RequiredSpanTypes
            ?.Select(s => Enum.Parse<SpanType>(s))
            .ToImmutableArray() ?? ImmutableArray<SpanType>.Empty,
        MinPageTransitions: tiDto.MinPageTransitions)
    : new TraceIntegrityExpectation();
```

## 8. 规则覆盖矩阵

| # | 规则 | 数据源 | 本期 | Phase 3 | 备注 |
|---|---|---|---|---|---|
| 1 | depth_first_order | ActionHistory | ✅ | — | 栈规程检查（深度≥0 + 至少一次回退），与 `dfs_properties:back_after_forward`（仅检查两者都存在）正交互补 |
| 2 | restore_operations_count | SpanType.RestoreOp | ❌ 无引擎行为 | 加 toggle 恢复逻辑 + emit RestoreOp | |
| 3 | skip_dangerous_buttons | ActionHistory | ❌ 无引擎行为 | 加危险按钮检测 | |
| 4 | no_duplicate_actions | ActionHistory | ✅ | — | 连续重复检测 |
| 5 | span_types_present | Trace.SpanTypes | ✅（5/11 可用） | 引擎加更多 SpanType emit | |
| 6 | page_transitions_recorded | Trace.PageFrom/To/Type | ✅（修 1 行引擎） | — | |

## 9. 改动清单

| 文件 | 操作 | 说明 |
|---|---|---|
| `OperationRulesExpectation.cs` | **新建** | 2 字段 sealed record |
| `TraceIntegrityExpectation.cs` | **新建** | 2 字段 sealed record |
| `ExpectedBehavior.cs` | 修改 | 主 record 加 2 参数 + 2 DTO + FromJson 扩展 |
| `ExpectedBehavior.Verify.cs` | 修改 | 加 VerifyOperationRules + VerifyTraceIntegrity + 主调度 |
| `TraversalEngine.cs` | 修改 | TraceRecord 创建填 PageFrom/PageTo/PageTransitionType |
| `settings-full-traversal.json` | 修改 | 加 operationRules + traceIntegrity |
| `settings-target-search.json` | 修改 | 加 operationRules + traceIntegrity（page_transitions 可能 < 10，按实际调整） |
| `simulation-baseline.md` | 修改 | §2 TODO 维度表更新状态 |
| `decisions/log.md` | 修改 | 新增 D-N 决策记录 |

## 10. 风险

| 风险 | 缓解 |
|---|---|
| `settings-target-search` 的 page_transitions 可能不够 10 | target-search 测试是深度优先到特定目标后停止，page transition 可能 < 10。JSON 中设较低值（如 5）或不设 |
| `SpanType` 枚举反序列化失败 | `Enum.Parse` 可能抛异常。FromJson 中 try-catch 为 safe default |
| TraversalEngine PageFrom/To 改动影响性能 | 只加 1 次 string 赋值 + 1 次比较，热路径无影响 |
