# 16 — CompletionPolicy 实现：RunAsync 循环检查设计

> 类型: refactor design (HOW)
> 依赖: simulation-baseline.md (WHAT — 场景定义 + 基线数值)
> 配套文档: → `docs/refactor/17-simulation-baseline-tests.md` (Phase B: 基线测试)

---

## 0. 问题陈述

TraversalEngine.RunAsync 当前有 5 条硬终止路径：

| 路径 | 来源 | 维度 |
|------|------|------|
| AllVisited | NodeStack.Depth≤1 + FrameCompleted | 引擎级（自然完成） |
| AntiLoop | StepOrchestrator 检测 | 引擎级（安全机制） |
| MaxSteps | for 循环耗尽 _config.MaxSteps | 引擎级（硬上限） |
| Cancelled | OperationCanceledException | 引擎级（用户取消） |
| Error | 异常捕获 | 引擎级（容错） |

**缺失维度**：用户意图层面的终止条件（CompletionPolicy）。`CompletionPolicy` 类型已完整定义（TargetFound/Timeout/MaxSteps + MatchMode + TargetFoundAction），`TraversalRuntimeContext._completionPolicy` 能存储它，但 **RunAsync 循环从不读取和检查它**。

Python 对应逻辑：`_check_completion_policy()` 在每步后检查 visited_nodes 中节点名是否匹配 target_name、elapsed 是否超过 timeout_seconds、步数是否达到用户指定上限。

---

## 1. CompletionPolicy 检查逻辑

### 1.1 检查位置

在 RunAsync 循环体中，**AntiLoop 检查之后、MaxSteps 检查之前**插入 CompletionPolicy 检查块：

```
AllVisited → AntiLoop → CompletionPolicy → MaxSteps(引擎硬上限) → Cancelled → Error
```

CompletionPolicy 是"用户意图层面的终止条件"，与 FSM 步进逻辑不同维度，放在 RunAsync 循环层面（与 AllVisited/AntiLoop 同层），不下沉到 StepOrchestrator。

### 1.2 检查伪代码

```csharp
// 位置: TraversalEngine.RunAsync, AntiLoop 检查之后 (约 line 225 之后)

// ── CompletionPolicy checks (user intent termination) ──
var policy = _ctx.CompletionPolicy;
if (policy != null && policy.Type != CompletionPolicyType.None)
{
    // TARGET_FOUND: 当前节点操作目标匹配目标名称
    if (policy.Type == CompletionPolicyType.TargetFound)
    {
        var currentNode = _ctx.CurrentFrame;
        if (currentNode != null)
        {
            // 匹配字段: Operation.Target.Value (元素文本, 如 "Dark mode")
            // 不用 Name (Name = template ID, 如 "switch_leaf", 永远不等于元素文本)
            // 静态/root 节点可能无 Operation.Target → fallback 到 Name
            var targetValue = currentNode.Operation?.Target?.Value;
            var matchValue = !string.IsNullOrEmpty(targetValue)
                ? targetValue
                : currentNode.Name;

            bool matched = policy.MatchMode == MatchMode.Exact
                ? string.Equals(matchValue, policy.TargetName, StringComparison.OrdinalIgnoreCase)
                : matchValue.Contains(policy.TargetName!, StringComparison.OrdinalIgnoreCase);

            if (matched)
            {
                // Phase A: MarkAndStop 等价处理
                // Phase 3: ExecuteThenStop 需先执行操作再终止
                return Done(TraversalResult.Reasons.TargetFound, i + 1,
                    stopwatch, traceRecords, visitedPages);
            }
        }
    }

    // TIMEOUT: elapsed >= timeout_seconds
    if (policy.Type == CompletionPolicyType.Timeout
        && stopwatch.Elapsed.TotalSeconds >= policy.TimeoutSeconds!)
    {
        return Done(TraversalResult.Reasons.Timeout, i + 1,
            stopwatch, traceRecords, visitedPages);
    }

    // MAX_STEPS (policy 软上限): 步数达到用户指定上限
    if (policy.Type == CompletionPolicyType.MaxSteps
        && i + 1 >= policy.MaxSteps!)
    {
        return Done(TraversalResult.Reasons.MaxSteps, i + 1,
            stopwatch, traceRecords, visitedPages);
    }
}
```

### 1.3 TargetFound 匹配对象

Python 检查 `visited_nodes` 中的节点 `name`。C# 对应：**`_ctx.CurrentFrame` 当前栈顶节点**。

**⚠️ 关键发现：TraversalNode.Name ≠ 元素文本**

动态节点的字段映射：

