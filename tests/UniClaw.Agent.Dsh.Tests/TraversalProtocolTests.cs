using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Dsh.Tests.Fixtures;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class TraversalProtocolTests
{
    [Fact]
    public async Task Traverse_Producer_Codec_Consumer_And_Nested_Context_RoundTrip()
    {
        var options = ProductProtocolJson.CreateOptions();
        var expected = Decision();
        var decisionWire = JsonSerializer.Serialize<AgentDecision>(expected, options);

        using var decisionDocument = JsonDocument.Parse(decisionWire);
        var readOnly = decisionDocument.RootElement.GetProperty("proposal").GetProperty("items")[0]
            .GetProperty("rules")[0].GetProperty("operation");
        Assert.Equal("ReadOnly", readOnly.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, readOnly.GetProperty("step").ValueKind);

        var traversal = TraversalViewWithFinding();
        var context = Context("traverse-consumer") with
        {
            PlanExecution = new PlanExecutionView(
                new PlanExecutionPosition("plan-traverse", 1, 0, 0, 0, null, 3),
                PlanExecutionStatus.Active, "inspect current menu", null,
                Array.Empty<PlanAttempt>(), Array.Empty<PlanBlockedEvidence>(),
                Traversal: traversal),
        };
        var contextWire = JsonSerializer.Serialize(context, options);
        var contextRead = JsonSerializer.Deserialize<AgentDecisionContext>(contextWire, options)!;
        Assert.Equal("lang-before", contextRead.PlanExecution?.Traversal?.Handlers[0].HandlerId);
        Assert.Equal(FindingDisposition.Pass,
            contextRead.PlanExecution?.Traversal?.Handlers[0].Result?.Disposition);
        Assert.Equal(CapabilityStatus.Completed,
            contextRead.PlanExecution?.Traversal?.Handlers[0].Result?.Status);
        var operationRecord = Assert.Single(contextRead.PlanExecution!.Traversal!.Operations!);
        Assert.Equal("wifi.state=unchecked", operationRecord.ObservedState);
        Assert.Equal("work-op-read-1", operationRecord.OperationId);
        Assert.Equal("op-read-template", operationRecord.TemplateId);
        Assert.Equal(2, contextRead.PlanExecution.Traversal.KnownRemainingWork);

        await using var adapter = new DshAgentAdapter(
            DecisionChannelFixture.Replay(new Dictionary<string, string> { ["traverse-consumer"] = decisionWire }),
            "product-session", "run-1");
        var actual = await adapter.ConsultAsync(contextRead);
        Assert.True(actual is not null, string.Join("; ", adapter.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        var plan = Assert.IsType<AgentDecision.Plan>(actual);
        var item = Assert.IsType<PlanItem.TraverseItem>(Assert.Single(plan.Proposal.Items));
        Assert.Equal("settings.current-navigation", item.Source);
        Assert.Equal(2, item.Rules.Count);
        Assert.Equal(TraversalOperationKind.Navigate, item.Rules[1].Operation.Kind);
        Assert.Equal("runtime.language-inspector", item.Rules[1].Operation.AfterVerified[0].CapabilityId);
        Assert.Empty(adapter.Diagnostics);

        using var schema = JsonDocument.Parse(ProductProtocolSchema.Current.Json);
        var branches = schema.RootElement.GetProperty("$defs").GetProperty("PlanItem")
            .GetProperty("oneOf").EnumerateArray();
        Assert.Contains(branches, branch => branch.GetProperty("properties").GetProperty("kind")
            .GetProperty("const").GetString() == "traverse");
        Assert.Equal("1.0", schema.RootElement.GetProperty("$defs").GetProperty("PlanLanguageHandler")
            .GetProperty("properties").GetProperty("protocolVersion").GetProperty("const").GetString());
    }

    [Fact]
    public void Generated_TypeScript_Expresses_ReadOnly_Step_As_Required_Null()
    {
        var generated = ProductProtocolSchemaGenerator.GenerateTypeScript();

        Assert.Contains("kind: 'ReadOnly'", generated);
        Assert.Contains("step: null", generated);
        Assert.DoesNotContain("kind: 'ReadOnly'; operationId: string; step: unknown", generated);
    }

    [Theory]
    [InlineData(PlanHandlerCallStatus.Running)]
    [InlineData(PlanHandlerCallStatus.Cancelled)]
    public void Async_Handler_Call_Association_And_Status_RoundTrip(PlanHandlerCallStatus status)
    {
        var record = new PlanHandlerRecord("op-read", "lang-before", "Before", "world-r1",
            status, null, status == PlanHandlerCallStatus.Cancelled ? "run-cancelled" : null,
            "call-async-1");
        var traversal = TraversalViewWithFinding() with { Handlers = [record] };
        var context = Context("async-handler-view") with
        {
            PlanExecution = new PlanExecutionView(
                new PlanExecutionPosition("plan-traverse", 1, 0, 0, 0, null, 3),
                PlanExecutionStatus.Active, null, null, [], [], Traversal: traversal),
        };
        var options = ProductProtocolJson.CreateOptions();
        var wire = JsonSerializer.Serialize(context, options);
        var read = JsonSerializer.Deserialize<AgentDecisionContext>(wire, options)!;
        var actual = Assert.Single(read.PlanExecution!.Traversal!.Handlers);
        Assert.Equal(record, actual);
        using var document = JsonDocument.Parse(wire);
        var handler = document.RootElement.GetProperty("planExecution").GetProperty("traversal")
            .GetProperty("handlers")[0];
        Assert.Equal(status.ToString(), handler.GetProperty("status").GetString());
        Assert.Equal("call-async-1", handler.GetProperty("callId").GetString());
        Assert.False(handler.TryGetProperty("result", out _));
    }

    [Theory]
    [InlineData("missing source", "source")]
    [InlineData("wrong maxDepth type", "maxDepth-type")]
    [InlineData("unknown item kind", "unknown-kind")]
    [InlineData("invalid capability binding", "capability")]
    [InlineData("unsupported language protocol version", "version")]
    [InlineData("ReadOnly step is not null", "readonly-step")]
    [InlineData("Navigate step is missing", "navigate-step")]
    [InlineData("depth out of bounds", "depth")]
    [InlineData("unknown traversal operation kind", "operation-kind")]
    [InlineData("duplicate handler identity across phases", "duplicate-handler")]
    [InlineData("reserved runtime operation identity", "reserved-operation")]
    public void Traverse_Codec_Rejects_Invalid_Wire(string _, string mutation)
    {
        var root = JsonNode.Parse(DecisionWire())!;
        var traverse = root["proposal"]!["items"]![0]!;
        var readonlyOperation = traverse["rules"]![0]!["operation"]!;
        var navigateOperation = traverse["rules"]![1]!["operation"]!;
        switch (mutation)
        {
            case "source": traverse.AsObject().Remove("source"); break;
            case "maxDepth-type": traverse["maxDepth"] = "deep"; break;
            case "unknown-kind": traverse["kind"] = "execute"; break;
            case "capability": readonlyOperation["before"]![0]!["capabilityId"] = "runtime.anything"; break;
            case "version": navigateOperation["afterVerified"]![0]!["protocolVersion"] = "2.0"; break;
            case "readonly-step": readonlyOperation["step"] = new JsonObject
                { ["targetRole"] = "menu", ["effectClass"] = "tap" }; break;
            case "navigate-step": navigateOperation.AsObject().Remove("step"); break;
            case "depth": traverse["maxDepth"] = 17; break;
            case "operation-kind": readonlyOperation["kind"] = "Execute"; break;
            case "duplicate-handler": navigateOperation["afterVerified"]![0]!["handlerId"] = "lang-before"; break;
            case "reserved-operation": navigateOperation["operationId"] = "$return"; break;
        }

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecision>(
            root.ToJsonString(), ProductProtocolJson.CreateOptions()));
    }

    [Fact]
    public void Traverse_Writer_Rejects_Invalid_Typed_Binding_And_ReadOnly_Effect()
    {
        var valid = Assert.IsType<AgentDecision.Plan>(Decision());
        var traverse = Assert.IsType<PlanItem.TraverseItem>(valid.Proposal.Items[0]);
        var badBinding = traverse with
        {
            Rules = [traverse.Rules[0] with
            {
                Operation = traverse.Rules[0].Operation with
                {
                    Before = [new PlanLanguageHandler("bad", "runtime.anything", "1.0", "en")],
                },
            }, traverse.Rules[1]],
        };
        var badReadOnly = traverse with
        {
            Rules = [traverse.Rules[0] with
            {
                Operation = traverse.Rules[0].Operation with
                {
                    Step = new AgentActionStep("menu", "$candidate", "tap", null),
                },
            }, traverse.Rules[1]],
        };
        var duplicateHandler = traverse with
        {
            Rules = [traverse.Rules[0], traverse.Rules[1] with
            {
                Operation = traverse.Rules[1].Operation with
                {
                    AfterVerified = [traverse.Rules[1].Operation.AfterVerified[0] with { HandlerId = "lang-before" }],
                },
            }],
        };
        var reservedOperation = traverse with
        {
            Rules = [traverse.Rules[0] with
            {
                Operation = traverse.Rules[0].Operation with { OperationId = "$return" },
            }, traverse.Rules[1]],
        };
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
            valid with { Proposal = valid.Proposal with { Items = [badBinding] } }, ProductProtocolJson.CreateOptions()));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
            valid with { Proposal = valid.Proposal with { Items = [badReadOnly] } }, ProductProtocolJson.CreateOptions()));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
            valid with { Proposal = valid.Proposal with { Items = [duplicateHandler] } }, ProductProtocolJson.CreateOptions()));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
            valid with { Proposal = valid.Proposal with { Items = [reservedOperation] } }, ProductProtocolJson.CreateOptions()));
    }

    private static AgentDecision.Plan Decision() => new("traverse-consumer",
        new AgentPlanProposal([TraversalItem()], "traverse current menu", "plan-traverse", 1,
            "inspect and safely navigate", 24));

    private static PlanItem.TraverseItem TraversalItem()
    {
        var handler = new PlanLanguageHandler("lang-before", "runtime.language-inspector", "1.0", "en",
            Required: true, RequirePass: false);
        var after = handler with { HandlerId = "lang-after", RequirePass = true };
        return new PlanItem.TraverseItem("settings.current-navigation",
        [
            new TraversalRule("record-current", "always",
                new TraversalOperation("op-read", TraversalOperationKind.ReadOnly, null, [handler], [])),
            new TraversalRule("open-candidate", "candidate.openable",
                new TraversalOperation("op-navigate", TraversalOperationKind.Navigate,
                    new AgentActionStep("menu item", "$candidate", "tap", null), [], [after])),
        ], 4, new AgentActionStep("Navigate up", null, "tap", null));
    }

    private static string DecisionWire() => JsonSerializer.Serialize<AgentDecision>(Decision(), ProductProtocolJson.CreateOptions());

    private static TraversalExecutionView TraversalViewWithFinding()
    {
        var finding = new Finding("finding-1", new CapabilityCorrelation("run-1", operationId: "op-read"),
            "1.0", "runtime.language-inspector", FindingDisposition.Pass, CapabilityStatus.Completed,
            "the visible menu text conforms");
        return new TraversalExecutionView("plan-traverse", 1, PlanExecutionStatus.Active, "root", 0,
            1, 3, null,
            [new PlanHandlerRecord("op-read", "lang-before", "Before", "world-r1",
                PlanHandlerCallStatus.Completed, finding, null)], [], ["root:record-current"], true, null,
            [new TraversalOperationRecord("work-op-read-1", "op-read-template", "root", null,
                "wifi.state=unchecked", "world-r1", ["obs:state"], true)], KnownRemainingWork: 2);
    }

    private static AgentDecisionContext Context(string decisionId) => new(
        decisionId, "run-1", "v0", "inspect menu", new HashSet<string> { "tap" },
        new Dictionary<string, ClaimSummary>(), Array.Empty<AgentObligationView>(), AgentDecisionPhase.InitialPlanning);
}
