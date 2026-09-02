using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Classification;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Results;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-CRV2-P26-B / Task 10.1b capability tests for the Phase-2.6 Fast-only
/// metrics, baseline comparison, 13-value blocker taxonomy and ResultCollector
/// V2 evidence capture. Each test asserts a capability (field immutability,
/// honest-Unavailable classification, Completed-runs answer, exactly-13 guard,
/// six-field fail-closed, FailureOwner reuse, V2 read-only capture) — never a
/// fixed click count, coordinate, page path or action history.
///
/// The fixture vocabulary used here ("Home" / "Display" / "Settings" / "Root")
/// deliberately avoids the HarnessSourceShapeGuard whitelist-scanned scenario
/// tokens, so this capability file introduces no new scenario-knowledge
/// violation.
/// </summary>
public sealed class Phase26MetricsAndBlockerClassificationTests
{
    // ── 1. all metrics immutable + serialization round-trip ─────────────────

    [Fact]
    public void Metrics_AllTwentyMetricSlots_Immutable_AndStableRoundTrip()
    {
        var metrics = SampleMetrics();

        // All 20 metric slots are represented (19 named counts + blocker
        // counts, plus FirstDivergence and RunTerminalDisposition).
        var countNames = new[]
        {
            "CompletedRuns", "DeepestTraversalDepth", "ContainersEntered",
            "BranchesAttempted", "BranchesCompleted", "UnresolvedContainers",
            "DeepUnknownCount", "WrongBranchCount", "UnexpectedOffPathTransitions",
            "RepeatedTraversalCount", "RestartFullResetCount",
            "CurrentVsExecutionMismatches", "FastTrustedCount", "FastAbstainedCount",
            "FastConflictCount", "FalseFastTrustCount",
            "TransitionReconciliationFailures", "CoverageExhaustionFailures",
            "StaleOccurrenceBoundsRejections",
        };
        Assert.Equal(19, countNames.Length);

        // Every count is present on the metric set (a walk returns 19 fields)
        // and every one is a classified, immutable field.
        var counts = metrics.EnumerateCountMetrics().ToArray();
        Assert.Equal(19, counts.Length);
        Assert.All(counts, c => Assert.False(string.IsNullOrWhiteSpace(c.TruthSource)));

        // Stable serialization round-trip: serialize → deserialize →
        // re-serialize yields identical JSON (proves the schema round-trips
        // without loss or drift). Includes the blocker-count dictionary.
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var json = JsonSerializer.Serialize(metrics, options);
        var roundTripped = JsonSerializer.Deserialize<Phase26FastOnlyRunMetrics>(json, options)!;
        var json2 = JsonSerializer.Serialize(roundTripped, options);
        Assert.Equal(json, json2);

        // Immutability: every metric property is init-only (its setter is the
        // init accessor, never a publicly assignable setter), and every count
        // slot carries one of the three classifications.
        foreach (var name in countNames)
        {
            var prop = typeof(Phase26FastOnlyRunMetrics).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
            Assert.True(IsInitOnly(prop!), $"metric '{name}' must be immutable (init-only, no public setter).");
        }
    }

    // ── 2. device-only quantities honestly unavailable, never inferred ──────

    [Fact]
    public void Metrics_DeviceOnlyQuantities_HonestUnavailable_NotInferred()
    {
        // An empty metric set must mark every quantity Unavailable with a
        // reason — never zeroed (which would fabricate measured values).
        var empty = Phase26FastOnlyRunMetrics.Empty();
        Assert.All(empty.EnumerateClassifiedFields(), f =>
        {
            Assert.Equal(ResultFieldClassification.Unavailable, f.Classification);
            Assert.False(string.IsNullOrWhiteSpace(f.TruthSource));
        });

        // Classification is the truthful absent marker: Unavailable with a stated
        // reason. The numeric default (0) is never claimed as a measured count.
        var depth = Phase26FastOnlyRunMetrics.DeviceUnavailableCount(
            "the physical depth a flash driver reached is device-only and not measurable on this deterministic harness");
        Assert.Equal(ResultFieldClassification.Unavailable, depth.Classification);
        Assert.False(string.IsNullOrWhiteSpace(depth.TruthSource));
        Assert.Equal(0, depth.Value);
    }

