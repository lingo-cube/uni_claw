using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using Coordinate = UniClaw.Core.Domain.Models.Content.Coordinate;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 模拟屏幕 (mock-only, sealed class, 见设计 §5): 拥有完整共享可变模拟设备状态 ——
/// 当前页、导航历史、视口页码、分页内容源、滚动行为 profile。
/// <see cref="ScrollableMockVisionService"/> (观察) 与 <see cref="ScrollableMockActionExecutor"/> (变异)
/// 在构造时注入同一个 <see cref="SimulatedScreen"/> 实例, 使一次 swipe 与随后的页面分析作用在一致状态上。
/// engine 永远看不到 <see cref="SimulatedScreen"/> (由 C-5 guard 强制)。
/// </summary>
public sealed class SimulatedScreen
{
    private readonly StateFixture _fixture;
    private readonly Dictionary<string, ScrollablePage> _scrollablePages;
    private readonly Dictionary<string, ScrollSwipeConfig> _scrollSwipeConfigs;
    private readonly ScrollBehaviorProfile _defaultProfile;
    private string _currentPageId;
    private readonly Stack<string> _navigationHistory;

    /// <summary>当前页面 ID</summary>
    public string CurrentPageId => _currentPageId;

    /// <summary>默认滚动行为 profile (可被单个页面覆盖)</summary>
    public ScrollBehaviorProfile DefaultProfile => _defaultProfile;

    /// <summary>当前页是否可滚动 (是否注册了内容源)</summary>
    public bool HasScroll => _scrollablePages.ContainsKey(_currentPageId);

    /// <summary>获取页面级滑动坐标配置，未配置返回 null</summary>
    public ScrollSwipeConfig? GetScrollSwipeConfig(string pageId)
        => _scrollSwipeConfigs.TryGetValue(pageId, out var cfg) ? cfg : null;

    /// <summary>
    /// 创建模拟屏幕。
    /// </summary>
    /// <param name="fixture">状态 fixture (页面 chrome + 导航转换)</param>
    /// <param name="profile">默认滚动行为 profile (默认 windowed/分页)</param>
    public SimulatedScreen(StateFixture fixture, ScrollBehaviorProfile? profile = null)
    {
        _fixture = fixture ?? throw new DomainValidationException(nameof(fixture), null, "fixture is required.");
        _defaultProfile = profile ?? ScrollBehaviorProfile.Paged;
        _scrollablePages = new Dictionary<string, ScrollablePage>();
        _scrollSwipeConfigs = new Dictionary<string, ScrollSwipeConfig>();
        _currentPageId = fixture.InitialPage;
        _navigationHistory = new Stack<string>();
    }

    /// <summary>
    /// 注册一个可滚动页面 (流式, 返回 this 便于配置)。该页面用 <see cref="IScrollContentSource"/> 按页生成内容。
    /// </summary>
    public SimulatedScreen WithScrollablePage(
        string pageId,
        IScrollContentSource contentSource,
        ScrollBehaviorProfile? profile = null,
        ScrollSwipeConfig? scrollSwipe = null)
    {
        if (contentSource == null)
            throw new DomainValidationException(nameof(contentSource), null, "contentSource is required.");
        _scrollablePages[pageId] = new ScrollablePage(contentSource, profile ?? _defaultProfile);
        if (scrollSwipe != null)
            _scrollSwipeConfigs[pageId] = scrollSwipe;
        return this;
    }

    /// <summary>当前页是否到达列表末尾 (视口已到末页)。不可滚动页视为已到底。</summary>
    public bool IsEndOfList()
    {
        if (!_scrollablePages.TryGetValue(_currentPageId, out var page))
            return true; // 无滚动数据 = 视为末尾
        return page.PageIndex >= LastPageIndex(page.Source);
    }

    /// <summary>当前视口滚动进度 (0.0=顶部, 1.0=底部)。无限流渐进逼近 1.0; 不可滚动返回 0.0。</summary>
    public double GetScrollProgress()
    {
        if (!_scrollablePages.TryGetValue(_currentPageId, out var page))
            return 0.0;
        int last = LastPageIndex(page.Source);
        if (last <= 0)
            return page.Source.TotalCount is null ? 0.0 : 1.0;
        if (page.Source.TotalCount is null)
            return page.PageIndex / (double)(page.PageIndex + 1); // 无限流渐进
        return page.PageIndex / (double)last;
    }

    /// <summary>
    /// 应用一次 swipe: 按方向 (sy&gt;ey=向下发现更多, sy&lt;ey=向上回顶) 与 profile 推进视口页码。
    /// </summary>
    public void ApplySwipe(double sx, double sy, double ex, double ey)
    {
        if (!_scrollablePages.TryGetValue(_currentPageId, out var page))
            return; // 不可滚动页: swipe 无效果

        bool downward = sy > ey;
        int step = Math.Max(1, (int)Math.Round(page.Profile.PagesPerSwipe * page.Profile.Jump.OvershootFactor))
                   + page.Profile.Jump.SkipPages;
        int last = LastPageIndex(page.Source);

        if (downward)
            page.PageIndex = last < 0 ? page.PageIndex + step : Math.Min(page.PageIndex + step, last);
        else
            page.PageIndex = Math.Max(page.PageIndex - Math.Max(1, page.Profile.PagesPerSwipe), 0);
    }

