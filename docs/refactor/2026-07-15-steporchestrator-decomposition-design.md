# Design: StepOrchestrator 分解 (D-IV)

> **创建时间**: 2026-07-15
> **状态**: 设计阶段
> **路线图**: P3, 前置 D-I (已完成)
> **方案**: A — 2 组件拆分（StepOrchestrator + InterceptionHandler）

## 1. 现状

`StepOrchestrator.cs` 366 行，`ExecuteStepAsync` 方法 197 行。三类代码混在同一个方法里：

| 类别 | 步骤 | 行数 | 复杂度 |
|------|------|------|--------|
| **Trace 生命周期** | 2, 4, 5, 7, 14 | 15 行 (5 个接口调用) | 低 — 已完全委托给 `ITraceCoordinator` |
| **FSM 调度** | 3 | 2 行 (`StateMachine.StepAsync`) | 低 — 单个接口调用 |
| **路径检测** | 4 | 5 行 | 低 — 字符串比较 |
| **Visited 记账** | 12 | 3 行 (`MarkNodeVisited`) | 低 — 单个方法调用 |
| **拦截/覆盖逻辑** | 8, 9, 10 | **107 行** | **高 — branch/dynamic/frame override 交织** |
| 私有方法 | TryHandleNavigation(47行) + TryHandleScrollAsync(42行) + FromFrame(9行) + GetElementIds(11行) | **109 行** | **高 — 共享 ref 参数, 被步骤 8/9 共用** |
| 占位/注释 | 1, 6, 11, 13 | 15 行 | 无 — 注释 |

**核心问题**: 107 行 override 逻辑 + 109 行私有方法 = **216 行拦截逻辑**与 90 行编排逻辑混在一起。两者职责完全不同：编排 = "什么时候做什么"，拦截 = "FSM 的决定要不要推翻"。

### 已验证的关键事实

- `ITraceCoordinator` 已通过接口完全解耦 — 零提取工作
- `TryHandleScrollAsync` 已是 `internal static` — 可直接搬到新类
- `TryHandleNavigation` 修改 3 个 `ref` 参数 — 提取后需改为返回 struct
- StepOrchestrator 的唯一实例字段 `_lastPushedChildNodeId` 仅被拦截逻辑使用 — 跟随搬出
- 所有依赖通过 `StepContext` 传入 — 无构造器注入问题

## 2. 设计目标

### Goals

- 拆出 `InterceptionHandler`：拥有所有 FSM 拦截/覆盖逻辑（步骤 8-10 + 私有方法）
- StepOrchestrator 保留 14-step 生命周期 + trace + FSM dispatch + visited 记账
- 通过 `IInterceptionHandler` 接口解耦
- `InterceptionResult` 值类型替代 `ref` 参数
- 零行为变更 — 纯机械搬移
- 不新建目录 — 两文件同放 `Traversal/`

### Non-Goals

- 不改 TraceCoordinator（已通过 ITraceCoordinator 完全解耦）
- 不拆 StateUpdater（仅 1 行 `MarkNodeVisited`，违反 YAGNI）
- 不合并步骤 8/9 的重复逻辑（风险高于收益，保持现有结构）
- 不改 StepContext 结构

## 3. 目标结构

```
src/UniClaw.Core/Traversal/
├── StepOrchestrator.cs              ← 保留, ~120 行 (从 366 ↓)
│   ├── ExecuteStepAsync()           ← 保留 14-step lifecycle (no override logic)
│   │   ├── Steps 1-7: trace + FSM + path  ← 不变
│   │   ├── Steps 8-10: → _handler.OnXxx() ← 委托
│   │   └── Steps 11-14: visited + trace   ← 不变
│   └── BranchAllowedSources         ← 保留 (step 8 条件用, 属编排逻辑)
│
├── InterceptionHandler.cs           ← NEW, ~250 行
│   ├── OnBranch()                   ← Step 8 逻辑
│   ├── OnDynamicMatchNodeSelect()   ← Step 9 逻辑
│   ├── OnFrameComplete()            ← Step 10 逻辑
│   ├── TryHandleNavigation()        ← 从 StepOrchestrator 搬入 (private)
│   ├── TryHandleScrollAsync()       ← 从 StepOrchestrator 搬入 (private)
│   ├── FromFrame()                  ← 从 StepOrchestrator 搬入 (private static)
│   ├── GetElementIds()              ← 从 StepOrchestrator 搬入 (private static)
│   └── _lastPushedChildNodeId       ← 从 StepOrchestrator 搬入 (private field)
│
└── IInterceptionHandler.cs          ← NEW, ~15 行
    ├── OnBranch(StepContext, TraversalState fromState) → InterceptionResult
    ├── OnDynamicMatchNodeSelect(StepContext) → InterceptionResult
    └── OnFrameComplete(StepContext) → InterceptionResult
```

