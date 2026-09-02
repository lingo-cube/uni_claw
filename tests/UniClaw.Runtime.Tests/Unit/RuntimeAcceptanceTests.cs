using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class RuntimeAcceptanceTests
{
    [Fact]
    public void SplitViewportMaterializesOneSliceWithMultipleRegions()
    {
        var input = Input(
            regions:
            [
                Region("nav", new ElementBounds(0f, 0f, 0.25f, 1f), scroll: false, coverage: true),
                Region("media", new ElementBounds(0.25f, 0f, 1f, 1f), scroll: true, coverage: true),
            ],
            visual:
            [
                Visual("settings", "Settings", new ElementBounds(0.02f, 0.2f, 0.22f, 0.3f)),
                Visual("song", "Track", new ElementBounds(0.35f, 0.2f, 0.9f, 0.3f)),
            ]);

        var result = RuntimeAcceptance.Evaluate(input);

        Assert.True(result.Accepted);
        Assert.NotNull(result.Commit);
        Assert.Equal(2, result.Commit!.Slice.SpatialRegionRefs.Length);
        Assert.Equal(2, result.Commit.Slice.OccurrenceRefs.Length);
        Assert.All(result.Commit.Occurrences, occurrence => Assert.Equal(input.SliceRef, occurrence.SliceRef));
        Assert.Single(new[] { result.Commit.Slice });
    }

    [Fact]
    public void StructuredEvidenceCorroboratesMatchingVisualOccurrence()
    {
        var bounds = new ElementBounds(0.1f, 0.2f, 0.8f, 0.3f);
        var structured = Structured("structured:wifi", "WiFi", bounds, clickable: true);
        var result = RuntimeAcceptance.Evaluate(Input(
            visual: [Visual("wifi", "WiFi", bounds)],
            structured: [structured]));

        var occurrence = Assert.Single(result.Commit!.Occurrences);
        Assert.Equal(new StructuredEvidenceRef("structured:wifi"), Assert.Single(occurrence.CorroborationRefs));
        Assert.True(occurrence.StateHints.Clickable);
        Assert.Empty(result.Commit.UnmatchedAuxiliaryEvidence);
    }

    [Fact]
    public void StructuredEvidenceWithoutVisualMatchRemainsAuxiliaryAndCreatesNoOccurrence()
    {
        var result = RuntimeAcceptance.Evaluate(Input(
            visual: [],
            structured:
            [
                Structured(
                    "structured:offscreen",
                    "Hidden action",
                    new ElementBounds(0.1f, 0.7f, 0.8f, 0.8f),
                    clickable: true),
            ]));

        Assert.True(result.Accepted);
        Assert.Empty(result.Commit!.Occurrences);
        var auxiliary = Assert.Single(result.Commit.UnmatchedAuxiliaryEvidence);
        Assert.Equal(new StructuredEvidenceRef("structured:offscreen"), auxiliary.EvidenceRef);
        Assert.True(auxiliary.Evidence.Clickable);
    }

    [Fact]
    public void StructuredClickableConflictCannotMintVisualTruth()
    {
        var visualBounds = new ElementBounds(0.1f, 0.1f, 0.8f, 0.2f);
        var structuredBounds = new ElementBounds(0.1f, 0.7f, 0.8f, 0.8f);
        var result = RuntimeAcceptance.Evaluate(Input(
            visual: [Visual("title", "Title", visualBounds)],
            structured: [Structured("structured:button", "Different", structuredBounds, clickable: true)]));

        var occurrence = Assert.Single(result.Commit!.Occurrences);
        Assert.Empty(occurrence.CorroborationRefs);
        Assert.Null(occurrence.StateHints.Clickable);
        Assert.Single(result.Commit.UnmatchedAuxiliaryEvidence);
        Assert.Single(result.Commit.Slice.OccurrenceRefs);
    }

    [Fact]
    public void AtomicCommitAddsSliceOccurrencesAndFastAssessmentsTogether()
    {
        var input = Input(
            visual: [Visual("wifi", "WiFi", new ElementBounds(0.1f, 0.2f, 0.8f, 0.3f))],
            hypotheses:
            [
                new FastStructuralHypothesis(
                    "row:wifi",
                    ["wifi"],
                    FastStructureHint.ListItem,
                    FastMemberRoleHint.Primary,
                    FastAffordanceHint.Navigate,
                    "fast:test"),
            ]);

        var prepared = RuntimeAcceptance.Prepare(ContainerRuntimeV2State.Empty, input);

        Assert.True(prepared.CanCommit);
        var slice = Assert.Single(prepared.State.Slices);
        var occurrence = Assert.Single(prepared.State.Occurrences);
        var assessment = Assert.Single(prepared.State.FastAssessments);
        Assert.Equal(occurrence.OccurrenceRef, Assert.Single(slice.OccurrenceRefs));
        Assert.Equal(assessment.AssessmentRef, Assert.Single(slice.FastAssessmentRefs));
        Assert.Equal(occurrence.OccurrenceRef, Assert.Single(assessment.TargetOccurrenceRefs));
        Assert.Equal(new SemanticEvidenceRevision(1), prepared.State.EvidenceRevision);
    }

    [Fact]
    public void DanglingOrStaleAcceptanceRejectsWithExactPriorState()
    {
        var accepted = RuntimeAcceptance.Prepare(
            ContainerRuntimeV2State.Empty,
            Input(visual: [Visual("wifi", "WiFi", new ElementBounds(0.1f, 0.2f, 0.8f, 0.3f))]));
        var stale = RuntimeAcceptance.Prepare(accepted.State, Input());

        var danglingSlice = new ContainerSlice(
            new ContainerSliceRef("slice:dangling"),
            new SemanticEvidenceRevision(2),
            observationRef: "observation:2",
            viewportBounds: FullBounds,
            spatialRegionRefs: [new SpatialRegionRef("primary")],
            occurrenceRefs: [new ViewportOccurrenceRef("occurrence:missing")],
            stabilityEvidenceRef: new StabilityEvidenceRef("stability:2"));
        var dangling = ContainerRuntimeV2Reducer.PrepareAcceptedEvidence(
            accepted.State,
            new SliceAcceptanceCommit(danglingSlice, [PrimaryRegion], [], []));

        Assert.False(stale.CanCommit);
        Assert.Same(accepted.State, stale.State);
        Assert.Contains("stale", stale.RejectionReason!, StringComparison.OrdinalIgnoreCase);
        Assert.False(dangling.CanCommit);
        Assert.Same(accepted.State, dangling.State);
        Assert.Contains("dangling", dangling.RejectionReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettlingObservationCreatesNoSliceAndEmitsStructuredRejectionDiagnostic()
    {
        using var recorder = new RuntimeTraceRecorder("runtime-acceptance-test", "trace-runtime-acceptance-test");
        var input = Input(settled: false);

        var result = RuntimeAcceptance.Evaluate(input);
        var trace = recorder.Finalize();

        Assert.False(result.Accepted);
        Assert.Null(result.Commit);
        var span = Assert.Single(trace.Spans, value => value.Component == ObservabilityComponent.PerceptionAcceptance);
        var rejection = Assert.Single(span.Events, value => value.EventId == ObservabilityEvidenceEvent.SliceAcceptanceRejected);
        Assert.Contains(rejection.Attributes, value => value.Key == "observation.ref" && value.Value == "observation:1");
        Assert.Contains(rejection.Attributes, value => value.Key == "candidate.summary" && value.Value == "slice");
        Assert.Contains(rejection.Attributes, value => value.Key == "reject.reason" && value.Value?.Contains("settling", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(rejection.Attributes, value => value.Key == "validator.decision" && value.Value == "REJECT");
    }

    [Fact]
    public void UnknownVisualPrimitiveIsAcceptedAsUnknownNotRejected()
    {
        var result = RuntimeAcceptance.Evaluate(Input(
            visual: [Visual("novel", "Novel", new ElementBounds(0.1f, 0.2f, 0.8f, 0.3f), rawType: "future-widget")]));

        Assert.True(result.Accepted);
        Assert.Equal(VisualPrimitiveKind.Unknown, Assert.Single(result.Commit!.Occurrences).PrimitiveKind);
    }

    [Fact]
    public void StabilizerHintIsCompatibilityShadowNotASecondField()
    {
        var fromNewPath = new ObservedElement("WiFi", null, 0) { StabilizerHint = "row_001" };
        var fromLegacyPath = new ObservedElement("WiFi", null, 0) { StableKey = "row_002" };

        Assert.Equal("row_001", fromNewPath.StableKey);
        Assert.Equal("row_002", fromLegacyPath.StabilizerHint);
        Assert.DoesNotContain(typeof(ObservedElement).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
            field => field.Name.Contains("stableKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FastAssessmentRemainsHintOnly()
    {
        var result = RuntimeAcceptance.Evaluate(Input(
            visual: [Visual("wifi", "WiFi", new ElementBounds(0.1f, 0.2f, 0.8f, 0.3f))],
            hypotheses:
            [
                new FastStructuralHypothesis(
                    "row:wifi", ["wifi"], FastStructureHint.ListItem,
                    FastMemberRoleHint.Primary, FastAffordanceHint.Navigate, "fast:test"),
            ]));

        var assessment = Assert.Single(result.Commit!.FastAssessments);
        Assert.Equal(FastAffordanceHint.Navigate, assessment.AffordanceHint);
        Assert.DoesNotContain(assessment.GetType().GetProperties(), property =>
            property.Name.Contains("Identity", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Obligation", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Authoriz", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly ElementBounds FullBounds = new(0f, 0f, 1f, 1f);
    private static readonly SpatialRegion PrimaryRegion = Region("primary", FullBounds, scroll: true, coverage: true);

    private static RuntimeAcceptanceInput Input(
        bool settled = true,
        ImmutableArray<SpatialRegion>? regions = null,
        ImmutableArray<VisualOccurrenceCandidate>? visual = null,
        ImmutableArray<StructuredEvidenceCandidate>? structured = null,
        ImmutableArray<FastStructuralHypothesis>? hypotheses = null)
        => new(
            new ContainerSliceRef("slice:1"),
            new SemanticEvidenceRevision(1),
            FullBounds,
            new ViewportStabilityEvidence(
                "observation:1",
                1,
                new StabilityEvidenceRef("stability:1"),
                IsFresh: true,
                IsSettled: settled,
                settled ? "settled" : "settling animation"),
            regions ?? [PrimaryRegion],
            visual ?? [],
            structured ?? [],
            hypotheses ?? []);

    private static SpatialRegion Region(
        string reference,
        ElementBounds bounds,
        bool scroll,
        bool coverage)
        => new(
            reference,
            SpatialRegionKind.ScrollableContent,
            bounds,
            scroll,
            coverage,
            ParticipatesInGrounding: true);

    private static VisualOccurrenceCandidate Visual(
        string reference,
        string text,
        ElementBounds bounds,
        string rawType = "menu_item")
        => new(reference, $"vision:{reference}", text, rawType, bounds, $"row:{reference}");

    private static StructuredEvidenceCandidate Structured(
        string reference,
        string text,
        ElementBounds bounds,
        bool? clickable)
        => new(
            new StructuredEvidenceRef(reference),
            new StructuredElementEvidence(
                "android.widget.TextView",
                null,
                clickable,
                null,
                null,
                true,
                true,
                bounds,
                RawText: text,
                SourceNodeIdentity: reference));
}
