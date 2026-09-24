using System.Text.Json;
using System.IO;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class ProtocolFoundationTests
{
    [Fact]
    public void Schema_IsDeterministic_AndHasHash()
    {
        var first = ProductProtocolSchema.Current;
        var second = ProductProtocolSchema.Current;

        Assert.Equal(first.Json, second.Json);
        Assert.Equal(first.SchemaHash, second.SchemaHash);
        Assert.Equal(64, first.SchemaHash.Length);
        Assert.Contains("AgentDecision", first.Json, StringComparison.Ordinal);
        Assert.Contains("submit_decision", JsonSerializer.Serialize(CapabilityManifest.ProductHeadless),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Decisions))]
    public void Four_Decision_Variants_RoundTrip(AgentDecision original)
    {
        var options = ProductProtocolJson.CreateOptions();
        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<AgentDecision>(json, options);

        Assert.NotNull(restored);
        Assert.Equal(original.GetType(), restored!.GetType());
        Assert.Equal(AgentDecisionCorrelation.TryGetDecisionId(original),
            AgentDecisionCorrelation.TryGetDecisionId(restored));
    }

    [Theory]
    [MemberData(nameof(Decisions))]
    public async Task Replay_Provider_Covers_All_Four_Decisions(AgentDecision expected)
    {
        var decisionId = AgentDecisionCorrelation.TryGetDecisionId(expected)!;
        var json = JsonSerializer.Serialize(expected, ProductProtocolJson.CreateOptions());
        await using var adapter = new DshAgentAdapter(
            new ReplayDshTransport(new Dictionary<string, string> { [decisionId] = json }),
            "product-session", "run-1");

        var actual = await adapter.ConsultAsync(Context(decisionId));

        Assert.Equal(expected.GetType(), actual?.GetType());
        Assert.Equal(decisionId, AgentDecisionCorrelation.TryGetDecisionId(actual!));
        Assert.Empty(adapter.Diagnostics);
    }

    [Fact]
    public void Handshake_Rejects_Version_Hash_And_Capability_Mismatch()
    {
        var expected = ProductHandshake.CreateRequest("session-1", "run-1");
        var baseResponse = new HandshakeResponse(true, expected.Protocol,
            expected.ExpectedCapabilities, "dsh-1");

        Assert.True(ProductHandshake.Validate(expected, baseResponse).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { Protocol = expected.Protocol with { SchemaHash = "bad" } }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { Protocol = expected.Protocol with { ProtocolVersion = "bad" } }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { Protocol = expected.Protocol with { SchemaVersion = "bad" } }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { Accepted = false, FailureReason = "rejected" }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { DshSessionId = null }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { ReportedCapabilities = new CapabilityManifest(new[] { "shell" }) }).Accepted);
        Assert.False(ProductHandshake.Validate(expected,
            baseResponse with { ReportedCapabilities = new CapabilityManifest(Array.Empty<string>()) }).Accepted);
    }

    [Fact]
    public void Handshake_Rejects_Unknown_And_Duplicate_Capabilities()
    {
        var expected = ProductHandshake.CreateRequest("session-1", "run-1");
        var response = new HandshakeResponse(true, expected.Protocol,
            new CapabilityManifest(new[] { "submit_decision", "unknown_capability" }), "dsh-1");
        Assert.False(ProductHandshake.Validate(expected, response).Accepted);

        var duplicate = response with
        {
            ReportedCapabilities = new CapabilityManifest(new[] { "submit_decision", "submit_decision" })
        };
        var duplicateValidation = ProductHandshake.Validate(expected, duplicate);
        Assert.False(duplicateValidation.Accepted);
        Assert.Equal("capability-manifest-duplicate", duplicateValidation.FailureReason);
    }

    [Theory]
    [InlineData("{\"kind\":\"future\",\"decisionId\":\"d\"}")]
    [InlineData("{\"kind\":\"noAction\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"justification\":\"ok\"},\"extra\":1}")]
    [InlineData("{\"kind\":\"noAction\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"other\",\"justification\":\"ok\"}}")]
    public void Malformed_Decision_Payload_Is_Rejected(string payload)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecision>(
            payload, ProductProtocolJson.CreateOptions()));
    }

    [Fact]
    public void Generated_Artifacts_Match_The_Runtime_Generator()
    {
        var root = FindRepositoryRoot();
        var schemaPath = Path.Combine(root, "schemas", "agt-002", "product-protocol.schema.json");
        var dtsPath = Path.Combine(root, "schemas", "agt-002", "product-protocol.d.ts");
        Assert.Equal(ProductProtocolSchema.Current.Json, File.ReadAllText(schemaPath).Trim());
        Assert.Equal(ProductProtocolSchemaGenerator.GenerateTypeScript(), File.ReadAllText(dtsPath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root");
    }

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId, "run-1", "v0", "objective", new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(),
        AgentDecisionPhase.InitialPlanning);

    public static IEnumerable<object[]> Decisions()
    {
        yield return new object[]
        {
            new AgentDecision.Act(new AgentActionProposal("decision-1",
                new[] { new AgentActionStep("switch", "wifi", "tap", "on") }, "act"))
        };
        yield return new object[]
        {
            new AgentDecision.Policy("decision-2", new PolicyProposal(
                "policy-1",
                new PolicyPredicate[] { new PolicyPredicate.ClaimEquals("switch.state", "off") },
                new PolicyActionTemplate("switch", "wifi", "tap", "on"),
                new PolicyPredicate[] { new PolicyPredicate.ClaimEquals("switch.state", "on") },
                new PolicyGuard[] { new PolicyGuard.ObservationUnchanged("switch.state", 2) },
                3, "policy"))
        };
        yield return new object[]
        {
            new AgentDecision.NoAction(new AgentNoActionProposal("decision-3", "already done"))
        };
        yield return new object[]
        {
            new AgentDecision.Defer("decision-4", new ObserveSpec("switch", 1))
        };
    }
}
