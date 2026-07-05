using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Simulation;
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
}
