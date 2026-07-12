# 2026-07-12 — ITraversalContext Reform (D-III) Design

> **状态**: Approved · 等待实施
> **优先级**: P2（Phase 2 核心任务）
> **前置依赖**: D-I (Context Decomposition) ✅
> **OpenSpec Change**: 待创建 `itraversalcontext-reform`

---

## 0. 决策来源

本文档基于 brainstorming session 的 3 个关键决策：

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 改革范围 | **A（保持属性，移除 setters）** | 最小改动，解决核心问题；属性精简可作为后续任务 |
| Mutation 模式 | **C（concrete class 方法）** | 符合 D-I/D-V 模式 — 接口只读，concrete 可变 |
| 接口暴露 | **混合方案** | ITraversalContext 保持所有 getters（只读），mutation 通过 concrete SetXxx() 方法 |

---

## 1. 问题陈述

### 1.1 当前问题

**D-7: GlobalState 暂留 ITraversalContext**

`ITraversalContext` 接口暴露了 3 个可写属性：

```csharp
public interface ITraversalContext
{
    // 只读属性（合理）
    INodeStack NodeStack { get; }
    IReadOnlyList<string> CurrentPath { get; }
    // ... 等

    // ❌ 可写属性（D-7 问题点）
    ITraversalNode? CurrentFrame { get; set; }   // FSM 可以修改
    GlobalState GlobalState { get; set; }         // 任何人都可修改
    Exception? LastError { get; set; }            // 任何人都可修改
}
```

### 1.2 痛点

| 痛点 | 影响 |
|------|------|
| **接口泄露 mutation** | 任何持有 ITraversalContext 的代码都可以修改 GlobalState/LastError |
| **违反 FSM 独立原则** | TraversalFSM 不应该通过接口修改 GlobalState（宏观 FSM 状态） |
| **设计债务** | 与 D-I (Context Decomposition) 和 D-V (Interface Extraction) 的模式不一致 |
| **无法区分读写权限** | 接口既用于只读访问又用于可变操作 |

### 1.3 目标

1. **接口隔离 mutation** — ITraversalContext 变成纯只读接口
2. **明确 mutation 入口** — mutation 通过 concrete class 的 SetXxx() 方法
3. **符合已建立的模式** — 与 D-I/D-V 的"接口只读，concrete 可变"一致
4. **最小改动** — 不移除属性，只移除 setters

---

## 2. 设计方案

### 2.1 核心原则

```
ITraversalContext       = 纯只读接口（只读 getters）
TraversalRuntimeContext = concrete class，暴露 SetXxx() 方法
ITraversalStateMachine.Context = 保持 ITraversalContext（只读视图）
FSM/Handler 内部        = 持有 TraversalRuntimeContext 引用用于 mutation
```

### 2.2 接口变更

```csharp
/// <summary>
/// 遍历上下文接口 (D-III: 纯只读接口，移除所有 setters)。
/// 消费者不能通过此接口修改任何状态。
/// </summary>
public interface ITraversalContext
{
    /// <summary>节点栈</summary>
    INodeStack NodeStack { get; }

    /// <summary>当前路径</summary>
    IReadOnlyList<string> CurrentPath { get; }

    /// <summary>已访问的页面</summary>
    IReadOnlySet<string> VisitedPages { get; }

    /// <summary>已访问的子节点</summary>
    IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren { get; }

    /// <summary>已访问的节点</summary>
    IReadOnlySet<string> VisitedNodes { get; }

    /// <summary>当前帧（节点） — 只读，修改通过 SetCurrentFrame()</summary>
    ITraversalNode? CurrentFrame { get; }  // ← 移除 set;

    /// <summary>步骤计数</summary>
    int StepCount { get; }

    /// <summary>全局状态 — 只读，修改通过 SetGlobalState()</summary>
    GlobalState GlobalState { get; }  // ← 移除 set;

    /// <summary>最后的错误 — 只读，修改通过 SetLastError()</summary>
    Exception? LastError { get; }  // ← 移除 set;
}
```

### 2.3 Concrete Class 变更

