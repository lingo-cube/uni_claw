using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;
using AppEntryPoint = UniClaw.Core.UniBrain.AppEntryPoint;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockPageAnalyzer — 状态感知的 IPageAnalyzer 实现。
/// 维护内部页面状态机（_currentPageId + _navigationHistory），
/// 根据 StateFixture 返回对应页面的 PageAnalysis。
/// 对齐: StatefulMockVisionService.AnalyzeCurrentPageAsync + FindAppEntryAsync 部分。
/// </summary>
public sealed class MockPageAnalyzer : IPageAnalyzer
{
    private readonly StateFixture _fixture;
    private string _currentPageId;
    private readonly Stack<string> _navigationHistory = new();

    public MockPageAnalyzer(StateFixture fixture)
    {
        _fixture = fixture;
        _currentPageId = fixture.InitialPage;
    }

    // ── IPageAnalyzer 实现 ──────────────────────────

    /// <inheritdoc />
    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page == null)
            return Task.FromResult<PageAnalysis?>(null);
        return Task.FromResult<PageAnalysis?>(BuildPageAnalysis(page));
    }

    /// <inheritdoc />
    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
    {
        return Task.FromResult<AppEntryPoint?>(new AppEntryPoint(targetApp, 0.5, 0.5));
    }

    /// <inheritdoc />
    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default)
    {
        // Mock: 简单实现, 不做精确验证
        return Task.FromResult(new PageTypeVerification(
            IsMatch: false,
            Confidence: 0.0,
            ActualType: expectedType));
    }

    // ── 仿真专用方法 (从 StatefulMockVisionService 继承) ──────────

    /// <summary>模拟用户操作 → 查找匹配 Transition → 切换页面。成功返回 true。</summary>
    public bool SimulateAction(string elementId, string action)
    {
        var target = _fixture.ResolveTarget(_currentPageId, elementId, action);
        if (target == null) return false;
        _navigationHistory.Push(_currentPageId);
        _currentPageId = target;
        return true;
    }

    /// <summary>模拟返回键 → 弹出导航历史。成功返回 true，空栈返回 false。</summary>
    public bool NavigateBack()
    {
        if (_navigationHistory.Count == 0) return false;
        _currentPageId = _navigationHistory.Pop();
        return true;
    }

    /// <summary>在当前页面上查找坐标 (x,y) 最近的元素（容差 ±0.05）。</summary>
    public PageElement? FindElementAt(double x, double y)
    {
        var page = _fixture.GetPage(_currentPageId);
        if (page == null) return null;
        return page.Elements.FirstOrDefault(e =>
            Math.Abs(e.X - x) < 0.05 && Math.Abs(e.Y - y) < 0.05);
    }

    /// <summary>重置到初始页面并清空导航历史。</summary>
    public void Reset()
    {
        _currentPageId = _fixture.InitialPage;
        _navigationHistory.Clear();
    }

    /// <summary>当前页面 ID（测试断言用）</summary>
    public string CurrentPageId => _currentPageId;

    /// <summary>导航历史深度（测试断言用）</summary>
    public int NavigationDepth => _navigationHistory.Count;

    // ── BuildPageAnalysis 映射 (与 StatefulMockVisionService 相同) ──────

    private static PageAnalysis BuildPageAnalysis(PageState page)
    {
        var tabs = page.Elements.Where(e => e.Type == "tab").ToImmutableArray();
        var items = page.Elements.Where(e => e.Type != "tab").ToImmutableArray();
        var backButton = items.FirstOrDefault(e => e.Type == "back_button");
        var contentItems = items.Where(e => e.Type != "back_button");

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
            IsEndOfList: page.IsComplete
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
            Coordinate: new Coordinate(e.X, e.Y),
            ExpectedAction: action,
            ExpectsPageChange: expectsPage,
            ExpectsStateChange: expectsState
        );
    }
}
