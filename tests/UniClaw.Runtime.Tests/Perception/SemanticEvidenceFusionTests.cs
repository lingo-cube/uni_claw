using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;
using UniClaw.Runtime.Model;
using Xunit;
using SemanticEvidence = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// T1–T10: minimal SemanticEvidence Fusion runtime integration proofs.
/// Confirms the void boundaries: Semantic stays an Evidence Provider, Runtime
/// Evidence Fusion is the sole consumer, confidence is a weight (never Truth),
/// stale evidence is rejected, and no Agent/Vision/Resolver behavior changes.
/// </summary>
public sealed class SemanticEvidenceFusionTests
{
    private static Observation Obs(long seq) =>
        new(ImmutableArray<ObservedElement>.Empty, "Foreground", seq);

    private static SemanticEvidence Ev(
        string id,
        long sequence,
        double confidence,
        SemanticEvidenceScope scope = SemanticEvidenceScope.CurrentObservation,
        DateTimeOffset? validUntil = null) =>
        new(
            evidenceId: id,
            version: "1",
            source: "Vector",
            kind: SemanticEvidenceKind.ContainerIdentity,
            candidate: "DeveloperOptions",
            confidence: confidence,
            scope: scope,
            observationSequence: sequence,
            createdAt: DateTimeOffset.UtcNow,
            validUntil: validUntil);

    private static SemanticEvidenceFusionInput Input(long sequence,
        SemanticEvidence? evidence = null) =>
        new(
            currentObservation: Obs(sequence),
            semanticEvidence: evidence is null
                ? ImmutableArray<SemanticEvidence>.Empty
                : ImmutableArray.Create(evidence));

    // T1: Empty SemanticEvidence → Runtime behavior unchanged
    [Fact]
    public void T1_EmptySemanticEvidence_BehaviorUnchanged()
    {
        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(Input(5));

        Assert.Empty(result.AcceptedEvidence);
        Assert.Empty(result.RejectedEvidence);
        Assert.Empty(result.ValidationReasons);
        Assert.Empty(result.ConfidenceWeights);
    }

    // T2: Fresh SemanticEvidence accepted
    [Fact]
    public void T2_FreshSemanticEvidence_Accepted()
    {
        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(Input(5, Ev("e1", 5, 0.9)));

        var accepted = Assert.Single(result.AcceptedEvidence);
        Assert.Equal("e1", accepted.EvidenceId);
        Assert.Empty(result.RejectedEvidence);
    }

    // T3: Stale (expired) SemanticEvidence rejected
    [Fact]
    public void T3_StaleSemanticEvidence_Rejected()
    {
        var fusion = new SemanticEvidenceFusion();
        var expired = Ev("e1", 5, 0.9, validUntil: DateTimeOffset.UtcNow.AddMinutes(-1));
        var result = fusion.Fuse(Input(5, expired));

        Assert.Empty(result.AcceptedEvidence);
        var rejected = Assert.Single(result.RejectedEvidence);
        var reason = Assert.Single(result.ValidationReasons);
        Assert.Equal("e1", rejected.EvidenceId);
        Assert.Equal(SemanticEvidenceRejectionReason.StaleExpired, reason.Reason);
    }

    // T4: Wrong ObservationSequence rejected
    [Fact]
    public void T4_WrongObservationSequence_Rejected()
    {
        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(Input(5, Ev("e1", 4, 0.9)));

        Assert.Empty(result.AcceptedEvidence);
        var reason = Assert.Single(result.ValidationReasons);
        Assert.Equal(SemanticEvidenceRejectionReason.StaleObservationSequence, reason.Reason);
    }

    // T5: Confidence not converted to Truth — only carried as EvidenceWeight
    [Fact]
    public void T5_Confidence_IsWeight_NotTruth()
    {
        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(Input(5, Ev("e1", 5, 0.91)));

        var weight = Assert.Single(result.ConfidenceWeights);
        Assert.Equal("e1", weight.EvidenceId);
        Assert.Equal(0.91, weight.Weight);

        // No truth/belief field is produced by the fusion result.
        foreach (var prop in typeof(ValidatedSemanticEvidenceResult).GetProperties())
        {
            Assert.DoesNotMatch("(Truth|Belief|Fact)", prop.Name);
        }
    }

    // T6: SemanticEvidence cannot bypass Runtime — output has no action/goal/world
    [Fact]
    public void T6_SemanticEvidence_CannotBypassRuntime()
    {
        var resultType = typeof(ValidatedSemanticEvidenceResult);
        foreach (var prop in resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotMatch("(Action|Goal|Plan|World)", prop.Name);
        }
    }

    // T7: No SemanticProvider still works (NoOp returns empty)
    [Fact]
    public async Task T7_NoSemanticProvider_StillWorks()
    {
        var pipeline = new SemanticEvidenceFusionPipeline();
        var result = await pipeline.ResolveAndFuseAsync(Input(5));

        Assert.Empty(result.AcceptedEvidence);
        Assert.Empty(result.RejectedEvidence);
    }

    // T8: ContainerIdentity interface exists but no behavior replacement
    [Fact]
    public void T8_ContainerIdentityInterface_Exists_NoBehaviorReplacement()
    {
        var iface = typeof(IContainerIdentityEvidenceFusion);
        Assert.True(iface.IsInterface);

        var concrete = iface.Assembly.GetTypes()
            .Where(t => !t.IsInterface && !t.IsAbstract && iface.IsAssignableFrom(t))
            .ToList();
        // Interface exists as a reserved seam; no concrete resolver replaces logic.
        Assert.Empty(concrete);
    }

    // T9: Vision-only path unchanged — no semantic, nothing changes
    [Fact]
    public void T9_VisionOnlyPath_Unchanged()
    {
        var fusion = new SemanticEvidenceFusion();
        var result = fusion.Fuse(Input(5));

        Assert.Empty(result.AcceptedEvidence);
        Assert.Empty(result.RejectedEvidence);
        Assert.Empty(result.ConfidenceWeights);
    }

    // T10: Agent receives only Runtime Belief result — fusion never emits a
    // decision-shaped output for an Agent to consume.
    [Fact]
    public async Task T10_AgentReceivesOnly_RuntimeBeliefResult()
    {
        var pipeline = new SemanticEvidenceFusionPipeline();
        var result = await pipeline.ResolveAndFuseAsync(Input(5, Ev("e1", 5, 0.8)));

        // The only output is validated evidence + weights — not an action/goal/plan.
        var accepted = Assert.Single(result.AcceptedEvidence);
        Assert.Equal("e1", accepted.EvidenceId);
        var weight = Assert.Single(result.ConfidenceWeights);
        Assert.Equal(0.8, weight.Weight);

        // No decision-shaped field leaks to the Agent consumer.
        foreach (var prop in typeof(ValidatedSemanticEvidenceResult).GetProperties())
        {
            Assert.DoesNotMatch("(Action|GoalDecision|Plan|WorldMutation)", prop.Name);
        }
    }
}
