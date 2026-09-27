using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception.Fusion;

public sealed class FusionEngineTests
{
    private static FusionCapture Capture(string id, string cycle = "cycle-1", bool mutation = false) =>
        new(id, "session-1", cycle, DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMilliseconds(id == "v" ? 20 : 0), KnownMutationMarker: mutation);

    private static FusionSourceEvidence Source(string id, FusionSourceKind kind, string value,
        CoverageDisposition coverage = CoverageDisposition.CompleteWithinDeclaredSurface) =>
        new(id, kind, "node-1", "checked", value, Capture(id), coverage);

    private static AssociationCandidate Unique() => new("h-1", new[] { "v-1" }, new[] { "o-1" }, AssociationDisposition.Unique);

    [Fact]
    public void Align_requires_same_cycle_and_bounded_window()
    {
        var engine = new FusionEngine();
        var aligned = engine.Align(new[] { Source("h", FusionSourceKind.Hierarchy, "checked"), Source("v", FusionSourceKind.Visual, "checked") }, TimeSpan.FromSeconds(1));
        Assert.Equal(TemporalAlignmentDisposition.Aligned, aligned.Disposition);
        var unaligned = engine.Align(new[] { Source("h", FusionSourceKind.Hierarchy, "checked"), Source("v", FusionSourceKind.Visual, "checked") with { Capture = Capture("v", "cycle-2") } }, TimeSpan.FromSeconds(1));
        Assert.Equal(TemporalAlignmentDisposition.Unaligned, unaligned.Disposition);
    }

    [Fact]
    public void Hierarchy_authority_preserves_overruled_visual_conflict()
    {
        var result = new FusionEngine().Fuse(new("node-1", "checked", new[] {
            Source("h", FusionSourceKind.Hierarchy, "unchecked"), Source("v", FusionSourceKind.Visual, "checked")
        }, Unique(), TimeSpan.FromSeconds(1)));
        Assert.Equal(FusionDisposition.Supported, result.Disposition);
        Assert.Equal(ConflictDisposition.OverruledSource, result.Conflict);
        Assert.Equal(FusionSourceKind.Hierarchy, result.AuthoritySource);
    }

    [Fact]
    public void Missing_correlation_is_temporal_unknown_and_not_admissible()
    {
        var result = new FusionEngine().Fuse(new("node-1", "checked", new[] {
            Source("h", FusionSourceKind.Hierarchy, "checked") with { Capture = new("h", "session-1", null, DateTimeOffset.UtcNow) }
        }, Unique(), TimeSpan.FromSeconds(1)));
        Assert.Equal(TemporalAlignmentDisposition.TemporalUnknown, result.Alignment.Disposition);
        Assert.False(result.IsAdmissible);
    }

    [Fact]
    public void Shared_leaf_is_deduplicated_and_cycle_is_rejected()
    {
        var graph = new Dictionary<string, EvidenceNode>
        {
            ["a"] = new("a", Array.Empty<string>(), IsSourceEvidence: true),
            ["b"] = new("b", new[] { "a" }), ["c"] = new("c", new[] { "a" })
        };
        var valid = LineageValidator.Validate(graph, new[] { "b", "c" });
        Assert.True(valid.IsValid);
        Assert.Equal(new[] { "a" }, valid.TransitiveEvidenceBasis);
        var cycle = new Dictionary<string, EvidenceNode>
        {
            ["a"] = new("a", new[] { "b" }), ["b"] = new("b", new[] { "a" })
        };
        Assert.Equal(LineageDisposition.MalformedLineage, LineageValidator.Validate(cycle, new[] { "a" }).Disposition);
    }

    [Fact]
    public void Admissible_result_projects_to_existing_observation_proposal()
    {
        var result = new FusionEngine().Fuse(new("node-1", "checked", new[] {
            Source("h", FusionSourceKind.Hierarchy, "checked")
        }, Unique(), TimeSpan.FromSeconds(1)));
        var proposal = result.ToObservationProposal();
        Assert.Equal("node-1", proposal.Claim.Subject);
        Assert.Equal("per-011.fusion", proposal.Provenance!.Producer);
    }

    [Fact]
    public void Rendered_field_uses_visual_authority_without_conflicting_with_semantic_checked_state()
    {
        var result = new FusionEngine().Fuse(new("node-1", "rendered.toggleAppearance", new[]
        {
            Source("v", FusionSourceKind.Visual, "off") with { Field = "rendered.toggleAppearance" },
        }, Unique(), TimeSpan.FromSeconds(1)));

        Assert.Equal(FusionDisposition.Supported, result.Disposition);
        Assert.Equal(FusionSourceKind.Visual, result.AuthoritySource);
        Assert.True(result.IsAdmissible);
    }

    [Fact]
    public void Visual_only_evidence_cannot_be_promoted_to_semantic_checked_claim()
    {
        var result = new FusionEngine().Fuse(new("node-1", "checked", new[]
        {
            Source("v", FusionSourceKind.Visual, "on"),
        }, Unique(), TimeSpan.FromSeconds(1)));

        Assert.Equal(FusionDisposition.Unsupported, result.Disposition);
        Assert.False(result.IsAdmissible);
    }

