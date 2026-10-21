using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Tests.SettingsCoverage;

public sealed class CoverageDirectiveSemanticsTests
{
    [Theory]
    [InlineData("Volume > media")]
    [InlineData("Scroll settings")]
    public void EntryLabelCannotChangeDirectiveMeaning(string target)
    {
        var config = Config(target);
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(config.RootRoute, new[] { ("ui.element", (string?)target) });
        var verification = new PostActionEffectVerification("rev-after",
            new TargetSpec("ui.element", target, "tap"), true,
            new[] { new AssuranceCheck("post-action-target-unique", true) }, null);
        var journal = new SettingsCoverageRunner.CoverageStepJournal(ledger, () => new[] { verification }, null);
        var director = new SettingsCoverageDirector(context => new AgentDecision.Act(
            new AgentActionProposal(context.DecisionId,
                new[] { new AgentActionStep("ui.element", target, "tap", null) }, "follow selected entry")),
            ledger, config, journal);

        Assert.IsType<AgentDecision.Act>(director.Consult(Context("enter")));
        journal.Enqueue(new EffectReceipt("receipt", "intent", "binding", "occurrence", "rev-before", 1,
            DispatchOutcome.DeliveryCompleted, "fixture-delivered", DateTimeOffset.UnixEpoch),
            config.RootRoute, "android.settings|route:child", "capture-after");
        ledger.RecordObservation("android.settings|route:child", new[] { ("ui.element", (string?)"Navigate up") });
        director.Consult(Context("after"));

        Assert.Null(ledger.Report(config).FirstDivergence);
        Assert.Equal(1, ledger.Report(config).StepsVerified);
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(
            Assert.Single(journal.Artifacts()), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.True(saved.RootElement.TryGetProperty("directiveKind", out var kind));
        Assert.Equal("enter", kind.GetString());
        Assert.Equal(target, saved.RootElement.GetProperty("directiveTargetDescriptor").GetString());
    }

    private static SettingsCoverageConfig Config(string target) => new(
        ConfigVersion: "1", Session: new("directive test", "workspace", true, false),
        Bounds: new(8, 8, 4, 3, 0),
        Coverage: new(true, "all-visible", 0, 0, false, 0),
        TargetPages: new[] { target }, Termination: new(true, true, true, true),
        RootRoute: "android.settings|route:Settings", ScrollContainerDescriptor: "list", BackDescriptor: "Navigate up");

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId, "run", "contract", "inspect settings", new HashSet<string> { "tap", "swipe-up" },
        new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(), AgentDecisionPhase.StepVerified);
}
