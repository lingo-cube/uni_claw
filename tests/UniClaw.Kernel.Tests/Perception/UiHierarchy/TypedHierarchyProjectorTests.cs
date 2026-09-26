using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception.UiHierarchy;

/// <summary>
/// PER-013 Gate C：UiHierarchyObservation → TypedHierarchyProposalProjector →
/// 既有 EvidenceLedger.Admit → EvidenceRecord 全链路；结构 metadata 经
/// Provenance.Hierarchy 可恢复（零 lineage 字符串解析）；EvidenceId 确定性。
/// </summary>
public class TypedHierarchyProjectorTests
{
    private const string CaptureId = "cap-proj-1";

    private static CaptureMetadata Metadata() => new(
        CaptureId: CaptureId,
        AndroidApiLevel: 35,
        AcquirerKind: UiHierarchyAcquirerKind.LegacyUiAutomatorXml,
        AcquirerVersion: "adb-uiautomator/legacy",
        HierarchyFormat: UiHierarchyFormat.UiAutomatorXml,
        CaptureTimestamp: DateTimeOffset.Parse("2026-09-27T12:00:00Z"),
        CaptureDuration: TimeSpan.FromSeconds(1.5),
        DeviceId: "emulator-5554",
        SessionCorrelation: "run-proj-1",
        ObservationCycleId: "cycle-9",
        Capabilities: new HierarchyCapabilities(
            HierarchyCapability.SemanticText
            | HierarchyCapability.ContentDescription
            | HierarchyCapability.CheckedBooleanCollapsed),
        Coverage: new HierarchyCoverage(CoverageCompleteness.CompleteWithinDeclaredSurface));

    private static UiNodeObservation Node(
        int index,
        int? parentIndex = null,
        ObservedValue<CheckedState>? checkedValue = null,
        ObservedValue<string>? text = null,
        ObservedValue<bool>? enabled = null,
        ObservedValue<UiBounds>? bounds = null) => new(
        OccurrenceRef: new OccurrenceRef(CaptureId, index),
        ParentOccurrenceRef: parentIndex is { } p ? new OccurrenceRef(CaptureId, p) : null,
        WindowOccurrenceRef: null,
        SiblingOrder: index,
        DrawingOrder: null,
        Class: ObservedValue<string>.Observed("android.widget.Switch"),
        ResourceId: ObservedValue<string>.Observed("com.android.settings:id/wifi_switch"),
        Package: ObservedValue<string>.Observed("com.android.settings"),
        Text: text ?? ObservedValue<string>.Observed("Wi-Fi"),
        ContentDescription: ObservedValue<string>.Observed("Wi-Fi"),
        Hint: ObservedValue<string>.Unsupported("capability:hint-absent"),
        Checkable: ObservedValue<bool>.Observed(true),
        Checked: checkedValue ?? ObservedValue<CheckedState>.Observed(CheckedState.Checked),
        Enabled: enabled ?? ObservedValue<bool>.Observed(true),
        Selected: ObservedValue<bool>.Unknown("attribute-missing"),
        Focused: ObservedValue<bool>.Observed(false),
        Scrollable: ObservedValue<bool>.Observed(false),
        Clickable: ObservedValue<bool>.Observed(true),
        Focusable: ObservedValue<bool>.Observed(true),
        VisibleToUser: ObservedValue<bool>.Unsupported("capability:visibility-absent"),
        Password: ObservedValue<bool>.Observed(false),
        Bounds: bounds ?? ObservedValue<UiBounds>.Observed(new UiBounds(940, 300, 1040, 360)));

    private static UiHierarchyObservation Observation(params UiNodeObservation[] nodes) =>
        new(Metadata(), Array.Empty<UiWindowOccurrence>(), nodes);

    private static IReadOnlyList<ObservationProposal> Project(UiHierarchyObservation observation) =>
        TypedHierarchyProposalProjector.Project(observation, ObservationContext.External);

