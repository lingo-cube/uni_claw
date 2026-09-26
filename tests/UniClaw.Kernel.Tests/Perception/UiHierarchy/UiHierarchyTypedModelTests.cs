using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception.UiHierarchy;

/// <summary>
/// PER-013 Gate A：typed domain invariants——missing ≠ false、Unsupported ≠ Unknown、
/// OccurrenceRef ≠ canonical identity、capture outcome 构造期 fail-closed 执法。
/// </summary>
public class UiHierarchyTypedModelTests
{
    private const string CaptureId = "cap-0001";

    private static CaptureMetadata Metadata(
        string captureId = CaptureId,
        CoverageCompleteness completeness = CoverageCompleteness.CompleteWithinDeclaredSurface,
        string? limitation = null) =>
        new(
            CaptureId: captureId,
            AndroidApiLevel: 35,
            AcquirerKind: UiHierarchyAcquirerKind.LegacyUiAutomatorXml,
            AcquirerVersion: "uiautomator-dump/1.0",
            HierarchyFormat: UiHierarchyFormat.UiAutomatorXml,
            CaptureTimestamp: DateTimeOffset.Parse("2026-09-27T10:00:00Z"),
            CaptureDuration: TimeSpan.FromSeconds(1.5),
            DeviceId: "emulator-5554",
            SessionCorrelation: "run-0001",
            ObservationCycleId: "cycle-0007",
            Capabilities: new HierarchyCapabilities(
                HierarchyCapability.SemanticText
                | HierarchyCapability.ContentDescription
                | HierarchyCapability.CheckedBooleanCollapsed),
            Coverage: new HierarchyCoverage(completeness, limitation));

    private static UiNodeObservation Node(string captureId = CaptureId, int index = 0) => new(
        OccurrenceRef: new OccurrenceRef(captureId, index),
        ParentOccurrenceRef: null,
        WindowOccurrenceRef: null,
        SiblingOrder: index,
        DrawingOrder: null,
        Class: ObservedValue<string>.Observed("android.widget.Switch"),
        ResourceId: ObservedValue<string>.Observed("com.android.settings:id/wifi_switch"),
        Package: ObservedValue<string>.Observed("com.android.settings"),
        Text: ObservedValue<string>.Observed("Wi-Fi"),
        ContentDescription: ObservedValue<string>.Observed("Wi-Fi"),
        Hint: ObservedValue<string>.Unsupported("capability:hint-absent"),
        Checkable: ObservedValue<bool>.Observed(true),
        Checked: ObservedValue<CheckedState>.Unknown(CheckedSemantics.PartialUnrepresentableReason),
        Enabled: ObservedValue<bool>.Observed(true),
        Selected: ObservedValue<bool>.Unknown("attribute-missing"),
        Focused: ObservedValue<bool>.Unknown("attribute-missing"),
        Scrollable: ObservedValue<bool>.Observed(false),
        Clickable: ObservedValue<bool>.Observed(true),
        Focusable: ObservedValue<bool>.Observed(true),
        VisibleToUser: ObservedValue<bool>.Unsupported("capability:visibility-absent"),
        Password: ObservedValue<bool>.Observed(false),
        Bounds: ObservedValue<UiBounds>.Observed(new UiBounds(0, 0, 1080, 200)));

    // ---- ObservedValue：missing ≠ false；empty string ≠ Unknown；Unsupported ≠ Unknown ----

    [Fact]
    public void MissingAttribute_IsUnknown_NotObservedFalse()
    {
        var missing = ObservedValue<bool>.Unknown("attribute-missing");
        var observedFalse = ObservedValue<bool>.Observed(false);

        Assert.NotEqual(missing, observedFalse);
        Assert.NotEqual(missing.State, observedFalse.State);
        Assert.False(missing.TryGetObserved(out _));
        Assert.True(observedFalse.TryGetObserved(out var value));
        Assert.False(value);
    }

