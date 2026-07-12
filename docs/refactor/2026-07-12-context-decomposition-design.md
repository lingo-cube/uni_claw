# 2026-07-12 — Context Decomposition Design

> **状态**: Approved · 等待实施
> **优先级**: P2（Phase 2 核心任务）
> **前置依赖**: D-15 (subsystem naming) ✅, D-V (interface extraction) ✅
> **OpenSpec Change**: 待创建 `context-decomposition`

---

## 0. 决策来源

本文档基于 brainstorming session 的 6 个关键决策：

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 拆分模式 | **C（Container 模式）** | 暴露 5 个 sub-contexts，结构清晰，可独立演进 |
| Sub-context 类型 | **A（可变 sealed class）** | Engine runtime state 需要 mutable，避免 record 复制开销 |
| Namespace | **A（全部 StateMachine）** | 统一管理，最小变更 |
| 只读接口 | **A（需要接口）** | 可 mock 测试，隔离 mutation，与 D-V 一致 |
| 接口内容 | **A（只暴露 getters）** | 完全只读，mutation 方法只在 concrete class |
| 实施顺序 | **B（逐个拆分）** | 渐进式，每步有测试保护，降低风险 |

---

## 1. 问题陈述

### 1.1 当前问题

**D-I: God Object**

`TraversalRuntimeContext` 是一个 God Object — 30 个可变状态集中在单一 class 中：

- 12 个导航相关字段（`_nodeStack`, `_visitedPages`, `_visitedChildren` 等）
- 5 个错误追踪字段（`_failedNodes`, `_consecutiveErrors`, `_retryCount` 等）
- 4 个会话字段（`_traceId`, `_globalState`, `_deviceExperience`, `_aiProvider`）
- 5 个进度字段（`_stepCount`, `_maxDepth`, `_completionPolicy`, `_actionHistory`, `_waitAfterActionMs`）
- 2+2 个缓存字段（`_pageCache`, `_cacheValid` + Phase 3 reserved）

### 1.2 痛点

| 痛点 | 影响 |
|------|------|
| **职责混乱** | 一个 class 承载 5 个不同子系统的状态 |
| **测试困难** | 无法单独 mock 某个子系统（如只 mock ErrorContext） |
| **演化困难** | 修改一个子系统需要理解整个 Context |
| **边界不清** | 哪些字段属于 Navigation vs Cache 不直观（D-15 前） |

### 1.3 目标

1. **清晰的职责边界** — 5 个独立的 sub-context classes，每个负责一个子系统
2. **可测试性** — 每个 sub-context 可独立 mock
3. **可演化性** — 单个子系统的修改不影响其他部分
4. **保持性能** — mutable runtime state，避免 record 复制开销

---

## 2. 设计方案

### 2.1 整体架构

```
TraversalRuntimeContext (Container)
    ├── NavigationContext (12 fields)   — DFS traversal
    ├── ErrorContext (5 fields)         — Error tracking
    ├── SessionContext ( 4 fields)      — Macro state
    ├── ProgressContext (5 fields)      — Progress control
    └── CacheContext (2+2 fields)       — Cache & config
```

### 2.2 Namespace 结构

所有 5 个 sub-contexts 位于 `UniClaw.Core.StateMachine`：

```
UniClaw.Core.StateMachine/
├── ITraversalContext.cs
├── TraversalRuntimeContext.cs         (Container)
│
├── Navigation/
│   ├── INavigationContext.cs           (只读接口)
│   └── NavigationContext.cs            (sealed class, mutable)
│
├── Error/
│   ├── IErrorContext.cs
│   └── ErrorContext.cs
│
├── Session/
│   ├── ISessionContext.cs
│   └── SessionContext.cs
│
├── Progress/
│   ├── IProgressContext.cs
│   └── ProgressContext.cs
│
└── Cache/
    ├── ICacheContext.cs
    └── CacheContext.cs
```

**为什么不拆到独立 namespace？**
- 当前 `TraversalRuntimeContext` 就在 `StateMachine`
- 这些是 FSM/Handler 的运行时状态，不是 Traversal 引擎的抽象
- 最小变更 — 不涉及跨 namespace 移动

### 2.3 每个 Sub-context 定义

#### NavigationContext (12 fields)

**职责**: DFS 遍历 — 节点选择、路径追踪、页面身份、已访问管理