### 接口定义

```csharp
// IInterceptionHandler.cs
namespace UniClaw.Core.Traversal;

public interface IInterceptionHandler
{
    /// <summary>Step 8: BRANCH interception — override FSM Branch decision</summary>
    InterceptionResult OnBranch(StepContext ctx, TraversalState fromState);

    /// <summary>Step 9: DYNAMIC_MATCH NodeSelect — child generation, nav, scroll, PressBack</summary>
    InterceptionResult OnDynamicMatchNodeSelect(StepContext ctx);

    /// <summary>Step 10: FRAME_COMPLETE override — DynamicMatch has remaining children</summary>
    InterceptionResult OnFrameComplete(StepContext ctx);
}

/// <summary>FSM transition override result (mutable value type — mutated via ref in TryHandleNavigation). 
/// Passed by ref for zero-copy mutation; not readonly because internal helper methods modify fields.</summary>
public record struct InterceptionResult(
    TraversalState NextState,
    bool ChildPushed,
    bool FrameCompleted,
    bool FrameOverrideTriggered);
```

### StepOrchestrator 简化后

```csharp
public async Task<StepResult> ExecuteStepAsync(StepContext ctx)
{
    bool pathChanged = false, childPushed = false, frameCompleted = false;
    bool antiLoopTriggered = false, frameOverrideTriggered = false;

    // Steps 1-7: trace lifecycle + FSM dispatch + path detection
    // (与现在完全一致 — 5 个 trace 调用 + 1 个 FSM 调用 + 路径检测)

    // Steps 8-10: 条件性委托给 InterceptionHandler
    // 只有当 FSM 转换到特定状态时才调用 handler
    bool intercepted = false;
    InterceptionResult interception = default;
    if (nextState == TraversalState.Branch && BranchAllowedSources.Contains(fromState))
    {
        interception = _handler.OnBranch(ctx, fromState);
        intercepted = true;
    }
    else if (nextState == TraversalState.NodeSelect && ctx.Context.CurrentFrame != null
        && ctx.Context.CurrentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
    {
        interception = _handler.OnDynamicMatchNodeSelect(ctx);
        intercepted = true;
    }
    else if (nextState == TraversalState.FrameComplete && ctx.Context.CurrentFrame != null
        && ctx.Context.CurrentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
    {
        interception = _handler.OnFrameComplete(ctx);
        intercepted = true;
    }

    // 只在拦截实际发生时应用 override — 否则保持 FSM 原始 nextState
    if (intercepted)
    {
        nextState = interception.NextState;
        childPushed = interception.ChildPushed;
        frameCompleted = interception.FrameCompleted;
        frameOverrideTriggered = interception.FrameOverrideTriggered;
    }

    // Steps 11-14: visited + trace
    // (与现在完全一致 — MarkNodeVisited + RecordStepEnd)
}
```

**关键**: `intercepted` flag 确保 FSM 的原始 `nextState` 只在拦截实际触发时才被覆盖。若 FSM 转换到 `ResultVerify` 等非拦截状态，`nextState` 保持不变。

### InterceptionHandler 内部结构

```
OnBranch(ctx, fromState)                OnDynamicMatchNodeSelect(ctx)        OnFrameComplete(ctx)
  ├── guard: currentFrame != null         ├── guard: DynamicMatch?             ├── guard: DynamicMatch?
  ├── GetNextUnvisitedChild()             ├── GetNextUnvisitedChild()          ├── GetNextUnvisitedChild()
  ├── if found: push, set flags           ├── if found: push, set flags        ├── if found: override FrameComplete
  ├── if DynamicMatch exhausted:          ├── if exhausted:                    ├── if none: let FrameComplete pass
  │   ├── TryHandleNavigation()           │   ├── TryHandleNavigation()
  │   ├── TryHandleScrollAsync()          │   ├── TryHandleScrollAsync()
  │   └── fallthrough: frameCompleted     │   └── fallthrough:
  └── if Static exhausted:                │       if depth>1: PressBack+Pop
      frameCompleted                      │       if depth=1: frameCompleted
                                          └── return InterceptionResult
```

## 4. 内聚与耦合验证

### 内聚

| 组件 | 单一职责 | 验证 |
|------|---------|------|
| **StepOrchestrator** | "每步应该执行什么生命周期操作" — trace, FSM dispatch, path detection, visited, 路由到拦截器 | ✅ 不包含任何 override 决策 |
| **InterceptionHandler** | "FSM 的转换决定是否应该被推翻" — branch/dynamic/frame override, 导航检测, 滚动判断 | ✅ 不包含任何 trace 或 FSM dispatch |