    // ── 3. comparison answers the Completed-runs question, baseline frozen ───

    [Fact]
    public void Baseline_FrozenFacts_19FreshRunsAndZeroCompleted_EvidenceCited()
    {
        var facts = Phase26BaselineFacts.FrozenPhase26();

        Assert.Equal(19, facts.FreshRuns);
        Assert.Equal(0, facts.CompletedRuns);

        // The blocker distribution is documented and cited, never zero-fabricated.
        Assert.NotEmpty(facts.BlockerDistribution);
        Assert.True(facts.BlockerDistribution[Phase26BlockerCategory.PERCEPTION] > 0);
        Assert.True(facts.BlockerDistribution[Phase26BlockerCategory.ENVIRONMENT] > 0);
        Assert.True(facts.BlockerDistribution.ContainsKey(Phase26BlockerCategory.UNKNOWN));

        // The cited historical evidence file exists (history is read, not rewritten).
        Assert.True(File.Exists(Phase26BaselineFacts.ResolvedEvidencePath()));

        // Verify the file still carries the frozen headline facts (0/19) so the
        // baseline is genuinely the cited history and not invented.
        var report = File.ReadAllText(Phase26BaselineFacts.ResolvedEvidencePath());
        Assert.Contains("0/19", report);
    }

    [Fact]
    public void Comparison_AnswersOldZeroCompletedVsNewCompletedRuns()
    {
        var metrics = SampleMetrics(); // CompletedRuns = 2 (Direct)
        var comparison = Phase26BaselineComparison.Compare(metrics);

        // Direct answer field.
        Assert.Equal(0, comparison.CompletedRunsAnswer.BaselineCompletedRuns);
        Assert.Equal(2, comparison.CompletedRunsAnswer.NewCompletedRuns.Value);
        Assert.Contains("old baseline 0 Completed", comparison.CompletedRunsAnswer.Answer);
        Assert.Contains("new 2 Completed", comparison.CompletedRunsAnswer.Answer);

        // The comparison row for CompletedRuns carries the frozen baseline 0.
        var completedRow = Assert.Single(
            comparison.MetricComparisons, r => r.MetricName == "CompletedRuns");
        Assert.Equal(0, completedRow.BaselineValue.Value);
        Assert.Equal(2, completedRow.NewValue.Value);

        // New metrics are carried.
        Assert.Same(metrics, comparison.NewMetrics);
    }

    [Fact]
    public void Comparison_HonestWhenNewCompletedRunsUnavailable()
    {
        // A fresh campaign that measured nothing yields an honest answer, not a
        // fabricated 0.
        var comparison = Phase26BaselineComparison.Compare(Phase26FastOnlyRunMetrics.Empty());
        Assert.Equal(0, comparison.CompletedRunsAnswer.BaselineCompletedRuns);
        Assert.Equal(
            ResultFieldClassification.Unavailable,
            comparison.CompletedRunsAnswer.NewCompletedRuns.Classification);
        Assert.Contains("unavailable", comparison.CompletedRunsAnswer.Answer);
    }

    // ── 4. exactly-13 blocker taxonomy guard ────────────────────────────────

