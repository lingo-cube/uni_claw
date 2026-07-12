using System.Collections.Immutable;
using System.Collections.ObjectModel;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.StateMachine.Navigation;
using UniClaw.Core.StateMachine.Error;
using UniClaw.Core.StateMachine.Session;
using UniClaw.Core.StateMachine.Progress;
using UniClaw.Core.StateMachine.Cache;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// 遍历上下文快照 (D-2: sealed record class) — 给 AI advisor 的不可变只读快照。
/// 与源 TraversalRuntimeContext 完全隔离：后续修改不影响已创建的快照。
/// </summary>
public sealed record class TraversalContextSnapshot
{
    /// <summary>栈中节点 ID 序列（快照时刻）</summary>
    public ImmutableArray<string> NodeIds { get; init; }

    /// <summary>当前路径（快照时刻）</summary>
    public ImmutableArray<string> CurrentPath { get; init; }

    /// <summary>已访问页面（快照时刻）</summary>
    public ImmutableHashSet<string> VisitedPages { get; init; }

    /// <summary>已访问节点（快照时刻）</summary>
    public ImmutableHashSet<string> VisitedNodes { get; init; }

    /// <summary>最大深度</summary>
    public int MaxDepth { get; init; }

    /// <summary>步骤计数（快照时刻）</summary>
    public int StepCount { get; init; }

    /// <summary>动作历史（快照时刻，最多保留 5 条）</summary>
    public ImmutableArray<ActionRecord> ActionHistory { get; init; }

    /// <summary>失败节点映射（快照时刻）</summary>
    public ImmutableDictionary<string, ErrorRecord> FailedNodes { get; init; }

    /// <summary>
    /// 构造快照
    /// </summary>
    public TraversalContextSnapshot(
        ImmutableArray<string> NodeIds,
        ImmutableArray<string> CurrentPath,
        ImmutableHashSet<string> VisitedPages,
        ImmutableHashSet<string> VisitedNodes,
        int MaxDepth,
        int StepCount,
        ImmutableArray<ActionRecord> ActionHistory,
        ImmutableDictionary<string, ErrorRecord> FailedNodes)
    {
        this.NodeIds = NodeIds.IsDefault ? ImmutableArray<string>.Empty : NodeIds;
        this.CurrentPath = CurrentPath.IsDefault ? ImmutableArray<string>.Empty : CurrentPath;
        this.VisitedPages = VisitedPages ?? ImmutableHashSet<string>.Empty;
        this.VisitedNodes = VisitedNodes ?? ImmutableHashSet<string>.Empty;
        this.MaxDepth = MaxDepth;
        this.StepCount = StepCount;
        this.ActionHistory = ActionHistory.IsDefault ? ImmutableArray<ActionRecord>.Empty : ActionHistory;
        this.FailedNodes = FailedNodes ?? ImmutableDictionary<string, ErrorRecord>.Empty;
    }
}

/// <summary>
/// 遍历运行时上下文 (D-2: sealed class, NOT record)。
/// 26 个可变字段对齐 Python src/trace/context.py。
/// 引擎每步直接赋值更新，无 with 表达式复制开销。
/// 实现 ITraversalContext 只读接口，暴露强类型只读集合视图。
/// Phase 2 Context Decomposition: Container 模式，持有 5 个 sub-contexts。
/// </summary>
public sealed class TraversalRuntimeContext : ITraversalContext
{
    // === 5 Sub-Contexts (Container Pattern) ===
    private readonly NavigationContext _navigation;
    private readonly ErrorContext _error;
    private readonly SessionContext _session;
    private readonly ProgressContext _progress;
    private readonly CacheContext _cache;

    /// <summary>导航子上下文 (只读暴露)</summary>
    public NavigationContext Navigation => _navigation;

    /// <summary>错误子上下文 (只读暴露)</summary>
    public ErrorContext Error => _error;

    /// <summary>会话子上下文 (只读暴露)</summary>
    public SessionContext Session => _session;

    /// <summary>进度子上下文 (只读暴露)</summary>
    public ProgressContext Progress => _progress;

    /// <summary>缓存子上下文 (只读暴露)</summary>
    public CacheContext Cache => _cache;

    /// <summary>构造 TraversalRuntimeContext</summary>
    public TraversalRuntimeContext(
        string traceId,
        int maxDepth = 10,
        NodeStack? nodeStack = null)
    {
        // Create NavigationContext with traceId, maxDepth, nodeStack
        _navigation = new NavigationContext(traceId, maxDepth, nodeStack);
        // Create ErrorContext
        _error = new ErrorContext();
        // Create SessionContext with traceId
        _session = new SessionContext(traceId);
        // Create ProgressContext with maxDepth
        _progress = new ProgressContext(maxDepth);
        // Create CacheContext
        _cache = new CacheContext();
    }

    // --- ITraversalContext readonly interface implementation (D-4) ---
    /// <inheritdoc />
    /// <remarks>委托到 NavigationContext</remarks>
    public INodeStack NodeStack => _navigation.NodeStack;