    [Fact]
    public void EmptyStringText_IsObservedEmpty_NotUnknown()
    {
        var observedEmpty = ObservedValue<string>.Observed("");
        var unknown = ObservedValue<string>.Unknown("attribute-missing");

        Assert.Equal(FieldState.Observed, observedEmpty.State);
        Assert.True(observedEmpty.TryGetObserved(out var value));
        Assert.Equal(string.Empty, value);
        Assert.NotEqual(observedEmpty, unknown);
    }

    [Fact]
    public void Unsupported_IsDistinctFromUnknown()
    {
        var unsupported = ObservedValue<bool>.Unsupported("capability:visibility-absent");
        var unknown = ObservedValue<bool>.Unknown("not-determined-this-capture");

        Assert.NotEqual(unsupported.State, unknown.State);
        Assert.NotEqual(unsupported, unknown);
        Assert.False(unsupported.TryGetObserved(out _));
        Assert.False(unknown.TryGetObserved(out _));
    }

    // ---- OccurrenceRef ≠ canonical identity ----

    [Fact]
    public void OccurrenceRef_IsCaptureScoped_SameLocalIndexAcrossCaptures_NeverEqual()
    {
        var first = new OccurrenceRef(CaptureId, 3);
        var second = new OccurrenceRef("cap-0002", 3);

        Assert.NotEqual(first, second);
        Assert.Equal(first, new OccurrenceRef(CaptureId, 3));
    }

