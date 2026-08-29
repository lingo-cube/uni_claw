using System.Reflection;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-P26-C capability tests: the ScenarioKnowledgeFixture record contract
/// (spec requirement "ScenarioKnowledgeFixture as a validation test asset" +
/// "Knowledge never substitutes for fresh evidence" + "Safety learning without
/// dangerous trial-and-error"; design D2/D3/D4) —
///  1. admission gate: missing SourceRunId / EvidenceRefs rejected; every
///     forbidden source class rejected with its explicit marker; a valid
///     observed-result record is admitted;
///  2. lifecycle: status transitions produce traceable Supersedes/SupersededBy
///     pairs; a downgraded record leaves ActiveKnowledge; lifecycle
///     statistics count per (KnowledgeType, Status);
///  3. conflict: CURRENT FRESH EVIDENCE FIRST — each outcome class downgrades
///     the ACTIVE record; after contradiction the anchor's active advisory no
///     longer returns the old record; the family, no force-apply / re-activate
///     path exists (API surface check) and re-admitting identical old content
///     is rejected as a duplicate;
///  4. scope isolation: a mismatched scenario/app/capability-version/locale/
///     android context returns nothing; the created-from run set does NOT
///     isolate;
///  5. determinism: same inputs ⇒ same RecordId; identity is content-sensitive
///     and stable under lifecycle downgrade.
/// Structure follows EvidenceFixture (candidate records) → Fixture
/// admission/transition → Evidence Evaluation; assertions check capabilities
/// (gate legality, provenance, lifecycle, scope, determinism) — never fixed
/// click counts, coordinates, page text, UI paths, or action histories.
/// </summary>
public sealed class ScenarioKnowledgeFixtureTests
{
    private static readonly string AnchorContainer = "settings.container:Settings-root";

    private static KnowledgeScope Scope(
        string? scenario = null,
        string? app = null,
        string? capabilityId = null,
        string? capabilityVersion = null,
        string? android = null,
        string? locale = null,
        string[]? runs = null)
        => new(
            ScenarioId: scenario ?? "settings-real-emulator",
            ApplicationPackage: app ?? "com.android.settings",
            SemanticCapabilityId: capabilityId ?? "uni-claw.settings.semantic",
            SemanticCapabilityVersion: capabilityVersion ?? "1",
            AndroidAssumptions: android ?? "emulator google_apis;API 35",
            Locale: locale ?? "en-US",
            CreatedFromRunIds: runs ?? new[] { "run-1" });

    private static ScenarioKnowledgeRecord Observed(
        KnowledgeScope scope,
        string? anchor = null,
        string? runId = null,
        IReadOnlyList<string>? evidenceRefs = null,
        KnowledgeType type = KnowledgeType.KnownContainer,
        KnowledgeStatus status = KnowledgeStatus.Active,
        int version = 1,
        int ordinal = 1,
        double confidence = 0.9,
        string? supersedes = null)
        => new(
            KnowledgeType: type,
            SemanticAnchor: anchor ?? AnchorContainer,
            SourceRunId: runId ?? "run-1",
            EvidenceRefs: evidenceRefs ?? new[] { "evidence:run-1:obs-1" },
            ObservedRole: "container observed",
            Scope: scope,
            Disposition: "record-only observed",
            Confidence: confidence,
            ValidityAssumption: "stable across frames",
            Version: version,
            Status: status,
            AdmissionOrdinal: ordinal,
            Supersedes: supersedes);

    // ── 1. Admission gate ───────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AdmissionGate_RecordWithoutSourceRunId_Rejected(string sourceRunId)
    {
        var candidate = Observed(Scope(), runId: sourceRunId);
        var outcome = KnowledgeAdmission.TryAdmit(candidate, KnowledgeAdmissionSource.ObservedResult);
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(outcome);
        Assert.Contains("SourceRunId", rejected.Reason);
        Assert.Null(rejected.ForbiddenSource);
    }