    [Fact]
    public void Project_CompleteObservation_OccurrenceQualifiedSubjects()
    {
        var proposals = Project(Observation(Node(0), Node(1, parentIndex: 0)));

        Assert.NotEmpty(proposals);
        Assert.All(proposals, p =>
            Assert.StartsWith($"ui.node.{CaptureId}#", p.Claim.Subject, StringComparison.Ordinal));
        // capture 限定 + 局部序号（F-D1）：subject 内含 captureId，跨 capture 不可误连续
        var checked0 = proposals.Single(p => p.Claim.Subject == $"ui.node.{CaptureId}#0.checked");
        var checked1 = proposals.Single(p => p.Claim.Subject == $"ui.node.{CaptureId}#1.checked");
        Assert.NotEqual(checked0.Claim.Subject, checked1.Claim.Subject);
    }

    [Fact]
    public void Project_OnlyObservedFields_CheckedTypedValueDomain()
    {
        var proposals = Project(Observation(Node(0)));

        var value = proposals.Single(p => p.Claim.Subject.EndsWith(".checked", StringComparison.Ordinal));
        Assert.Equal("checked", value.Claim.Value); // typed 名，非 on/off

        // Unknown（selected）与 Unsupported（hint/visible）不产 claim
        Assert.DoesNotContain(proposals, p => p.Claim.Subject.EndsWith(".selected", StringComparison.Ordinal));
        Assert.DoesNotContain(proposals, p => p.Claim.Subject.EndsWith(".hint", StringComparison.Ordinal));
        Assert.DoesNotContain(proposals, p => p.Claim.Subject.EndsWith(".visible_to_user", StringComparison.Ordinal));
        // Observed(false)（focused）照常投影——false 是观测值
        var focused = proposals.Single(p => p.Claim.Subject.EndsWith(".focused", StringComparison.Ordinal));
        Assert.Equal("false", focused.Claim.Value);
    }