    /// <summary>
    /// 构造当前页的 <see cref="PageAnalysis"/>: fixture chrome + 可见内容 (按 profile Cumulative/Windowed)。
    /// </summary>
    public PageAnalysis? GetPageAnalysis()
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page == null)
            return null;

        var visible = GetVisibleElements(page);
        var tabs = visible.Where(e => e.Type == "tab").ToImmutableArray();
        var backButton = visible.FirstOrDefault(e => e.Type == "back_button");
        var contentItems = visible.Where(e => e.Type != "tab" && e.Type != "back_button");

        return new PageAnalysis(
            Level1Dir: Direction.Top,
            Level2Dir: Direction.Left,
            Level1Menus: tabs.Select(MapToMenuInfo).ToImmutableArray(),
            Level2Menus: ImmutableArray<MenuInfo>.Empty,
            CurrentPath: ImmutableArray.Create(page.PageName),
            Items: contentItems.Select(MapToMenuItem).ToImmutableArray(),
            IsPopup: false,
            BackButton: backButton != null ? new Coordinate(backButton.X, backButton.Y) : null,
            HasScroll: HasScroll,
            IsEndOfList: IsEndOfList());
    }

    /// <summary>模拟用户操作 → 查找匹配 fixture Transition → 切换页面 (返回是否切换)。</summary>
    public bool SimulateAction(string elementId, string action)
    {
        var target = _fixture.ResolveTarget(_currentPageId, elementId, action);
        if (target == null) return false;
        _navigationHistory.Push(_currentPageId);
        _currentPageId = target;
        return true;
    }

    /// <summary>模拟返回键 → 弹出导航历史。</summary>
    public bool NavigateBack()
    {
        if (_navigationHistory.Count == 0) return false;
        _currentPageId = _navigationHistory.Pop();
        return true;
    }

    /// <summary>查找坐标最近的元素 (先 fixture chrome, 再可见滚动内容)。</summary>
    public PageElement? FindElementAt(double x, double y)
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page != null)
        {
            var fixtureElement = page.Elements.FirstOrDefault(e =>
                Math.Abs(e.X - x) < 0.05 && Math.Abs(e.Y - y) < 0.05);
            if (fixtureElement != null)
                return fixtureElement;
        }

        if (HasScroll)
        {
            var scrollElement = GetVisibleElements(page!).FirstOrDefault(e =>
                Math.Abs(e.X - x) < 0.05 && Math.Abs(e.Y - y) < 0.05);
            return scrollElement;
        }

        return null;
    }

    /// <summary>重置到初始页面并清空导航历史、视口与滑动坐标配置。</summary>
    public void Reset()
    {
        _currentPageId = _fixture.InitialPage;
        _navigationHistory.Clear();
        _scrollSwipeConfigs.Clear();
        foreach (var p in _scrollablePages.Values)
            p.PageIndex = 0;
    }

    // ── 内部 ──────────────────────────────────────────────

    private ImmutableArray<PageElement> GetVisibleElements(PageState page)
    {
        // chrome (fixture 页面元素: tabs/back_button/switch 等) 始终可见
        var result = new List<PageElement>(page.Elements);
        var seenIds = new HashSet<string>(page.Elements.Select(e => e.Id));

        // 不可滚动页: 仅 chrome
        if (!_scrollablePages.TryGetValue(_currentPageId, out var scrollable))
            return result.ToImmutableArray();

        // 可滚动页: chrome + 按可见性模型可见的内容项
        var source = scrollable.Source;
        int fromPage = scrollable.Profile.Cumulative ? 0 : scrollable.PageIndex;
        int toPage = scrollable.PageIndex;

        for (int p = fromPage; p <= toPage; p++)
        {
            foreach (var item in source.GetPage(p))
            {
                if (seenIds.Add(item.Name)) // 累积模式去重: 最低页优先
                    result.Add(MapToPageElement(item));
            }
        }

        return result.ToImmutableArray();
    }

    private static int LastPageIndex(IScrollContentSource source)
    {
        // null TotalCount = 无限流 → 返回 -1 表示无上界
        if (source.TotalCount is not { } total || total <= 0)
            return source.TotalCount is null ? -1 : 0;
        int last = (total - 1) / source.PageSize;
        return last < 0 ? 0 : last;
    }

    private static PageElement MapToPageElement(MockItem item)
    {
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
        return new PageElement(Id: item.Name, Type: typeString, Text: item.Name, X: item.X, Y: item.Y);
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
            Coordinate: new Coordinate(e.X, e.Y),
            ExpectedAction: action,
            ExpectsPageChange: expectsPage,
            ExpectsStateChange: expectsState);
    }

    /// <summary>单个可滚动页面的运行时视口状态。</summary>
    private sealed class ScrollablePage
    {
        public IScrollContentSource Source { get; }
        public ScrollBehaviorProfile Profile { get; }
        public int PageIndex { get; set; }

        public ScrollablePage(IScrollContentSource source, ScrollBehaviorProfile profile)
        {
            Source = source;
            Profile = profile;
            PageIndex = 0;
        }
    }
}
