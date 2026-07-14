## Context

D-E4 定义了 7 个 ExpectedBehavior 验证维度，其中 5 个已实现（completion、pageCoverage、elementCoverage、collisionProof、dfsProperties），`operation_rules` 和 `trace_integrity` 两个维度一直是 TODO。相关数据结构已就绪：`SpanType` enum（11 值，5 个引擎 emit）、`TraceRecord` 已有 `PageFrom`/`PageTo`/`PageTransitionType` 默认 null 字段（未填充）、`ActionRecord.Parameters["element_id"]` 模式在 `VerifyElementCoverage` 和 `VerifyDfsProperties` 中已验证可用。

**决策**: Path A — 直接在 ExpectedBehavior.Verify 里读现有数据（`TraversalResult.ActionHistory` + `TraversalResult.Trace`），不建新 `IExecutionPlanDigest` 服务。如果以后需要跨 run 分析 / CI artifact 上传 / 趋势对比，再把 static 方法抽成接口（纯机械重构）。

## Goals / Non-Goals

**Goals:**
- 实现 `operation_rules` 维度中本期可验证的 2 条规则：`depth_first_order`（DFS 栈规程检查）、`no_duplicate_actions`（连续重复检测）
- 实现 `trace_integrity` 维度中本期可验证的 2 条规则：`span_types_present`（5/11 SpanType 可用）、`page_transitions_recorded`（修 1 行引擎代码后可用）
- 保持与现有 5 维度一致的模式（sealed record class + JSON 可选字段 + 默认值全关）
- 向后兼容：现有 JSON 文件不加新 key 也正常工作和反序列化
- `depth_first_order` 与 `dfs_properties:back_after_forward` 正交互补（后者检查"两者是否存在"，本规则检查栈操作序列无 underflow + 确有一致回退）

**Non-Goals:**
- `restore_operations_count` — 引擎无 toggle 后恢复操作逻辑（defer Phase 3）
- `skip_dangerous_buttons` — 引擎无危险按钮检测（defer Phase 3）
- 新增 `IExecutionPlanDigest` 服务（YAGNI，需要时再抽）
- 改现有 5 个维度的行为

## Decisions

### 1. operation_rules 维度：2 规则

**OperationRulesExpectation**:
```csharp
public sealed record class OperationRulesExpectation(
    bool DepthFirstOrder = false,       // 栈规程检查，默认跳过
    int NoDuplicateActionsMax = 0       // 连续重复限制，0=跳过
);
```

| 规则 | 逻辑 | 数据源 |
|------|------|--------|
| `depth_first_order` | 遍历 ActionHistory，tap（非 back 元素）= push(+1)、back = pop(-1)；深度在任何时刻 < 0 → FAIL；从未 back → FAIL；无 forward → FAIL | `result.ActionHistory` |
| `no_duplicate_actions` | ActionHistory 中同一 element_id 最大连续重复次数 ≤ NoDuplicateActionsMax | `result.ActionHistory` |

**与 `dfs_properties:back_after_forward` 的关系**: `back_after_forward` 只检查 forward 和 back "是否都存在"（`forwardActions.Count > 0 && backActions.Count > 0`）。`depth_first_order` 进一步检查栈操作序列的正确性 —— 深度永不负数（back before forward → underflow）+ 至少一次回退（单分支直达底部 → never back）。两者正交互补，分别属于不同验证维度。

### 2. trace_integrity 维度：2 规则

**TraceIntegrityExpectation**:
```csharp
public sealed record class TraceIntegrityExpectation(
    ImmutableArray<SpanType> RequiredSpanTypes = default,  // 空=跳过
    int MinPageTransitions = 0                             // 0=跳过
);
```

| 规则 | 逻辑 | 数据源 |
|------|------|--------|
| `span_types_present` | 为 `RequiredSpanTypes` 中每个 SpanType 产出一条 RuleResult；该 SpanType 在任意 TraceRecord 中存在 → PASS | `result.Trace[].SpanTypes` |
| `page_transitions_recorded` | Trace 中 `PageTransitionType != null` 的记录数 ≥ MinPageTransitions → PASS | `result.Trace[].PageTransitionType` |

### 3. 引擎埋点：TraceRecord 填 PageFrom/PageTo/PageTransitionType

`TraceRecord.PageFrom`/`PageTo`/`PageTransitionType` 字段已存在（默认 null），当前 TraceRecord 创建处未填充。在 `TraversalEngine.RunAsync()` 中：

```csharp
// for 循环前
string? _lastPageId = null;

// TraceRecord 创建处，SpanTypes 行之后
PageFrom: _lastPageId,
PageTo: _lastPageId != GetCurrentPageId() ? GetCurrentPageId() : null,
PageTransitionType: _lastPageId != null && _lastPageId != GetCurrentPageId() ? "navigation" : null,

// RecordPageVisit 前更新
_lastPageId = GetCurrentPageId();
```

3 行代码，热路径 1 次 string 比较，零性能影响。

### 4. JSON Schema 向后兼容

两个新 key（`operationRules`、`traceIntegrity`）都是可选的。DTO 反序列化时 key 不存在 → DTO 属性 null → `FromJson` 使用 default 值（`OperationRulesExpectation()` / `TraceIntegrityExpectation()`）→ 全 false/0/empty → `VerifyOperationRules`/`VerifyTraceIntegrity` 不产出 RuleResult。现有基线 JSON 无需修改即可正常工作。

## Risks / Trade-offs

- **[`settings-target-search` page_transitions 可能不够 10]** → target-search 深度优先到目标即停，页面跳转少。JSON 设较低值（如 5）或不设 `minPageTransitions`
- **[`SpanType` 枚举反序列化失败]** → `Enum.Parse<SpanType>` 可能抛异常。FromJson 中 try-catch 为 safe default
- **[TraversalEngine PageFrom/To 改动影响性能]** → 1 次 string 赋值 + 1 次比较，热路径无影响
- **[`depth_first_order` 首次实现可能过于严格/宽松]** → 规则默认 `false`（opt-in），不对现有基线产生 blocking 影响

## Open Questions
_(无 — 设计已充分评审，修正了与 `dfs_properties:back_after_forward` 的冗余问题)_
