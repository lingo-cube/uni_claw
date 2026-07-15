## Context

Python→C# gap triage 桶 C 的 5 项统一主题："让 silent failure 变 loud failure"。完整设计见 `docs/refactor/2026-07-15-fail-fast-validation-baseline-design.md`。

## Goals / Non-Goals

**Goals:**
- 12 个 Graph 模型 record 加构造期 `DomainValidationException`
- `PlaceholderResolver` 未知占位符抛异常替代静默保留
- `TemplateInstantiator` 补 3 个丢弃字段
- `ErrorHandler` 读 `node.ErrorPolicy`
- P3 五项零碎

**Non-Goals:**
- 不改 `ErrorClassifier`/`RecoveryExecutor`
- 不新增接口
- 不新增测试（仅改现有失败测试）

## Decisions

### 1. Primary constructor → 手动构造函数

C# `sealed record class` 不直接支持构造期校验。选择手动构造函数（与已有 `EntryConfig` 一致），而非 init-only property validation。12 个 record 各 2-4 字段，改动量小。

### 2. ErrorPolicy null = 向后兼容

C-3 只在 `node.ErrorPolicy != null` 时读取，null 走现有的硬编码默认路径。不影响没有显式 ErrorPolicy 的节点。

### 3. 校验顺序

C-1（构造期）先做 → C-2（模板字段）→ C-4（根节点）→ C-3（ErrorPolicy）→ C-8（Domain 零碎）。C-1 可能暴露非法数据，需优先修。

## C-1: 12 项校验

| # | Record | 字段 | 规则 |
|---|--------|------|------|
| 1 | Precondition | TimeoutSeconds | >0 && ≤300 |
| 2 | DynamicRule | RuleId | 非空 |
| 3 | DynamicRule | ChildTemplate | 非空 |
| 4 | ChildrenStrategy | MaxChildren | 0-10000 |
| 5 | ErrorPolicy | MaxRetries | 0-100 |
| 6 | ExitCondition | MaxDepth (DepthLimited) | >0 && ≤1000 |
| 7 | CompletionPolicy | TargetName (TargetFound) | 非空 |
| 8 | CompletionPolicy | TimeoutSeconds | >0 && ≤86400 |
| 9 | CompletionPolicy | MaxSteps | 1-1000000 |
| 10 | EntryPolicy | TimeoutSeconds | >0 && ≤300 |
| 11 | TraversalNode | NodeId | 非空 |
| 12 | TraversalNode | Name | 非空 |

## C-2: PlaceholderResolver + TemplateInstantiator

- PlaceholderResolver: `Resolve()` 末尾调 `HasUnresolvedPlaceholders()` → 抛 DomainValidationException
- TemplateInstantiator: `CreateOperation()` 传 `meta` → Target.Meta；读 restore `target`/`params` → RestoreAction；`CreatePrecondition()` 读 `ui_condition`

## C-3: ErrorPolicy wiring

ErrorStrategySelector 读 `node.ErrorPolicy`:
- MaxRetries ← `node.ErrorPolicy.MaxRetries`
- OnError 映射: Retry→Retry链, Skip→Skip链, Abort→Abort, Backtrack→Backtrack链

## Coupling

零新增依赖。所有改动在已有文件内。C-3 唯一改 ErrorHandler 内部逻辑，不影响其他组件。

## Risks

| 风险 | 缓解 |
|------|------|
| 校验破坏现有构造调用 | 先跑 baseline test，非法值先修 |
| 未知占位符导致中断 | grep 模板确认 |
| ErrorPolicy 影响恢复 | null policy 向后兼容 + 现有 ErrorHandler tests |
