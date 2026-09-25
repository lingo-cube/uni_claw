using UniClaw.Agent.Dsh;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class ObserverProjectionTests
{
    [Fact]
    public void Projection_Merges_Product_And_Dsh_Evidence_Without_Reordering_Product_Time()
    {
        var events = new[]
        {
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:04Z"),
                ObserverEvidenceSource.ProductTrace, "Grounding", "target resolved"),
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:01Z"),
                ObserverEvidenceSource.ProductTrace, "Kernel", "NeedDecision"),
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:02Z"),
                ObserverEvidenceSource.DshTrajectory, "Agent", "DSH Turn #3"),
        };

        var projection = UnifiedObserverProjection.Build(new ObserverProjectionInput(
            "temperature", "24 to 20", "run-1", "session-1", "dsh-1",
            ModelConfiguration.DeepSeekFlash, 1, "Policy P-001", "P-001", 4,
            "set temperature", "success", "Goal satisfied", events));

        Assert.Equal(UnifiedObserverProjection.DefaultWorkspaceId, projection.WorkspaceId);
        Assert.Equal(new[] { "NeedDecision", "DSH Turn #3", "target resolved" },
            projection.Timeline.Select(static e => e.Message));
        Assert.Equal(ObserverEvidenceSource.ProductTrace, projection.Timeline[0].Source);
        Assert.Equal(4, projection.PolicyApplications);
    }

    [Fact]
    public void Workspace_Is_Read_Only_And_Can_Keep_Reading_When_Source_Changes()
    {
        var running = true;
        var workspace = new ObserverWorkspace(() => UnifiedObserverProjection.Build(
            new ObserverProjectionInput("task", "goal", "run-1", "session-1", "dsh-1",
                ModelConfiguration.Free, 0, null, null, 0, null, null,
                running ? "running" : "completed", Array.Empty<ObserverEvent>())));

        Assert.Equal("running", workspace.ReadOnlySnapshot().Outcome);
        running = false; // Simulates an observer refresh/source stream reconnect.
        Assert.Equal("completed", workspace.ReadOnlySnapshot().Outcome);
    }

    [Fact]
    public void Model_Switch_Does_Not_Change_Product_Profile()
    {
        var local = UniagentProdConfiguration.Create(ModelConfiguration.Free);
        var deepSeek = UniagentProdConfiguration.Create(ModelConfiguration.DeepSeekFlash);

        Assert.Equal(local.Profile, deepSeek.Profile);
        Assert.NotEqual(local.Model, deepSeek.Model);
        Assert.Equal("http://127.0.0.1:3080/", local.Service.BaseUri.ToString());
        Assert.Equal(CapabilityManifest.ProductHeadless.ManifestHash,
            local.Profile.Capabilities.ManifestHash);
    }

    [Fact]
    public void Handshake_Rejects_Profile_And_Capability_Hash_Drift()
    {
        var expected = ProductHandshake.CreateRequest("session-1", "run-1");
        var response = new HandshakeResponse(true, expected.Protocol,
            expected.ExpectedCapabilities, "dsh-1");

        Assert.False(ProductHandshake.Validate(expected,
            response with { Protocol = expected.Protocol with { ProfileId = "other" } }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            response with { Protocol = expected.Protocol with { ProfileVersion = "2" } }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            response with { Protocol = expected.Protocol with { CapabilityManifestHash = "bad" } }).Accepted);
    }
}
