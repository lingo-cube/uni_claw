using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Deterministic tests for correction and checkpoint evidence projections.</summary>
public sealed class ContainerSemanticCorrectionTests
{
    [Fact]
    public void TraversalMisclickCombinesOwnerIntendedCWithSlowObservedDWithoutEffects()
    {
        var request = Request(7);
        var consumption = CurrentConsumption(
            request,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.WrongChild,
            actualTriggerSemantic: "D",
            observedContainerSemantic: "DPage",
            correctedIdentityCandidate: "D");

        var correction = ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption);
        var context = new ContainerObligationContext(
            new ContainerObligationContextRef("obligation:explore"),
            ContainerObligationContextKind.TraversalMisclick,
            "C");
        var input = ContainerSemanticCorrectionProjector.ProjectObligationInput(correction, context);

        Assert.NotNull(correction);
        Assert.Equal("D", correction!.ActualTriggerSemantic);
        Assert.Equal("DPage", correction.ObservedContainerSemantic);
        Assert.Equal("D", correction.CorrectedIdentityCandidate);
        Assert.NotNull(input);
        Assert.Equal("C", input!.IntendedPendingCandidate);
        Assert.Equal("D", input.ObservedActualCandidate);
        Assert.Equal("obligation:explore", input.OwnerContext.ContextRef.Value);
        Assert.True(input.RequiresOwnerReevaluation);
        Assert.False(input.HasAppliedObligationMutation);
        Assert.False(input.HasAction);
        Assert.False(input.HasRecovery);
        Assert.False(input.HasCompletion);
    }

    [Fact]
    public void DirectedEntryWrongBranchRequiresOwnerDecisionWithoutEmittingCommand()
    {
        var request = Request(8);
        var consumption = CurrentConsumption(
            request,
            SlowContainerSemanticAssessmentKind.Challenge,
            SlowContainerSceneKind.OffPath,
            actualTriggerSemantic: "ObservedBranch");
        var correction = ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption);
        var context = new ContainerObligationContext(
            new ContainerObligationContextRef("obligation:directed-entry"),
            ContainerObligationContextKind.DirectedEntryWrongBranch,
            "TargetBranch");
        var input = ContainerSemanticCorrectionProjector.ProjectObligationInput(correction, context);

        Assert.NotNull(input);
        Assert.True(input!.RequiresSeparateOwnerAuthorization);
        Assert.True(input.RequiresOwnerReevaluation);
        Assert.Equal("TargetBranch", input.IntendedPendingCandidate);
        Assert.Null(input.ObservedActualCandidate);
        Assert.False(input.HasAction);
        Assert.False(input.HasRecovery);
        Assert.False(input.HasCompletion);
    }

    [Theory]
    [InlineData(SlowContainerSemanticAvailability.Disabled, false, false)]
    [InlineData(SlowContainerSemanticAvailability.Unavailable, false, false)]
    [InlineData(SlowContainerSemanticAvailability.Stale, false, true)]
    [InlineData(SlowContainerSemanticAvailability.Rejected, false, false)]
    [InlineData(SlowContainerSemanticAvailability.Available, true, false)]
    public void OnlyCurrentAvailableSlowCorrectionCanProduceCurrentFact(
        SlowContainerSemanticAvailability availability,
        bool isCurrent,
        bool isStale)
    {
        var request = Request(9);
        var assessment = Assessment(
            request,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.WrongChild);
        var consumption = new SlowContainerSemanticConsumption(
            SlowContainerSemanticMode.Shadow,
            availability,
            availability == SlowContainerSemanticAvailability.Disabled ? null : assessment,
            isCurrent,
            isStale,
            false);

        var correction = ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption);

        if (availability == SlowContainerSemanticAvailability.Available)
            Assert.NotNull(correction);
        else
            Assert.Null(correction);
    }

    [Theory]
    [InlineData(SlowContainerSemanticAssessmentKind.Confirm)]
    [InlineData(SlowContainerSemanticAssessmentKind.Insufficient)]
    public void ConfirmAndInsufficientSlowAssessmentCannotProduceCorrection(
        SlowContainerSemanticAssessmentKind kind)
    {
        var request = Request(10);
        var consumption = CurrentConsumption(request, kind, SlowContainerSceneKind.Normal);

        Assert.Null(ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption));
    }

    [Fact]
    public void CorrectedAndObservedSemanticsCannotBeInjectedByOwnerContext()
    {
        var request = Request(11);
        var consumption = CurrentConsumption(
            request,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.WrongChild,
            actualTriggerSemantic: "D",
            observedContainerSemantic: "DPage",
            correctedIdentityCandidate: "D");
        var correction = ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption);
        var context = new ContainerObligationContext(
            new ContainerObligationContextRef("obligation:explore"),
            ContainerObligationContextKind.TraversalMisclick,
            "C");
        var input = ContainerSemanticCorrectionProjector.ProjectObligationInput(correction, context);

        Assert.NotNull(input);
        Assert.Equal("C", input!.IntendedPendingCandidate);
        Assert.Equal("D", input.ObservedActualCandidate);
        Assert.NotEqual("forged-observed", input.ObservedActualCandidate);
    }

    [Fact]
    public void DirectedEntryUsesAssessmentBranchCandidateInsteadOfTriggerCandidate()
    {
        var request = Request(12);
        var assessment = Assessment(
            request,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.OffPath,
            actualTriggerSemantic: "trigger-D",
            observedContainerSemantic: "DPage",
            correctedIdentityCandidate: "D");
        var correction = ContainerSemanticCorrectionProjector.TryCreateCorrection(CurrentConsumption(request, assessment));
        var context = new ContainerObligationContext(
            new ContainerObligationContextRef("obligation:directed-entry"),
            ContainerObligationContextKind.DirectedEntryWrongBranch,
            "C");

        var input = ContainerSemanticCorrectionProjector.ProjectObligationInput(correction, context);

        Assert.Equal("D", input!.ObservedActualCandidate);
        Assert.NotEqual("trigger-D", input.ObservedActualCandidate);
    }

    [Fact]
    public void MissingRequiredWrongChildReferencesFailClosed()
    {
        var request = new SlowContainerSemanticRequest("obs:13", new SemanticEvidenceRevision(13));
        var assessment = Assessment(request, SlowContainerSemanticAssessmentKind.Correct, SlowContainerSceneKind.WrongChild);
        var consumption = CurrentConsumption(request, assessment);

        Assert.Null(ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption));
    }

    [Fact]
    public void CorrectWithoutAssessmentObservedSemanticFailsClosed()
    {
        var request = Request(14);
        var assessment = Assessment(
            request,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.WrongChild,
            correctedIdentityCandidate: null,
            observedContainerSemantic: null,
            actualTriggerSemantic: null);

        Assert.Null(ContainerSemanticCorrectionProjector.TryCreateCorrection(CurrentConsumption(request, assessment)));
    }

    [Fact]
    public void CheckpointUsesOnlyCurrentSufficientCorrectPathNode()
    {
        var path = new ContainerExecutionPath(new[]
        {
            new ContainerPathConfirmation("obs:old", new SemanticEvidenceRevision(3), new ContainerNodeRef("node:old"), true, true),
            new ContainerPathConfirmation("obs:off", new SemanticEvidenceRevision(4), new ContainerNodeRef("node:off"), true, true, true),
            new ContainerPathConfirmation("obs:current", new SemanticEvidenceRevision(4), new ContainerNodeRef("node:current"), true, true),
        });

        var proposal = ContainerSemanticCorrectionProjector.ProjectCheckpoint(path, new SemanticEvidenceRevision(4));

        Assert.NotNull(proposal);
        Assert.Equal("node:current", proposal!.NodeRef.Value);
        Assert.Equal("obs:current", proposal.ObservationRef);
        Assert.Equal(new SemanticEvidenceRevision(4), proposal.EvidenceRevision);
    }

    [Fact]
    public void CheckpointLastDependsOnExplicitPathOrderAndRejectsStaleOrOffPath()
    {
        var path = new ContainerExecutionPath(new[]
        {
            new ContainerPathConfirmation("obs:first", new SemanticEvidenceRevision(5), new ContainerNodeRef("node:first"), true, true),
            new ContainerPathConfirmation("obs:last", new SemanticEvidenceRevision(5), new ContainerNodeRef("node:last"), true, true),
        });
        var orderedProposal = ContainerSemanticCorrectionProjector.ProjectCheckpoint(path, new SemanticEvidenceRevision(5));
        Assert.Equal("node:last", orderedProposal!.NodeRef.Value);

        var invalidPath = new ContainerExecutionPath(new[]
        {
            new ContainerPathConfirmation("obs:stale", new SemanticEvidenceRevision(3), new ContainerNodeRef("node:stale"), true, true),
            new ContainerPathConfirmation("obs:off", new SemanticEvidenceRevision(5), new ContainerNodeRef("node:off"), true, false),
            new ContainerPathConfirmation("obs:weak", new SemanticEvidenceRevision(5), new ContainerNodeRef("node:weak"), false, true),
        });
        Assert.Null(ContainerSemanticCorrectionProjector.ProjectCheckpoint(invalidPath, new SemanticEvidenceRevision(5)));
    }

    private static SlowContainerSemanticRequest Request(long revision)
        => new(
            $"obs:{revision}",
            new SemanticEvidenceRevision(revision),
            new ContainerNodeRef("node:destination"),
            new ContainerNodeRef("node:source"),
            "trigger:occurrence",
            new TransitionOccurrenceRef("transition:occurrence"));

    private static SlowContainerSemanticAssessment Assessment(
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessmentKind kind,
        SlowContainerSceneKind scene,
        string? actualTriggerSemantic = "C",
        string? observedContainerSemantic = "DPage",
        string? correctedIdentityCandidate = "D")
        => new(
            request.ObservationRef,
            request.EvidenceRevision,
            kind,
            scene,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef,
            kind == SlowContainerSemanticAssessmentKind.Correct ? correctedIdentityCandidate : null,
            containerSemantic: observedContainerSemantic,
            triggerSemantic: actualTriggerSemantic);

    private static SlowContainerSemanticConsumption CurrentConsumption(
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessmentKind kind,
        SlowContainerSceneKind scene,
        string? actualTriggerSemantic = "C",
        string? observedContainerSemantic = "DPage",
        string? correctedIdentityCandidate = "D")
        => CurrentConsumption(request, Assessment(
            request,
            kind,
            scene,
            actualTriggerSemantic,
            observedContainerSemantic,
            correctedIdentityCandidate));

    private static SlowContainerSemanticConsumption CurrentConsumption(
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessment assessment)
        => new(
            SlowContainerSemanticMode.Shadow,
            SlowContainerSemanticAvailability.Available,
            assessment,
            true,
            false,
            false);
}
