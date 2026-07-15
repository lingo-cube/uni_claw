## Why

Python→C# gap triage 裁剪出 8 项 backlog（桶 C）。其中 C-1~C-4 + C-8 共享同一主题：**当前代码静默接受非法值、丢弃字段、忽略已有策略字段**，违反项目 fail-fast 约定。12 个 Graph 模型 record 无构造期校验（`Precondition.TimeoutSeconds = 0` 合法），`PlaceholderResolver` 对未知占位符静默保留，`TemplateInstantiator` 丢弃 `Target.Meta`/`Restore` 字段，`ErrorHandler` 忽略已定义的 `node.ErrorPolicy`。这些都是"silent failure"——不抛异常，但行为错误。

## What Changes

- **C-1 构造期校验**: 12 个 Graph 模型 record 从 primary constructor 改为手动构造函数 + `DomainValidationException`（`Precondition`、`DynamicRule`、`ChildrenStrategy`、`ErrorPolicy`、`ExitCondition`、`CompletionPolicy`、`EntryPolicy`、`TraversalNode`），覆盖范围/非空约束
- **C-2 静默失败 → 抛异常**: `PlaceholderResolver.Resolve()` 调用已有 `HasUnresolvedPlaceholders()` 检测未解析占位符并抛 `DomainValidationException`；`TemplateInstantiator` 补 `Target.Meta`、`RestoreAction.Target`/`Params`、`Precondition.UiCondition` 三个字段
- **C-3 ErrorPolicy 接线**: `ErrorStrategySelector.SelectStrategy()` 读取 `node.ErrorPolicy`（若非 null），将其 `MaxRetries` 和 `OnError` 映射到恢复策略；null policy 保持向后兼容
- **C-4 根节点校验**: `TraversalPlan` 构造函数断言 `RootNode` 非 null、类型为 Container/Screen、操作为 NoAction
- **C-8 P3 五项**: `ContentNode.ToMarkdown()`、`Region.Id` 非空校验、`TypeHint` 加 `[JsonPropertyName]`、`TypeHintExtensions.IsCanonical(string)`
- **BREAKING — 潜在**: 若现有代码构造了非法值（如 `TimeoutSeconds: 0`），校验会抛异常。需先用 baseline 测试验证，若发现非法值则先修数据

## Capabilities

### New Capabilities
_(无 — 纯校验/补字段，不新增功能)_

### Modified Capabilities
- `graph-foundation`: Graph 模型 record 从无校验 primary constructor → 手动构造函数 + `DomainValidationException`
- `traversal-fsm`: `ErrorHandler.ErrorStrategySelector` 读取 `node.ErrorPolicy` 字段（当前被忽略）

## Impact

- **修改 10 文件**: `TraversalNode.cs`、`TraversalPlan.cs`、`EntryConfig.cs`、`PlaceholderResolver.cs`、`TemplateInstantiator.cs`、`ErrorHandler.cs`、`PlanCompiler.cs`、`TreeAndFingerprint.cs`、`Region.cs`、`TypeHint.cs` + `TypeHintExtensions.cs`
- **依赖**: 零新增。所有改动在已有文件内
- **风险**: C-1 构造期校验可能暴露现有非法值（baseline 测试数据）；C-2 未知占位符若存在于现有模板会抛异常；C-3 策略映射影响错误恢复路径
- **详细设计**: 见 `docs/refactor/2026-07-15-fail-fast-validation-baseline-design.md`
