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

### 4. C-4 RootNode 保留可空（实现期裁决 2026-07-15）

原 spec 要求 `RootNode == null` 抛异常。实现期发现与 `TraversalEngine.BuildDefaultRoot` 兜底特性冲突（专测 `Constructor_NoRootNode_BuildsDefaultRoot` 守护）。裁决：**null 保留合法**（引擎 fail-safe 兜底，非 silent failure，不在「让 silent failure 变 loud」主题内），仅在显式提供根节点时校验类型(Screen/Container)+操作(NoAction)。spec/design 已同步。待归档记入 decisions/log。

### 5. C-3 经 ITraversalNode.ErrorPolicy 接线（实现期裁决 2026-07-15）

C-3 要让 `ErrorStrategySelector` 读「当前节点」的 ErrorPolicy，但 `CurrentFrame` 暴露的是最小接口 `ITraversalNode`（无 ErrorPolicy）。裁决：给 `ITraversalNode` 增加只读属性 `ErrorPolicy? ErrorPolicy { get; }`（TraversalNode 已实现，零改动；2 个测试 mock 各加一行）。理由：ErrorPolicy 本就是「描述遍历节点」的 Graph 概念，属于接口既有职责，非「新增接口」（不违反 Non-Goal）。`TraversalFSM` 经 `ctx.CurrentFrame?.ErrorPolicy` 透传进 `StrategySelectionContext.ErrorPolicy`。Fallback 的 OnError 不映射为单独策略（无 ErrorStrategy.Fallback），回退到 ErrorType 默认链，FallbackTarget 由上层驱动。

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