    [Fact]
    public void Project_CollapsedFalse_Unknown_EmitsNoCheckedClaim()
    {
        // M-02 经 projector：collapsed false → Unknown → 不产 checked claim（≠ off）
        var proposals = Project(Observation(Node(0,
            checkedValue: ObservedValue<CheckedState>.Unknown(CheckedSemantics.PartialUnrepresentableReason))));

        Assert.DoesNotContain(proposals, p => p.Claim.Subject.EndsWith(".checked", StringComparison.Ordinal));
        Assert.DoesNotContain(proposals, p => p.Claim.Value is "off" or "unchecked" or "false" && p.Claim.Subject.EndsWith(".checked", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_UnknownAndEmpty_EmitNoClaims_MissingNotFalse()
    {
        var proposals = Project(Observation(Node(0,
            text: ObservedValue<string>.Observed(""), // 空串：合法观测但不产 claim
            enabled: ObservedValue<bool>.Unknown("attribute-missing")))); // 缺席 ≠ false

        Assert.DoesNotContain(proposals, p => p.Claim.Subject.EndsWith(".text", StringComparison.Ordinal));
        Assert.DoesNotContain(proposals, p => p.Claim.Subject.EndsWith(".enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_EmptyCapture_ZeroProposals()
    {
        var proposals = Project(Observation());

        Assert.Empty(proposals); // A8：Empty ≠ absence claim
    }

    [Fact]
    public void Project_InvalidObservation_FailClosed()
    {
        var foreign = Node(0) with { OccurrenceRef = new OccurrenceRef("cap-other", 0) };

        Assert.Throws<InvalidOperationException>(() => Project(Observation(foreign)));
    }

    [Fact]
    public void Project_LineageCarriesNoStructuredProtocolStrings()
    {
        var proposals = Project(Observation(Node(0)));

        // lineage 只有进程标记；CaptureId 等 metadata 不得走字符串（xml-map:/api:NN 禁止回潮）
        Assert.All(proposals, p =>
        {
            Assert.Equal(new[] { TypedHierarchyProposalProjector.LineageMarker }, p.Provenance!.TransformationLineage);
            Assert.DoesNotContain("xml-map:", p.Provenance.TransformationLineage, StringComparer.Ordinal);
            Assert.DoesNotContain("xml-checkable:", p.Provenance.TransformationLineage, StringComparer.Ordinal);
        });
    }

    [Fact]
    public void ProjectThenAdmit_AcceptedRecord_DescriptorFullyRecoverable()
    {
        var ledger = new EvidenceLedger();
        var proposals = Project(Observation(Node(0), Node(1, parentIndex: 0)));

        foreach (var proposal in proposals)
        {
            var (admission, record) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
            Assert.NotNull(record);

            // 结构 metadata 从 accepted EvidenceRecord 直接恢复——零 lineage 解析
            var descriptor = record!.Provenance.Hierarchy;
            Assert.NotNull(descriptor);
            Assert.Equal(CaptureId, descriptor!.CaptureId);
            Assert.Equal(35, descriptor.AndroidApiLevel);
            Assert.Equal(UiHierarchyAcquirerKind.LegacyUiAutomatorXml, descriptor.AcquirerKind);
            Assert.Equal("adb-uiautomator/legacy", descriptor.AcquirerVersion);
            Assert.Equal(UiHierarchyFormat.UiAutomatorXml, descriptor.HierarchyFormat);
            Assert.Equal("emulator-5554", descriptor.DeviceId);
            Assert.Equal("run-proj-1", descriptor.SessionCorrelation);
            Assert.Equal("cycle-9", descriptor.ObservationCycleId);
            Assert.Equal(TimeSpan.FromSeconds(1.5), descriptor.CaptureDuration);
            Assert.Equal(CoverageCompleteness.CompleteWithinDeclaredSurface, descriptor.CoverageCompleteness);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Field));
            if (descriptor.NodeLocalIndex == 1)
            {
                Assert.Equal(0, descriptor.ParentLocalIndex); // parent 关联保留
            }
        }

        // 存在 checked 字段的 descriptor（字段名精确匹配）
        Assert.Contains(proposals, p => p.Provenance!.Hierarchy!.Field == "checked");
        Assert.Equal(proposals.Count, ledger.CanonicalRecords.Count);
    }

    [Fact]
    public void EvidenceId_Deterministic_MetadataParticipates_LegacyUnchanged()
    {
        var first = Project(Observation(Node(0)));
        var second = Project(Observation(Node(0)));

        // 同输入 → 同 EvidenceId（确定性）
        Assert.Equal(
            EvidenceLedger.ComputeEvidenceId(first[0]),
            EvidenceLedger.ComputeEvidenceId(second[0]));

        // metadata 参与 id：不同 capture 同值 claim → 不同 EvidenceId
        var otherCapture = first[0] with
        {
            Provenance = first[0].Provenance! with
            {
                Hierarchy = first[0].Provenance.Hierarchy! with { CaptureId = "cap-proj-2" },
            },
        };
        Assert.NotEqual(
            EvidenceLedger.ComputeEvidenceId(first[0]),
            EvidenceLedger.ComputeEvidenceId(otherCapture));

        // legacy path（descriptor 恒 null）→ EvidenceId 与既有拼法一致：
        // null 不追加内容（此处以「同 claim ± descriptor 必不同」反向锁住 null 分支）
        var legacy = first[0] with { Provenance = first[0].Provenance! with { Hierarchy = null } };
        Assert.NotEqual(
            EvidenceLedger.ComputeEvidenceId(first[0]),
            EvidenceLedger.ComputeEvidenceId(legacy));
        Assert.Equal(
            EvidenceLedger.ComputeEvidenceId(legacy),
            EvidenceLedger.ComputeEvidenceId(legacy with { })); // null 分支自身确定
    }
}