    [Theory]
    [InlineData("")]
    public void AdmissionGate_RecordWithoutEvidenceRefs_Rejected(string evidenceRef)
    {
        // Empty refs list (no provenance).
        var noRefs = Observed(Scope(), evidenceRefs: Array.Empty<string>());
        var rejectedEmpty = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(noRefs, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("EvidenceRef", rejectedEmpty.Reason);

        // Non-empty list of blank refs (provenance but no resolvable evidence).
        var blankRefs = Observed(Scope(), evidenceRefs: new[] { evidenceRef });
        var rejectedBlank = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(blankRefs, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("EvidenceRef", rejectedBlank.Reason);
    }

    [Theory]
    [InlineData(KnowledgeAdmissionSource.Guesswork)]
    [InlineData(KnowledgeAdmissionSource.HardcodedTextAsTruth)]
    [InlineData(KnowledgeAdmissionSource.Coordinates)]
    [InlineData(KnowledgeAdmissionSource.FixedPath)]
    [InlineData(KnowledgeAdmissionSource.SelectorScript)]
    [InlineData(KnowledgeAdmissionSource.ProbeByExecution)]
    [InlineData(KnowledgeAdmissionSource.RuntimeInternalAssumption)]
    public void AdmissionGate_ForbiddenSource_RejectedWithExplicitMarker(KnowledgeAdmissionSource source)
    {
        var candidate = Observed(Scope());
        var outcome = KnowledgeAdmission.TryAdmit(candidate, source);
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(outcome);
        Assert.Equal(source, rejected.ForbiddenSource);
        Assert.Contains("forbidden", rejected.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(KnowledgeType.KnownContainer)]
    [InlineData(KnowledgeType.KnownRecordOnly)]
    [InlineData(KnowledgeType.KnownLocalControl)]
    [InlineData(KnowledgeType.KnownExternalBoundary)]
    [InlineData(KnowledgeType.KnownNonInteractive)]
    [InlineData(KnowledgeType.KnownUnresolved)]
    [InlineData(KnowledgeType.KnownPotentiallyStateMutating)]
    public void AdmissionGate_ValidObservedResultRecord_Admitted(KnowledgeType type)
    {
        var candidate = Observed(Scope(), type: type);
        var outcome = KnowledgeAdmission.TryAdmit(candidate, KnowledgeAdmissionSource.ObservedResult);
        var admitted = Assert.IsType<KnowledgeAdmission.Admitted>(outcome);
        Assert.Equal(candidate.RecordId, admitted.Record.RecordId);
        Assert.False(string.IsNullOrWhiteSpace(admitted.Record.RecordId));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void AdmissionGate_OutOfRangeConfidence_Rejected(double confidence)
    {
        var candidate = Observed(Scope(), confidence: confidence);
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(candidate, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("Confidence", rejected.Reason);
    }

    [Fact]
    public void AdmissionGate_IncompleteScope_Rejected()
    {
        var incompleteScopes = new[]
        {
            Scope(scenario: " "),
            Scope(app: string.Empty),
            Scope(capabilityId: "  "),
            Scope(capabilityVersion: ""),
            Scope(android: " "),
            Scope(locale: ""),
            Scope(runs: Array.Empty<string>()),
        };

        foreach (var incomplete in incompleteScopes)
        {
            var candidate = Observed(incomplete);
            var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(
                KnowledgeAdmission.TryAdmit(candidate, KnowledgeAdmissionSource.ObservedResult));
            Assert.Contains("Scope is incomplete", rejected.Reason);
        }
    }

    [Theory]
    [InlineData(7)] // outside KnowledgeType 0..6 — an eighth vocabulary word is forbidden
    [InlineData(20)] // outside KnowledgeStatus 0..4
    public void AdmissionGate_UndefinedTypeOrStatus_Rejected(int undefinedValue)
    {
        // Casting an out-of-range integer yields an enum value outside the
        // closed vocabularies — the gate must reject it (closed vocabulary:
        // no eighth KnowledgeType / new semantic word).
        var candidate = undefinedValue < 10
            ? Observed(Scope(), type: (KnowledgeType)undefinedValue)
            : Observed(Scope(), status: (KnowledgeStatus)undefinedValue);
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(candidate, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("not a", rejected.Reason);
    }

    [Fact]
    public void AdmissionGate_EmptyAnchor_Rejected()
    {
        var candidate = Observed(Scope(), anchor: "   ");
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(candidate, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("SemanticAnchor", rejected.Reason);
    }

    // ── 2. Lifecycle ────────────────────────────────────────────────────────

    [Fact]
    public void Lifecycle_Supersede_TransitionsToSuperseded_WithTraceablePair()
    {
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);

        var older = Observed(scope, anchor: "settings.preference-row:Wi-Fi", runId: "run-1", version: 1, ordinal: 1);
        var newer = Observed(scope, anchor: "settings.preference-row:Wi-Fi", runId: "run-2",
            evidenceRefs: new[] { "evidence:run-2:obs-1" }, version: 2, ordinal: 2, supersedes: older.RecordId);

        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(older));
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(newer));

        var advanced = fixture.ApplyFreshEvidence(
            "settings.preference-row:Wi-Fi", scope, FreshEvidenceOutcome.Supersedes(older, newer.RecordId));

        // Traceable pair: old.SupersededBy == new.RecordId AND new.Supersedes == old.RecordId.
        var oldAfter = advanced.Records.Single(r => r.RecordId == older.RecordId);
        var newAfter = advanced.Records.Single(r => r.RecordId == newer.RecordId);
        Assert.Equal(KnowledgeStatus.Superseded, oldAfter.Status);
        Assert.Equal(newer.RecordId, oldAfter.SupersededBy);
        Assert.Equal(older.RecordId, newAfter.Supersedes);
        Assert.Equal(KnowledgeStatus.Active, newAfter.Status);

        // Identity is stable under the lifecycle downgrade (diffable freeze).
        Assert.Equal(older.RecordId, oldAfter.RecordId);

        // The superseded record leaves the active advisory; the replacement stays.
        var active = advanced.ActiveKnowledge(scope);
        Assert.DoesNotContain(active, r => r.RecordId == older.RecordId);
        Assert.Contains(active, r => r.RecordId == newer.RecordId);
    }

    [Fact]
    public void LifecycleStatistics_CountsPerTypeAndStatus_AcrossWholeHistory()
    {
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);

        fixture.Admit(Observed(scope, anchor: "settings.container:a", runId: "run-1", ordinal: 1));
        var local = Observed(scope, anchor: "settings.preference-row:b", type: KnowledgeType.KnownLocalControl,
            runId: "run-2", ordinal: 2);
        fixture.Admit(local);
        fixture.Admit(Observed(scope, anchor: "settings.preference-row:c", type: KnowledgeType.KnownRecordOnly,
            runId: "run-3", ordinal: 3));
        var unresolved = Observed(scope, anchor: "settings.preference-row:d", type: KnowledgeType.KnownUnresolved,
            runId: "run-4", ordinal: 4);
        fixture.Admit(unresolved);

        var advanced = fixture.ApplyFreshEvidence(local.SemanticAnchor, scope, FreshEvidenceOutcome.Stales(local));
        advanced = advanced.ApplyFreshEvidence(unresolved.SemanticAnchor, scope, FreshEvidenceOutcome.Contradicts(unresolved));

        var stats = advanced.LifecycleStatistics();
        Assert.Equal(1, stats[(KnowledgeType.KnownContainer, KnowledgeStatus.Active)]);
        Assert.Equal(1, stats[(KnowledgeType.KnownRecordOnly, KnowledgeStatus.Active)]);
        Assert.Equal(1, stats[(KnowledgeType.KnownLocalControl, KnowledgeStatus.Stale)]);
        Assert.Equal(1, stats[(KnowledgeType.KnownUnresolved, KnowledgeStatus.Contradicted)]);
        Assert.Equal(4, stats.Values.Sum());
    }

    // ── 3. Conflict: CURRENT FRESH EVIDENCE FIRST ───────────────────────────

    [Fact]
    public void FreshEvidence_ContradictsStalesInvalidates_DowngradeActiveRecord_HistoryIntact()
    {
        var scope = Scope();
        var cases = new[]
        {
            (anchor: "settings.container:updates", expected: KnowledgeStatus.Contradicted,
                make: (Func<ScenarioKnowledgeRecord, FreshEvidenceOutcome>)(r => FreshEvidenceOutcome.Contradicts(r))),
            (anchor: "settings.container:storage", expected: KnowledgeStatus.Stale,
                make: r => FreshEvidenceOutcome.Stales(r)),
            (anchor: "settings.container:security", expected: KnowledgeStatus.Invalidated,
                make: r => FreshEvidenceOutcome.Invalidates(r)),
        };

        foreach (var (anchor, expected, make) in cases)
        {
            var fixture = new ScenarioKnowledgeFixture(scope);
            var old = Observed(scope, anchor: anchor, runId: "run-1");
            Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(old));

            var advanced = fixture.ApplyFreshEvidence(anchor, scope, make(old));

            // Fresh evidence wins: the old record is downgraded in the new instance.
            var downgraded = advanced.Records.Single(r => r.RecordId == old.RecordId);
            Assert.Equal(expected, downgraded.Status);

            // It is no longer returned by the active advisory for the anchor.
            Assert.DoesNotContain(advanced.ActiveKnowledge(scope), r => r.RecordId == old.RecordId);

            // Immutable-list semantics: the ORIGINAL instance still holds the
            // ACTIVE record — history is never silently mutated/rewritten.
            Assert.Contains(fixture.ActiveKnowledge(scope), r => r.RecordId == old.RecordId);
        }
    }

    [Fact]
    public void FreshEvidence_Contradiction_AnchorAdvisoryNoLongerReturnsOldRecord()
    {
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);
        var old = Observed(scope, anchor: "settings.preference-row:Airplane mode", runId: "run-1");
        fixture.Admit(old);

        var advanced = fixture.ApplyFreshEvidence(old.SemanticAnchor, scope, FreshEvidenceOutcome.Contradicts(old));

        var advisory = advanced.ActiveKnowledge(scope);
        Assert.DoesNotContain(advisory, r => r.RecordId == old.RecordId);
        Assert.Empty(advisory);
    }

    [Fact]
    public void FixtureApi_ExposesNoForceApplyOrReactivatePath()
    {
        // The guarantee is the ABSENCE of any re-activation / force-apply API:
        // a downgraded record can never be resurrected or force-applied over
        // fresh evidence through the fixture. Light but serious surface check.
        var methodNames = typeof(ScenarioKnowledgeFixture)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        foreach (var name in methodNames)
        {
            Assert.DoesNotContain("Reactiva", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ForceApply", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Force", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Reapply", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Overwrite", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReadmitIdenticalOldKnowledgeAfterDowngrade_RejectedAsDuplicate_FreshWins()
    {
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);
        var old = Observed(scope, anchor: "settings.preference-row:Data usage", runId: "run-1");
        fixture.Admit(old);

        var advanced = fixture.ApplyFreshEvidence(old.SemanticAnchor, scope, FreshEvidenceOutcome.Contradicts(old));

        // Attempting to re-apply the SAME old knowledge as active (identical
        // canonical content ⇒ identical RecordId) is rejected — re-admission
        // would re-apply old knowledge without fresh evidence.
        var reactivationAttempt = advanced.Admit(old);
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(reactivationAttempt);
        Assert.Contains("already admitted", rejected.Reason, StringComparison.OrdinalIgnoreCase);

        // A GENUINELY new observation of the same anchor (fresh evidence:
        // new run + new refs + bumped version) is admitted and becomes active.
        var fresh = Observed(scope, anchor: old.SemanticAnchor, runId: "run-2",
            evidenceRefs: new[] { "evidence:run-2:obs-1" }, version: 2, ordinal: 2);
        Assert.IsType<KnowledgeAdmission.Admitted>(advanced.Admit(fresh));
        Assert.Contains(advanced.ActiveKnowledge(scope), r => r.RecordId == fresh.RecordId);
    }

    [Fact]
    public void ApplyFreshEvidence_UnknownOrMismatchedTarget_Throws()
    {
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);
        var admitted = Observed(scope, anchor: "settings.container:display", runId: "run-1");
        fixture.Admit(admitted);

        // A record never admitted to this fixture cannot be downgraded.
        var stranger = Observed(scope, anchor: "settings.container:other", runId: "run-9");
        var unknown = Assert.Throws<ArgumentException>(
            () => fixture.ApplyFreshEvidence(stranger.SemanticAnchor, scope, FreshEvidenceOutcome.Contradicts(stranger)));
        Assert.Contains("not admitted", unknown.Message, StringComparison.OrdinalIgnoreCase);

        // The anchor must match the target record.
        var mismatchedAnchor = Assert.Throws<ArgumentException>(
            () => fixture.ApplyFreshEvidence("settings.container:wrong-anchor", scope, FreshEvidenceOutcome.Contradicts(admitted)));
        Assert.Contains("anchor", mismatchedAnchor.Message, StringComparison.OrdinalIgnoreCase);

        // The scope must match the target record's context.
        var mismatchedScope = Assert.Throws<ArgumentException>(
            () => fixture.ApplyFreshEvidence(admitted.SemanticAnchor, Scope(locale: "en-GB"), FreshEvidenceOutcome.Contradicts(admitted)));
        Assert.Contains("scope", mismatchedScope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 4. Scope isolation ──────────────────────────────────────────────────

    [Fact]
    public void ActiveKnowledge_ContextMismatch_ReturnsNothing_IsolationPerField()
    {
        var scope = Scope();
        var fixture = new ScenarioKnowledgeFixture(scope);
        var record = Observed(scope, anchor: AnchorContainer, runId: "run-1");
        Assert.IsType<KnowledgeAdmission.Admitted>(fixture.Admit(record));

        var mismatches = new[]
        {
            Scope(scenario: "other-settings-scenario"),
            Scope(app: "com.other.app"),
            Scope(capabilityId: "uni-other.semantic"),
            Scope(capabilityVersion: "2"),
            Scope(android: "emulator google_apis;API 34"),
            Scope(locale: "en-GB"),
        };

        foreach (var mismatched in mismatches)
        {
            Assert.Empty(fixture.ActiveKnowledge(mismatched));
        }

        // The created-from run set does NOT isolate: Matches deliberately
        // excludes it (knowledge is reusable across runs of the SAME context).
        var sameContextOtherRuns = Scope(runs: new[] { "run-A", "run-B" });
        Assert.Contains(fixture.ActiveKnowledge(sameContextOtherRuns), r => r.RecordId == record.RecordId);
    }

    [Fact]
    public void Admit_CrossContextRecord_RejectedScopeBound()
    {
        var fixture = new ScenarioKnowledgeFixture(Scope());
        var foreign = Observed(Scope(locale: "en-GB"), anchor: AnchorContainer, runId: "run-1");

        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(fixture.Admit(foreign));
        Assert.Contains("Scope mismatch", rejected.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Records);
    }

    // ── 5. Determinism ──────────────────────────────────────────────────────

    [Fact]
    public void RecordId_SameInputs_SameIdentity_ContentSensitive_OrdinalExcluded()
    {
        var scope = Scope();

        // Identical content ⇒ identical RecordId even across different
        // admission ordinals (the ordinal replaces wall-clock time but is NOT
        // part of identity — freezing stays diffable).
        var a = Observed(scope, anchor: "settings.container:network", runId: "run-1", version: 1, ordinal: 1);
        var b = Observed(scope, anchor: "settings.container:network", runId: "run-1", version: 1, ordinal: 42);
        Assert.Equal(a.RecordId, b.RecordId);

        // Content sensitivity: differing evidence changes identity.
        var differentEvidence = Observed(scope, anchor: "settings.container:network", runId: "run-1",
            evidenceRefs: new[] { "evidence:run-1:obs-2" }, version: 1, ordinal: 1);
        Assert.NotEqual(a.RecordId, differentEvidence.RecordId);

        // Content sensitivity: differing scope context changes identity.
        var differentLocale = Observed(Scope(locale: "en-GB"), anchor: "settings.container:network",
            runId: "run-1", version: 1, ordinal: 1);
        Assert.NotEqual(a.RecordId, differentLocale.RecordId);

        // Deterministic identity even though values differ: raw SHA-256 hex.
        Assert.Matches("^[0-9a-f]{64}$", a.RecordId);
    }

    [Fact]
    public void RecordId_UnchangedByPermissionlessStatusTransition_WithStatusKeepsIdentity()
    {
        var scope = Scope();
        var active = Observed(scope, anchor: "settings.container:about", runId: "run-1");
        var downgraded = active.WithStatus(KnowledgeStatus.Contradicted, supersededBy: "replacement-1");

        Assert.Equal(active.RecordId, downgraded.RecordId);
        Assert.Equal(KnowledgeStatus.Contradicted, downgraded.Status);
        Assert.Equal("replacement-1", downgraded.SupersededBy);
        Assert.Equal(KnowledgeStatus.Active, active.Status);
    }
}