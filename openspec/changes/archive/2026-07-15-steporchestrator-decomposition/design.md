## Context

`StepOrchestrator.ExecuteStepAsync` 197 行，14 个步骤中混杂编排（trace/FSM/visited ~90 行）和拦截（override 逻辑 216 行含私有方法）。`ITraceCoordinator` 已完全解耦，`StateUpdater` 仅 1 行 — 路线图 4 组件设想与代码现实不符。本设计采用方案 A（2 组件拆分），聚焦唯一痛点。完整设计见 `docs/refactor/2026-07-15-steporchestrator-decomposition-design.md`。

## Goals / Non-Goals

**Goals:**
- 拆出 `InterceptionHandler`：拥有所有 FSM 拦截/覆盖逻辑
- StepOrchestrator 保留 14-step 生命周期编排
- `IInterceptionHandler` 接口解耦，可 mock 测试
- `InterceptionResult` 值类型替代 4 个 `ref` 参数

**Non-Goals:**
- 不改 TraceCoordinator（已解耦）
- 不拆 StateUpdater（1 行，YAGNI）
- 不合并步骤 8/9 重复逻辑
- 不改 StepContext

## Decisions

### 1. 2 组件而非 4 组件

代码分析显示 TraceCoordinator 已通过 `ITraceCoordinator` 完全解耦（零提取工作），StateUpdater 仅 1 行 `MarkNodeVisited`（拆出违反 YAGNI）。路线图写于代码分析之前，按代码现实调整。

### 2. InterceptionResult 替代 4 个 ref 参数

当前 `TryHandleNavigation` 接收 3 个 `ref bool` + 1 个 `ref TraversalState`。改为 `record struct`（非 readonly，支持 `ref` 修改）→ 1 个 `ref InterceptionResult`。接口方法直接返回 `InterceptionResult`，清晰表达 "FSM override 结果"。

### 3. intercepted flag 防止 default 污染

`InterceptionResult` 的 `default` 值 `(default(TraversalState), false, false, false)` 会覆盖 FSM 的有效 `nextState`。StepOrchestrator 加 `intercepted` flag，仅当 handler 实际被调用时才应用 override。

### 4. BranchAllowedSources 留 StepOrchestrator

`BranchAllowedSources` 是"是否触发拦截"的编排条件，不是拦截逻辑本身。留在 StepOrchestrator 中作为 handler 调用的 guard。

### 5. TryHandleScrollAsync 保持 internal static (修正)

~~当前为 `internal static` 但仅步骤 8/9 内联使用。搬入 InterceptionHandler 后改为 `private`，保持封装。~~

**修正 (apply 时发现)**: `ScrollLoopTerminationTests.cs` 直接调用 `StepOrchestrator.TryHandleScrollAsync` 共 10 处 (契约测试)。
经用户确认: 搬入 InterceptionHandler 后**保持 `internal static`**, 测试调用点改为 `InterceptionHandler.TryHandleScrollAsync`, 契约测试保持不变。

### 6. OnBranch / OnDynamicMatchNodeSelect 为 async (编译必然)

两方法体内 `await TryHandleScrollAsync` / `await ctx.Action.PressBackAsync()`, 故返回 `Task<InterceptionResult>`。
`OnFrameComplete` 无异步工作, 保持同步返回 `InterceptionResult`。

## Target Structure

```
src/UniClaw.Core/Traversal/
├── StepOrchestrator.cs              ← ~120 行 (from 366)
│   └── ExecuteStepAsync()           ← lifecycle only, delegates steps 8-10
├── IInterceptionHandler.cs          ← NEW
│   ├── IInterceptionHandler         ← 3 methods
│   └── InterceptionResult           ← record struct (4 fields)
└── InterceptionHandler.cs           ← NEW (~250 行)
    ├── OnBranch / OnDynamicMatchNodeSelect / OnFrameComplete
    ├── TryHandleNavigation / TryHandleScrollAsync (private)
    └── FromFrame / GetElementIds (private static)
```

## Interface

```csharp
public interface IInterceptionHandler
{
    Task<InterceptionResult> OnBranch(StepContext ctx, TraversalState fromState);
    Task<InterceptionResult> OnDynamicMatchNodeSelect(StepContext ctx);
    InterceptionResult OnFrameComplete(StepContext ctx);
}

public record struct InterceptionResult(
    TraversalState NextState,
    bool ChildPushed,
    bool FrameCompleted,
    bool FrameOverrideTriggered);
```

## Coupling

```
StepOrchestrator → IInterceptionHandler   (唯一新增)
InterceptionHandler → StepContext          (已有)
InterceptionHandler → StepOrchestrator     ❌ 零引用
```

## Risks

| 风险 | 缓解 |
|------|------|
| `ref` → `InterceptionResult` 语义 | TryHandleNavigation 内部只改 1 个 ref 参数，逻辑零变化 |
| default 值污染 | `intercepted` flag 守卫 |
| TryHandleScrollAsync 可见性 | grep 确认零外部调用 |
| BranchAllowedSources 归属 | 留 StepOrchestrator（编排条件） |

## Migration

单分支提交，每步 build 验证:
1. 新建 `IInterceptionHandler.cs` + `InterceptionHandler.cs`
2. 搬移 `_lastPushedChildNodeId` + 4 个私有方法 → InterceptionHandler
3. 搬移步骤 8-10 逻辑 → InterceptionHandler 的 3 个 public 方法
4. StepOrchestrator 改为委托 `_handler.OnXxx()`
5. TraversalEngine 注入 `new InterceptionHandler()`
6. Guard test + `dotnet test` 全量回归