    /// <inheritdoc />
    /// <remarks>委托到 NavigationContext</remarks>
    public IReadOnlyList<string> CurrentPath => _navigation.CurrentPath;

    /// <inheritdoc />
    /// <remarks>委托到 NavigationContext</remarks>
    public IReadOnlySet<string> VisitedPages => _navigation.VisitedPages;

    /// <inheritdoc />
    /// <remarks>委托到 NavigationContext</remarks>
    public IReadOnlySet<string> VisitedNodes => _navigation.VisitedNodes;

    /// <inheritdoc />
    /// <remarks>委托到 NavigationContext</remarks>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren => _navigation.VisitedChildren;

    /// <inheritdoc />
    /// <remarks>委托到 NavigationContext（只读）</remarks>
    public ITraversalNode? CurrentFrame => _navigation.CurrentFrame;

    /// <inheritdoc />
    /// <remarks>委托到 ProgressContext</remarks>
    public int StepCount => _progress.StepCount;

    /// <inheritdoc />
    /// <remarks>委托到 SessionContext（只读）</remarks>
    public GlobalState GlobalState => _session.GlobalState;

    /// <inheritdoc />
    /// <remarks>委托到 ErrorContext（只读）</remarks>
    public Exception? LastError => _error.LastError;

    // --- Internal mutable field accessors (engine-only) ---
    /// <summary>追踪ID (委托到 SessionContext)</summary>
    public string TraceId => _session.TraceId;
    /// <summary>缓存是否有效 (委托到 CacheContext)</summary>
    public bool CacheValid => _cache.CacheValid;
    /// <summary>连续错误数 (委托到 ErrorContext)</summary>
    public int ConsecutiveErrors => _error.ConsecutiveErrors;

    // === Navigation Context Delegates (临时，用于渐进式迁移) ===
    /// <summary>当前页面分析 (委托到 NavigationContext)</summary>
    public PageAnalysis? CurrentPageAnalysis => _navigation.CurrentPageAnalysis;
    /// <summary>当前指纹 (委托到 NavigationContext)</summary>
    public VisitFingerprint? CurrentFingerprint => _navigation.CurrentFingerprint;
    /// <summary>已访问一级菜单 (委托到 NavigationContext)</summary>
    public HashSet<string> VisitedLevel1Menus => _navigation.VisitedLevel1Menus as HashSet<string>;
    /// <summary>已访问二级菜单 (委托到 NavigationContext)</summary>
    public HashSet<string> VisitedLevel2Menus => _navigation.VisitedLevel2Menus as HashSet<string>;
    /// <summary>页面树 (委托到 NavigationContext)</summary>
    public ContentNode? PageTree => _navigation.PageTree;
    /// <summary>最大深度 (委托到 ProgressContext)</summary>
    public int MaxDepth => _progress.MaxDepth;
    /// <summary>重试计数 (委托到 ErrorContext)</summary>
    public int RetryCount => _error.RetryCount;
    /// <summary>完成策略 (委托到 ProgressContext)</summary>
    public CompletionPolicy? CompletionPolicy => _progress.CompletionPolicy;
    /// <summary>设备经验 (委托到 SessionContext)</summary>
    public string? DeviceExperience => _session.DeviceExperience;
    /// <summary>异常链 (委托到 ErrorContext)</summary>
    public List<Exception>? ExceptionChain => _error.ExceptionChain as List<Exception>;
    /// <summary>AI provider (委托到 SessionContext)</summary>
    public string? AIProvider => _session.AIProvider;
    /// <summary>页面缓存 (委托到 CacheContext)</summary>
    public Dictionary<string, object> PageCache => _cache.GetPageCacheInternal();
    /// <summary>等待动作后毫秒 (委托到 ProgressContext)</summary>
    public int WaitAfterActionMs => _progress.WaitAfterActionMs;
    /// <summary>动作历史 (最多 5 条) (委托到 ProgressContext)</summary>
    public List<ActionRecord> ActionHistoryInternal => _progress.ActionHistory as List<ActionRecord>;

    // --- Engine-internal mutation methods (NOT on ITraversalContext) ---
    /// <summary>设置当前帧（节点）</summary>
    public void SetCurrentFrame(ITraversalNode? value) =>
        _navigation.CurrentFrame = value;

    /// <summary>设置全局状态</summary>
    public void SetGlobalState(GlobalState value) =>
        _session.GlobalState = value;

    /// <summary>设置最后的错误</summary>
    public void SetLastError(Exception? value) =>
        _error.LastError = value;

