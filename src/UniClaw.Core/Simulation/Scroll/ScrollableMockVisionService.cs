using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using Coordinate = UniClaw.Core.Domain.Models.Content.Coordinate;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 支持滚动的 Mock Vision Service。
/// 扩展 StatefulMockVisionService，添加滚动状态管理和累积模式元素可见性。
/// </summary>
public sealed class ScrollableMockVisionService : IVisionProvider
{
    private readonly StateFixture _fixture;
    private readonly ScrollDataStore _scrollDataStore;
    private readonly Dictionary<string, ScrollState> _scrollStates;
    private string _currentPageId;
    private readonly Stack<string> _navigationHistory;

    /// <summary>滚动处理器配置</summary>
    public ScrollHandlerConfig Config { get; init; }

    /// <summary>当前页面 ID</summary>
    public string CurrentPageId => _currentPageId;

    /// <summary>导航历史深度</summary>
    public int NavigationDepth => _navigationHistory.Count;

    /// <summary>
    /// 创建 ScrollableMockVisionService
    /// </summary>
    /// <param name="fixture">状态 fixture</param>
    /// <param name="scrollDataStore">滚动数据存储（可选）</param>
    /// <param name="config">滚动配置（可选）</param>
    public ScrollableMockVisionService(
        StateFixture fixture,
        ScrollDataStore? scrollDataStore = null,
        ScrollHandlerConfig? config = null)
    {
        _fixture = fixture;
        _scrollDataStore = scrollDataStore ?? ScrollDataStore.Empty();
        Config = config ?? ScrollHandlerConfig.Default();
        _currentPageId = fixture.InitialPage;
        _scrollStates = new Dictionary<string, ScrollState>();
        _navigationHistory = new Stack<string>();
    }

