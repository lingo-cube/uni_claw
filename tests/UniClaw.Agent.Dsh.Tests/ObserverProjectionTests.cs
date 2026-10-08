using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class ObserverProjectionTests
{
    private static readonly DshServiceEndpoint TestEndpoint =
        new(new Uri("http://127.0.0.1:3080/", UriKind.Absolute));

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
            new ModelConfiguration("opencode-go", "deepseek-flash"), 1, "Policy P-001", "P-001", 4,
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
                new ModelConfiguration("opencode-go", "space-bunny-free"), 0, null, null, 0, null, null,
                running ? "running" : "completed", Array.Empty<ObserverEvent>())));

        Assert.Equal("running", workspace.ReadOnlySnapshot().Outcome);
        running = false; // Simulates an observer refresh/source stream reconnect.
        Assert.Equal("completed", workspace.ReadOnlySnapshot().Outcome);
    }

    [Fact]
    public void Timeline_Correlates_Timeout_NextDecision_And_LateResponse_By_Generation()
    {
        // F4: Decision N timeout, Decision N+1 start, and Decision N late
        // response must be unambiguously distinguishable in one timeline via
        // DecisionId + Generation correlation (correlation only — no authority).
        var events = new[]
        {
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:01Z"),
                ObserverEvidenceSource.DshDiagnostic, "Consultation", "decision-N started",
                DecisionId: "decision-abc-3", Generation: 3),
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:31Z"),
                ObserverEvidenceSource.DshDiagnostic, "Abort", "decision-N timeout",
                DecisionId: "decision-abc-3", Generation: 3),
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:32Z"),
                ObserverEvidenceSource.ProductTrace, "Kernel", "decision N+1 started",
                DecisionId: "decision-abc-4", Generation: 4),
            new ObserverEvent(DateTimeOffset.Parse("2026-09-25T10:41:40Z"),
                ObserverEvidenceSource.DshDiagnostic, "LateResponse", "decision-N late response discarded",
                DecisionId: "decision-abc-3", Generation: 3),
        };

        var projection = UnifiedObserverProjection.Build(new ObserverProjectionInput(
            "task", "goal", "run-1", "session-1", "dsh-1",
            new ModelConfiguration("opencode-go", "deepseek-flash"), 2, "Defer", null, 0,
            null, null, null, events));

        var timeout = Assert.Single(projection.Timeline, e => e.Message == "decision-N timeout");
        var nextStart = Assert.Single(projection.Timeline, e => e.Message == "decision N+1 started");
        var late = Assert.Single(projection.Timeline, e => e.Message == "decision-N late response discarded");

        Assert.Equal("decision-abc-3", timeout.DecisionId);
        Assert.Equal(3, timeout.Generation);
        Assert.Equal("decision-abc-4", nextStart.DecisionId);
        Assert.Equal(4, nextStart.Generation);
        // The late response correlates with the timed-out decision, not the
        // active one — same DecisionId/generation as the timeout row.
        Assert.Equal(timeout.DecisionId, late.DecisionId);
        Assert.Equal(timeout.Generation, late.Generation);
        Assert.NotEqual(nextStart.DecisionId, late.DecisionId);
    }

    [Fact]
    public void Runtime_Configuration_Loads_From_Product_Profile_And_Dsh_Bindings()
    {
        // PRF-002（ADR-0041）：host-neutral 产品 profile 与 DSH 绑定分轨。
        // 产品侧：身份/角色声明，无 provider/model/endpoint。
        var profile = UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault();
        Assert.Equal(UniagentProdProfile.ProfileId, profile.Identity.ProfileId);
        Assert.Equal(CapabilityManifest.ProductHeadless.ManifestHash,
            profile.Identity.Capabilities.ManifestHash);
        Assert.Equal(
            new[]
            {
                new UniClaw.Agent.Profile.ModelRoleDeclaration("agent.decision", Required: true),
                new UniClaw.Agent.Profile.ModelRoleDeclaration("slow.semantic.text", Required: true),
                new UniClaw.Agent.Profile.ModelRoleDeclaration("slow.semantic.visual", Required: false),
            },
            profile.ModelRoles);

        // DSH 侧：provider/model/baseUrl 只在绑定文件（CAP-007 语义）。
        // 2026-10-06 所有者指令：默认模型 deepseek-flash（deepseek-official
        // 直连）；CAP-012 D9：专线按 docs/agents/test-emulator.md 注册 3081。
        var bindings = UniagentDshBindingsYaml.LoadDefault();
        Assert.Equal("deepseekFlash", bindings.SelectedModelKey);
        Assert.Equal(new ModelConfiguration("deepseek-official", "deepseek-flash"), bindings.Model);
        Assert.Equal("http://127.0.0.1:3081/", bindings.Service.BaseUri.ToString());
    }

    [Fact]
    public void Product_Profile_Fails_Closed_On_Identity_Drift()
    {
        var root = FindRepositoryRoot();
        var original = File.ReadAllText(Path.Combine(root,
            UniClaw.Agent.Profile.UniAgentProfileYaml.DefaultConfigRelativePath));
        var drifted = original.Replace("profileVersion: \"1\"", "profileVersion: \"2\"");
        Assert.NotEqual(original, drifted);

        var temporary = Path.Combine(Path.GetTempPath(), "uniagent-prod-drift-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(temporary, drifted);
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => UniClaw.Agent.Profile.UniAgentProfileYaml.Load(temporary));
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void Explicit_Bindings_Keep_Profile_Identity_Across_Model_Switch()
    {
        // PRF-002：换 provider/model（绑定层）不触碰产品身份（profile 层）。
        var profile = UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault();
        var free = UniagentDshBindings.Create(
            new ModelConfiguration("opencode-go", "space-bunny-free"), TestEndpoint, "free");
        var deepSeek = UniagentDshBindings.Create(
            new ModelConfiguration("opencode-go", "deepseek-flash"), TestEndpoint, "deepseekFlash");

        Assert.Equal(profile.Identity, UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault().Identity);
        Assert.NotEqual(free.Model, deepSeek.Model);
        Assert.Equal(CapabilityManifest.ProductHeadless.ManifestHash,
            profile.Identity.Capabilities.ManifestHash);
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

    [Fact]
    public void Handshake_Rejects_Missing_Mandatory_Identity_Fields()
    {
        // F1: profileId / profileVersion / capabilityManifestHash are
        // mandatory; a reported stamp that omits any of them is a malformed
        // handshake and the attachment must be refused.
        var expected = ProductHandshake.CreateRequest("session-1", "run-1");
        HandshakeResponse ReportedWith(ProtocolStamp protocol) =>
            new(true, protocol, expected.ExpectedCapabilities, "dsh-1");

        var profileId = ProductHandshake.Validate(expected,
            ReportedWith(expected.Protocol with { ProfileId = null! }));
        Assert.False(profileId.Accepted);
        Assert.Equal("handshake-field-missing:reported:profileId", profileId.FailureReason);

        var profileVersion = ProductHandshake.Validate(expected,
            ReportedWith(expected.Protocol with { ProfileVersion = null! }));
        Assert.False(profileVersion.Accepted);
        Assert.Equal("handshake-field-missing:reported:profileVersion", profileVersion.FailureReason);

        var manifestHash = ProductHandshake.Validate(expected,
            ReportedWith(expected.Protocol with { CapabilityManifestHash = null! }));
        Assert.False(manifestHash.Accepted);
        Assert.Equal("handshake-field-missing:reported:capabilityManifestHash", manifestHash.FailureReason);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root");
    }
}
