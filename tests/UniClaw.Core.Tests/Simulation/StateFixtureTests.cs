using UniClaw.Core.Simulation;
using Xunit;

namespace UniClaw.Core.Tests.Simulation;

public class StateFixtureTests
{
    private const string TwoPageJson = """
    {
      "initialPage": "home",
      "pages": {
        "home": {
          "pageName": "HomeScreen",
          "isComplete": false,
          "elements": [
            { "id": "btn_settings", "type": "button", "text": "Settings", "x": 0.5, "y": 0.9, "actionTarget": "settings" }
          ]
        },
        "settings": {
          "pageName": "SettingsScreen",
          "isComplete": true,
          "elements": [
            { "id": "btn_back", "type": "back_button", "text": "Back", "x": 0.05, "y": 0.05 }
          ]
        }
      },
      "transitions": [
        { "id": "go", "trigger": "btn_settings", "fromPage": "home", "toPage": "settings", "action": "click" },
        { "id": "back", "trigger": "btn_back", "fromPage": "settings", "toPage": "home", "action": "click" }
      ]
    }
    """;

    [Fact]
    public void FromJson_LoadsTwoPageFixture()
    {
        var fixture = StateFixture.FromJson(TwoPageJson);

        Assert.Equal("home", fixture.InitialPage);
        Assert.Equal(2, fixture.Pages.Count);
        Assert.Equal(2, fixture.Transitions.Length);
    }

    [Fact]
    public void ResolveTarget_Hit()
    {
        var fixture = StateFixture.FromJson(TwoPageJson);

        var result = fixture.ResolveTarget("home", "btn_settings", "click");

        Assert.Equal("settings", result);
    }

    [Fact]
    public void ResolveTarget_Miss()
    {
        var fixture = StateFixture.FromJson(TwoPageJson);

        var result = fixture.ResolveTarget("home", "nonexistent", "click");

        Assert.Null(result);
    }

    [Fact]
    public void GetPage_Existing()
    {
        var fixture = StateFixture.FromJson(TwoPageJson);

        var page = fixture.GetPage("home");

        Assert.NotNull(page);
        Assert.Equal("HomeScreen", page!.PageName);
        Assert.Single(page.Elements);
    }

    [Fact]
    public void GetPage_NotFound()
    {
        var fixture = StateFixture.FromJson(TwoPageJson);

        var page = fixture.GetPage("unknown");

        Assert.Null(page);
    }

    [Fact]
    public void Builder_ProducesEquivalentFixture()
    {
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p
                .Name("HomeScreen")
                .Button("btn_settings", "Settings", 0.5, 0.9))
            .Page("settings", p => p
                .Name("SettingsScreen")
                .BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("go").Click("btn_settings").From("home").To("settings"))
            .Build();

        Assert.Equal("home", fixture.InitialPage);
        Assert.Equal(2, fixture.Pages.Count);
        Assert.Equal("settings", fixture.ResolveTarget("home", "btn_settings", "click"));
    }
}