    // ── IVisionProvider 实现 ──────────────────────────

    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page == null)
            return Task.FromResult<PageAnalysis?>(null);

        return Task.FromResult<PageAnalysis?>(BuildPageAnalysis(page));
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
    {
        return Task.FromResult<AppEntryPoint?>(new AppEntryPoint(0.5, 0.5));
    }

    // ── IVisionProvider 滚动接口实现 ──────────────────────────────────

    /// <inheritdoc/>
    bool IVisionProvider.HasScroll() => HasScroll;

    /// <inheritdoc/>
    double IVisionProvider.GetScrollProgress() => GetScrollProgress(_currentPageId);

    /// <inheritdoc/>
    bool IVisionProvider.IsEndOfList() => IsEndOfList;

    // ── 滚动相关方法 ──────────────────────────────────

    /// <summary>获取页面当前滚动进度</summary>
    public double GetScrollProgress(string pageId)
    {
        EnsureScrollState(pageId);
        return _scrollStates[pageId].CurrentProgress;
    }

    /// <summary>获取当前页面滚动距离（0.0-1.0）</summary>
    public double GetScrollDistance()
    {
        return GetScrollProgress(_currentPageId);
    }

    /// <summary>模拟滚动操作，更新进度并记录历史</summary>
    /// <param name="delta">滚动增量（正数向下，负数向上）</param>
    /// <returns>新进度值</returns>
    public double SimulateScroll(double delta)
    {
        EnsureScrollState(_currentPageId);
        var currentState = _scrollStates[_currentPageId];
        var newState = currentState.ApplyDelta(delta);
        _scrollStates[_currentPageId] = newState;
        return newState.CurrentProgress;
    }

    /// <summary>检查页面是否有滚动数据</summary>
    public bool HasScroll => _scrollDataStore.HasScrollData(_currentPageId);

    /// <summary>检查是否到达列表末尾</summary>
    public bool IsEndOfList
    {
        get
        {
            if (!HasScroll)
                return true; // 无滚动数据 = 视为末尾

            var progress = GetScrollProgress(_currentPageId);
            var maxThreshold = _scrollDataStore.GetMaxThreshold(_currentPageId);
            var epsilon = Config.ProgressEpsilon;

            // 在 epsilon 容差内视为到达末尾
            return (maxThreshold - progress) <= epsilon;
        }
    }

    /// <summary>获取页面最大滚动阈值</summary>
    public double GetMaxThreshold(string pageId) => _scrollDataStore.GetMaxThreshold(pageId);

    /// <summary>获取页面滚动状态（测试用）</summary>
    public ScrollState? GetScrollState(string pageId)
    {
        return _scrollStates.TryGetValue(pageId, out var state) ? state : null;
    }

    // ── 仿真专用方法 ──────────────────────────────────

    /// <summary>模拟用户操作 → 查找匹配 Transition → 切换页面</summary>
    public bool SimulateAction(string elementId, string action)
    {
        var target = _fixture.ResolveTarget(_currentPageId, elementId, action);
        if (target == null) return false;
        _navigationHistory.Push(_currentPageId);
        _currentPageId = target;
        return true;
    }

    /// <summary>模拟返回键 → 弹出导航历史</summary>
    public bool NavigateBack()
    {
        if (_navigationHistory.Count == 0) return false;
        _currentPageId = _navigationHistory.Pop();
        return true;
    }

    /// <summary>查找坐标最近的元素（先搜索 fixture 元素，再搜索滚动数据可见元素）</summary>
    public PageElement? FindElementAt(double x, double y)
    {
        // 1. Search fixture page elements first
        var page = _fixture.GetPage(_currentPageId);
        if (page != null)
        {
            var fixtureElement = page.Elements.FirstOrDefault(e =>
                Math.Abs(e.X - x) < 0.05 && Math.Abs(e.Y - y) < 0.05);
            if (fixtureElement != null)
                return fixtureElement;
        }

        // 2. If HasScroll, search visible scroll data elements (cumulative + dedup)
        if (HasScroll)
        {
            var visibleElements = GetVisibleElementsFromScrollData();
            var scrollElement = visibleElements.FirstOrDefault(e =>
                Math.Abs(e.X - x) < 0.05 && Math.Abs(e.Y - y) < 0.05);
            return scrollElement;
        }

        return null;
    }

    /// <summary>获取滚动数据中的当前可见 PageElement（用于 FindElementAt 的后备搜索）</summary>
    private ImmutableArray<PageElement> GetVisibleElementsFromScrollData()
    {
        var currentProgress = GetScrollProgress(_currentPageId);
        var segments = _scrollDataStore.GetSegments(_currentPageId);

        if (segments.IsEmpty)
            return ImmutableArray<PageElement>.Empty;

        var result = new List<PageElement>();
        var elementIds = new HashSet<string>();

        foreach (var segment in segments)
        {
            if (segment.Threshold <= currentProgress)
            {
                foreach (var menuItem in segment.Elements)
                {
                    if (elementIds.Add(menuItem.Name))
                    {
                        result.Add(MapToPageElement(menuItem));
                    }
                }
            }
        }

        return result.ToImmutableArray();
    }

    /// <summary>重置到初始页面并清空导航历史</summary>
    public void Reset()
    {
        _currentPageId = _fixture.InitialPage;
        _navigationHistory.Clear();
        _scrollStates.Clear();
    }

    // ── BuildPageAnalysis 映射 ─────────────────────────

    private PageAnalysis BuildPageAnalysis(PageState page)
    {
        // 获取当前可见元素（累积模式 + 去重）
        var visibleElements = GetVisibleElements(page);

        // 分类元素
        var tabs = visibleElements.Where(e => e.Type == "tab").ToImmutableArray();
        var backButton = visibleElements.FirstOrDefault(e => e.Type == "back_button");
        var contentItems = visibleElements.Where(e => e.Type != "tab" && e.Type != "back_button");

        return new PageAnalysis(
            Level1Dir: Direction.Top,
            Level2Dir: Direction.Left,
            Level1Menus: tabs.Select(MapToMenuInfo).ToImmutableArray(),
            Level2Menus: ImmutableArray<MenuInfo>.Empty,
            CurrentPath: ImmutableArray.Create(page.PageName),
            Items: contentItems.Select(MapToMenuItem).ToImmutableArray(),
            IsPopup: false,
            BackButton: backButton != null
                ? new Coordinate(backButton.X, backButton.Y) : null,
            HasScroll: HasScroll,
            IsEndOfList: IsEndOfList
        );
    }

    /// <summary>获取当前可见元素（累积模式 + 去重）</summary>
    private ImmutableArray<PageElement> GetVisibleElements(PageState page)
    {
        if (!HasScroll)
            return page.Elements; // 无滚动数据，返回所有元素

        var currentProgress = GetScrollProgress(_currentPageId);
        var segments = _scrollDataStore.GetSegments(_currentPageId);

        if (segments.IsEmpty)
            return ImmutableArray<PageElement>.Empty;

        // 累积模式：收集所有 threshold <= progress 的分段元素
        var visibleElements = new List<PageElement>();
        var elementIds = new HashSet<string>();

        foreach (var segment in segments)
        {
            if (segment.Threshold <= currentProgress)
            {
                foreach (var menuItem in segment.Elements)
                {
                    // 去重：ID 已存在则跳过（最低 threshold 的优先）
                    if (!elementIds.Contains(menuItem.Name))
                    {
                        elementIds.Add(menuItem.Name);
                        // 将 MenuItem 转换为 PageElement
                        visibleElements.Add(MapToPageElement(menuItem));
                    }
                }
            }
        }

        return visibleElements.ToImmutableArray();
    }

    /// <summary>将 MenuItem 映射为 PageElement</summary>
    private PageElement MapToPageElement(MenuItem item)
    {
        // 反向映射 MenuItemType → PageElement 类型字符串
        var typeString = item.Type switch
        {
            MenuItemType.Button => "button",
            MenuItemType.Switch => "switch",
            MenuItemType.Toggle => "toggle",
            MenuItemType.BackButton => "back_button",
            MenuItemType.Icon => "icon",
            MenuItemType.Item => "input",
            MenuItemType.Readonly => "readonly",
            MenuItemType.Text => "text",
            MenuItemType.MenuItem => "menu_item",
            _ => "button"
        };

        return new PageElement(
            Id: item.Name,
            Type: typeString,
            Text: item.Name,
            X: item.Coordinate.X,
            Y: item.Coordinate.Y,
            ActionTarget: item.ExpectsPageChange ? "navigate" : null
        );
    }

    private static MenuInfo MapToMenuInfo(PageElement e)
        => new(Name: e.Text, Coordinate: new Coordinate(e.X, e.Y), Active: false);

    private static MenuItem MapToMenuItem(PageElement e)
    {
        var (type, action, expectsPage, expectsState) = e.Type switch
        {
            "button"      => (MenuItemType.Button,  ExpectedAction.Navigate, true,  false),
            "switch"      => (MenuItemType.Switch,  ExpectedAction.Toggle,   false, true),
            "toggle"      => (MenuItemType.Toggle,  ExpectedAction.Toggle,   false, true),
            "back_button" => (MenuItemType.BackButton, ExpectedAction.Navigate, true, false),
            "icon"        => (MenuItemType.Icon,    ExpectedAction.Action,   true,  false),
            "input"       => (MenuItemType.Item,    ExpectedAction.Action,   false, false),
            "readonly"    => (MenuItemType.Readonly, ExpectedAction.None,    false, false),
            "text"        => (MenuItemType.Text,    ExpectedAction.None,     false, false),
            _             => (MenuItemType.Button,  ExpectedAction.Action,   true,  false),
        };

        return new MenuItem(
            Name: e.Text,
            Type: type,
            Coordinate: new Domain.Models.Content.Coordinate(e.X, e.Y),
            ExpectedAction: action,
            ExpectsPageChange: expectsPage,
            ExpectsStateChange: expectsState
        );
    }

    /// <summary>确保页面有滚动状态</summary>
    private void EnsureScrollState(string pageId)
    {
        if (!_scrollStates.ContainsKey(pageId))
        {
            _scrollStates[pageId] = ScrollState.Initial();
        }
    }
}
