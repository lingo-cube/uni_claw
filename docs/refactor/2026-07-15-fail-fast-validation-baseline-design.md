# Design: Fail-Fast Validation Baseline (C-1~C-4 + C-8)

> **创建时间**: 2026-07-15
> **状态**: 设计阶段
> **来源**: Python↔C# gap triage 桶 C（当前值得实施）
> **主题**: 让 silent failure 变成 loud failure — 构造期校验 + 补字段 + 接线

## 1. 背景

Python→C# gap triage 将 40 项差距裁剪为 8 项 backlog。其中 C-1~C-4 + C-8 共享同一主题：**当前代码静默接受非法值、丢弃字段、忽略已有策略**，违反项目自身声明的 fail-fast 约定。5 项合在一起形成统一的"fail-fast 基线"变更。

## 2. C-1: Graph 模型构造期校验 (12 项)

### 原则

所有校验使用 `DomainValidationException`（已有），格式 `throw new DomainValidationException("FieldName", illegalValue)`。校验逻辑放在 record 的 init 逻辑中（使用 `init` accessor 或手动构造函数）。

### 清单

| # | Record | 字段 | 校验 | 错误示例 |
|---|--------|------|------|---------|
| 1 | `Precondition` | `TimeoutSeconds` | `> 0 && <= 300` | `new Precondition(TimeoutSeconds: 0)` |
| 2 | `DynamicRule` | `RuleId` | 非 null 非空 | `new DynamicRule(RuleId: "")` |
| 3 | `DynamicRule` | `ChildTemplate` | 非 null 非空 | `new DynamicRule(ChildTemplate: null)` |
| 4 | `ChildrenStrategy` | `MaxChildren` | `>= 0 && <= 10000` | `new ChildrenStrategy(MaxChildren: -1)` |
| 5 | `ErrorPolicy` | `MaxRetries` | `>= 0 && <= 100` | `new ErrorPolicy(MaxRetries: 200)` |
| 6 | `ExitCondition` | `MaxDepth` | 当 `Type == DepthLimited` 时: `MaxDepth > 0 && <= 1000` | `new ExitCondition(Type: DepthLimited, MaxDepth: null)` |
| 7 | `CompletionPolicy` | `TargetName` | 当 `Type == TargetFound` 时: 非 null 非空 | `new CompletionPolicy(Type: TargetFound, TargetName: "")` |
| 8 | `CompletionPolicy` | `TimeoutSeconds` | `> 0 && <= 86400` | `new CompletionPolicy(TimeoutSeconds: 100000)` |
| 9 | `CompletionPolicy` | `MaxSteps` | `>= 1 && <= 1000000` | `new CompletionPolicy(MaxSteps: 0)` |
| 10 | `EntryPolicy` | `TimeoutSeconds` | `> 0 && <= 300` | `new EntryPolicy(TimeoutSeconds: 500)` |
| 11 | `TraversalNode` | `NodeId` | 非 null 非空 | `new TraversalNode(NodeId: "")` |
| 12 | `TraversalNode` | `Name` | 非 null 非空 | `new TraversalNode(Name: null)` |

### 实现方式

C# `sealed record class` 不支持 `__post_init__`（Python 概念）。两种方式：

