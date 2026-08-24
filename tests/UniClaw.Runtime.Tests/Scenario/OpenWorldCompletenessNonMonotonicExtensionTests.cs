using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// COMPLETENESS NON-MONOTONIC EVIDENCE EXTENSION — NM-1..NM-14.
///
/// After a Container's DISCOVERY EPOCH (first forward exploration) proves
/// completeness, the epoch is FROZEN: later same-Container fresh evidence
/// (backward revisit / parent return) is validated ONLY for consistency against
/// the proven logical-source inventory — the forward ordered-overlap normalizer
/// never re-consumes post-completeness evidence, and the inventory is never
/// expanded. Fresh occurrences must uniquely re-establish exactly one frozen
/// logical source class (evidence-backed; the signature is a resolution key, not
/// identity); Unknown / new-source / ambiguous mapping invalidates completeness.
/// </summary>
public sealed class OpenWorldCompletenessNonMonotonicExtensionTests
{
    private const string App = "com.uniclaw.fixture";

    private static StructuredElementEvidence Row(string title, int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: "com.uniclaw.fixture:id/row_title",
            Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1)), RawText: title);

    private static StructuredElementEvidence ClickableTextlessRow(int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: null,
            Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1)));

    private static StructuredElementEvidence SwitchRow(int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: "com.uniclaw.fixture:id/local_switch",
            Clickable: true, Checkable: true, Checked: false, Enabled: true, Focusable: true,
            Bounds: new ElementBounds(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1)), RawText: "Local");

    private static Observation Obs(long seq, params string[] titles)
        => Qualify(new([], App, seq)
        {
            Sources = ImmutableArray.Create(new ObservationSourceMetadata(
                ObservationSourceTier.PrimaryVision, true, seq, $"vision-{seq}", 1080, 1920,
                "vision-test", "deterministic-vision")),
            // Vision fixture facts are declared independently of the optional
            // structured evidence below; no structured-to-Vision promotion.
            Elements = titles.Select((t, i) => new ObservedElement(
                t, null, i, new ElementBounds(0, 0.1f * i, 1, 0.1f * (i + 1)))).ToImmutableArray(),
            StructuredElements = titles.Select((t, i) => Row(t, i)).ToImmutableArray(),
        });

    private static Observation Qualify(Observation raw)
    {
        var context = SemanticObservationFactProjector.Project(raw);
        var manifest = new SemanticCapabilityManifest("fixture.semantic", "1", ["fixture.navigation"]);
        var output = ImmutableArray.CreateBuilder<SemanticEvidenceV2Envelope>();
        foreach (var fact in context.Facts.Where(f => f.SourceTier == SemanticSourceTier.Primary && f.Kind == SemanticObservationFactKind.Text))
        {
            var symbol = new SemanticSymbolReference(manifest.ManifestId, manifest.Version, "fixture.navigation");
            var scope = new SemanticScopeReference($"occurrence:{fact.OccurrenceId}");
            var provenance = new SemanticProvenance(fact.SourceId, fact.SourceTier, fact.ProvenanceId, DateTimeOffset.UnixEpoch, fact.FrameId);
            var candidate = new ElementAffordanceCandidateEvidence(fact.OccurrenceId,
                string.Equals(fact.RawProviderType, "switch", StringComparison.Ordinal)
                    ? ElementAffordanceKind.LocalControl
                    : ElementAffordanceKind.NavigationCandidate,
                symbol, context.Observation, scope, provenance, .9,
                DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue);
            output.Add(new SemanticEvidenceV2Envelope($"fixture:{fact.OccurrenceId}", candidate));
        }
        var capability = new FixtureCapability(manifest, output.ToImmutable());
        var runtime = new SemanticCapabilityRuntime(capability);
        var batch = runtime.EvaluateAsync(context, context.Observation, context.Sources, DateTimeOffset.UnixEpoch)
            .GetAwaiter().GetResult();
        return raw with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) };
    }

    private sealed class FixtureCapability(SemanticCapabilityManifest manifest, ImmutableArray<SemanticEvidenceV2Envelope> output) : IExternalSemanticCapability
    {
        public SemanticCapabilityManifest Manifest { get; } = manifest;
        public ValueTask<ImmutableArray<SemanticEvidenceV2Envelope>> InterpretAsync(ExternalSemanticCapabilityContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(output);
    }

    private static Observation ObsRaw(long seq, params StructuredElementEvidence[] structured)
    {
        // Fresh evidence mirrors the structured rows as PRIMARY Vision elements
        // (the same-channel occurrences the frozen discovery epoch uses), so
        // post-completeness consistency resolves by the shared signature key.
        var elements = structured
            .Select((s, i) => new ObservedElement(s.RawText, null, i, s.Bounds,
                s.Checkable == true || s.ResourceId?.Contains("local_switch", StringComparison.Ordinal) == true
                    ? "switch" : null))
            .ToImmutableArray();
        var raw = new Observation(elements, App, seq)
        {
            Sources = ImmutableArray.Create(new ObservationSourceMetadata(
                ObservationSourceTier.PrimaryVision, true, seq, $"vision-{seq}", 1080, 1920,
                "vision-test", "deterministic-vision")),
            StructuredElements = structured.ToImmutableArray(),
        };
        return Qualify(raw);
    }

    private static Observation VisualUnknown(long seq)
        => new(
            ImmutableArray.Create(
                new ObservedElement("Child 01", null, 0, new ElementBounds(0, 0, 1, .1f)),
                new ObservedElement(string.Empty, null, 1, new ElementBounds(0, .1f, 1, .2f)),
                new ObservedElement("Child 02", null, 2, new ElementBounds(0, .2f, 1, .3f))),
            App, seq)
        {
            Sources = ImmutableArray.Create(new ObservationSourceMetadata(
                ObservationSourceTier.PrimaryVision, true, seq, $"vision-{seq}", 1080, 1920,
                "vision-test", "deterministic-vision")),
        };

    private static ImmutableArray<Observation> ForwardChain()
        => ImmutableArray.Create(
            Obs(2, "Child 01", "Child 02", "Child 03", "Child 04"),
            Obs(3, "Child 03", "Child 04", "Child 05", "Child 06", "Child 07"),
            Obs(4, "Child 05", "Child 06", "Child 07", "Child 08"),
            Obs(5, "Child 05", "Child 06", "Child 07", "Child 08"));

    private static ContainerInventoryCompletenessEvidence Freeze(
        ImmutableArray<Observation> discovery,
        out SourceNormalizationResult normalization)
    {
        normalization = SourceEquivalenceNormalizer.Normalize(discovery);
        Assert.True(normalization.IsResolved);
        var sources = PostCompletenessConsistencyValidator.BuildFrozenSources(discovery, normalization);
        return new ContainerInventoryCompletenessEvidence(
            "Fixture Root",
            discovery.Select(o => o.SequenceNumber).ToImmutableArray(),
            normalization.UniqueSourceSignatures,
            ExplorationExhausted: true,
            UnresolvedCandidateCount: 0,
            Reason: "test",
            ProvenLogicalSources: sources);
    }

    // ── NM-1: forward top→mid→bottom→terminal → completeness proven ─────────

    [Fact]
    public void NM1_ForwardChain_CompletenessProven()
    {
        var evidence = Freeze(ForwardChain(), out _);

        Assert.True(evidence.IsComplete);
        Assert.True(evidence.PositiveExhaustionEvidence);
        Assert.Equal(8, evidence.ProvenLogicalSources.Length);
        Assert.Equal(new long[] { 2, 3, 4, 5 }, evidence.FrozenDiscoveryObservationSequences);
    }

    // ── NM-2: append-equivalent backward view → consistency PASS, no re-normalize ──

    [Fact]
    public void NM2_BackwardEquivalentView_ConsistentWithoutRenormalizingHistory()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var backwardView = Obs(6, "Child 01", "Child 02", "Child 03", "Child 04"); // top viewport again

        // The OLD behavior (re-normalizing the whole history) is Unresolved —
        // this is the bug this extension removes from the post-completeness path.
        Assert.False(SourceEquivalenceNormalizer.Normalize(ForwardChain().Add(backwardView)).IsResolved);

        var result = PostCompletenessConsistencyValidator.Validate(backwardView, evidence, continuityVerified: true);
        Assert.True(result.Consistent);
    }

    // ── NM-3: parent return to a historical viewport → consistency PASS ─────

    [Fact]
    public void NM3_ParentReturnToHistoricalViewport_Consistent()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var returned = Obs(6, "Child 03", "Child 04", "Child 05", "Child 06", "Child 07"); // mid viewport

        var result = PostCompletenessConsistencyValidator.Validate(returned, evidence, continuityVerified: true);
        Assert.True(result.Consistent);
    }

    // ── NM-4: fresh view = subset of known sources → PASS ───────────────────

    [Fact]
    public void NM4_FreshViewSubsetOfKnownSources_Passes()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var subset = Obs(6, "Child 01", "Child 02");

        var result = PostCompletenessConsistencyValidator.Validate(subset, evidence, continuityVerified: true);
        Assert.True(result.Consistent);
    }

    // ── NM-5: previously unknown NAVIGATION_CANDIDATE → INVALIDATED ─────────

    [Fact]
    public void NM5_PreviouslyUnknownCandidate_Invalidates()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var novel = Obs(6, "Child 01", "Child 02", "Child 09");

        var result = PostCompletenessConsistencyValidator.Validate(novel, evidence, continuityVerified: true);
        Assert.False(result.Consistent);
        Assert.Contains("previously-unknown NAVIGATION_CANDIDATE", result.Reason);
    }

    // ── NM-6: fresh interactive UNKNOWN → INVALIDATED ───────────────────────

    [Fact]
    public void NM6_FreshInteractiveUnknown_Invalidates()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var fresh = VisualUnknown(6);

        var result = PostCompletenessConsistencyValidator.Validate(fresh, evidence, continuityVerified: true);
        Assert.False(result.Consistent);
        Assert.Contains("UNKNOWN", result.Reason);
    }

    // ── NM-7: ambiguous mapping to frozen classes → INVALIDATED ─────────────

    [Fact]
    public void NM7_AmbiguousMappingToFrozenClasses_Invalidates()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var signature = evidence.ProvenLogicalSources[0].Signature; // "Child 01" class
        // Defensive crafted epoch where TWO frozen classes share a signature.
        var ambiguousEvidence = evidence with
        {
            ProvenLogicalSources = ImmutableArray.Create(
                new ProvenLogicalSource(signature, []),
                new ProvenLogicalSource(signature, [])),
        };

        var result = PostCompletenessConsistencyValidator.Validate(
            Obs(6, "Child 01"), ambiguousEvidence, continuityVerified: true);
        Assert.False(result.Consistent);
        Assert.Contains("ambiguously", result.Reason);
    }

    // ── NM-8: fresh LOCAL_CONTROL → no child-inventory expansion ────────────

    [Fact]
    public void NM8_FreshLocalControl_DoesNotExpandInventory()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var fresh = ObsRaw(6, Row("Child 01", 0), SwitchRow(1), Row("Child 02", 2));

        var result = PostCompletenessConsistencyValidator.Validate(fresh, evidence, continuityVerified: true);
        Assert.True(result.Consistent); // Switch produces no occurrence; ignored
        Assert.Equal(8, evidence.ProvenLogicalSources.Length); // cardinality unchanged
    }

    // ── NM-9: bounds/node-path change, source uniquely re-established → PASS ─

    [Fact]
    public void NM9_BoundsChanged_SourceUniquelyReestablished_Passes()
    {
        var evidence = Freeze(ForwardChain(), out _);
        // Same title/class/resource-id signature but a different bounds ordinal —
        // the signature (resolution key) ignores bounds, so the source is
        // uniquely re-established.
        var moved = ObsRaw(6, Row("Child 04", 7));

        var result = PostCompletenessConsistencyValidator.Validate(moved, evidence, continuityVerified: true);
        Assert.True(result.Consistent);
    }

    // ── NM-10: same-Container continuity failure → INVALIDATED ──────────────

    [Fact]
    public void NM10_ContinuityFailure_Invalidates()
    {
        var evidence = Freeze(ForwardChain(), out _);

        var result = PostCompletenessConsistencyValidator.Validate(
            Obs(6, "Child 01", "Child 02"), evidence, continuityVerified: false);
        Assert.False(result.Consistent);
        Assert.Contains("same-Container continuity FAILED", result.Reason);
    }

    // ── NM-11: DiscoveryEvidenceSet stays frozen after completeness ─────────

    [Fact]
    public void NM11_DiscoveryEvidenceSetRemainsFrozen()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var frozen = evidence.FrozenDiscoveryObservationSequences;
        var proven = evidence.ProvenLogicalSources;

        _ = PostCompletenessConsistencyValidator.Validate(Obs(6, "Child 01", "Child 02"), evidence, continuityVerified: true);
        _ = PostCompletenessConsistencyValidator.Validate(Obs(7, "Child 05", "Child 06"), evidence, continuityVerified: true);

        Assert.Equal(frozen, evidence.FrozenDiscoveryObservationSequences);
        Assert.Equal(proven, evidence.ProvenLogicalSources); // post-completeness validation never mutates the epoch
    }

    // ── NM-12: post-completeness observations never increase cardinality ────

    [Fact]
    public void NM12_PostCompletenessObservationsDoNotIncreaseInventoryCardinality()
    {
        var evidence = Freeze(ForwardChain(), out _);
        Assert.Equal(8, evidence.ProvenLogicalSources.Length);

        _ = PostCompletenessConsistencyValidator.Validate(Obs(6, "Child 01", "Child 02"), evidence, continuityVerified: true);
        _ = PostCompletenessConsistencyValidator.Validate(Obs(7, "Child 03", "Child 04"), evidence, continuityVerified: true);

        // The frozen epoch is the only inventory-generation input; later fresh
        // views are never folded into ProvenLogicalSources.
        Assert.Equal(8, evidence.ProvenLogicalSources.Length);
        Assert.Equal(8, evidence.UniqueNavigationSourceIdentities.Length);
    }

    // ── NM-13: duplicate-signature classes → INVALIDATED, no signature guessing ──

    [Fact]
    public void NM13_DuplicateSignatureClasses_InvalidatedWithoutGuessing()
    {
        var evidence = Freeze(ForwardChain(), out _);
        var signature = evidence.ProvenLogicalSources[0].Signature;
        var duplicateClasses = evidence with
        {
            ProvenLogicalSources = ImmutableArray.Create(
                new ProvenLogicalSource(signature, []),
                new ProvenLogicalSource(signature, [])),
        };

        var result = PostCompletenessConsistencyValidator.Validate(
            Obs(6, "Child 01"), duplicateClasses, continuityVerified: true);
        Assert.False(result.Consistent);
        // The validator refuses to guess which class the signature denotes.
        Assert.Contains("ambiguously to multiple frozen logical source classes", result.Reason);
        Assert.Contains("no signature guessing", result.Reason);
    }

    // ── NM-14: GoalEvidence authority unchanged ─────────────────────────────
    // Covered at the Agent level by CURRENT_NoScroll_CurrentStaysAndRunCompletesFromLatestGoalEvidence
    // (OpenWorldPostExplorationCurrentRepairTests): the satisfied GoalEvidence
    // reads ObservationHistory[^1] — the goal evaluator path is untouched.
}
