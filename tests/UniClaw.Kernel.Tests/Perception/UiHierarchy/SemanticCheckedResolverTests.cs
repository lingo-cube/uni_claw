using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception.UiHierarchy;

/// <summary>
/// PER-014 Slice A（ruling R1）：SemanticCheckedResolver 只读缝单元测试。
/// T1 Checked 解析 / T2 Unchecked 解析 / T3 Partial 保持不折叠 / T4 Unknown
/// fail-closed 带 reason / T5 Unsupported fail-closed。全部确定性。
/// </summary>
public sealed class SemanticCheckedResolverTests
{
    private static readonly TargetDescriptor Switch = new("switch");
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);

    private static WorldBeliefRevision Belief(
        IReadOnlyDictionary<string, WorldClaim> worldState,
        params OccurrenceBelief[] occurrences) =>
        new(
            RevisionId: "rev-1", ParentRevisionId: null, RevisionNumber: 1,
            WorldState: worldState, WorldGraph: Array.Empty<string>(),
            EvidenceBasis: (IReadOnlySet<string>)new HashSet<string>(
                worldState.Values.Select(c => c.EvidenceId)),
            FreshnessBasis: new FreshnessBasis(T0), Uncertainty: new Uncertainty(0),
            Conflicts: Array.Empty<Conflict>(),
            Occurrences: occurrences);

    private static OccurrenceBelief Occ(string role = "switch") =>
        new("occ-1", null, role, null, Array.Empty<string>());

    private static WorldClaim Claim(string value, string evidenceId) =>
        new(value, evidenceId, TypedHierarchyProposalProjector.Producer);

    private static EvidenceRecord TypedRecord(
        string subject, string value, string evidenceId,
        HierarchyCapability capabilities,
        DateTimeOffset? captureTimestamp = null) =>
        new(evidenceId,
            new ObservationClaim(subject, value),
            IngressKind.Observation, ObservationContext.External,
            new Provenance(
                TypedHierarchyProposalProjector.Producer, T0, $"scope:{subject}",
                new[] { TypedHierarchyProposalProjector.LineageMarker },
                Hierarchy: new HierarchyCaptureDescriptor(
                    CaptureId: "cap-1", AndroidApiLevel: 34,
                    UiHierarchyAcquirerKind.LegacyUiAutomatorXml, "1.0",
                    UiHierarchyFormat.UiAutomatorXml, "dev-1", "sess-1",
                    ObservationCycleId: null,
                    CaptureTimestamp: captureTimestamp ?? T0,
                    CaptureDuration: null, capabilities,
                    CoverageCompleteness.CompleteWithinDeclaredSurface,
                    CoverageLimitation: null,
                    NodeLocalIndex: 0, ParentLocalIndex: null, Field: "checked")));

    [Fact]
    public void T1_CheckedClaim_ResolvesChecked()
    {
        var record = TypedRecord("ui.node.cap-1#0.checked", "checked", "ev-checked-0001",
            HierarchyCapability.CheckedBooleanCollapsed);
        var belief = Belief(
            new Dictionary<string, WorldClaim>
            {
                ["ui.node.cap-1#0.checked"] = Claim("checked", "ev-checked-0001"),
            },
            Occ());

        var resolution = SemanticCheckedResolver.ResolveDetailed(belief, Switch, Canonical(record));

        Assert.True(resolution.IsObserved);
        Assert.Equal(CheckedState.Checked, resolution.Value.Value);
        Assert.Equal("cap-1", resolution.CaptureId);
        Assert.Equal(T0, resolution.CaptureTimestamp);
    }

    [Fact]
    public void T2_UncheckedClaim_ResolvesUnchecked()
    {
        var record = TypedRecord("ui.node.cap-1#0.checked", "unchecked", "ev-unchecked-01",
            HierarchyCapability.CheckedBooleanExact);
        var belief = Belief(
            new Dictionary<string, WorldClaim>
            {
                ["ui.node.cap-1#0.checked"] = Claim("unchecked", "ev-unchecked-01"),
            },
            Occ());

        var resolution = SemanticCheckedResolver.ResolveDetailed(belief, Switch, Canonical(record));

        Assert.True(resolution.IsObserved);
        Assert.Equal(CheckedState.Unchecked, resolution.Value.Value);
    }

    [Fact]
    public void T3_PartialClaim_Preserved_NotCollapsed()
    {
        var record = TypedRecord("ui.node.cap-1#0.checked", "partial", "ev-partial-0001",
            HierarchyCapability.CheckedTriState);
        var belief = Belief(
            new Dictionary<string, WorldClaim>
            {
                ["ui.node.cap-1#0.checked"] = Claim("partial", "ev-partial-0001"),
            },
            Occ());

        var resolution = SemanticCheckedResolver.ResolveDetailed(belief, Switch, Canonical(record));

        Assert.True(resolution.IsObserved);
        Assert.Equal(CheckedState.Partial, resolution.Value.Value);
    }

    [Fact]
    public void T4_UnknownConditions_FailClosedWithReason()
    {
        // 无 occurrence
        var noOccurrence = SemanticCheckedResolver.Resolve(
            Belief(new Dictionary<string, WorldClaim>
            {
                ["ui.node.cap-1#0.checked"] = Claim("checked", "ev-no-occ-0001"),
            }),
            Switch);
        Assert.Equal(FieldState.Unknown, noOccurrence.State);
        Assert.Equal("occurrence-absent", noOccurrence.Reason);

        // occurrence 歧义
        var ambiguous = SemanticCheckedResolver.Resolve(
            Belief(
                new Dictionary<string, WorldClaim>
                {
                    ["ui.node.cap-1#0.checked"] = Claim("checked", "ev-amb-a-00001"),
                },
                Occ(), Occ("switch") with { OccurrenceId = "occ-2" }),
            Switch);
        Assert.Equal(FieldState.Unknown, ambiguous.State);
        Assert.Equal("occurrence-ambiguous", ambiguous.Reason);

        // 无 claim（缺席 ≠ Unchecked）
        var noClaim = SemanticCheckedResolver.Resolve(
            Belief(new Dictionary<string, WorldClaim>(), Occ()), Switch);
        Assert.Equal(FieldState.Unknown, noClaim.State);
        Assert.Equal("no-checked-claim", noClaim.Reason);

        // capture mismatch：无 canonical 时序且多 capture 并存 → 不猜新旧
        var old = TypedRecord("ui.node.cap-old#0.checked", "checked", "ev-cap-old-0001",
            HierarchyCapability.CheckedBooleanCollapsed);
        var newRecord = TypedRecord("ui.node.cap-new#0.checked", "checked", "ev-cap-new-0001",
            HierarchyCapability.CheckedBooleanCollapsed, T0.AddSeconds(1));
        var mismatch = SemanticCheckedResolver.Resolve(
            Belief(
                new Dictionary<string, WorldClaim>
                {
                    ["ui.node.cap-old#0.checked"] = Claim("checked", "ev-cap-old-0001"),
                    ["ui.node.cap-new#0.checked"] = Claim("checked", "ev-cap-new-0001"),
                },
                Occ()),
            Switch);
        Assert.Equal(FieldState.Unknown, mismatch.State);
        Assert.Equal("checked-claim-ambiguous", mismatch.Reason);
        _ = (old, newRecord);
    }

    [Fact]
    public void T5_UnsupportedCapability_FailClosed()
    {
        // capture 元数据未声明任何 checked 能力 → Unsupported（非 Unknown 非 false）
        var record = TypedRecord("ui.node.cap-1#0.checked", "checked", "ev-unsupported1",
            HierarchyCapability.None);
        var belief = Belief(
            new Dictionary<string, WorldClaim>
            {
                ["ui.node.cap-1#0.checked"] = Claim("checked", "ev-unsupported1"),
            },
            Occ());

        var resolution = SemanticCheckedResolver.ResolveDetailed(belief, Switch, Canonical(record));

        Assert.Equal(FieldState.Unsupported, resolution.Value.State);
        Assert.Equal("capability:checked-absent", resolution.Value.Reason);
    }

    [Fact]
    public void NewestCapture_Wins_WhenCanonicalTimestampsAvailable()
    {
        var old = TypedRecord("ui.node.cap-old#0.checked", "checked", "ev-cap-old-0001",
            HierarchyCapability.CheckedBooleanCollapsed, T0);
        var newer = TypedRecord("ui.node.cap-new#0.checked", "unchecked", "ev-cap-new-0002",
            HierarchyCapability.CheckedBooleanExact, T0.AddSeconds(5));
        var belief = Belief(
            new Dictionary<string, WorldClaim>
            {
                ["ui.node.cap-old#0.checked"] = Claim("checked", "ev-cap-old-0001"),
                ["ui.node.cap-new#0.checked"] = Claim("unchecked", "ev-cap-new-0002"),
            },
            Occ());
        var canonical = Canonical(old, newer);

        var resolution = SemanticCheckedResolver.ResolveDetailed(belief, Switch, canonical);

        Assert.True(resolution.IsObserved);
        Assert.Equal(CheckedState.Unchecked, resolution.Value.Value);
        Assert.Equal("cap-new", resolution.CaptureId);
    }

    private static IReadOnlyDictionary<string, EvidenceRecord> Canonical(params EvidenceRecord[] records) =>
        records.ToDictionary(r => r.EvidenceId, r => r);
}
