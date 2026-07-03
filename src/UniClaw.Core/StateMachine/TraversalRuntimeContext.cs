using System.Collections.Immutable;
using System.Collections.ObjectModel;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

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
/// </summary>
public sealed class TraversalRuntimeContext : ITraversalContext
{
    // --- 26 mutable fields (对齐 Python context.py) ---
    private string _traceId;
    private readonly NodeStack _nodeStack;
    private readonly List<string> _currentPath;
    private PageAnalysis? _currentPageAnalysis;
    private VisitFingerprint? _currentFingerprint;
    private bool _cacheValid;
    private readonly HashSet<string> _visitedPages;
    private readonly HashSet<string> _visitedLevel1Menus;
    private readonly HashSet<string> _visitedLevel2Menus;
    private readonly HashSet<string> _visitedNodes;
    private readonly Dictionary<string, HashSet<string>> _visitedChildren;
    private ContentNode? _pageTree;
    private readonly List<ActionRecord> _actionHistory; // keep last 5
    private readonly Dictionary<string, ErrorRecord> _failedNodes;
    private int _consecutiveErrors;
    private int _maxDepth;
    private int _stepCount;
    private int _retryCount;
    private CompletionPolicy? _completionPolicy;
    private string? _deviceExperience;
    private GlobalState _globalState;
    private Exception? _lastError;
    private List<Exception>? _exceptionChain;
    private string? _aiProvider;
    private readonly Dictionary<string, object> _pageCache;
    private int _waitAfterActionMs;

    // --- Reserved interface positions (Phase 3) ---
    // TODO: Phase 3 — 实现 IScrollHandler 接口和 scroll 交互逻辑
    private object? _scrollHandler;

    // TODO: Phase 3 — 实现 IPageSnapshot 接口和页面快照管理
    private object? _currentSnapshot;

    /// <summary>构造 TraversalRuntimeContext</summary>
    public TraversalRuntimeContext(
        string traceId,
        int maxDepth = 10,
        NodeStack? nodeStack = null)
    {
        _traceId = traceId;
        _maxDepth = maxDepth;
        _nodeStack = nodeStack ?? new NodeStack(maxDepth);
        _currentPath = new List<string>();
        _visitedPages = new HashSet<string>();
        _visitedLevel1Menus = new HashSet<string>();
        _visitedLevel2Menus = new HashSet<string>();
        _visitedNodes = new HashSet<string>();
        _visitedChildren = new Dictionary<string, HashSet<string>>();
        _actionHistory = new List<ActionRecord>(5);
        _failedNodes = new Dictionary<string, ErrorRecord>();
        _consecutiveErrors = 0;
        _stepCount = 0;
        _retryCount = 0;
        _pageCache = new Dictionary<string, object>();
        _waitAfterActionMs = 300;
        _completionPolicy = null;
        _cacheValid = false;
        _globalState = GlobalState.Idle;
        _pageTree = null;
        _exceptionChain = null;
        _deviceExperience = null;
        _aiProvider = null;
        _currentPageAnalysis = null;
        _currentFingerprint = null;
        _lastError = null;
    }

    // --- ITraversalContext readonly interface implementation (D-4) ---
    /// <inheritdoc />
    public INodeStack NodeStack => _nodeStack;

    /// <inheritdoc />
    /// <remarks>CurrentPath 通过 .AsReadOnly() 包装返回，防止 cast-back 修改</remarks>
    public IReadOnlyList<string> CurrentPath => _currentPath.AsReadOnly();

    /// <inheritdoc />
    /// <remarks>VisitedPages 直接暴露 HashSet，IReadOnlySet 不暴露 Add/Remove</remarks>
    public IReadOnlySet<string> VisitedPages => _visitedPages;

    /// <inheritdoc />
    /// <remarks>VisitedNodes 直接暴露 HashSet，IReadOnlySet 不暴露 Add/Remove</remarks>
    public IReadOnlySet<string> VisitedNodes => _visitedNodes;