    [Fact]
    public void Hierarchy_only_evidence_cannot_be_promoted_to_rendered_appearance_claim()
    {
        var result = new FusionEngine().Fuse(new("node-1", "rendered.toggleAppearance", new[]
        {
            Source("h", FusionSourceKind.Hierarchy, "checked") with { Field = "checked" },
        }, Unique(), TimeSpan.FromSeconds(1)));

        Assert.Equal(FusionDisposition.Unsupported, result.Disposition);
        Assert.False(result.IsAdmissible);
    }

    [Fact]
    public void No_source_evidence_is_unknown_and_carries_source_unavailable_coverage()
    {
        var result = new FusionEngine().Fuse(new("node-1", "checked", Array.Empty<FusionSourceEvidence>(),
            Unique(), TimeSpan.FromSeconds(1)));

        Assert.Equal(FusionDisposition.Unknown, result.Disposition);
        Assert.Equal(CoverageDisposition.SourceUnavailable, result.Coverage);
        Assert.False(result.IsAdmissible);
    }

    [Fact]
    public void Capability_declared_but_value_not_observed_is_unknown()
    {
        var result = new FusionEngine().Fuse(new("node-1", "checked", new[]
        {
            Source("h", FusionSourceKind.Hierarchy, "") with { ValueObserved = false },
        }, Unique(), TimeSpan.FromSeconds(1)));

        Assert.Equal(FusionDisposition.Unknown, result.Disposition);
        Assert.False(result.IsAdmissible);
    }

    [Fact]
    public void Coordinate_space_mismatch_blocks_joint_fusion()
    {
        var first = Source("h", FusionSourceKind.Hierarchy, "checked") with
        {
            Capture = Capture("h") with { Space = new CoordinateSpace("physical", 1080, 1920, ScreenRotation.None, "h") },
        };
        var second = Source("v", FusionSourceKind.Visual, "checked") with
        {
            Capture = Capture("v") with { Space = new CoordinateSpace("physical", 1920, 1080, ScreenRotation.Rot90, "v") },
        };

        var result = new FusionEngine().Fuse(new("node-1", "checked", new[] { first, second }, Unique(), TimeSpan.FromSeconds(1)));

        Assert.Equal(TemporalAlignmentDisposition.Unaligned, result.Alignment.Disposition);
        Assert.False(result.IsAdmissible);
    }

    [Fact]
    public void Bounded_escalation_spends_focused_then_deep_budget_once()
    {
        var policy = new BoundedEscalationPolicy();
        var request = new EscalationRequest(FusionDisposition.Conflicted, CoverageDisposition.Partial, true, true);
        var first = policy.Decide(request, new EscalationBudget());
        var second = policy.Decide(request, first.Remaining);
        var third = policy.Decide(request, second.Remaining);

        Assert.Equal(EscalationAction.FocusedRescan, first.Action);
        Assert.Equal(EscalationAction.DeepPerception, second.Action);
        Assert.Equal(EscalationAction.Stop, third.Action);
    }

    [Fact]
    public void Occurrence_association_is_feature_only_and_ambiguous_candidates_fail_closed()
    {
        var hierarchy = new OccurrenceAssociator.Features("h-1", Bounds: "1,2,3,4");
        var candidates = new[]
        {
            new OccurrenceAssociator.Features("v-1", Bounds: "1,2,3,4"),
            new OccurrenceAssociator.Features("v-2", Bounds: "1,2,3,4"),
        };

        var result = OccurrenceAssociator.Associate(hierarchy, candidates, Array.Empty<OccurrenceAssociator.Features>());

        Assert.Equal(AssociationDisposition.Ambiguous, result.Disposition);
        Assert.Equal(new[] { "v-1", "v-2" }, result.VisualOccurrenceIds);
        Assert.Equal("deterministic-feature-intersection", result.Features!["method"]);
    }

    [Fact]
    public void Occurrence_association_preserves_many_to_one_and_one_to_many_shapes()
    {
        var h1 = new OccurrenceAssociator.Features("h-1", ResourceId: "card", Role: "button");
        var h2 = new OccurrenceAssociator.Features("h-2", ResourceId: "card", Role: "button");
        var visual = new OccurrenceAssociator.Features("v-1", ResourceId: "card", Role: "button");
        var manyToOne = OccurrenceAssociator.Associate(new[] { h1, h2 }, new[] { visual }, Array.Empty<OccurrenceAssociator.Features>());
        var oneToMany = OccurrenceAssociator.Associate(h1, new[]
        {
            visual,
            new OccurrenceAssociator.Features("v-2", ResourceId: "card", Role: "button", Bounds: "10,10,20,20"),
        }, Array.Empty<OccurrenceAssociator.Features>());

        Assert.Equal(AssociationDisposition.ManyToOne, manyToOne.Disposition);
        Assert.Equal(AssociationDisposition.OneToMany, oneToMany.Disposition);
    }
}
