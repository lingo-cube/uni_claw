using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using UniClaw.Semantic.Settings;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class GenericSemanticCompositionTests
{
    [Fact]
    public async Task Decorative_icon_is_child_relation_only_and_does_not_become_unknown()
    {
        var observation = BuildObservation(
            new ObservedElement("", null, 0, new ElementBounds(.10f, .10f, .14f, .14f), "icon"),
            Parent(search: true));

        var batch = await Evaluate(observation);
        var relation = Assert.Single(batch.Accepted.Select(e => e.Candidate)
            .OfType<ContainerRelationCandidateEvidence>());
        Assert.Equal(ContainerRelationKind.Child, relation.RelationKind);
        Assert.Equal("settings.search-role", relation.RelatedContainer.SymbolId);
        Assert.Equal("icon", observation.Elements[0].PerceptionType);

        var primary = InteractionAffordanceAnalyzer.Analyze(
            observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) })
            .Where(e => e.EligibleForAuthorization)
            .ToArray();
        Assert.Empty(primary);
    }

    [Fact]
    public async Task Interactive_icon_keeps_child_relation_and_independent_navigation_affordance()
    {
        var childBounds = new ElementBounds(.10f, .10f, .14f, .14f);
        var observation = BuildObservation(
            new ObservedElement("", null, 0, childBounds, "icon"),
            Parent(search: false),
            new StructuredElementEvidence(
                "android.widget.ImageButton", null, true, false, false, true, true,
                childBounds, SourceNodeIdentity: "child", ParentSourceNodeIdentity: "parent"));

        var batch = await Evaluate(observation);
        Assert.Contains(batch.Accepted, e => e.Candidate is ContainerRelationCandidateEvidence
            { RelationKind: ContainerRelationKind.Child });
        Assert.Contains(batch.Accepted, e => e.Candidate is ElementAffordanceCandidateEvidence
            { AffordanceKind: ElementAffordanceKind.NavigationCandidate });

        var affordance = Assert.Single(InteractionAffordanceAnalyzer.Analyze(
                observation with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) })
            .Where(e => e.EligibleForAuthorization));
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, affordance.Classification);
        Assert.Equal("icon", observation.Elements[0].PerceptionType);
    }

    [Fact]
    public async Task Unrelated_icon_is_not_attached_to_nearby_container()
    {
        var context = Context(
            IconFacts("icon", new SemanticNormalizedBounds(.80, .80, .04, .04))
                .Concat(ParentFacts(search: true, bounds: new SemanticNormalizedBounds(.05, .05, .40, .20)))
                .ToArray());

        var result = await new SettingsSemanticCapability().InterpretAsync(context);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Ambiguous_parent_relation_fails_closed()
    {
        var context = Context(
            IconFacts("icon", new SemanticNormalizedBounds(.10, .10, .04, .04))
                .Concat(ParentFacts("parent-a", search: false, bounds: new SemanticNormalizedBounds(.05, .05, .40, .20)))
                .Concat(ParentFacts("parent-b", search: false, bounds: new SemanticNormalizedBounds(.05, .05, .40, .20)))
                .ToArray());

        var result = await new SettingsSemanticCapability().InterpretAsync(context);

        Assert.Empty(result);
    }

    private static async Task<SemanticCapabilityEvaluationBatch> Evaluate(Observation observation)
    {
        var projected = SemanticObservationFactProjector.Project(observation);
        return await new SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync(
            projected, projected.Observation, projected.Sources, DateTimeOffset.UnixEpoch);
    }

    private static Observation BuildObservation(ObservedElement visual, params StructuredElementEvidence[] structured)
    {
        return new Observation([visual], "com.android.settings", 1)
        {
            StructuredElements = structured.ToImmutableArray(),
            Sources =
            [
                new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, 1,
                    "frame-1", 100, 100, "vision", "vision"),
                new ObservationSourceMetadata(ObservationSourceTier.AuxiliaryStructured, true, 1,
                    "frame-1", 100, 100, "adb", "adb"),
            ],
        };
    }

    private static StructuredElementEvidence Parent(bool search) =>
        new("android.view.ViewGroup", search ? "com.android.settings:id/search_action_bar" : null,
            true, false, false, true, true,
            new ElementBounds(0, 0, 1, .40f), SourceNodeIdentity: "parent", ParentSourceNodeIdentity: "root");

    private static IEnumerable<SemanticObservationFact> IconFacts(string id, SemanticNormalizedBounds bounds) =>
    [
        new(id, SemanticObservationFactKind.Text, "vision", SemanticSourceTier.Primary,
            "vision-capture", 1, "frame-1", rawProviderType: "icon", rawText: ""),
        new(id, SemanticObservationFactKind.Geometry, "vision", SemanticSourceTier.Primary,
            "vision-capture", 1, "frame-1", bounds: bounds),
    ];

    private static SemanticObservationFact[] ParentFacts(
        string id = "parent", bool search = false, SemanticNormalizedBounds? bounds = null)
    {
        var b = bounds ?? new SemanticNormalizedBounds(0, 0, 1, .40);
        return
        [
            new SemanticObservationFact(id, SemanticObservationFactKind.ClassName, "adb", SemanticSourceTier.Auxiliary,
                "adb-capture", 1, "frame-1", rawClassName: "android.view.ViewGroup",
                rawResourceName: search ? "com.android.settings:id/search_action_bar" : null,
                clickable: true, parentOccurrenceId: "root"),
            new SemanticObservationFact(id, SemanticObservationFactKind.Geometry, "adb", SemanticSourceTier.Auxiliary,
                "adb-capture", 1, "frame-1", bounds: b, parentOccurrenceId: "root"),
        ];
    }

    private static ExternalSemanticCapabilityContext Context(params SemanticObservationFact[] facts) =>
        new(new SemanticObservationReference("observation:1", 1, "frame-1"),
            new[]
            {
                new SemanticSourceMetadata("vision", SemanticSourceTier.Primary, true, "frame-1"),
                new SemanticSourceMetadata("adb", SemanticSourceTier.Auxiliary, true, "frame-1"),
            }, facts: facts);
}