    [Fact]
    public void OccurrenceRef_SurfaceFrozen_CaptureIdAndLocalIndexOnly()
    {
        var props = typeof(OccurrenceRef).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "CaptureId", "LocalIndex" }, props);
    }

    // ---- observation capture-locality ----

    [Fact]
    public void Observation_RejectsForeignCaptureRefs()
    {
        var observation = new UiHierarchyObservation(
            Metadata(),
            Windows: Array.Empty<UiWindowOccurrence>(),
            Nodes: new[] { Node(captureId: "cap-9999", index: 0) });

        Assert.False(observation.IsValid);
        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Complete, Metadata(), observation, Diagnostic: null).Validate());
    }

    [Fact]
    public void Observation_RejectsForeignParentRef()
    {
        var node = Node() with { ParentOccurrenceRef = new OccurrenceRef("cap-9999", 0) };
        var observation = new UiHierarchyObservation(Metadata(), Array.Empty<UiWindowOccurrence>(), new[] { node });

        Assert.False(observation.IsValid);
    }

    // ---- capture outcome 构造期 fail-closed 执法 ----

    [Fact]
    public void CompleteOutcome_ValidObservation_Passes()
    {
        var observation = new UiHierarchyObservation(Metadata(), Array.Empty<UiWindowOccurrence>(), new[] { Node() });
        new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Complete, Metadata(), observation, null).Validate();
    }

    [Fact]
    public void CompleteOutcome_ZeroNodes_Rejected_MustBeEmpty()
    {
        var observation = new UiHierarchyObservation(Metadata(), Array.Empty<UiWindowOccurrence>(), Array.Empty<UiNodeObservation>());

        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Complete, Metadata(), observation, null).Validate());
    }

    [Fact]
    public void CompleteOutcome_PartialCoverage_Rejected()
    {
        var metadata = Metadata(completeness: CoverageCompleteness.Partial, limitation: "clipped");
        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(), new[] { Node() });

        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Complete, metadata, observation, null).Validate());
    }

    [Fact]
    public void EmptyOutcome_ZeroNodes_Passes()
    {
        var observation = new UiHierarchyObservation(Metadata(), Array.Empty<UiWindowOccurrence>(), Array.Empty<UiNodeObservation>());
        new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Empty, Metadata(), observation, null).Validate();
    }

    [Fact]
    public void EmptyOutcome_WithNodes_Rejected()
    {
        var observation = new UiHierarchyObservation(Metadata(), Array.Empty<UiWindowOccurrence>(), new[] { Node() });

        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Empty, Metadata(), observation, null).Validate());
    }

    [Fact]
    public void PartialOutcome_WithLimitation_Passes()
    {
        var metadata = Metadata(completeness: CoverageCompleteness.Partial, limitation: "virtualized-list");
        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(), new[] { Node() });
        new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Partial, metadata, observation, null).Validate();
    }

    [Fact]
    public void PartialOutcome_WithoutLimitation_Rejected()
    {
        var metadata = Metadata(completeness: CoverageCompleteness.Partial, limitation: null);

        Assert.False(metadata.IsValid);

        var observation = new UiHierarchyObservation(metadata, Array.Empty<UiWindowOccurrence>(), new[] { Node() });
        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(UiHierarchyCaptureOutcome.Partial, metadata, observation, null).Validate());
    }

    [Theory]
    [InlineData(UiHierarchyCaptureOutcome.SourceUnavailable)]
    [InlineData(UiHierarchyCaptureOutcome.Malformed)]
    public void FailureOutcome_WithObservation_Rejected_NoPartialNodes(UiHierarchyCaptureOutcome outcome)
    {
        var observation = new UiHierarchyObservation(Metadata(), Array.Empty<UiWindowOccurrence>(), new[] { Node() });

        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(outcome, Metadata(), observation, "diag").Validate());
    }

    [Theory]
    [InlineData(UiHierarchyCaptureOutcome.SourceUnavailable)]
    [InlineData(UiHierarchyCaptureOutcome.Malformed)]
    public void FailureOutcome_WithoutDiagnostic_Rejected(UiHierarchyCaptureOutcome outcome)
    {
        Assert.Throws<ArgumentException>(() =>
            new UiHierarchyCaptureResult(outcome, Metadata(), Observation: null, Diagnostic: null).Validate());
    }

    [Theory]
    [InlineData(UiHierarchyCaptureOutcome.SourceUnavailable)]
    [InlineData(UiHierarchyCaptureOutcome.Malformed)]
    public void FailureOutcome_NoObservationWithDiagnostic_Passes(UiHierarchyCaptureOutcome outcome)
    {
        new UiHierarchyCaptureResult(outcome, Metadata(), Observation: null, Diagnostic: "adb-timeout-3000ms").Validate();
    }

    // ---- capability precedence ----

    [Fact]
    public void ResolveCheckedCapability_Precedence_TriStateOverExactOverCollapsed()
    {
        Assert.Equal(
            CheckedCapability.CheckedTriState,
            new HierarchyCapabilities(HierarchyCapability.CheckedTriState | HierarchyCapability.CheckedBooleanExact | HierarchyCapability.CheckedBooleanCollapsed).ResolveCheckedCapability());
        Assert.Equal(
            CheckedCapability.CheckedBooleanExact,
            new HierarchyCapabilities(HierarchyCapability.CheckedBooleanExact | HierarchyCapability.CheckedBooleanCollapsed).ResolveCheckedCapability());
        Assert.Equal(
            CheckedCapability.CheckedBooleanCollapsed,
            new HierarchyCapabilities(HierarchyCapability.CheckedBooleanCollapsed).ResolveCheckedCapability());
        Assert.Null(new HierarchyCapabilities(HierarchyCapability.SemanticText).ResolveCheckedCapability());
    }

    // ---- metadata（F-C1：完成时刻 + 可选 duration）----

    [Fact]
    public void CaptureMetadata_CarriesTimestampAndOptionalDuration_ProvenanceOnly()
    {
        var metadata = Metadata();

        Assert.True(metadata.IsValid);
        Assert.Equal(TimeSpan.FromSeconds(1.5), metadata.CaptureDuration);
        Assert.Equal("cycle-0007", metadata.ObservationCycleId);

        var noDuration = Metadata() with { CaptureDuration = null };
        Assert.True(noDuration.IsValid);

        var negativeDuration = Metadata() with { CaptureDuration = TimeSpan.FromSeconds(-1) };
        Assert.False(negativeDuration.IsValid);
    }
}