| 字段 | 动态节点值 | 说明 |
|------|-----------|------|
| `Name` | `"switch_leaf"` | **template ID** — TemplateInstantiator 用 `template.TemplateId` 赋值 |
| `NodeId` | `"dyn_switch_leaf_Dark mode"` | 复合 ID — 包含元素文本，但格式不稳定，不适合精确匹配 |
| `Operation.Target.Value` | `"Dark mode"` | **元素文本** — PlaceholderResolver 解析 `{{item_text}}` 后赋值 |

因此 TargetFound 匹配**必须用 `Operation.Target.Value`**，不能用 `Name`：
- Exact match: `Name == "Dark mode"` → ❌ 永远不命中 (`Name` = `"switch_leaf"`)
- Exact match: `Operation.Target.Value == "Dark mode"` → ✅ 命中

静态/root 节点可能无 `Operation.Target.Value`（如 root node Operation = NoAction），此时 fallback 到 `Name`。

匹配时机：每步完成后检查，而非遍历 visited 历史集合。因为当前步刚访问的节点才是最新匹配机会。

MatchMode 两种模式：
- `Exact`: `string.Equals(matchValue, policy.TargetName, StringComparison.OrdinalIgnoreCase)`（忽略大小写精确匹配）
- `Contains`: `matchValue.Contains(policy.TargetName, StringComparison.OrdinalIgnoreCase)`（忽略大小写子串匹配）

### 1.4 优先级规则

| 检查 | 优先级 | 说明 |
|------|--------|------|
| AllVisited | 1 | 自然完成，最高优先 |
| AntiLoop | 2 | 安全机制 |
| CompletionPolicy | 3 | 用户意图 |
| MaxSteps (引擎硬上限) | 4 | 兜底保护 |

**CompletionPolicy.MaxSteps vs 引擎硬上限 MaxSteps**：
- CompletionPolicy.MaxSteps 是用户指定的软上限（如 50 步）
- 引擎 _config.MaxSteps 是硬上限（如 1000 步）
- CompletionPolicy **优先于**引擎硬上限：如果用户指定 50 步上限，即使引擎硬上限是 1000，也应于 50 步时终止
- 但 AllVisited/AntiLoop 仍然优先于 CompletionPolicy（自然完成和安全机制高于用户意图）

### 1.5 ExecuteThenStop 处理

Phase A 对 `TargetFoundAction.ExecuteThenStop` **等价 MarkAndStop** 处理：
- 检测到目标后立即终止，不先执行操作
- Phase 3 完整实现时需：先完成当前操作 → 再终止 → 返回 TraversalResult 标记 ExecuteThenStop 完成状态

---

## 2. 代码改动清单

| # | 文件 | 改动类型 | 具体改动 |
|---|------|---------|---------|
| A1 | `Traversal/TraversalResult.cs` | 新增 | Reasons 类加 `TargetFound = "target_found"` 和 `Timeout = "timeout"` 两个 const string |
| A2 | `Traversal/TraversalEngine.cs` RunAsync (line ~225) | 新增 | AntiLoop 检查后插入 CompletionPolicy 检查块（3 个 if 分支） |
| A3 | `Traversal/TraversalEngine.cs` Done() (line ~310-316) | 修改 | GlobalState 映射新增：TargetFound → Completed, Timeout → Terminated |
| A4 | tests: TraversalEngine 单元测试 | 新增 | `TargetFound_StopsAtTargetNode` — 目标节点名精确匹配后终止 |
| A5 | tests: TraversalEngine 单元测试 | 新增 | `TargetFound_ContainsMatch` — 子串匹配后终止 |
| A6 | tests: TraversalEngine 单元测试 | 新增 | `Timeout_ExceedsPolicySeconds` — elapsed 超过 policy.TimeoutSeconds 后终止 |
| A7 | tests: TraversalEngine 单元测试 | 新增 | `MaxStepsPolicy_ReachesUserLimit` — CompletionPolicy.MaxSteps=50 优于引擎硬上限 |
| A8 | tests: TraversalEngine 单元测试 | 新增 | `CompletionPolicy_None_NoEffect` — CompletionPolicyType.None 不触发额外终止 |

改动量：8 处，约 30 行生产代码 + 5 个新增测试方法。

### A3: Done() GlobalState 映射变更

现有映射：
```csharp
_ctx.GlobalState = reason is Reasons.AllVisited or Reasons.AntiLoop
    ? GlobalState.Completed
    : reason is Reasons.Cancelled
        ? GlobalState.Terminated
        : GlobalState.Error;
```

