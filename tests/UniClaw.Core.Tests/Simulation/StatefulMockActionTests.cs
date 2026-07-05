using UniClaw.Core.Simulation;
using Xunit;

namespace UniClaw.Core.Tests.Simulation;

public class StatefulMockActionTests
{
    private static (StatefulMockVisionService Vision, StatefulMockActionExecutor Action) CreateServices()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p
                .Name("HomeScreen")
                .Button("btn_settings", "Settings", 0.5, 0.9))
            .Page("settings", p => p
                .Name("SettingsScreen")
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("go").Click("btn_settings").From("home").To("settings"))
            .Transition(t => t.Id("back").Click("btn_back").From("settings").To("home"))
            .Build();
        var vision = new StatefulMockVisionService(fixture);
        var action = new StatefulMockActionExecutor(vision);
        return (vision, action);
    }

    [Fact]
    public async Task TapAsync_MatchingElement_TriggersPageChange()
    {
        var (vision, action) = CreateServices();

        var result = await action.TapAsync(0.5, 0.9);

        Assert.True(result);
        Assert.Equal("settings", vision.CurrentPageId); // 页面已切换
        Assert.Single(action.GetHistory());
        Assert.Equal("tap", action.GetHistory()[0].Action);
        Assert.True(action.GetHistory()[0].Success);
    }

    [Fact]
    public async Task TapAsync_EmptyArea_ReturnsFalse()
    {
        var (vision, action) = CreateServices();

        var result = await action.TapAsync(0.9, 0.9);

        Assert.False(result);
        Assert.Equal("home", vision.CurrentPageId); // 未切换
        Assert.Single(action.GetHistory());
        Assert.False(action.GetHistory()[0].Success);
    }

    [Fact]
    public async Task PressBackAsync_AfterPageSwitch_ReturnsTrue()
    {
        var (vision, action) = CreateServices();
        await action.TapAsync(0.5, 0.9); // home → settings

        var result = await action.PressBackAsync();

        Assert.True(result);
        Assert.Equal("home", vision.CurrentPageId); // 已回退
        Assert.Equal(2, action.GetHistory().Count);
        Assert.Equal("back", action.GetHistory()[1].Action);
    }

    [Fact]
    public async Task PressBackAsync_EmptyStack_ReturnsFalse()
    {
        var (_, action) = CreateServices();

        var result = await action.PressBackAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task GetHistory_ReturnsOrderedRecords()
    {
        var (_, action) = CreateServices();
        await action.TapAsync(0.5, 0.9);
        await action.PressBackAsync();
        await action.InputTextAsync("test");

        var history = action.GetHistory();

        Assert.Equal(3, history.Count);
        Assert.Equal("tap", history[0].Action);
        Assert.Equal("back", history[1].Action);
        Assert.Equal("input_text", history[2].Action);
    }
}
