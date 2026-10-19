using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class ComposablePlanProtocolTests
{
    [Fact]
    public void DynamicPlan_RoundTripsThroughFormalDecisionChannel()
    {
        AgentDecision decision = new AgentDecision.Plan("decision-plan-1", new AgentPlanProposal(
            new PlanItem[]
            {
                new PlanItem.ForEachItem("settings.root-navigation", new PlanItem[]
                {
                    new PlanItem.ActItem("$candidate", "$candidate", "tap", null),
                    new PlanItem.ObserveItem(null),
                    new PlanItem.ActItem("ui.element", "Navigate up", "tap", null),
                }, 3),
                new PlanItem.CheckpointItem("review scoped results"),
            }, "inspect observed entries", "plan-menu", 1, "Inspect safe scoped entries and return", 40));
        var options = ProductProtocolJson.CreateOptions();
        var wire = JsonSerializer.Serialize(decision, options);
        var restored = Assert.IsType<AgentDecision.Plan>(JsonSerializer.Deserialize<AgentDecision>(wire, options));
        Assert.Equal("decision-plan-1", AgentDecisionCorrelation.TryGetDecisionId(restored));
        Assert.Equal("plan-menu", restored.Proposal.PlanId);
        Assert.Equal(1, restored.Proposal.Version);
        var loop = Assert.IsType<PlanItem.ForEachItem>(restored.Proposal.Items[0]);
        Assert.Equal("settings.root-navigation", loop.Source);
        Assert.Equal(3, loop.MaxIterations);
        Assert.Equal("$candidate", Assert.IsType<PlanItem.ActItem>(loop.Body[0]).TargetDescriptor);
        Assert.IsType<PlanItem.CheckpointItem>(restored.Proposal.Items[1]);
        Assert.Equal(wire, JsonSerializer.Serialize<AgentDecision>(restored, options));
    }
    [Fact]
    public void ConditionalBlocks_RoundTripThroughFormalChannelAndPreserveNestedResumePath()
    {
        var options = ProductProtocolJson.CreateOptions();
        AgentDecision decision = new AgentDecision.Plan("d-composed", new AgentPlanProposal(new PlanItem[]
        {
            new PlanItem.RepeatItem(new PlanItem[]
            {
                new PlanItem.IfItem("records.need-evidence",
                    new PlanItem[] { new PlanItem.ObserveItem("record:receipt") },
                    new PlanItem[]
                    {
                        new PlanItem.ForEachItem("records", new PlanItem[]
                        {
                            new PlanItem.IfItem("record.can-verify",
                                new PlanItem[] { new PlanItem.ActItem("$candidate", "$candidate", "verify-record", null) },
                                Array.Empty<PlanItem>()),
                        }, 2),
                    }),
            }, 3),
        }, "combine declared control operations", "composed-wire", MaxLocalOperations: 30));
        var wire = JsonSerializer.Serialize(decision, options);
        Assert.Contains("\"kind\":\"if\"", wire);
        Assert.Contains("\"kind\":\"repeat\"", wire);
        var restored = JsonSerializer.Deserialize<AgentDecision>(wire, options);
        Assert.Equal(wire, JsonSerializer.Serialize(restored, options));
        var position = new PlanExecutionPosition("composed-wire", 1, 1, 0, 0, "r17", 9, "0.0.0:repeat:2/0.0.0:then:0");
        AgentDecision continuation = new AgentDecision.ContinuePlan("d-resume", position);
        var resumed = Assert.IsType<AgentDecision.ContinuePlan>(JsonSerializer.Deserialize<AgentDecision>(JsonSerializer.Serialize(continuation, options), options));
        Assert.Equal(position, resumed.Position);
    }

    [Fact]
    public void ExplicitContinuation_PreservesEveryPositionField()
    {
        var position = new PlanExecutionPosition("plan-A", 3, 2, 4, 1, "record:next", 19);
        AgentDecision decision = new AgentDecision.ContinuePlan("decision-2", position);
        var options = ProductProtocolJson.CreateOptions();
        var restored = Assert.IsType<AgentDecision.ContinuePlan>(JsonSerializer.Deserialize<AgentDecision>(
            JsonSerializer.Serialize(decision, options), options));
        Assert.Equal(position, restored.Position);
        Assert.Equal("decision-2", AgentDecisionCorrelation.TryGetDecisionId(restored));
    }

    [Theory]
    [InlineData("{\"kind\":\"unknown\"}")]
    [InlineData("{\"kind\":\"act\",\"targetRole\":\"button\",\"effectClass\":\"tap\",\"coordinates\":[1,2]}")]
    [InlineData("{\"kind\":\"forEach\",\"source\":\"records\",\"body\":[{\"kind\":\"forEach\",\"source\":\"records\",\"body\":[],\"maxIterations\":1}],\"maxIterations\":1}")]
    public void UnknownOrMalformedOperations_AreRejectedByFormalCodec(string item)
    {
        var wire = "{\"kind\":\"plan\",\"decisionId\":\"d\",\"proposal\":{\"planId\":\"p\",\"version\":1,\"maxLocalOperations\":10,\"items\":[" + item + "]}}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecision>(wire, ProductProtocolJson.CreateOptions()));
    }

    [Fact]
    public void BlockedOutcome_RoundTripsWithoutAnEffectInstruction()
    {
        AgentDecision decision = new AgentDecision.Blocked("d-blocked", "candidate condition remains unknown");
        var options = ProductProtocolJson.CreateOptions();
        var restored = JsonSerializer.Deserialize<AgentDecision>(JsonSerializer.Serialize(decision, options), options);
        Assert.Equal(decision, restored);
    }
}
