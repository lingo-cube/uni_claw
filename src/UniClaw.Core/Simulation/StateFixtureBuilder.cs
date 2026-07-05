using System.Collections.Immutable;

namespace UniClaw.Core.Simulation;

/// <summary>
/// StateFixture 的 Fluent Builder。
/// 代码驱动构建页面和跳转规则，是 JSON 文件之外的另一种 fixture 创建方式。
/// </summary>
public sealed class StateFixtureBuilder
{
    private readonly Dictionary<string, PageStateBuilder> _pages = new();
    private readonly List<PageTransitionBuilder> _transitions = new();
    private string _initialPage = "";

    /// <summary>定义一个新页面。第一个 Page() 调用的 id 自动成为 initialPage。返回 this 以支持链式调用。</summary>
    public StateFixtureBuilder Page(string id, Action<PageStateBuilder> configure)
    {
        if (_initialPage == "")
            _initialPage = id;
        var builder = new PageStateBuilder(id);
        configure(builder);
        _pages[id] = builder;
        return this;
    }

    /// <summary>定义一条跳转规则。</summary>
    public StateFixtureBuilder Transition(Action<PageTransitionBuilder> configure)
    {
        var builder = new PageTransitionBuilder();
        configure(builder);
        _transitions.Add(builder);
        return this;
    }

    /// <summary>构建 StateFixture。</summary>
    public StateFixture Build()
    {
        return new StateFixture(
            InitialPage: _initialPage,
            Pages: _pages.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Build()),
            Transitions: _transitions.Select(t => t.Build()).ToImmutableArray());
    }
}

/// <summary>Fluent Builder: 页面</summary>
public sealed class PageStateBuilder
{
    private readonly string _id;
    private string _pageName = "";
    private readonly List<PageElementBuilder> _elements = new();

    internal PageStateBuilder(string id) => _id = id;

    public PageStateBuilder Name(string name) { _pageName = name; return this; }

    public PageStateBuilder Element(string id, Action<PageElementBuilder> configure)
    {
        var builder = new PageElementBuilder(id);
        configure(builder);
        _elements.Add(builder);
        return this;
    }

    public PageStateBuilder Button(string id, string text, double x, double y)
        => Element(id, e => e.Type("button").Text(text).At(x, y));

    public PageStateBuilder Switch(string id, string text, double x, double y)
        => Element(id, e => e.Type("switch").Text(text).At(x, y));

    public PageStateBuilder BackButton(string id, double x, double y)
        => Element(id, e => e.Type("back_button").Text(id).At(x, y));

    public PageStateBuilder Tab(string id, string text, double x, double y)
        => Element(id, e => e.Type("tab").Text(text).At(x, y));

    public PageStateBuilder Readonly(string id, string text, double x, double y)
        => Element(id, e => e.Type("readonly").Text(text).At(x, y));

    public PageState Build()
        => new(_pageName, _elements.Select(e => e.Build()).ToImmutableArray());
}

/// <summary>Fluent Builder: 页面元素</summary>
public sealed class PageElementBuilder
{
    private readonly string _id;
    private string _type = "button";
    private string _text = "";
    private double _x, _y;
    private string? _actionTarget;

    internal PageElementBuilder(string id) => _id = id;

    public PageElementBuilder Type(string type) { _type = type; return this; }
    public PageElementBuilder Text(string text) { _text = text; return this; }
    public PageElementBuilder At(double x, double y) { _x = x; _y = y; return this; }
    public PageElementBuilder Targets(string target) { _actionTarget = target; return this; }

    public PageElement Build() => new(_id, _type, _text, _x, _y, _actionTarget);
}

/// <summary>Fluent Builder: 跳转规则</summary>
public sealed class PageTransitionBuilder
{
    private string _id = "";
    private string _trigger = "";
    private string _fromPage = "";
    private string _toPage = "";
    private string _action = "click";

    public PageTransitionBuilder Id(string id) { _id = id; return this; }
    public PageTransitionBuilder Click(string trigger) { _trigger = trigger; _action = "click"; return this; }
    public PageTransitionBuilder Swipe(string trigger) { _trigger = trigger; _action = "swipe"; return this; }
    public PageTransitionBuilder From(string from) { _fromPage = from; return this; }
    public PageTransitionBuilder To(string to) { _toPage = to; return this; }

    public PageTransition Build() => new(_id, _trigger, _fromPage, _toPage, _action);
}