    /// <inheritdoc />
    /// <remarks>VisitedChildren 包装嵌套集合，确保值类型为 IReadOnlySet</remarks>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren => EnsureVisitedChildrenReadOnly();
    private ReadOnlyDictionary<string, IReadOnlySet<string>>? _visitedChildrenReadOnly;
    private ReadOnlyDictionary<string, IReadOnlySet<string>> GetVisitedChildrenReadOnly()
    {
        var dict = new Dictionary<string, IReadOnlySet<string>>();
        foreach (var (key, set) in _visitedChildren)
            dict[key] = set; // HashSet<string> 可以直接作为 IReadOnlySet<string>（安全：不暴露突变方法）
        return new ReadOnlyDictionary<string, IReadOnlySet<string>>(dict);
    }

    /// <inheritdoc />
    public ITraversalNode? CurrentFrame { get; set; }

    /// <inheritdoc />
    public int StepCount => _stepCount;

    /// <inheritdoc />
    public GlobalState GlobalState { get => _globalState; set => _globalState = value; }

    /// <inheritdoc />
    public Exception? LastError { get => _lastError; set => _lastError = value; }

    // --- Internal mutable field accessors (engine-only) ---
    /// <summary>追踪ID</summary>
    public string TraceId => _traceId;
    /// <summary>当前页面分析</summary>
    public PageAnalysis? CurrentPageAnalysis => _currentPageAnalysis;
    /// <summary>当前指纹</summary>
    public VisitFingerprint? CurrentFingerprint => _currentFingerprint;
    /// <summary>缓存是否有效</summary>
    public bool CacheValid => _cacheValid;
    /// <summary>已访问一级菜单</summary>
    public HashSet<string> VisitedLevel1Menus => _visitedLevel1Menus;
    /// <summary>已访问二级菜单</summary>
    public HashSet<string> VisitedLevel2Menus => _visitedLevel2Menus;
    /// <summary>页面树</summary>
    public ContentNode? PageTree => _pageTree;
    /// <summary>连续错误数</summary>
    public int ConsecutiveErrors => _consecutiveErrors;
    /// <summary>最大深度</summary>
    public int MaxDepth => _maxDepth;
    /// <summary>重试计数</summary>
    public int RetryCount => _retryCount;
    /// <summary>完成策略</summary>
    public CompletionPolicy? CompletionPolicy => _completionPolicy;
    /// <summary>设备经验</summary>
    public string? DeviceExperience => _deviceExperience;
    /// <summary>异常链</summary>
    public List<Exception>? ExceptionChain => _exceptionChain;
    /// <summary>AI provider</summary>
    public string? AIProvider => _aiProvider;
    /// <summary>页面缓存</summary>
    public Dictionary<string, object> PageCache => _pageCache;
    /// <summary>等待动作后毫秒</summary>
    public int WaitAfterActionMs => _waitAfterActionMs;
    /// <summary>动作历史 (最多 5 条)</summary>
    public List<ActionRecord> ActionHistoryInternal => _actionHistory;

    // --- Engine-internal mutation methods (NOT on ITraversalContext) ---
    /// <summary>追加路径</summary>
    public void AppendPath(string page) => _currentPath.Add(page);
    /// <summary>弹出路径末尾</summary>
    public void PopPath()
    {
        if (_currentPath.Count > 0)
            _currentPath.RemoveAt(_currentPath.Count - 1);
    }
    /// <summary>标记页面已访问</summary>
    public void MarkVisited(string page) => _visitedPages.Add(page);
    /// <summary>标记节点已访问</summary>
    public void MarkNodeVisited(string nodeId) => _visitedNodes.Add(nodeId);
    /// <summary>递增步骤计数</summary>
    public void IncrementStepCount() => _stepCount++;
    /// <summary>递增重试计数</summary>
    public void IncrementRetryCount() => _retryCount++;
    /// <summary>递增连续错误</summary>
    public void IncrementConsecutiveErrors() => _consecutiveErrors++;
    /// <summary>重置连续错误为 0</summary>
    public void ResetConsecutiveErrors() => _consecutiveErrors = 0;

