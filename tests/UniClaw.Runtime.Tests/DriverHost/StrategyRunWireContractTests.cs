using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

public sealed class StrategyRunWireContractTests
{
    private const string ValidRequest = """
        {
          "strategy": {
            "strategyId": "wire-strategy-1",
            "contractVersion": 1,
            "objective": { "kind": "exploreScope" },
            "scope": {
              "applicationIdentity": "SampleApplication",
              "semanticRoot": "SampleRoot",
              "maximumDepth": 2
            },
            "exploration": "exhaustiveWithinScope",
            "constraints": {
              "allowedInteractionCategories": ["navigableContainer"],
              "prohibitedEffects": ["stateMutation", "externalBoundaryCrossing"]
            },
            "completion": { "kind": "exhaustiveCoverageWithinScope" },
            "adaptation": {
              "allowedAdaptations": ["reconcileBelief", "reviseExecutionHypothesis"]
            }
          },
          "device": "serial:sample-device"
        }
        """;

    [Fact]
    public void ClosedWireContract_ParsesTypedBoundedStrategy()
    {
        var request = UniClawStrategyRunStartWire.Parse(JsonNode.Parse(ValidRequest)!.AsObject());

        Assert.Equal("wire-strategy-1", request.Strategy.StrategyId);
        Assert.Equal(StrategyObjectiveKind.ExploreScope, request.Strategy.Objective.Kind);
        Assert.Equal(2, request.Strategy.Scope.MaximumDepth);
        Assert.Equal("serial:sample-device", request.Device.Key);
    }

    [Theory]
    [InlineData("actions")]
    [InlineData("route")]
    [InlineData("clickPlan")]
    public void ClosedWireContract_RejectsConcreteExecutionFields(string forbiddenField)
    {
        var json = JsonNode.Parse(ValidRequest)!.AsObject();
        json["strategy"]![forbiddenField] = new JsonArray();

        var error = Assert.Throws<ArgumentException>(() => UniClawStrategyRunStartWire.Parse(json));
        Assert.Contains("unsupported strategy field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWireContract_RejectsUnresolvedObjectiveProse()
    {
        var json = JsonNode.Parse(ValidRequest)!.AsObject();
        var objective = json["strategy"]!["objective"]!.AsObject();
        objective.Remove("kind");
        objective["text"] = "Find whatever seems important";

        Assert.Throws<ArgumentException>(() => UniClawStrategyRunStartWire.Parse(json));
    }

    [Fact]
    public void RejectedAdmissionWireReceipt_HasNoRunOrState()
    {
        var dto = UniClawStrategyRunStartWire.ToDto(
            StrategyRunAdmission.Reject(
                StrategyRejectionCode.UnsupportedCriterion,
                "Criterion is not supported."));

        Assert.False(dto.Accepted);
        Assert.Null(dto.RunId);
        Assert.Null(dto.RunState);
        Assert.Equal("unsupportedCriterion", dto.RejectionCode);
    }
}
