using System.Collections.ObjectModel;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.StateMachine.Navigation;

/// <summary>
/// Navigation context — DFS traversal state.
/// 12 个字段封装所有遍历相关状态：节点栈、路径、页面身份、已访问追踪。
/// </summary>
public sealed class NavigationContext : INavigationContext
{
    // --- 12 private fields ---
    private readonly NodeStack _nodeStack;
    private readonly List<string> _currentPath;
    private PageAnalysis? _currentPageAnalysis;
    private VisitFingerprint? _currentFingerprint;
    private readonly HashSet<string> _visitedPages;
    private readonly HashSet<string> _visitedNodes;
    private readonly Dictionary<string, HashSet<string>> _visitedChildren;
    private readonly HashSet<string> _visitedLevel1Menus;
    private readonly HashSet<string> _visitedLevel2Menus;
    private ContentNode? _pageTree;
    private ITraversalNode? _currentFrame;
    private ReadOnlyDictionary<string, IReadOnlySet<string>>? _visitedChildrenReadOnly;

    /// <summary>构造 NavigationContext</summary>
    public NavigationContext(string traceId, int maxDepth, NodeStack? nodeStack = null)
    {
        _nodeStack = nodeStack ?? new NodeStack(maxDepth);
        _currentPath = new List<string>();
        _visitedPages = new HashSet<string>();
        _visitedNodes = new HashSet<string>();
        _visitedChildren = new Dictionary<string, HashSet<string>>();
        _visitedLevel1Menus = new HashSet<string>();
        _visitedLevel2Menus = new HashSet<string>();
        _pageTree = null;
        _currentFrame = null;
        _currentPageAnalysis = null;
        _currentFingerprint = null;
    }

    // --- ReadOnlySetWrapper: wraps HashSet<string> as IReadOnlySet<string> without exposing reference ---
    private sealed class ReadOnlySetWrapper : IReadOnlySet<string>
    {
        private readonly HashSet<string> _set;

        public ReadOnlySetWrapper(HashSet<string> set) => _set = set;

        public int Count => _set.Count;
        public bool Contains(string item) => _set.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<string> other) => _set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<string> other) => _set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<string> other) => _set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<string> other) => _set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<string> other) => _set.Overlaps(other);
        public bool SetEquals(IEnumerable<string> other) => _set.SetEquals(other);
        public IEnumerator<string> GetEnumerator() => _set.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _set.GetEnumerator();
    }

    // --- INavigationContext implementation ---

    /// <inheritdoc />
    public INodeStack NodeStack => _nodeStack;

    /// <inheritdoc />
    public IReadOnlyList<string> CurrentPath => _currentPath.AsReadOnly();

    /// <inheritdoc />
    public PageAnalysis? CurrentPageAnalysis => _currentPageAnalysis;

    /// <inheritdoc />
    public VisitFingerprint? CurrentFingerprint => _currentFingerprint;

    /// <inheritdoc />
    public IReadOnlySet<string> VisitedPages => _visitedPages;

    /// <inheritdoc />
    public IReadOnlySet<string> VisitedNodes => _visitedNodes;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren => EnsureVisitedChildrenReadOnly();

    /// <inheritdoc />
    public IReadOnlySet<string> VisitedLevel1Menus => _visitedLevel1Menus;

    /// <inheritdoc />
    public IReadOnlySet<string> VisitedLevel2Menus => _visitedLevel2Menus;

    /// <inheritdoc />
    public ContentNode? PageTree => _pageTree;

    /// <inheritdoc />
    public ITraversalNode? CurrentFrame
    {
        get => _currentFrame;
        set => _currentFrame = value;
    }

    // --- Mutation methods (engine-only) ---

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

    /// <summary>重置节点的已访问子节点集合（用于滚动后重新发现元素）</summary>
    public void ResetVisitedChildren(string parentId)
    {
        if (_visitedChildren.ContainsKey(parentId))
        {
            _visitedChildren[parentId].Clear();
            // Invalidate the read-only wrapper since _visitedChildren changed
            _visitedChildrenReadOnly = null;
        }
    }

    /// <summary>更新节点的已访问子节点集合（用于滚动后选择性重置）</summary>
    /// <remarks>
    /// 用于滚动场景：仅重置滚动前存在的元素，保留滚动后才标记访问的元素。
    /// 这样可以避免重新访问已经在新发现的元素。
    /// </remarks>
    public void UpdateVisitedChildren(string parentId, System.Collections.Immutable.IImmutableSet<string> newVisitedSet)
    {
        // Remove the old entry entirely
        if (_visitedChildren.ContainsKey(parentId))
        {
            _visitedChildren.Remove(parentId);
        }

        // Add new visited entries from the new set
        foreach (var childId in newVisitedSet)
        {
            AddVisitedChild(parentId, childId);
        }

        // Invalidate the read-only wrapper since _visitedChildren changed
        _visitedChildrenReadOnly = null;
    }

    /// <summary>设置当前页面分析</summary>
    public void SetCurrentPageAnalysis(PageAnalysis? value) => _currentPageAnalysis = value;

    /// <summary>设置当前指纹</summary>
    public void SetCurrentFingerprint(VisitFingerprint? value) => _currentFingerprint = value;

    /// <summary>设置页面树</summary>
    public void SetPageTree(ContentNode? value) => _pageTree = value;

    // --- VisitedChildren read-only wrapper (lazy rebuild) ---
    private ReadOnlyDictionary<string, IReadOnlySet<string>> EnsureVisitedChildrenReadOnly()
    {
        if (_visitedChildrenReadOnly == null)
        {
            _visitedChildrenReadOnly = GetVisitedChildrenReadOnly();
        }
        return _visitedChildrenReadOnly;
    }

    private ReadOnlyDictionary<string, IReadOnlySet<string>> GetVisitedChildrenReadOnly()
    {
        var dict = new Dictionary<string, IReadOnlySet<string>>();
        foreach (var (key, set) in _visitedChildren)
            dict[key] = new ReadOnlySetWrapper(set);
        return new ReadOnlyDictionary<string, IReadOnlySet<string>>(dict);
    }
}
