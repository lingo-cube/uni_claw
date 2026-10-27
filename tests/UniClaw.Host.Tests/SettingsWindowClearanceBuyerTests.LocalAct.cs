using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed partial class SettingsWindowClearanceBuyerTests
{
    [Theory]
    [InlineData("same-route", true)]
    [InlineData("other-popup", false)]
    public void LocalAct_ClosingOverlayDoesNotExtendItsTailIntoTheUnderlyingSlice(string mode, bool cleared)
    {
        var source = new WindowSource(mode);
        var contexts = new List<AgentDecisionContext>();
        var root = Path.Combine(Path.GetTempPath(), "local-act-overlay-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new SettingsCoverageConfig("1", new("local overlay", "offline", false, false),
                new(5, 6, 4, 3, 0), new(true, "configured", 0, 0, false, 0), ["SIM name & color", "Other entry"],
                new(true, true, true, true), WindowSource.Route, "unused", null,
                ActionPolicy: SettingsCoverageConfig.LoadDefault().ActionPolicy, ActionPolicyRequired: true);
            var result = SettingsCoverageRunner.Run(root, new("synthetic-window-device",
                new("synthetic-window-device", "unused", "/unused", "/unused/python"), config, context =>
                {
                    contexts.Add(context);
                    return contexts.Count switch
                    {
                        1 => new AgentDecision.Act(new(context.DecisionId, [new("ui.element", "SIM name & color", "tap", null)], "open overlay")),
                        2 => new AgentDecision.Act(new(context.DecisionId,
                            [new("ui.element", "CANCEL", "tap", null), new("ui.element", "Other entry", "tap", null)], "invalid overlay tail")),
                        _ => new AgentDecision.Blocked(context.DecisionId, "fixture ended"),
                    };
                }, ViewportWidth: 100, ViewportHeight: 100, FeedNext: source.Next,
                CurrentCaptureId: () => source.CaptureId, EffectDriver: source, UseCoverageDirector: false));
            Assert.Equal(2, source.Deliveries);
            Assert.Equal(new[] { "SIM name & color", "CANCEL" }, result.Steps.Select(step => step.TargetDescriptor));
            Assert.Equal(cleared, result.Steps[1].Verified);
            Assert.Equal(cleared ? AgentDecisionPhase.StepRejected : AgentDecisionPhase.VerificationFailed, contexts[2].Phase);
            Assert.Equal(cleared ? 1 : 0, contexts[2].FailedStepIndex);
            if (cleared) Assert.StartsWith("act-scope-changed", contexts[2].FailureReason);
            if (Environment.GetEnvironmentVariable("UNICLAW_LOCAL_ACT_EVIDENCE") is { Length: > 0 } output)
            {
                var target = Path.Combine(output, "overlay-tail-" + mode);
                Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(result.RunDir))
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