- **方式 A**: 手动写构造函数替代 primary constructor，在构造函数体内校验
- **方式 B**: 用 init-only properties + `field` keyword (C# 12 preview) 在 setter 中校验

选择**方式 A**：手动写构造函数。`Precondition`、`DynamicRule`、`ChildrenStrategy`、`ErrorPolicy`、`ExitCondition`、`CompletionPolicy`、`EntryPolicy` 各有 2-4 个字段，构造函数改动量小。已有 `EntryConfig` 使用此模式（手动构造函数 + DomainValidationException），保持一致。

### 示例

```csharp
// BEFORE
public sealed record class Precondition(
    string? PageName = null,
    string? Path = null,
    string? UiCondition = null,
    double TimeoutSeconds = 5.0);

// AFTER
public sealed record class Precondition
{
    public string? PageName { get; init; }
    public string? Path { get; init; }
    public string? UiCondition { get; init; }
    public double TimeoutSeconds { get; init; }

    public Precondition(
        string? pageName = null,
        string? path = null,
        string? uiCondition = null,
        double timeoutSeconds = 5.0)
    {
        if (timeoutSeconds <= 0 || timeoutSeconds > 300)
            throw new DomainValidationException("TimeoutSeconds", timeoutSeconds);
        PageName = pageName;
        Path = path;
        UiCondition = uiCondition;
        TimeoutSeconds = timeoutSeconds;
    }
}
```

## 3. C-2: PlaceholderResolver fail-fast + TemplateInstantiator 丢字段

### 3.1 PlaceholderResolver: 未知占位符 → 抛异常

**当前**: `Resolve("click {{unknown}}", context)` → 返回 `"click {{unknown}}"`（静默保留）

**改为**: 调用已有 `HasUnresolvedPlaceholders()` 方法检查。若有未解析占位符 → `throw new DomainValidationException("placeholder", unresolvedList)`。

影响: 仅 `TemplateInstantiator.Instantiate()` 调用 `PlaceholderResolver.Resolve()`。若现有模板没有未知占位符（应如此），零行为变化。

### 3.2 TemplateInstantiator: 补 3 个字段

**Target.Meta**: `CreateOperation()` 传 `meta` 字典到 `Target` 构造函数 (`Target.Meta` 已在 `Target` record 存在，默认 `ImmutableDictionary<string, string>.Empty`)

**Restore Target/Params**: `CreateOperation()` 解析 restore dict 时提取 `target` 和 `params` 键，传入 `RestoreAction(actionType, target, params)`

**UiCondition**: `CreatePrecondition()` 读取 `ui_condition` 键，传入 `Precondition(UiCondition: value)`

## 4. C-3: Per-node error_policy 接线

**当前**: `TraversalNode.ErrorPolicy` 字段存在但 `ErrorHandler` 从未读取。`StrategySelectionContext.MaxRetries` 硬编码默认值 3。

**改为**: `ErrorStrategySelector.SelectStrategy()` 读取 `context.CurrentNode?.ErrorPolicy`:

- 若 `node.ErrorPolicy` 非 null: `MaxRetries = node.ErrorPolicy.MaxRetries`；`OnError` 类型映射到 `StrategyChain` 优先级
- 若 `node.ErrorPolicy` 为 null: 使用当前硬编码默认值（向后兼容）

### 策略映射

| ErrorPolicy.OnError | ErrorStrategy |
|---------------------|---------------|
| `Retry` | 先 Retry，失败后 Backtrack |
| `Skip` | 先 Skip，失败后 Continue |
| `Abort` | 直接 Abort |
| `Fallback` | `FallbackTarget` 驱动 |
| `Backtrack` | 先 Backtrack，失败后 Skip |

不影响 `ErrorClassifier` 和 `RecoveryExecutor`——只改 `ErrorStrategySelector` 的选择逻辑。

## 5. C-4: Plan 根节点校验

**当前**: `BuildRootNode()` 无任何断言。`TraversalEngine` 在 `RootNode` 为 null 时由 `BuildDefaultRoot(entryApp)` 兜底构建默认根（已测试特性 `Constructor_NoRootNode_BuildsDefaultRoot`）。

**改为**: `TraversalPlan` 构造函数在 `RootNode` **非 null** 时校验其类型与操作（null 保留合法，引擎兜底）:

```csharp
// TraversalPlan 构造函数:
if (RootNode is not null)
{
    if (RootNode.NodeType != NodeType.Screen && RootNode.NodeType != NodeType.Container)
        throw new DomainValidationException("RootNode.NodeType", RootNode.NodeType);
    if (RootNode.Operation.Action != OperationType.NoAction)
        throw new DomainValidationException("RootNode.Operation", RootNode.Operation.Action);
}
```

同时 `PlanCompiler.BuildRootNode()` 加注释说明校验已在 `TraversalPlan` 构造函数中完成。

### 实现期裁决 (2026-07-15)

原设计/spec 要求 `RootNode == null` 抛异常。实现期发现与 `TraversalEngine.BuildDefaultRoot` 兜底特性（专测守护）冲突。裁决：**null 保留合法**——引擎 fail-safe 兜底非 silent failure，不在本 change「让 silent failure 变 loud」主题内；仅在显式提供根节点时校验。spec/design 已同步修订（去掉「null 抛异常」场景，改为「畸形根抛异常 + null 合法」）。待归档时记入 decisions/log。

## 6. C-8: P3 五项零碎

| # | 项 | 操作 |
|---|-----|------|
| 1 | `ContentNode.ToMarkdown()` | 新增方法，递归输出 markdown 缩进树 |
| 2 | `Region.Id` 非空校验 | 构造函数加 `DomainValidationException` |
| 3 | `TypeHint` 加 `[JsonPropertyName]` | 8 个 enum 值各加 `[JsonPropertyName("clickable_text")]` 等 |
| 4 | `TypeHint.Values` 类型 | 保持 `IReadOnlyList<TypeHint>`（枚举值），无改动 |
| 5 | `IsCanonical(string)` | 在 `TypeHintExtensions` 新增，区分精确值 vs 别名 |

## 7. 改动清单

| 层 | 文件 | 操作 | 内容 |
|----|------|------|------|
| Graph.Models | `TraversalNode.cs` | 修改 | 7 个 record 改手动构造函数 + 校验 (Precondition/DynamicRule/ChildrenStrategy/ErrorPolicy/ExitCondition) + TraversalNode(NodeId/Name) |
| Graph.Models | `TraversalPlan.cs` | 修改 | CompletionPolicy/EntryPolicy 校验 + RootNode 断言 |
| Graph.Models | `EntryConfig.cs` | 修改 | 加上界校验 |
| Graph.Services | `PlaceholderResolver.cs` | 修改 | Resolve 末尾抛异常若未解析 |
| Graph.Services | `TemplateInstantiator.cs` | 修改 | 补 Target.Meta + Restore Target/Params + UiCondition |
| StateMachine | `ErrorHandler.cs` | 修改 | ErrorStrategySelector 读 node.ErrorPolicy |
| Domain.Content | `TreeAndFingerprint.cs` | 修改 | ContentNode.ToMarkdown() |
| Domain.Vision | `Region.cs` | 修改 | Id 非空校验 |
| Domain.Vision | `TypeHint.cs` | 修改 | 加 [JsonPropertyName] |
| Domain.Vision | `TypeHintExtensions.cs` | 修改 | 新增 IsCanonical(string) |

## 8. 内聚与耦合

**主题统一**: 所有改动都是"阻止错误状态进入系统"——构造期 fail-fast、运行时抛异常替代静默降级、接线已有但被忽略的策略字段。

**耦合**: 各项改动独立，无相互依赖。C-3 是唯一的运行时行为变更（ErrorHandler），其余都是纯校验/补字段。

## 9. 风险

| 项 | 风险 | 缓解 |
|----|------|------|
| C-1 校验可能破坏现有测试 | 现有测试如果构造了非法值会抛异常 | 用 baseline test 数据验证所有现有构造调用合法；若非法则先修数据再补校验 |
| C-2 未知占位符抛异常 | 若现有模板有未知占位符则中断 | grep 现有模板确认无未知占位符 |
| C-3 接线影响错误恢复行为 | ErrorPolicy 可能影响遍历路径 | 现有 ErrorHandler tests 做回归护栏；null policy 保持向后兼容 |
| C-8 TypeHint JsonPropertyName | 序列化键名变更 | System.Text.Json 枚举默认输出数字，JsonPropertyName 无影响（除非已配置 enum-as-string） |

## 10. 验证

- `dotnet build` 0 错误
- `dotnet test` 670+ 全绿
- C-1: 每个 record 各 1 测试（非法值 → DomainValidationException）
- C-2: PlaceholderResolver 测试（未知占位符 → 异常）
- C-3: ErrorHandler 测试（有 ErrorPolicy 的 node → 策略被读取）
