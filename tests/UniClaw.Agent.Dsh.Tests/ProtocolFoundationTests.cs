using System.Text.Json;
using System.IO;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
using UniClaw.Agent.Dsh.Tests.Fixtures;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Capability;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class ProtocolFoundationTests
{
    [Theory]
    [InlineData("act")]
    [InlineData("noAction")]
    [InlineData("defer")]
    public void Renewal_Survives_Decision_Wire_RoundTrip(string kind)
    {
        var renewal = new BudgetRenewalRequest(32, 40, "coverage progressing");
        AgentDecision original = kind switch
        {
            "act" => new AgentDecision.Act(new AgentActionProposal("renewal-1",
                new[] { new AgentActionStep("button", "Apps", "tap", null) }, null, Renewal: renewal)),
            "noAction" => new AgentDecision.NoAction(new AgentNoActionProposal("renewal-1", "continue", Renewal: renewal)),
            _ => new AgentDecision.Defer("renewal-1", new ObserveSpec("page", 1), renewal),
        };
        var options = ProductProtocolJson.CreateOptions();
        var wire = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<AgentDecision>(wire, options);
        var actual = restored switch
        {
            AgentDecision.Act act => act.Proposal.Renewal,
            AgentDecision.NoAction noAction => noAction.Proposal.Renewal,
            AgentDecision.Defer defer => defer.Renewal,
            _ => null,
        };
        Assert.Equal(renewal, actual);
        using var schema = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        if (kind == "defer")
        {
            var deferSchema = schema.RootElement.GetProperty("$defs").GetProperty("AgentDecision")
                .GetProperty("oneOf").EnumerateArray()
                .Single(item => item.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString() == "defer");
            Assert.True(deferSchema.GetProperty("properties").TryGetProperty("renewal", out _));
        }
    }

    [Fact]
    public void Explicit_BudgetReviewResponse_RoundTrips_At_Top_Level_For_All_Wire_Kinds()
    {
        var response = new BudgetReviewResponse("review-17", BudgetReviewOutcome.RequestRenewal,
            new AgentProgressAssessment("overview-17", "advancing", "open the discovered menu",
                "inspect the menu and capture its visible state", ["observed menu state"], ["obs:17"],
                DeclinedDimensions: ["rounds"], RetriggerCondition: "after the next evidence checkpoint",
                NextItemKey: "root:observed-menu"),
            new BudgetRenewalRequest(20, 0, "one new branch remains"));
        var options = ProductProtocolJson.CreateOptions();
        foreach (var original in Decisions().Select(row => (AgentDecision)row[0]!).Select(decision => decision switch
        {
            AgentDecision.Act act => act with { ReviewResponse = response },
            AgentDecision.NoAction noAction => noAction with { ReviewResponse = response },
            AgentDecision.Defer defer => defer with { ReviewResponse = response },
            AgentDecision.Policy policy => policy with { ReviewResponse = response },
            AgentDecision.Skip skip => skip with { ReviewResponse = response },
            _ => decision,
        }))
        {
            var wire = JsonSerializer.Serialize(original, options);
            using var document = JsonDocument.Parse(wire);
            Assert.True(document.RootElement.TryGetProperty("reviewResponse", out _));
            Assert.Equal("RequestRenewal", document.RootElement.GetProperty("reviewResponse")
                .GetProperty("outcome").GetString());
            Assert.False(document.RootElement.TryGetProperty("proposal", out var proposal)
                && proposal.TryGetProperty("reviewResponse", out _));
            var restored = JsonSerializer.Deserialize<AgentDecision>(wire, options)!;
            var actualResponse = restored switch
            {
                AgentDecision.Act act => act.ReviewResponse,
                AgentDecision.NoAction noAction => noAction.ReviewResponse,
                AgentDecision.Defer defer => defer.ReviewResponse,
                AgentDecision.Policy policy => policy.ReviewResponse,
                AgentDecision.Skip skip => skip.ReviewResponse,
                _ => null,
            };
            Assert.Equal(response.ReviewId, actualResponse?.ReviewId);
            Assert.Equal(response.Outcome, actualResponse?.Outcome);
            Assert.Equal("root:observed-menu", actualResponse?.Assessment?.NextItemKey);
            Assert.Equal(response.Renewal, actualResponse?.Renewal);
            Assert.Equal(response.Assessment?.OverviewId, actualResponse?.Assessment?.OverviewId);
            Assert.Equal(response.Assessment?.ExpectedObservableEvidence,
                actualResponse?.Assessment?.ExpectedObservableEvidence);
            Assert.Equal(response.Assessment?.EvidenceRefs, actualResponse?.Assessment?.EvidenceRefs);
        }
    }

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
    public void Decision_Variants_RoundTrip(AgentDecision original)
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
    public async Task Replay_Provider_Covers_All_Decisions(AgentDecision expected)
    {
        var decisionId = AgentDecisionCorrelation.TryGetDecisionId(expected)!;
        var json = JsonSerializer.Serialize(expected, ProductProtocolJson.CreateOptions());
        await using var adapter = new DshAgentAdapter(
            DecisionChannelFixture.Replay(new Dictionary<string, string> { [decisionId] = json }),
            "product-session", "run-1");

        var actual = await adapter.ConsultAsync(Context(decisionId));

        Assert.Equal(expected.GetType(), actual?.GetType());
        Assert.Equal(decisionId, AgentDecisionCorrelation.TryGetDecisionId(actual!));
        Assert.Empty(adapter.Diagnostics);
    }

    [Fact]
    public async Task Accepted_Output_Remains_Traceable_To_Product_Session_Run_And_Request()
    {
        DecisionRequest? observed = null;
        var transport = DecisionChannelFixture.Open(request =>
        {
            observed = request;
            var decision = new AgentDecision.Policy(request.Context.DecisionId, new PolicyProposal(
                "policy-trace-1",
                new[] { new PolicyPredicate.ClaimEquals("wifi.state", "off") },
                new PolicyActionTemplate("switch", "wifi", "tap", "on"),
                new[] { new PolicyPredicate.ClaimEquals("wifi.state", "on") },
                new[] { new PolicyGuard.ObservationUnchanged("wifi.state", 2) },
                2, "traceable policy"));
            return new DecisionChannelResponse(request.RequestId, request.Generation, decision);
        });
        await using var adapter = new DshAgentAdapter(transport, "product-session-trace", "run-trace");

        var result = await adapter.ConsultAsync(Context("decision-trace", "run-trace"));

        Assert.NotNull(result);
        Assert.NotNull(observed);
        Assert.Equal("product-session-trace", observed!.ProductSessionId);
        Assert.Equal("run-trace", observed.ProductRunId);
        Assert.Equal("run-trace", observed.Context.RunId);
        Assert.Equal("decision-trace", observed.Context.DecisionId);
        var policy = Assert.IsType<AgentDecision.Policy>(result);
        Assert.Equal("decision-trace", policy.DecisionId);
        Assert.Equal("policy-trace-1", policy.Proposal.PolicyId);
        Assert.Empty(adapter.Diagnostics);
    }

    [Fact]
    public async Task TaskInitializationCapabilitySelection_IsOneShot()
    {
        var calls = 0;
        await using var adapter = new DshAgentAdapter(
            DecisionChannelFixture.Open(request =>
            {
                calls++;
                var decision = new AgentDecision.NoAction(new AgentNoActionProposal(
                    request.Context.DecisionId, "inspect-only"));
                return new DecisionChannelResponse(
                    request.RequestId,
                    request.Generation,
                    decision,
                    TaskInitialization: new AgentTaskInitialization(
                        new CapabilitySelectionPayload("runtime.language-inspector", "en")));
            }),
            "product-session-selection", "run-selection");

        var first = await adapter.ConsultAsync(Context("selection-1", "run-selection"));
        var selection = adapter.TakeInitialTaskInitialization()?.CapabilitySelection;
        var second = await adapter.ConsultAsync(Context("selection-2", "run-selection"));

        Assert.NotNull(first);
        Assert.Equal("runtime.language-inspector", selection?.CapabilityId);
        Assert.Null(adapter.TakeInitialTaskInitialization());
        Assert.Null(second);
        Assert.Contains(adapter.Diagnostics,
            diagnostic => diagnostic.Code == "capability-selection-not-initial");
        Assert.Equal(2, calls);
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
    [InlineData("{\"kind\":\"defer\",\"decisionId\":\"d\",\"spec\":{\"subject\":\"wifi\",\"maxRounds\":0}}")]
    [InlineData("{\"kind\":\"policy\",\"decisionId\":\"d\",\"proposal\":{\"policyId\":\"p\",\"match\":[],\"actionTemplate\":{\"targetRole\":\"switch\",\"effectClass\":\"tap\"},\"termination\":[],\"guards\":[],\"maxApplications\":0}}")]
    [InlineData("{\"kind\":\"skip\",\"decisionId\":\"d\",\"target\":\"wifi\",\"reason\":\"blocked\",\"evidenceRefs\":[]}")]
    [InlineData("{\"kind\":\"skip\",\"decisionId\":\"d\",\"target\":\"wifi\",\"reason\":\"blocked\",\"evidenceRefs\":[\"\"]}")]
    [InlineData("{\"kind\":\"act\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"steps\":[{\"targetRole\":\"button\",\"effectClass\":\"tap\"}]},\"reviewResponse\":{\"reviewId\":\"r\",\"outcome\":\"Maybe\"}}")]
    [InlineData("{\"kind\":\"noAction\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"justification\":\"ok\"},\"reviewResponse\":{\"reviewId\":\"r\",\"outcome\":\"Replan\",\"unknown\":true}}")]
    [InlineData("{\"kind\":\"noAction\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"justification\":\"ok\"},\"reviewResponse\":{\"reviewId\":\"r\",\"outcome\":\"RequestRenewal\",\"renewal\":{\"additionalRounds\":2}}}")]
    [InlineData("{\"kind\":\"act\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"steps\":[{\"targetRole\":\"button\",\"effectClass\":\"tap\"}],\"renewal\":{\"additionalSteps\":20,\"additionalRounds\":0}},\"reviewResponse\":{\"reviewId\":\"r\",\"outcome\":\"RequestRenewal\",\"renewal\":{\"additionalSteps\":20,\"additionalRounds\":0}}}")]
    [InlineData("{\"kind\":\"act\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"steps\":[{\"targetRole\":\"button\",\"effectClass\":\"tap\"}]},\"reviewResponse\":{\"reviewId\":\"r\",\"outcome\":\"ContinueWithoutRenewal\"}}")]
    [InlineData("{\"kind\":\"act\",\"decisionId\":\"d\",\"proposal\":{\"decisionId\":\"d\",\"steps\":[{\"targetRole\":\"button\",\"effectClass\":\"tap\"}]},\"reviewResponse\":{\"reviewId\":\"r\",\"outcome\":\"RequestRenewal\",\"assessment\":{\"overviewId\":\"o\",\"judgment\":\"progress\",\"expectedObservableEvidence\":[],\"evidenceRefs\":[]}}}")]
    public void Malformed_Decision_Payload_Is_Rejected(string payload)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecision>(
            payload, ProductProtocolJson.CreateOptions()));
    }

    [Fact]
    public void Generated_Schema_Expresses_Bounded_Decision_Inputs()
    {
        using var document = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        var definitions = document.RootElement.GetProperty("$defs");
        var maxRounds = definitions.GetProperty("ObserveSpec").GetProperty("properties").GetProperty("maxRounds");
        var maxApplications = definitions.GetProperty("PolicyProposal").GetProperty("properties").GetProperty("maxApplications");
        Assert.Equal("integer", maxRounds.GetProperty("type").GetString());
        Assert.Equal(1, maxRounds.GetProperty("minimum").GetInt32());
        Assert.Equal("integer", maxApplications.GetProperty("type").GetString());
        Assert.Equal(1, maxApplications.GetProperty("minimum").GetInt32());
    }

    [Fact]
    public void Generated_Schema_Exposes_Explicit_BudgetReviewResponse_At_Union_Top_Level()
    {
        using var document = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        var definitions = document.RootElement.GetProperty("$defs");
        var response = definitions.GetProperty("BudgetReviewResponse");
        Assert.Contains("outcome", response.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString()));
        Assert.Contains("assessment", response.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString()));
        Assert.Equal(new[] { "CollectEvidence", "ContinueWithoutRenewal", "Replan", "RequestRenewal", "SubmitCompletion" },
            response.GetProperty("properties").GetProperty("outcome").GetProperty("enum").EnumerateArray()
                .Select(value => value.GetString()).Order(StringComparer.Ordinal));
        var act = definitions.GetProperty("AgentDecision").GetProperty("oneOf").EnumerateArray()
            .Single(variant => variant.GetProperty("properties").GetProperty("kind")
                .GetProperty("const").GetString() == "act");
        Assert.Equal("BudgetReviewResponse",
            act.GetProperty("properties").GetProperty("reviewResponse").GetProperty("anyOf")[0]
                .GetProperty("$ref").GetString()!.Split('/').Last());
    }

    [Fact]
    public void Generated_Schema_Exposes_Optional_Element_State_For_Safe_Actions()
    {
        using var document = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        var elementSummary = document.RootElement.GetProperty("$defs").GetProperty("ElementSummary");
        var state = elementSummary.GetProperty("properties").GetProperty("state");

        Assert.Equal("string", state.GetProperty("type").GetString());
        Assert.DoesNotContain("state", elementSummary.GetProperty("required")
            .EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void Generated_Schema_Exposes_PerCall_AvailableCapabilities()
    {
        using var document = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        var context = document.RootElement.GetProperty("$defs").GetProperty("AgentDecisionContext");
        var available = context.GetProperty("properties").GetProperty("availableCapabilities");

        Assert.Equal("object", available.GetProperty("type").GetString());
        Assert.Equal("CapabilityProfileReport",
            available.GetProperty("additionalProperties").GetProperty("$ref").GetString()!.Split('/').Last());
        Assert.DoesNotContain("availableCapabilities",
            context.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void Generated_Schema_Exposes_Trajectory_Context_Projections_And_Skip()
    {
        using var document = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        var definitions = document.RootElement.GetProperty("$defs");
        var context = definitions.GetProperty("AgentDecisionContext");
        Assert.Contains("heartbeat", context.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name));
        Assert.Contains("contextReferenceIndex", context.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name));
        Assert.Contains("casePacket", context.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name));
        Assert.Contains("executionOverview", context.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name));
        Assert.Contains("budgetReview", context.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name));
        var skip = document.RootElement.GetProperty("$defs").GetProperty("AgentDecision")
            .GetProperty("oneOf").EnumerateArray()
            .Single(variant => variant.GetProperty("properties").GetProperty("kind")
                .GetProperty("const").GetString() == "skip");
        Assert.Equal("string", skip.GetProperty("properties").GetProperty("target").GetProperty("type").GetString());
        Assert.Contains("evidenceRefs", skip.GetProperty("required").EnumerateArray()
            .Select(value => value.GetString()));
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

    private static AgentDecisionContext Context(string decisionId, string runId = "run-1") => new(
        decisionId, runId, "v0", "objective", new HashSet<string> { "tap" },
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
        yield return new object[]
        {
            new AgentDecision.Skip("decision-5", "wifi", "already covered", new[] { "ref-1" })
        };
    }
}
