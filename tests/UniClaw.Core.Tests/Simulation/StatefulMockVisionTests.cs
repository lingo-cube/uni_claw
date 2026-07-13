using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Simulation;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Simulation;

public class StatefulMockVisionTests
{
    private static StateFixture CreateTwoPageFixture()
    {
        return new StateFixtureBuilder()
            .Page("home", p => p
                .Name("HomeScreen")
                .Button("btn_settings", "Settings", 0.5, 0.9)
                .Tab("tab_home", "Home", 0.2, 0.95)
                .Readonly("txt_title", "Welcome", 0.5, 0.1))
            .Page("settings", p => p
                .Name("SettingsScreen")
                .Switch("sw_wifi", "Wi-Fi", 0.8, 0.3)
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("go").Click("btn_settings").From("home").To("settings"))
            .Transition(t => t.Id("back").Click("btn_back").From("settings").To("home"))
            .Build();
    }

    [Fact]
    public void InitialPage_MatchesFixture()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        Assert.Equal("home", vision.CurrentPageId);
    }

    [Fact]
    public void SimulateAction_SwitchesPage()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var result = vision.SimulateAction("btn_settings", "click");

        Assert.True(result);
        Assert.Equal("settings", vision.CurrentPageId);
        Assert.Equal(1, vision.NavigationDepth);
    }

    [Fact]
    public void SimulateAction_UnknownElement_ReturnsFalse()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var result = vision.SimulateAction("nonexistent", "click");

        Assert.False(result);
        Assert.Equal("home", vision.CurrentPageId);
    }

    [Fact]
    public void NavigateBack_ReturnsToPreviousPage()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());
        vision.SimulateAction("btn_settings", "click"); // home → settings

        var result = vision.NavigateBack();

        Assert.True(result);
        Assert.Equal("home", vision.CurrentPageId);
        Assert.Equal(0, vision.NavigationDepth);
    }

    [Fact]
    public void NavigateBack_EmptyStack_ReturnsFalse()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var result = vision.NavigateBack();

        Assert.False(result);
    }

    [Fact]
    public void FindElementAt_MatchWithinTolerance()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var element = vision.FindElementAt(0.51, 0.89); // close to (0.5, 0.9)

        Assert.NotNull(element);
        Assert.Equal("btn_settings", element!.Id);
    }

    [Fact]
    public void FindElementAt_NoMatchOutsideTolerance()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var element = vision.FindElementAt(0.9, 0.9); // far from any element

        Assert.Null(element);
    }

    [Fact]
    public void Reset_ReturnsToInitialPage()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());
        vision.SimulateAction("btn_settings", "click");

        vision.Reset();

        Assert.Equal("home", vision.CurrentPageId);
        Assert.Equal(0, vision.NavigationDepth);
    }

    [Fact]
    public async Task AnalyzeCurrentPage_BuildsPageAnalysis()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var analysis = await vision.AnalyzeCurrentPageAsync();

        Assert.NotNull(analysis);
        // 1 tab → Level1Menus
        Assert.Single(analysis!.Level1Menus);
        Assert.Equal("Home", analysis.Level1Menus[0].Name);
        // 2 non-tab elements (button + readonly) → Items
        Assert.Equal(2, analysis.Items.Length);
        Assert.Contains(analysis.Items, i => i.Name == "Settings" && i.Type == MenuItemType.Button);
        Assert.Contains(analysis.Items, i => i.Name == "Welcome" && i.Type == MenuItemType.Readonly);
    }

    [Fact]
    public async Task FindAppEntry_ReturnsScreenCenter()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());

        var entry = await vision.FindAppEntryAsync("AnyApp");

        Assert.NotNull(entry);
        Assert.Equal(0.5, entry!.X);
        Assert.Equal(0.5, entry.Y);
    }

    [Fact]
    public async Task AnalyzeCurrentPage_WithBackButton_ExtractsCoordinate()
    {
        var vision = new StatefulMockVisionService(CreateTwoPageFixture());
        vision.SimulateAction("btn_settings", "click"); // go to settings page (has back_button)

        var analysis = await vision.AnalyzeCurrentPageAsync();

        Assert.NotNull(analysis);
        Assert.NotNull(analysis!.BackButton);
        Assert.Equal(0.05, analysis.BackButton!.X);
        Assert.Equal(0.05, analysis.BackButton.Y);
    }

    // ── IVisionProvider scroll state query methods (Task 1.4) ──────────

    [Fact(DisplayName = "StatefulMockVision: HasScroll returns false (default implementation)")]
    public void HasScroll_ReturnsFalse_DefaultImplementation()
    {
        IVisionProvider vision = new StatefulMockVisionService(CreateTwoPageFixture());
        Assert.False(vision.HasScroll());
    }

    [Fact(DisplayName = "StatefulMockVision: GetScrollProgress returns 0.0 (default implementation)")]
    public void GetScrollProgress_ReturnsZero_DefaultImplementation()
    {
        IVisionProvider vision = new StatefulMockVisionService(CreateTwoPageFixture());
        Assert.Equal(0.0, vision.GetScrollProgress());
    }

    [Fact(DisplayName = "StatefulMockVision: IsEndOfList returns true (default implementation)")]
    public void IsEndOfList_ReturnsTrue_DefaultImplementation()
    {
        IVisionProvider vision = new StatefulMockVisionService(CreateTwoPageFixture());
        Assert.True(vision.IsEndOfList());
    }

    [Fact(DisplayName = "ScrollableMockVision: HasScroll returns true when scroll data exists")]
    public void HasScroll_ReturnsTrue_WhenScrollDataExists()
    {
        var fixture = new StateFixtureBuilder()
            .Page("list", p => p.Name("List"))
            .Build();
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("list", new ScrollSegment(0.0, System.Collections.Immutable.ImmutableArray.Create(
                new MenuItem("Item1", new Coordinate(0.5, 0.3), MenuItemType.Button))))
            .Build();
        var vision = new ScrollableMockVisionService(fixture, scrollData);

        Assert.True(vision.HasScroll);
    }

    [Fact(DisplayName = "ScrollableMockVision: HasScroll returns false when no scroll data")]
    public void HasScroll_ReturnsFalse_WhenNoScrollData()
    {
        var fixture = new StateFixtureBuilder()
            .Page("list", p => p.Name("List"))
            .Build();
        var vision = new ScrollableMockVisionService(fixture);

        Assert.False(vision.HasScroll);
    }

    [Fact(DisplayName = "ScrollableMockVision: GetScrollProgress returns 0.0 initially")]
    public void GetScrollProgress_ReturnsZero_Initially()
    {
        var fixture = new StateFixtureBuilder()
            .Page("list", p => p.Name("List"))
            .Build();
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("list",
                new ScrollSegment(0.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item1", new Coordinate(0.5, 0.3), MenuItemType.Button))),
                new ScrollSegment(0.5, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item2", new Coordinate(0.5, 0.7), MenuItemType.Button))))
            .Build();
        var vision = new ScrollableMockVisionService(fixture, scrollData);

        Assert.Equal(0.0, vision.GetScrollProgress("list"));
    }

    [Fact(DisplayName = "ScrollableMockVision: IsEndOfList false when more content available")]
    public void IsEndOfList_ReturnsFalse_WhenMoreContentAvailable()
    {
        var fixture = new StateFixtureBuilder()
            .Page("list", p => p.Name("List"))
            .Build();
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("list",
                new ScrollSegment(0.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item1", new Coordinate(0.5, 0.3), MenuItemType.Button))),
                new ScrollSegment(1.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item2", new Coordinate(0.5, 0.7), MenuItemType.Button))))
            .Build();
        var vision = new ScrollableMockVisionService(fixture, scrollData);

        Assert.False(vision.IsEndOfList);
    }

    [Fact(DisplayName = "ScrollableMockVision: IsEndOfList true when scrolled to end")]
    public void IsEndOfList_ReturnsTrue_WhenScrolledToEnd()
    {
        var fixture = new StateFixtureBuilder()
            .Page("list", p => p.Name("List"))
            .Build();
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("list",
                new ScrollSegment(0.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item1", new Coordinate(0.5, 0.3), MenuItemType.Button))),
                new ScrollSegment(1.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item2", new Coordinate(0.5, 0.7), MenuItemType.Button))))
            .Build();
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        vision.SimulateScroll(1.0); // scroll to end

        Assert.True(vision.IsEndOfList);
    }

    [Fact(DisplayName = "ScrollableMockVision: IVisionProvider interface methods delegate correctly")]
    public void IVisionProvider_ScrollMethods_DelegateCorrectly()
    {
        var fixture = new StateFixtureBuilder()
            .Page("list", p => p.Name("List"))
            .Build();
        var scrollData = ScrollDataStore.CreateBuilder()
            .Add("list",
                new ScrollSegment(0.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item1", new Coordinate(0.5, 0.3), MenuItemType.Button))),
                new ScrollSegment(1.0, System.Collections.Immutable.ImmutableArray.Create(
                    new MenuItem("Item2", new Coordinate(0.5, 0.7), MenuItemType.Button))))
            .Build();
        var vision = new ScrollableMockVisionService(fixture, scrollData);
        IVisionProvider ivp = vision;

        // Explicit interface implementation delegates to instance methods
        Assert.True(ivp.HasScroll());
        Assert.Equal(0.0, ivp.GetScrollProgress());
        Assert.False(ivp.IsEndOfList());
    }
}