    /// <summary>追加路径 (委托到 NavigationContext)</summary>
    public void AppendPath(string page) => _navigation.AppendPath(page);
    /// <summary>弹出路径末尾 (委托到 NavigationContext)</summary>
    public void PopPath() => _navigation.PopPath();
    /// <summary>标记页面已访问 (委托到 NavigationContext)</summary>
    public void MarkVisited(string page) => _navigation.MarkVisited(page);
    /// <summary>标记节点已访问 (委托到 NavigationContext)</summary>
    public void MarkNodeVisited(string nodeId) => _navigation.MarkNodeVisited(nodeId);
    /// <summary>添加子节点访问记录 (委托到 NavigationContext)</summary>
    public void AddVisitedChild(string parentId, string childId) => _navigation.AddVisitedChild(parentId, childId);
    /// <summary>重置节点的已访问子节点集合 (委托到 NavigationContext，用于滚动)</summary>
    public void ResetVisitedChildren(string parentId) => _navigation.ResetVisitedChildren(parentId);
    /// <summary>更新节点的已访问子节点集合 (委托到 NavigationContext，用于滚动选择性重置)</summary>
    public void UpdateVisitedChildren(string parentId, System.Collections.Immutable.IImmutableSet<string> newVisitedSet) => _navigation.UpdateVisitedChildren(parentId, newVisitedSet);
    /// <summary>递增步骤计数 (委托到 ProgressContext)</summary>
    public void IncrementStepCount() => _progress.IncrementStepCount();
    /// <summary>递增重试计数 (委托到 ErrorContext)</summary>
    public void IncrementRetryCount() => _error.IncrementRetryCount();
    /// <summary>递增连续错误 (委托到 ErrorContext)</summary>
    public void IncrementConsecutiveErrors() => _error.IncrementConsecutiveErrors();
    /// <summary>重置连续错误为 0 (委托到 ErrorContext)</summary>
    public void ResetConsecutiveErrors() => _error.ResetConsecutiveErrors();

    // --- Mutable field setters (engine-only) ---
    /// <summary>设置当前页面分析 (委托到 NavigationContext)</summary>
    public void SetCurrentPageAnalysis(PageAnalysis? value) => _navigation.SetCurrentPageAnalysis(value);
    /// <summary>设置当前指纹 (委托到 NavigationContext)</summary>
    public void SetCurrentFingerprint(VisitFingerprint? value) => _navigation.SetCurrentFingerprint(value);
    /// <summary>设置页面树 (委托到 NavigationContext)</summary>
    public void SetPageTree(ContentNode? value) => _navigation.SetPageTree(value);
    /// <summary>设置缓存有效 (委托到 CacheContext)</summary>
    public void SetCacheValid(bool value) => _cache.SetCacheValid(value);
    /// <summary>设置完成策略 (委托到 ProgressContext)</summary>
    public void SetCompletionPolicy(CompletionPolicy? value) => _progress.SetCompletionPolicy(value);
    /// <summary>设置设备经验 (委托到 SessionContext)</summary>
    public void SetDeviceExperience(string? value) => _session.DeviceExperience = value;
    /// <summary>设置异常链 (委托到 ErrorContext)</summary>
    public void SetExceptionChain(List<Exception>? value) => _error.SetExceptionChain(value);
    /// <summary>设置 AI provider (委托到 SessionContext)</summary>
    public void SetAIProvider(string? value) => _session.AIProvider = value;
    /// <summary>设置等待时间 (委托到 ProgressContext)</summary>
    public void SetWaitAfterActionMs(int value) => _progress.SetWaitAfterActionMs(value);
    /// <summary>添加动作历史 (委托到 ProgressContext, 保持最多 5 条)</summary>
    public void AddActionHistory(ActionRecord record) => _progress.AddActionHistory(record);
    /// <summary>添加失败节点 (委托到 ErrorContext)</summary>
    public void AddFailedNode(string nodeId, ErrorRecord error) => _error.AddFailedNode(nodeId, error);

    // --- CreateReadOnlySnapshot (D-2: 创建不可变快照给 AI advisor) ---
    /// <summary>
    /// 创建只读快照 — 与源 TraversalRuntimeContext 完全隔离。
    /// 后续对源上下文的修改（MarkVisited, IncrementStepCount 等）不影响已创建的快照。
    /// </summary>
    public TraversalContextSnapshot CreateReadOnlySnapshot()
    {
        // Capture NodeStack IDs at snapshot time (not a reference to mutable NodeStack)
        var nodeIds = ImmutableArray.CreateBuilder<string>();
        for (int i = 0; i < _navigation.NodeStack.Depth; i++)
        {
            var frame = _navigation.NodeStack.Peek(i);
            if (frame != null)
                nodeIds.Add(frame.NodeId);
        }

        return new TraversalContextSnapshot(
            NodeIds: nodeIds.ToImmutable(),
            CurrentPath: ImmutableArray.CreateRange(_navigation.CurrentPath),
            VisitedPages: _navigation.VisitedPages.ToImmutableHashSet(),
            VisitedNodes: _navigation.VisitedNodes.ToImmutableHashSet(),
            MaxDepth: _progress.MaxDepth,
            StepCount: _progress.StepCount,
            ActionHistory: ImmutableArray.CreateRange(_progress.ActionHistory),
            FailedNodes: _error.FailedNodes.ToImmutableDictionary()
        );
    }
}
