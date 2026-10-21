using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class DshSemanticPerceptionTests
{
    private static SemanticPerceptionRequest Request() => new("r1",
        new CapabilityCorrelation("s1", captureId: "c1", observationCycleId: "o1"),
        new("c1", "o1", "s1", DateTimeOffset.UtcNow,
            CoordinateSpace.DeviceViewport(100, 100, captureId: "c1"),
            new[] { new SemanticLayoutItem("t", "text", "Settings", new UiBounds(0, 0, 80, 20)) }, new[] { "e1" }),
        new[] { new SemanticPageCandidate("settings", "Settings", "system settings") });

    [Fact]
    public async Task UsesHighReasoningAndProjectsObservedPage()
    {
        string? effort = null;
        var adapter = new DshSemanticPerception((_, prompt, model, reasoning, _) =>
        { effort = reasoning; Assert.Contains("当前capture", prompt); return Task.FromResult(new DshSlowResponse("r1", "{\"pageKey\":\"settings\",\"observedTitle\":\"Settings\",\"relation\":\"Unknown\",\"expectedMatch\":\"Unknown\"}", null, "{\"reasoningEffort\":\"high\"}")); }, new ModelConfiguration("p", "m"));
        var result = await Assert.IsAssignableFrom<ISemanticPerception>(adapter.CreateCapability()).ObserveSemanticsAsync(Request());
        Assert.True(result.Status == CapabilityStatus.Completed, result.Assessment.Diagnostic);
        Assert.Equal("high", effort);
        Assert.Equal("settings", result.Observation!.PageKey);
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("{\"pageKey\":\"other\",\"observedTitle\":\"Settings\",\"relation\":\"Unknown\",\"expectedMatch\":\"Unknown\"}")]
    [InlineData("{\"pageKey\":\"settings\",\"observedTitle\":\"Other\",\"relation\":\"Unknown\",\"expectedMatch\":\"Unknown\"}")]
    public async Task InvalidResponsesFailClosed(string text)
    {
        var adapter = new DshSemanticPerception((_, _, _, _, _) => Task.FromResult(new DshSlowResponse("r1", text, null)), new ModelConfiguration("p", "m"));
        var result = await Assert.IsAssignableFrom<ISemanticPerception>(adapter.CreateCapability()).ObserveSemanticsAsync(Request());
        Assert.Null(result.Observation);
    }

    [Fact]
    public async Task CancellationDoesNotProduceObservation()
    {
        var adapter = new DshSemanticPerception(async (_, _, _, _, token) => { await Task.Delay(Timeout.Infinite, token); return null!; }, new ModelConfiguration("p", "m"));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var result = await Assert.IsAssignableFrom<ISemanticPerception>(adapter.CreateCapability()).ObserveSemanticsAsync(Request(), cts.Token);
        Assert.Equal(CapabilityStatus.Cancelled, result.Status);
        Assert.Null(result.Observation);
    }
}