```csharp
public interface INavigationContext
{
    INodeStack NodeStack { get; }
    IReadOnlyList<string> CurrentPath { get; }
    PageAnalysis? CurrentPageAnalysis { get; }
    VisitFingerprint? CurrentFingerprint { get; }
    IReadOnlySet<string> VisitedPages { get; }
    IReadOnlySet<string> VisitedNodes { get; }
    IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren { get; }
    IReadOnlySet<string> VisitedLevel1Menus { get; }
    IReadOnlySet<string> VisitedLevel2Menus { get; }
    ContentNode? PageTree { get; }
    ITraversalNode? CurrentFrame { get; }
}

public sealed class NavigationContext : INavigationContext
{
    // 12 private fields
    // 只读属性实现 INavigationContext
    // Mutation 方法: AppendPath, PopPath, MarkVisited, MarkNodeVisited, AddVisitedChild, ...
}
```

#### ErrorContext (5 fields)

**职责**: 错误追踪 — 错误记录、重试计数、失败追踪、恢复状态

```csharp
public interface IErrorContext
{
    IReadOnlyDictionary<string, ErrorRecord> FailedNodes { get; }
    int ConsecutiveErrors { get; }
    int RetryCount { get; }
    Exception? LastError { get; }
    IReadOnlyList<Exception>? ExceptionChain { get; }
}

public sealed class ErrorContext : IErrorContext
{
    // 5 private fields
    // 只读属性
    // Mutation 方法: IncrementConsecutiveErrors, ResetConsecutiveErrors, IncrementRetryCount, AddFailedNode, ...
}
```

#### SessionContext (4 fields)

**职责**: 宏观状态 — 全局 FSM 状态、trace 身份、设备/AI 配置

```csharp
public interface ISessionContext
{
    string TraceId { get; }
    GlobalState GlobalState { get; }  // D-7: setter 在 concrete class，不在接口
    string? DeviceExperience { get; }
    string? AIProvider { get; }
}

public sealed class SessionContext : ISessionContext
{
    // 4 private fields
    // 只读属性
    // GlobalState setter 在 concrete class (D-7 遗留，D-III 解决)
}
```

#### ProgressContext (5 fields)

**职责**: 进度控制 — 步骤计数、完成策略、动作审计、时序配置

```csharp
public interface IProgressContext
{
    int StepCount { get; }
    int MaxDepth { get; }
    CompletionPolicy? CompletionPolicy { get; }
    IReadOnlyList<ActionRecord> ActionHistory { get; }
    int WaitAfterActionMs { get; }
}

public sealed class ProgressContext : IProgressContext
{
    // 5 private fields
    // 只读属性
    // Mutation 方法: IncrementStepCount, AddActionHistory, SetCompletionPolicy, ...
}
```

#### CacheContext (2+2 fields)

**职责**: 缓存与配置 — 页面缓存、缓存有效性、快照（Phase 3 预留）

```csharp
public interface ICacheContext
{
    IReadOnlyDictionary<string, object> PageCache { get; }
    bool CacheValid { get; }
    // Phase 3 reserved: ScrollHandler, CurrentSnapshot
}

public sealed class CacheContext : ICacheContext
{
    // 2 core private fields
    // 2 Phase 3 reserved fields (object?)
    // 只读属性
    // Mutation 方法: SetCacheValid, PageCache indexer (读写), ...
}
```

### 2.4 TraversalRuntimeContext Container

```csharp
public sealed class TraversalRuntimeContext
{
    // 5 sub-contexts（构造时创建，不可替换）
    public NavigationContext Navigation { get; }
    public ErrorContext Error { get; }
    public SessionContext Session { get; }
    public ProgressContext Progress { get; }
    public CacheContext Cache { get; }

    // ITraversalContext 实现委托到 sub-contexts
    public INodeStack NodeStack => Navigation.NodeStack;
    public IReadOnlyList<string> CurrentPath => Navigation.CurrentPath;
    public IReadOnlySet<string> VisitedPages => Navigation.VisitedPages;
    public int StepCount => Progress.StepCount;
    // ... 等

    // 构造器
    public TraversalRuntimeContext(string traceId, int maxDepth = 10, NodeStack? nodeStack = null)
    {
        Navigation = new NavigationContext(traceId, maxDepth, nodeStack);
        Error = new ErrorContext();
        Session = new SessionContext(traceId);
        Progress = new ProgressContext(maxDepth);
        Cache = new CacheContext();
    }

    // CreateReadOnlySnapshot 保持不变（D-III 再精简）
    public TraversalContextSnapshot CreateReadOnlySnapshot() { ... }
}
```

---

## 3. Consumer 变更

### 3.1 使用方式变化

```csharp
// 改前
context.VisitedPages.Contains("page");
context.IncrementStepCount();
context.StepCount++;
context.FailedNodes.TryGetValue(nodeId, out var error);

// 改后
context.Navigation.VisitedPages.Contains("page");
context.Progress.IncrementStepCount();
context.Progress.StepCount++;
context.Error.FailedNodes.TryGetValue(nodeId, out var error);
```

### 3.2 主要 Consumers