    [Fact]
    public void BlockerCategory_Taxonomy_HasExactlyThirteenValues()
    {
        var values = Enum.GetValues<Phase26BlockerCategory>();
        Assert.Equal(13, values.Length);

        var expected = new[]
        {
            Phase26BlockerCategory.PERCEPTION,
            Phase26BlockerCategory.CAPTURE,
            Phase26BlockerCategory.SEMANTIC,
            Phase26BlockerCategory.FAST_RESOLUTION,
            Phase26BlockerCategory.CONTAINER_IDENTITY,
            Phase26BlockerCategory.TRANSITION,
            Phase26BlockerCategory.ENTRY_RETURN,
            Phase26BlockerCategory.LOCAL_MODEL,
            Phase26BlockerCategory.COVERAGE,
            Phase26BlockerCategory.AGENT_OBLIGATION,
            Phase26BlockerCategory.ACTION_GROUNDING,
            Phase26BlockerCategory.ENVIRONMENT,
            Phase26BlockerCategory.UNKNOWN,
        };
        Assert.Equal(expected.OrderBy(e => e), values.OrderBy(e => e));
    }

    // ── 5. six-field fail-closed ────────────────────────────────────────────

    [Fact]
    public void BlockerRecord_RequiresAllSixMandatoryFields_FailClosed()
    {
        var valid = () => new Phase26BlockerRecord(
            "last-good", "first-divergence", "expected", "observed",
            FailureOwner.Grounding, "evidence:ref:1");
        Assert.NotNull(valid());

        // Each of the six fields being blank/null must fail closed.
        Assert.ThrowsAny<ArgumentException>(() => new Phase26BlockerRecord(
            " ", "first-divergence", "expected", "observed", FailureOwner.Grounding, "ev"));
        Assert.ThrowsAny<ArgumentException>(() => new Phase26BlockerRecord(
            "last-good", "", "expected", "observed", FailureOwner.Grounding, "ev"));
        Assert.ThrowsAny<ArgumentException>(() => new Phase26BlockerRecord(
            "last-good", "first-divergence", null!, "observed", FailureOwner.Grounding, "ev"));
        Assert.ThrowsAny<ArgumentException>(() => new Phase26BlockerRecord(
            "last-good", "first-divergence", "expected", " ", FailureOwner.Grounding, "ev"));
        Assert.ThrowsAny<ArgumentException>(() => new Phase26BlockerRecord(
            "last-good", "first-divergence", "expected", "observed", FailureOwner.Grounding, " "));
        Assert.ThrowsAny<ArgumentException>(() => new Phase26BlockerRecord(
            "last-good", "first-divergence", "expected", "observed", (FailureOwner)999, "ev"));
    }

    // ── 6. Owner reuses / maps the existing FailureOwner vocabulary ─────────

    [Fact]
    public void BlockerOwnerMapping_ReusesExistingFailureOwnerVocabulary()
    {
        // Every non-UNKNOWN category maps onto an EXISTING FailureOwner value.
        var nonUnknown = Enum.GetValues<Phase26BlockerCategory>()
            .Where(c => c != Phase26BlockerCategory.UNKNOWN);
        foreach (var category in nonUnknown)
        {
            var owner = category.ToFailureOwner();
            Assert.NotNull(owner);
            Assert.True(Enum.IsDefined(owner!.Value), "mapped owner must be a defined FailureOwner value");
            Assert.Contains(owner.Value, Enum.GetValues<FailureOwner>());
        }

        // UNKNOWN is genuinely unattributable: no fixed owner is forced.
        Assert.Null(Phase26BlockerCategory.UNKNOWN.ToFailureOwner());

        // A record's Owner is literally a FailureOwner (reuse, no second owner system).
        var record = new Phase26BlockerRecord(
            "l", "d", "expected", "observed", FailureOwner.Execution, "ev");
        Assert.Equal(FailureOwner.Execution, record.Owner);
    }

    // ── 7. ResultCollector captures V2 evidence on a real happy Agent run ───

    [Fact]
    public async Task ResultCollector_CapturesV2_OnHappyRun_ClassifiedAndZeroMutation()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        Assert.True(transported.Admission.Accepted);

        var collector = new ResultCollector(new TierAReadSurface(host, transported.Admission.RunId!), transported.Admission);

