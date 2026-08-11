using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Focused behavior-preservation proofs for RC2-02 through RC2-05.</summary>
public sealed class RuntimeInternalComponentizationTests
{
    [Fact]
    public void BindingReconciler_PreservesSupportedIndicesAndSourceBasis()
    {
        var obj = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
        var evidence = ImmutableArray.Create(
            new BindingEvidence(
                "WifiConnectivity",
                [3],
                new SemanticEvidence(
                    "TEXT_IDENTITY",
                    "binds to WifiConnectivity",
                    SemanticEvidenceStance.Supports,
                    "text matches")),
            new BindingEvidence(
                "WifiConnectivity",
                [3, 5],
                new SemanticEvidence(
                    "SPATIAL_RELATION",
                    "binds to WifiConnectivity",
                    SemanticEvidenceStance.Supports,
                    "share a row")),
            new BindingEvidence(
                "WifiConnectivity",
                [9],
                new SemanticEvidence(
                    "IGNORED",
                    "binds to WifiConnectivity",
                    SemanticEvidenceStance.Insufficient,
                    "not supporting evidence")));

        var binding = Assert.Single(BindingReconciler.Reconcile(evidence, [obj]));

        Assert.Equal("WifiConnectivity", binding.ObjectIdentity);
        Assert.Equal([3, 5], binding.ElementIndices);
        Assert.Equal("TEXT_IDENTITY+SPATIAL_RELATION", binding.EvidenceBasis);
    }

    [Fact]
    public void StateBeliefReducer_ExactlyOneCurrentToggle_ProducesBelief()
    {
        var observation = new Observation(
            [new ObservedElement("", true, 5, null, "toggle")],
            "settings",
            2);
        var binding = new ObjectBinding("WifiConnectivity", [5], "SPATIAL_RELATION");

        var beliefs = StateBeliefReducer.Reduce(observation, [binding]);

        Assert.True(beliefs["WifiConnectivity.Enabled"]);
    }

    [Fact]
    public void StateBeliefReducer_MissingOrStaleToggle_RemainsUnknown()
    {
        var current = new Observation(
            [new ObservedElement("Wi-Fi", null, 1, null, "menuItem")],
            "settings",
            3);
        var staleBinding = new ObjectBinding("WifiConnectivity", [7], "OLD_OBSERVATION");

        var beliefs = StateBeliefReducer.Reduce(current, [staleBinding]);

        Assert.Null(beliefs["WifiConnectivity.Enabled"]);
    }

    [Fact]
    public void StateBeliefReducer_AmbiguousCurrentToggles_RemainsUnknown()
    {
        var observation = new Observation(
            [
                new ObservedElement("", true, 2, null, "toggle"),
                new ObservedElement("", false, 3, null, "toggle"),
            ],
            "settings",
            4);
        var binding = new ObjectBinding("WifiConnectivity", [2, 3], "AMBIGUOUS");

        var beliefs = StateBeliefReducer.Reduce(observation, [binding]);

        Assert.Null(beliefs["WifiConnectivity.Enabled"]);
    }

    [Fact]
    public void SemanticActionLowerer_MatchesTraversalCompatibilitySurface()
    {
        var bounds = new ElementBounds(0.7f, 0.2f, 0.9f, 0.3f);
        var observation = new Observation(
            [new ObservedElement("", false, 4, bounds, "toggle")],
            "settings",
            5);
        var binding = new ObjectBinding("WifiConnectivity", [4], "SPATIAL_RELATION");
        var action = new SemanticAction("WifiConnectivity", "SetEnabled", "Enabled", true);

        var extracted = SemanticActionLowerer.Lower(action, binding, observation);
        var compatibility = RuntimeTraversal.LowerAction(action, binding, observation);

        Assert.Equal(compatibility, extracted);
        var dispatched = Assert.IsType<SemanticActionResult.Dispatched>(extracted);
        var setSwitch = Assert.IsType<DeviceAction.SetSwitch>(dispatched.Action);
        Assert.Equal(4, setSwitch.TargetElementIndex);
        Assert.Equal(bounds, setSwitch.TargetBounds);
    }

    [Fact]
    public void TargetGrounder_CriterionFailure_NeverFallsBackToTextGrounding()
    {
        var observation = new Observation(
            [new ObservedElement("WiFi", null, 0)],
            "settings",
            1);
        var criterion = new TargetGroundingCriterion(
            (_, _) => new TargetGroundingEvidence(false, "criterion rejected"),
            _ => new TargetGroundingEvidence(true, "unused"));
        var receipts = ImmutableDictionary<int, CandidateAuthorizationEvidence>.Empty
            .Add(0, new CandidateAuthorizationEvidence(true, "authorized"));

        var selected = TargetGrounder.GroundCriterion(
            "WiFi",
            observation,
            observation.Elements,
            criterion,
            receipts,
            out var failure);

        Assert.Null(selected);
        Assert.Contains("insufficient", failure);
        Assert.Equal(0, TargetGrounder.Ground("WiFi", "Tap", observation.Elements));
    }
}