    // --- Mutable field setters (engine-only) ---
    /// <summary>设置当前页面分析</summary>
    public void SetCurrentPageAnalysis(PageAnalysis? value) => _currentPageAnalysis = value;
    /// <summary>设置当前指纹</summary>
    public void SetCurrentFingerprint(VisitFingerprint? value) => _currentFingerprint = value;
    /// <summary>设置缓存有效</summary>
    public void SetCacheValid(bool value) => _cacheValid = value;
    /// <summary>设置页面树</summary>
    public void SetPageTree(ContentNode? value) => _pageTree = value;
    /// <summary>设置完成策略</summary>
    public void SetCompletionPolicy(CompletionPolicy? value) => _completionPolicy = value;
    /// <summary>设置设备经验</summary>
    public void SetDeviceExperience(string? value) => _deviceExperience = value;
    /// <summary>设置异常链</summary>
    public void SetExceptionChain(List<Exception>? value) => _exceptionChain = value;
    /// <summary>设置 AI provider</summary>
    public void SetAIProvider(string? value) => _aiProvider = value;
    /// <summary>设置等待时间</summary>
    public void SetWaitAfterActionMs(int value) => _waitAfterActionMs = value;
    /// <summary>添加子节点访问记录</summary>
    public void AddVisitedChild(string parentId, string childId)
    {
        if (!_visitedChildren.TryGetValue(parentId, out var set))
        {
            set = new HashSet<string>();
            _visitedChildren[parentId] = set;
        }
        set.Add(childId);
        // Invalidate the read-only wrapper since _visitedChildren changed
        _visitedChildrenReadOnly = null;
    }
    /// <summary>添加动作历史 (保持最多 5 条)</summary>
    public void AddActionHistory(ActionRecord record)
    {
        _actionHistory.Add(record);
        if (_actionHistory.Count > 5)
            _actionHistory.RemoveAt(0);
    }
    /// <summary>添加失败节点</summary>
    public void AddFailedNode(string nodeId, ErrorRecord error) => _failedNodes[nodeId] = error;

    // --- VisitedChildren read-only wrapper (lazy rebuild) ---
    private ReadOnlyDictionary<string, IReadOnlySet<string>> EnsureVisitedChildrenReadOnly()
    {
        if (_visitedChildrenReadOnly == null)
            _visitedChildrenReadOnly = GetVisitedChildrenReadOnly();
        return _visitedChildrenReadOnly;
    }

    // --- CreateReadOnlySnapshot (D-2: 创建不可变快照给 AI advisor) ---
    /// <summary>
    /// 创建只读快照 — 与源 TraversalRuntimeContext 完全隔离。
    /// 后续对源上下文的修改（MarkVisited, IncrementStepCount 等）不影响已创建的快照。
    /// </summary>
    public TraversalContextSnapshot CreateReadOnlySnapshot()
    {
        // Capture NodeStack IDs at snapshot time (not a reference to mutable NodeStack)
        var nodeIds = ImmutableArray.CreateBuilder<string>();
        for (int i = 0; i < _nodeStack.Depth; i++)
        {
            var frame = _nodeStack.Peek(i);
            if (frame != null)
                nodeIds.Add(frame.NodeId);
        }

        return new TraversalContextSnapshot(
            NodeIds: nodeIds.ToImmutable(),
            CurrentPath: ImmutableArray.CreateRange(_currentPath),
            VisitedPages: _visitedPages.ToImmutableHashSet(),
            VisitedNodes: _visitedNodes.ToImmutableHashSet(),
            MaxDepth: _maxDepth,
            StepCount: _stepCount,
            ActionHistory: ImmutableArray.CreateRange(_actionHistory),
            FailedNodes: _failedNodes.ToImmutableDictionary()
        );
    }
}