变更后：
```csharp
_ctx.GlobalState = reason is Reasons.AllVisited
                       or Reasons.AntiLoop
                       or Reasons.TargetFound  // 新增: 目标搜索成功 = Completed
    ? GlobalState.Completed
    : reason is Reasons.Cancelled
        or Reasons.Timeout          // 新增: 超时 = Terminated (非错误终止)
        ? GlobalState.Terminated
        : GlobalState.Error;
```

Success 映射同步变更：
```csharp
Success: reason is Reasons.AllVisited
             or Reasons.AntiLoop
             or Reasons.TargetFound  // 新增
```

---

## 3. 测试设计

### A4: TargetFound_StopsAtTargetNode

```csharp
[Fact]
public void TargetFound_StopsAtTargetNode()
{
    // 构建: 3 页 fixture (home → wifi → bluetooth)
    // Root node: DynamicMatch with menu_rule + switch_rule
    // CompletionPolicy: TargetFound, TargetName="Wi-Fi", Exact, MarkAndStop
    // 匹配逻辑: Operation.Target.Value="Wi-Fi" (menu_item 生成的动态节点)
    
    var result = engine.Run();
    
    Assert.True(result.Success);
    Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
    Assert.Contains("wifi", result.VisitedPages);  // 目标页被访问
}
```

### A5: TargetFound_ContainsMatch

```csharp
[Fact]
public void TargetFound_ContainsMatch()
{
    // CompletionPolicy: TargetFound, TargetName="Blue", Contains, MarkAndStop
    // 匹配逻辑: Operation.Target.Value="Bluetooth" 包含 "Blue"
    
    Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
}
```

### A6: Timeout_ExceedsPolicySeconds

```csharp
[Fact]
public void Timeout_ExceedsPolicySeconds()
{
    // CompletionPolicy: Timeout, TimeoutSeconds=0.001 (极短超时)
    // + DelayPerStepMs=50 (每步延迟确保 elapsed 超过 timeout)
    
    Assert.Equal(TraversalResult.Reasons.Timeout, result.CompletionReason);
    Assert.Equal(GlobalState.Terminated, ctx.GlobalState);
}
```

### A7: MaxStepsPolicy_ReachesUserLimit

```csharp
[Fact]
public void MaxStepsPolicy_ReachesUserLimit()
{
    // TraversalEngineConfig.MaxSteps = 1000 (引擎硬上限)
    // CompletionPolicy: MaxSteps, MaxSteps=5 (用户软上限)
    
    Assert.Equal(TraversalResult.Reasons.MaxSteps, result.CompletionReason);
    Assert.True(result.TotalSteps <= 5);  // 用户上限优先
}
```

### A8: CompletionPolicy_None_NoEffect

```csharp
[Fact]
public void CompletionPolicy_None_NoEffect()
{
    // CompletionPolicy: Type=None (不触发额外终止)
    // 验证: 正常走 AllVisited 路径
    
    Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
}
```

---

## 4. 不改的文件

| 文件 | 原因 |
|------|------|
| ArchitectureGuardTests.cs | CompletionPolicyType/MatchMode/TargetFoundAction 已在 Phase2 enum guard 中锁定，无需新增 |
| StepOrchestrator.cs | CompletionPolicy 检查不进入 14 步拦截层（维度不同） |
| TraversalRuntimeContext.cs | `_completionPolicy` 字段已存在，`SetCompletionPolicy()` 方法已存在，只需在 RunAsync 中读取它 |
| simulation-baseline.md | Tier 3 文档不改（WHAT 不变，只是 HOW 补实现） |

---

## 5. 依赖与前置

| 依赖 | 状态 |
|------|------|
| TraversalEngine.RunAsync 已有 AllVisited/AntiLoop/MaxSteps/Cancelled/Error 终止 | ✅ 已实现 |
| CompletionPolicy 类型定义 (TargetFound/Timeout/MaxSteps + MatchMode + TargetFoundAction) | ✅ 已定义 |
| TraversalRuntimeContext._completionPolicy + SetCompletionPolicy() | ✅ 已实现 |
| TraversalPlan.CompletionPolicy 字段 | ✅ 已定义 |
| StateFixtureBuilder 支持 button/switch/BackButton/Readonly | ✅ 已实现 |
| 7 页 Settings App fixture Builder 代码 | ✅ 在 simulation-baseline.md §1.0 中已有完整代码 |

无阻塞依赖。Phase A 可立即实施。

---

## 6. 验证方案

1. 新增 5 个单元测试全绿
2. 原有 516 测试不受影响（CompletionPolicy=null 时检查块被跳过）
3. `dotnet test` 总测试数 = 516 + 5 = 521+

Phase B 基线测试依赖 Phase A 的 CompletionPolicy 实现（场景 2 需要 TargetFound 终止），但 Phase A 可独立实施和验证。