```csharp
/// <summary>
/// 遍历运行时上下文 (D-III: 添加明确的 mutation 方法)。
/// </summary>
public sealed class TraversalRuntimeContext : ITraversalContext
{
    // === 5 Sub-Contexts (不变) ===
    private readonly NavigationContext _navigation;
    private readonly ErrorContext _error;
    private readonly SessionContext _session;
    private readonly ProgressContext _progress;
    private readonly CacheContext _cache;

    // === ITraversalContext 只读实现（不变） ===
    public INodeStack NodeStack => _navigation.NodeStack;
    public IReadOnlyList<string> CurrentPath => _navigation.CurrentPath;
    public IReadOnlySet<string> VisitedPages => _navigation.VisitedPages;
    public IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren => _navigation.VisitedChildren;
    public IReadOnlySet<string> VisitedNodes => _navigation.VisitedNodes;
    public int StepCount => _progress.StepCount;

    // ← 移除 setters，改为只读
    public ITraversalNode? CurrentFrame => _navigation.CurrentFrame;
    public GlobalState GlobalState => _session.GlobalState;
    public Exception? LastError => _error.LastError;

    // === 新增：明确的 mutation 方法 ===
    /// <summary>设置当前帧（节点）</summary>
    public void SetCurrentFrame(ITraversalNode? value) =>
        _navigation.CurrentFrame = value;

    /// <summary>设置全局状态</summary>
    public void SetGlobalState(GlobalState value) =>
        _session.GlobalState = value;

    /// <summary>设置最后的错误</summary>
    public void SetLastError(Exception? value) =>
        _error.LastError = value;

    // === 其他 engine-only 方法（已存在，不变） ===
    public void AppendPath(string page) => _navigation.AppendPath(page);
    public void PopPath() => _navigation.PopPath();
    // ... 等
}
```

### 2.4 TraversalFSM 变更

```csharp
/// <summary>
/// 遍历状态机 (D-III: 添加 RuntimeContext 属性用于 mutation)。
/// </summary>
public sealed class TraversalFSM : ITraversalStateMachine
{
    private readonly TraversalRuntimeContext _runtimeContext;  // ← 新增 concrete 引用
    private readonly TraversalState _currentState;

    public TraversalFSM(TraversalRuntimeContext context)
    {
        _runtimeContext = context;
        _currentState = TraversalState.NodeSelect;
    }

    // ITraversalStateMachine 实现（只读视图）
    public TraversalState CurrentState => _currentState;
    public ITraversalContext Context => _runtimeContext;  // ← 只读视图

    // ← 新增：可写视图，供内部使用
    public TraversalRuntimeContext RuntimeContext => _runtimeContext;

    // === Handler 方法中使用 RuntimeContext 进行 mutation ===
    private TraversalState HandleErrorHandling(...)
    {
        try
        {
            // ... handler logic
        }
        catch (Exception ex)
        {
            // ← 改前：Context.LastError = ex;
            // ← 改后：
            RuntimeContext.SetLastError(ex);
            RuntimeContext.SetGlobalState(GlobalState.Error);
        }
    }
}
```

### 2.5 PopupHandler 变更

```csharp
/// <summary>
/// 弹窗处理器 (D-III: 使用 concrete context 的 SetXxx() 方法)。
/// </summary>
public sealed class PopupHandler
{
    private readonly TraversalRuntimeContext _context;  // ← 已经是 concrete

    // StateRestorer 使用 SetXxx() 方法
    private void RestoreState(PopupPreservedState preserved)
    {
        // ← 改前：context.CurrentFrame = preserved.NodeStackFrames[0].Node;
        // ← 改后：
        _context.SetCurrentFrame(preserved.NodeStackFrames[0].Node);

        // ← 改前：context.GlobalState = preserved.CurrentState;
        // ← 改后：
        _context.SetGlobalState(preserved.CurrentState);

        // ← 改前：context.LastError = preserved.ExecutionResult != null ? ... : null;
        // ← 改后：
        _context.SetLastError(preserved.ExecutionResult != null
            ? new InvalidOperationException(preserved.ExecutionResult)
            : null);
    }
}
```

---

## 3. Consumer 变更清单

### 3.1 需要修改的文件

| 文件 | 变更类型 | 变更内容 |
|------|---------|---------|
| `TraversalState.cs` | 接口定义 | ITraversalContext 移除 3 个 setters |
| `TraversalRuntimeContext.cs` | 方法新增 | 添加 SetCurrentFrame(), SetGlobalState(), SetLastError() |
| `TraversalFSM.cs` | 属性新增 | 添加 RuntimeContext 属性，改用 SetXxx() |
| `PopupHandler.cs` | 方法修改 | StateRestorer 改用 SetXxx() |
| 相关测试文件 | 测试更新 | 使用 SetXxx() 而非属性赋值 |

### 3.2 变更位置明细

