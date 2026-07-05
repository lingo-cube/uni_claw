using System.Collections.Immutable;
using System.Text.Json;

namespace UniClaw.Core.Simulation;

/// <summary>仿真页面/跳转规则的顶层容器。</summary>
public sealed record class StateFixture
{
    /// <summary>初始页面 ID</summary>
    public string InitialPage { get; }

    /// <summary>页面 ID → PageState 映射</summary>
    public ImmutableDictionary<string, PageState> Pages { get; }

    /// <summary>跳转规则列表</summary>
    public ImmutableArray<PageTransition> Transitions { get; }

    /// <summary>运行时索引: (fromPage, trigger, action) → toPage</summary>
    private readonly Dictionary<(string, string, string), string> _transitionIndex;

    public StateFixture(
        string InitialPage,
        ImmutableDictionary<string, PageState> Pages,
        ImmutableArray<PageTransition> Transitions)
    {
        this.InitialPage = InitialPage;
        this.Pages = Pages;
        this.Transitions = Transitions;

        _transitionIndex = new Dictionary<(string, string, string), string>();
        foreach (var t in Transitions)
        {
            _transitionIndex[(t.FromPage, t.Trigger, t.Action)] = t.ToPage;
        }
    }

    /// <summary>解析跳转目标，未匹配返回 null。</summary>
    public string? ResolveTarget(string fromPage, string elementId, string action)
        => _transitionIndex.TryGetValue((fromPage, elementId, action), out var to) ? to : null;

    /// <summary>按 ID 获取页面定义，不存在返回 null。</summary>
    public PageState? GetPage(string pageId)
        => Pages.TryGetValue(pageId, out var page) ? page : null;

    // ── JSON 反序列化 ──────────────────────────────────

    /// <summary>从 JSON 字符串加载 StateFixture</summary>
    public static StateFixture FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<StateFixtureDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dto == null)
            throw new InvalidOperationException("Failed to deserialize StateFixture from JSON");

        return new StateFixture(
            InitialPage: dto.InitialPage,
            Pages: dto.Pages.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => new PageState(
                    kvp.Value.PageName,
                    kvp.Value.Elements
                        .Select(e => new PageElement(e.Id, e.Type, e.Text, e.X, e.Y, e.ActionTarget))
                        .ToImmutableArray(),
                    kvp.Value.IsComplete)),
            Transitions: dto.Transitions
                .Select(t => new PageTransition(t.Id, t.Trigger, t.FromPage, t.ToPage, t.Action))
                .ToImmutableArray());
    }

    /// <summary>仅用于 JSON 反序列化的内部 DTO</summary>
    internal sealed class StateFixtureDto
    {
        public string InitialPage { get; set; } = "";
        public Dictionary<string, PageStateDto> Pages { get; set; } = new();
        public List<PageTransitionDto> Transitions { get; set; } = new();
    }

    internal sealed class PageStateDto
    {
        public string PageName { get; set; } = "";
        public bool IsComplete { get; set; }
        public List<PageElementDto> Elements { get; set; } = new();
    }

    internal sealed class PageElementDto
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string? ActionTarget { get; set; }
    }

    internal sealed class PageTransitionDto
    {
        public string Id { get; set; } = "";
        public string Trigger { get; set; } = "";
        public string FromPage { get; set; } = "";
        public string ToPage { get; set; } = "";
        public string Action { get; set; } = "";
    }
}

/// <summary>单个页面的完整定义</summary>
public sealed record class PageState(
    string PageName,
    ImmutableArray<PageElement> Elements,
    bool IsComplete = false);

/// <summary>页面上的一个可交互元素</summary>
public sealed record class PageElement(
    string Id,
    string Type,
    string Text,
    double X,
    double Y,
    string? ActionTarget = null);

/// <summary>一条页面跳转规则</summary>
public sealed record class PageTransition(
    string Id,
    string Trigger,
    string FromPage,
    string ToPage,
    string Action);