| Consumer | 原访问 | 新访问 |
|----------|--------|--------|
| DynamicChildManager | `context.VisitedLevel1Menus` | `context.Navigation.VisitedLevel1Menus` |
| ErrorHandler | `context.FailedNodes` | `context.Error.FailedNodes` |
| RecoveryExecutor | `context.ConsecutiveErrors` | `context.Error.ConsecutiveErrors` |
| GlobalFSM | `context.GlobalState` | `context.Session.GlobalState` |
| CompletionDetector | `context.StepCount` | `context.Progress.StepCount` |
| PageCacheManager | `context.PageCache` | `context.Cache.PageCache` |
| TraceCoordinator | `context.TraceId` | `context.Session.TraceId` |
| NodeStackAdapter | `context.NodeStack` | `context.Navigation.NodeStack` |

### 3.3 Engine 内部访问

`TraversalEngine` 持有 `TraversalRuntimeContext` 引用，通过 sub-contexts 修改状态：

```csharp
// TraversalEngine.Step()
context.Navigation.MarkVisited(pageFingerprint);
context.Progress.IncrementStepCount();
context.Error.ResetConsecutiveErrors();
```

---

## 4. 实施计划

### 4.1 实施顺序

| 阶段 | Subcontext | 字段数 | 复杂度 | 主要 Consumers |
|------|-----------|--------|--------|----------------|
| 1 | NavigationContext | 12 | 高 | DynamicChildManager, NodeStackAdapter, StepOrchestrator |
| 2 | ErrorContext | 5 | 中 | ErrorHandler, RecoveryExecutor |
| 3 | SessionContext | 4 | 低 | GlobalFSM, TraceCoordinator |
| 4 | ProgressContext | 5 | 低 | CompletionDetector, StepCounter |
| 5 | CacheContext | 2 | 低 | PageCacheManager, PageSnapshotManager |

**为什么先 Navigation？**
- 最复杂（12 字段），啃硬骨头
- 被 Traversal 层大量消费，影响面最大
- 验证 Container 模式的可行性

### 4.2 每阶段任务

每个阶段包含：

1. **创建接口** — `IXxxContext.cs`（只读 getters）
2. **创建实现** — `XxxContext.cs`（sealed class，mutable）
3. **修改 TraversalRuntimeContext** — 添加 `Xxx` 属性，委托相关属性
4. **更新 Consumers** — 改为 `context.Xxx.Field`
5. **更新测试** — 确保 603+ tests 全绿
6. **提交验证** — dotnet test + CI

### 4.3 渐进式约束

每阶段必须满足：

| 约束 | 验证方式 |
|------|---------|
| 测试全绿 | dotnet test + CI |
| 单子系统边界 | code review |
| OpenSpec change | openspec/changes/context-decomposition/tasks.md |

---

## 5. 与其他 Decisions 的关系

| Decision | 关系 |
|----------|------|
| D-7 (M-14 GlobalState) | D-III 包含此修复 — GlobalState setter 从 ITraversalContext 移除 |
| D-15 (subsystem naming) | 前置依赖 — 本设计直接使用 D-15 的 canonical 定义 |
| D-V (interface extraction) | 模式一致 — sub-context 接口提取遵循 D-V 建立的 pattern |
| D-III (ITraversalContext reform) | 后续任务 — 本拆分完成后，精简 ITraversalContext |
| D-IV (StepOrchestrator decomposition) | 后续任务 — 本拆分完成后，StepOrchestrator 可按 subsystem 重构 |

---

## 6. D-III 前瞻：ITraversalContext Reform

Context Decomposition 完成后，D-III 将精简 `ITraversalContext`：

**目标**:
- ITraversalContext 只服务于 engine（不用于 AI advisor）
- AI advisor 使用 `TraversalContextSnapshot`（已有，无需修改）
- 移除 GlobalState setter（D-7）

**D-III 不在本设计 scope**，但本设计为 D-III 铺路：
- 每个 sub-context 有明确的只读接口
- Container 模式使 ITraversalContext 可以简化为只读视图

---

## 7. 未决问题（Open Questions）

当前无未决问题。所有 6 个关键决策已确认。

---

## 8. 参考

- D-15: `docs/system/decisions/log.md D-15` — 5 subsystem canonical 定义
- D-V: `docs/system/decisions/log.md D-V` — Interface extraction pattern
- Roadmap: `docs/refactor/20-b-refactoring-roadmap-design.md` — P2 详细
- FSM Design: `docs/system/patterns/fsm-design.md` — 双 FSM 架构
- Readonly Isolation: `docs/system/patterns/readonly-isolation.md` — 集合安全模式

---

## 9. 变更历史

| 日期 | 变更 | 作者 |
|------|------|------|
| 2026-07-12 | 初始设计 — 基于 brainstorming session 6 个决策 | Claude + 用户 |