**TraversalFSM.cs** (2 处):
```csharp
// Line 118
- Context.LastError = ex;
+ RuntimeContext.SetLastError(ex);

// Line 217
- Context.LastError = ex;
+ RuntimeContext.SetLastError(ex);
```

**PopupHandler.cs** (3 处):
```csharp
// Line 350
- context.CurrentFrame = preserved.NodeStackFrames[0].Node;
+ context.SetCurrentFrame(preserved.NodeStackFrames[0].Node);

// Line 362
- context.GlobalState = preserved.CurrentState;
+ context.SetGlobalState(preserved.CurrentState);

// Line 365
- context.LastError = preserved.ExecutionResult != null ? ... : null;
+ context.SetLastError(preserved.ExecutionResult != null ? ... : null);
```

---

## 4. 实施计划

### 4.1 实施步骤

| 步骤 | 任务 | 验证 |
|------|------|------|
| 1 | 修改 `ITraversalContext` — 移除 3 个 setters | 编译通过 |
| 2 | 修改 `TraversalRuntimeContext` — 添加 3 个 SetXxx() 方法，移除 ITraversalContext 实现的 setters | 编译通过 |
| 3 | 修改 `TraversalFSM` — 添加 RuntimeContext 属性，改用 SetXxx() | 测试通过 |
| 4 | 修改 `PopupHandler` — 改用 SetXxx() | 测试通过 |
| 5 | 更新所有相关测试 | 617 tests 全绿 |
| 6 | 更新 docs/system/decisions/log.md D-7 | 手动验证 |

### 4.2 渐进式验证

每步必须满足：

| 约束 | 验证方式 |
|------|---------|
| 测试全绿 | dotnet test + CI |
| 编译无警告 | dotnet build |
| 接口一致性 | code review |

---

## 5. 与其他 Decisions 的关系

| Decision | 关系 |
|----------|------|
| D-7 (GlobalState 暂留 ITraversalContext) | **本设计解决此决策** — D-III 完成后，D-7 状态改为 Fixed |
| D-I (Context Decomposition) | **前置依赖** — 本设计利用 D-I 建立的 sub-context 边界 |
| D-V (Interface Extraction) | **模式一致** — 本设计遵循 D-V 的"接口隔离 mutation" 原则 |
| D-15 (Subsystem naming) | **模式一致** — GlobalState → SessionContext, CurrentFrame → NavigationContext, LastError → ErrorContext |

---

## 6. 未决问题（Open Questions）

当前无未决问题。所有 3 个关键决策已确认。

---

## 7. 设计决策记录

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 是否移除属性 | 否（只移除 setters） | AI advisor 已有 Snapshot，属性保留不影响；改动范围最小 |
| Mutation 模式 | Concrete class SetXxx() 方法 | 符合 D-I/D-V 模式；明确 mutation 入口 |
| 接口暴露 | 混合方案 | ITraversalContext 保持所有 getters（只读），mutation 通过 concrete |

---

## 8. 参考

- D-7: `docs/system/decisions/log.md D-7` — GlobalState 暂留 ITraversalContext
- D-I: `docs/refactor/2026-07-12-context-decomposition-design.md` — Context Decomposition 设计
- D-V: `docs/system/decisions/log.md D-V` — Interface Extraction pattern
- D-15: `docs/system/decisions/log.md D-15` — Subsystem canonical definition
- M-14: `docs/refactor/11-m14-globalstate-evaluation.md` — M-14 评估文档
- Roadmap: `docs/refactor/20-b-refactoring-roadmap-design.md` — P2 详细

---

## 9. 变更历史

| 日期 | 变更 | 作者 |
|------|------|------|
| 2026-07-12 | 初始设计 — 基于 brainstorming session 3 个决策 | Claude + 用户 |

---

## 10. 实施完成记录

*(待实施完成后填写)*

### 实施结果

**状态**: ⏳ 待实施

**步骤 1 - ITraversalContext**:
- ⏳ 移除 3 个 setters

**步骤 2 - TraversalRuntimeContext**:
- ⏳ 添加 SetCurrentFrame(), SetGlobalState(), SetLastError()

**步骤 3 - TraversalFSM**:
- ⏳ 添加 RuntimeContext 属性
- ⏳ 改用 SetXxx() 方法

**步骤 4 - PopupHandler**:
- ⏳ StateRestorer 改用 SetXxx() 方法

**步骤 5 - 测试验证**:
- ⏳ 更新所有相关测试
- ⏳ 617 tests 全绿

**步骤 6 - 文档更新**:
- ⏳ 更新 docs/system/decisions/log.md D-7 状态为 Fixed