### 耦合

```
StepOrchestrator ──→ IInterceptionHandler   (唯一新增依赖, 构造器注入)
                  ──→ ITraceCoordinator     (已有, 不变)
                  ──→ ITraversalStateMachine (已有, 不变)
                  ──→ StepContext           (已有, 不变)

InterceptionHandler ──→ StepContext          (已有, 通过方法参数)
                     ──→ IDynamicChildManager (ctx.ChildMgr)
                     ──→ IVisionProvider      (ctx.Vision)
                     ──→ IActionExecutor      (ctx.Action)
                     ──→ IPageSnapshotManager (ctx.SnapshotMgr)
                     ──→ ITraceCoordinator    (ctx.Trace)
                     ──→ INodeStackAdapter    (ctx.Stack)
                     ──→ INodeRegistry        (ctx.NodeRegistry)

InterceptionHandler ──→ StepOrchestrator      ❌ 零引用 (单向依赖)
```

**无循环依赖。InterceptionHandler 不引用 StepOrchestrator。**

### 依赖方向

```
StepOrchestrator → IInterceptionHandler → StepContext → 8 个已有接口
                                                    → TraversalRuntimeContext
```

InterceptionHandler 的所有依赖都来自 StepContext，不引入新耦合。

## 5. 接口合理性：InterceptionResult 替代 ref 参数

**Before (ref 参数, 不可测试)**:
```csharp
private bool TryHandleNavigation(StepContext ctx, ITraversalNode currentFrame,
    ref bool frameCompleted, ref bool childPushed, ref TraversalState nextState)
```

**After (单 ref 参数, 可单独测试)**:
```csharp
// InterceptionResult 承载所有 FSM override 状态
// record struct (mutable, not readonly) — TryHandleNavigation 内部通过 ref 修改字段
private bool TryHandleNavigation(StepContext ctx, ITraversalNode currentFrame,
    ref InterceptionResult result)
```

`InterceptionResult` 是 `record struct`（非 readonly）— 栈分配、零 GC 压力、值语义、`with` 表达式支持。3 个 `ref bool` + 1 个 `ref TraversalState` → 1 个 `ref InterceptionResult`。内部 helper 通过 `ref` 直接修改字段，调用方看到更新后的值。

## 6. 不改的内容

| 项 | 理由 |
|----|------|
| TraceCoordinator | 已通过 `ITraceCoordinator` 完全解耦 |
| StateUpdater | 仅 1 行 `MarkNodeVisited`，拆出违反 YAGNI |
| 步骤 8/9 重复逻辑合并 | DRY 但改变控制流，风险 > 收益 |
| StepContext 结构 | 15 参数已足够，不加不减 |
| _lastPushedChildNodeId 改为 StepContext 字段 | 这是拦截器的内部状态，不应暴露到 StepContext |

## 7. 改动清单

| # | 文件 | 操作 | 说明 |
|---|------|------|------|
| 1 | `IInterceptionHandler.cs` | **新建** | 3 方法 interface + InterceptionResult struct |
| 2 | `InterceptionHandler.cs` | **新建** | 实现 IInterceptionHandler，搬入 216 行拦截逻辑 |
| 3 | `StepOrchestrator.cs` | **修改** | 删除 steps 8-10 + 4 个私有方法 + `_lastPushedChildNodeId`；添加 `_handler` 字段 + 3 个委托调用 (~90 行保留) |
| 4 | `TraversalEngine.cs` | **修改** | `StepOrchestrator` 构造时注入 `new InterceptionHandler()` |
| 5 | `ArchitectureGuardTests.cs` | **修改** | 新增 `InterceptionHandler_ImplementsIInterceptionHandler` guard |
| 6 | `docs/system/layers/traversal.md` | **修改** | §2 更新为 2 组件架构 |

## 8. 风险

| 风险 | 缓解 |
|------|------|
| `ref` → `InterceptionResult` 语义变化 | TryHandleNavigation 内部只改 `ref InterceptionResult` 一个参数，逻辑零变化 |
| `TryHandleScrollAsync` 从 `internal static` 变为 `private` | 确认无外部调用者（仅步骤 8/9 内联使用） |
| `FromFrame` / `GetElementIds` 搬移遗漏引用 | `dotnet build` 每步验证 |
| BranchAllowedSources 归属判断 | 留在 StepOrchestrator — 它是"是否触发拦截"的条件，属编排逻辑 |

## 9. 验证

- `dotnet build` 0 错误（每步验证）
- `dotnet test` 665 全绿（行为零变更）
- 新 guard: `InterceptionHandler_ImplementsIInterceptionHandler`
- `openspec validate` (if available)