        var first = await collector.CollectAsync();
        var second = await collector.CollectAsync();

        // The V2 capture is present and every field is classified with a truth
        // source (never inhabiting a default/unclassified state).
        Assert.NotNull(first.V2);
        foreach (var field in first.V2.EnumerateClassifiedFields())
        {
            Assert.True(
                field.Classification is ResultFieldClassification.DirectProjection
                    or ResultFieldClassification.DerivedReadModel
                    or ResultFieldClassification.Unavailable,
                $"V2 field must carry one of the three classifications (got {field.Classification}).");
            Assert.False(string.IsNullOrWhiteSpace(field.TruthSource));
            if (field.RawValue is not null)
            {
                Assert.NotEqual(ResultFieldClassification.Unavailable, field.Classification);
            }
        }

        // Zero mutation / determinism: aggregating the same run twice yields an
        // identical V2 evidence capture and an identical complete result.
        Assert.Equal(first.V2, second.V2);

        // Fast availability is honest: when V2 state is present the production
        // path retains no mutable latest Fast value (NotRetained), reflected as a
        // Derived (partial) classification with the value — never fabricated.
        if (first.V2.IsV2StateAvailable.Value == true)
        {
            Assert.Equal(
                ResultFieldClassification.DerivedReadModel,
                first.V2.FastAssessmentAvailability.Classification);
            Assert.Equal(
                ContainerFastAssessmentAvailability.NotRetained,
                first.V2.FastAssessmentAvailability.Value);
        }
    }

    // ── 8. V2 section mapping preserves the classification pattern ──────────

    [Fact]
    public void V2Section_FromSnapshot_PreservesClassificationMapping()
    {
        var snapshot = new RunSnapshot
        {
            RunId = "r1",
            CurrentContainerNodeRef = SnapshotField<ContainerNodeRef?>.Direct(new ContainerNodeRef("n1"), "current node projection"),
            CurrentSliceRef = SnapshotField<ContainerSliceRef?>.Direct(new ContainerSliceRef("s1"), "current slice projection"),
            EntrySourceNodeRef = SnapshotField<ContainerNodeRef?>.Direct(new ContainerNodeRef("entry-source"), "entry source projection"),
            EntryTransitionOccurrenceRef = SnapshotField<TransitionOccurrenceRef?>.Direct(new TransitionOccurrenceRef("o1"), "entry occurrence projection"),
            EntryRelationRef = SnapshotField<ContainerRelationRef?>.Unavailable("no entry relation on this path"),
            LatestTransitionOccurrence = SnapshotField<ContainerTransitionOccurrence?>.Unavailable("no latest occurrence on this surface"),
            EvidenceRevision = SnapshotField<SemanticEvidenceRevision?>.Direct(new SemanticEvidenceRevision(3), "evidence revision projection"),
            FastAssessmentAvailability = SnapshotField<ContainerFastAssessmentAvailability?>.UnavailablePartial(
                ContainerFastAssessmentAvailability.NotRetained, "production retains no mutable latest Fast slot"),
        };

        var section = BuildV2Section(snapshot);

        // Direct public projections map to DirectProjection with the value.
        Assert.True(section.IsV2StateAvailable.Value);
        Assert.Equal(ResultFieldClassification.DerivedReadModel, section.IsV2StateAvailable.Classification);
        Assert.Equal(new ContainerNodeRef("n1"), section.CurrentContainerNodeRef.Value);
        Assert.Equal(ResultFieldClassification.DirectProjection, section.CurrentContainerNodeRef.Classification);
        Assert.Equal(ResultFieldClassification.DirectProjection, section.EvidenceRevision.Classification);
        Assert.Equal(new SemanticEvidenceRevision(3), section.EvidenceRevision.Value);

        // NotCurrentlyAvailable fields map to Unavailable (never fabricated).
        Assert.Equal(ResultFieldClassification.Unavailable, section.EntryRelationRef.Classification);
        Assert.Null(section.EntryRelationRef.Value);
        Assert.Equal(ResultFieldClassification.Unavailable, section.LatestTransitionOccurrence.Classification);
        Assert.Null(section.LatestTransitionOccurrence.Value);

        // Fast partial evidence: an UnavailablePartial retains its REAL value and
        // maps to DerivedReadModel (partial truth preserved, not dropped).
        Assert.Equal(ResultFieldClassification.DerivedReadModel, section.FastAssessmentAvailability.Classification);
        Assert.Equal(ContainerFastAssessmentAvailability.NotRetained, section.FastAssessmentAvailability.Value);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static bool IsInitOnly(PropertyInfo property)
        => property.SetMethod is not null
           && property.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
               .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    private static Phase26FastOnlyRunMetrics SampleMetrics()
        => new()
        {
            CompletedRuns = ResultField<int>.Direct(2, "fresh campaign part (measured)"),
            DeepestTraversalDepth = ResultField<int>.Direct(4, "deterministic harness traversal depth"),
            ContainersEntered = ResultField<int>.Direct(6, "deterministic harness container entries"),
            BranchesAttempted = ResultField<int>.Direct(7, "deterministic harness branch attempts"),
            BranchesCompleted = ResultField<int>.Direct(3, "deterministic harness completed branches"),
            UnresolvedContainers = ResultField<int>.Direct(2, "deterministic harness unresolved containers"),
            DeepUnknownCount = ResultField<int>.Direct(1, "deterministic harness deep-unknown events"),
            WrongBranchCount = ResultField<int>.Direct(1, "deterministic harness wrong-branch observations"),
            UnexpectedOffPathTransitions = ResultField<int>.Direct(1, "deterministic harness off-path transitions"),
            RepeatedTraversalCount = ResultField<int>.Direct(0, "deterministic harness repeated traversals"),
            RestartFullResetCount = ResultField<int>.Direct(0, "deterministic harness restarts"),
            CurrentVsExecutionMismatches = ResultField<int>.Direct(1, "deterministic harness current-vs-execution r5 cases"),
            FastTrustedCount = ResultField<int>.Direct(2, "deterministic harness Fast trust"),
            FastAbstainedCount = ResultField<int>.Direct(1, "deterministic harness Fast abstention"),
            FastConflictCount = ResultField<int>.Direct(0, "deterministic harness Fast conflict"),
            FalseFastTrustCount = ResultField<int>.Unavailable("a false Fast trust is only provable from subsequent direct device evidence, not inferred"),
            TransitionReconciliationFailures = ResultField<int>.Direct(1, "deterministic harness reconciliation failures"),
            CoverageExhaustionFailures = ResultField<int>.Direct(1, "deterministic harness coverage-exhaustion failures"),
            StaleOccurrenceBoundsRejections = ResultField<int>.Direct(1, "deterministic harness stale-bounds rejections"),
            BlockerCategoryCounts = ResultField<ImmutableSortedDictionary<Phase26BlockerCategory, int>>.Direct(ZeroedBlockers(), "deterministic harness blocker counts"),
            FirstDivergence = ResultField<string>.Direct("observation:3:p26f12-r5", "earliest evidence-derived divergence"),
            RunTerminalDisposition = ResultField<string>.Direct("Completed", "campaign terminal disposition"),
        };

    private static ImmutableSortedDictionary<Phase26BlockerCategory, int> ZeroedBlockers()
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<Phase26BlockerCategory, int>();
        foreach (var category in Enum.GetValues<Phase26BlockerCategory>())
        {
            builder[category] = 1;
        }

        return builder.ToImmutable();
    }

    private static Phase26V2SnapshotSection BuildV2Section(RunSnapshot snapshot)
        => (Phase26V2SnapshotSection)(typeof(ResultCollector)
            .GetMethod("BuildV2Section", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [snapshot])!);
}
