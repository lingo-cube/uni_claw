using System.Reflection;
using UniClaw.Kernel.Compatibility;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Compatibility;

/// <summary>
/// PER-013 Slice D：M-04（egress-only 无损投影）/ M-05（no-loss refusal——
/// Unknown/Partial/Conflicted → unavailable/degraded，绝不 ON/OFF）+
/// forward path 全链路（observation → projector → 既有 Admit → WorldModel）+
/// architecture closure（Compatibility namespace 不可达 P2/Ledger/WorldModel）。
/// </summary>
public class LegacyStateProjectionTests
{
    // ---- M-04 Egress-only legacy projection ----

    [Fact]
    public void M04_TypedChecked_ProjectsLosslessOn()
    {
        var result = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Observed(CheckedState.Checked));

        Assert.Equal("on", result.Value);
        Assert.True(result.IsLossless);
        Assert.False(result.Degraded);
    }

    [Fact]
    public void M04_TypedUnchecked_ProjectsLosslessOff()
    {
        var result = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Observed(CheckedState.Unchecked));

        Assert.Equal("off", result.Value);
        Assert.True(result.IsLossless);
    }

    // ---- M-05 No-loss projection refusal ----

    [Fact]
    public void M05_Unknown_ProjectsUnavailableDegraded_NeverOnOff()
    {
        var result = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Unknown(CheckedSemantics.PartialUnrepresentableReason));

        Assert.Equal("unavailable", result.Value);
        Assert.True(result.Degraded);
        Assert.Equal(CheckedSemantics.PartialUnrepresentableReason, result.Reason);
    }

    [Fact]
    public void M05_Unsupported_ProjectsUnavailableDegraded()
    {
        var result = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Unsupported("capability:checked-absent"));

        Assert.Equal("unavailable", result.Value);
        Assert.True(result.Degraded);
    }

    [Fact]
    public void M05_TypedPartial_ProjectsUnavailableDegraded()
    {
        // 保守裁决（owner Slice D 规则 + plan.md 记录）：legacy "partial" 通道
        // 从未经真实 legacy consumer 验证 → 迁移期不无损投递
        var result = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Observed(CheckedState.Partial));

        Assert.Equal("unavailable", result.Value);
        Assert.True(result.Degraded);
    }

    [Fact]
    public void M05_Conflict_ForcesDegraded_EvenWhenObserved()
    {
        var result = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Observed(CheckedState.Checked),
            conflictPresent: true);

        Assert.Equal("unavailable", result.Value);
        Assert.True(result.Degraded);
        Assert.Equal("conflicted", result.Reason);
    }

    [Theory]
    [InlineData(CheckedState.Partial)]
    public void M05_DegradedResults_NeverCarryOnOff(CheckedState state)
    {
        var degraded = LegacyStateProjection.Project(ObservedValue<CheckedState>.Observed(state));
        var unknown = LegacyStateProjection.Project(ObservedValue<CheckedState>.Unknown());
        var unsupported = LegacyStateProjection.Project(ObservedValue<CheckedState>.Unsupported());
        var conflicted = LegacyStateProjection.Project(
            ObservedValue<CheckedState>.Observed(CheckedState.Unchecked), conflictPresent: true);

        foreach (var result in new[] { degraded, unknown, unsupported, conflicted })
        {
            Assert.NotEqual("on", result.Value);
            Assert.NotEqual("off", result.Value);
            Assert.True(result.Degraded);
        }
    }

    // ---- Forward path：typed hierarchy → projector → P2 → Ledger → WorldModel ----

    [Fact]
    public void ForwardPath_ObservationToProjectorToAdmitToReconcile()
    {
        var captureId = "cap-fwd-1";
        var metadata = new CaptureMetadata(
            CaptureId: captureId,
            AndroidApiLevel: 35,
            AcquirerKind: UiHierarchyAcquirerKind.LegacyUiAutomatorXml,
            AcquirerVersion: "adb-uiautomator/legacy",
            HierarchyFormat: UiHierarchyFormat.UiAutomatorXml,
            CaptureTimestamp: DateTimeOffset.Parse("2026-09-27T12:00:00Z"),
            CaptureDuration: TimeSpan.FromSeconds(1),
            DeviceId: "emulator-5554",
            SessionCorrelation: "run-fwd-1",
            ObservationCycleId: "cycle-1",
            Capabilities: new HierarchyCapabilities(
                HierarchyCapability.SemanticText | HierarchyCapability.CheckedTriState),
            Coverage: new HierarchyCoverage(CoverageCompleteness.CompleteWithinDeclaredSurface));
        var provenance = new FieldProvenance(captureId);
        var node = new UiNodeObservation(
            OccurrenceRef: new OccurrenceRef(captureId, 0),
            ParentOccurrenceRef: null,
            WindowOccurrenceRef: null,
            SiblingOrder: 0,
            DrawingOrder: null,
            Class: ObservedValue<string>.Observed("android.widget.Switch"),
            ResourceId: ObservedValue<string>.Observed("com.android.settings:id/wifi_switch"),
            Package: ObservedValue<string>.Observed("com.android.settings"),
            Text: ObservedValue<string>.Observed("Wi-Fi"),
            ContentDescription: ObservedValue<string>.Observed("Wi-Fi"),
            Hint: ObservedValue<string>.Unsupported("capability:hint-absent"),
            Checkable: ObservedValue<bool>.Observed(true),
            Checked: ObservedValue<CheckedState>.Observed(CheckedState.Checked),
            Enabled: ObservedValue<bool>.Observed(true),
            Selected: ObservedValue<bool>.Unknown("attribute-missing"),
            Focused: ObservedValue<bool>.Observed(false),
            Scrollable: ObservedValue<bool>.Observed(false),
            Clickable: ObservedValue<bool>.Observed(true),
            Focusable: ObservedValue<bool>.Observed(true),
            VisibleToUser: ObservedValue<bool>.Unsupported("capability:visibility-absent"),
            Password: ObservedValue<bool>.Observed(false),
            Bounds: ObservedValue<UiBounds>.Observed(new UiBounds(940, 300, 1040, 360)));
        var observation = new UiHierarchyObservation(
            metadata, Array.Empty<UiWindowOccurrence>(), new[] { node });

        var proposals = TypedHierarchyProposalProjector.Project(observation, ObservationContext.External);
        var ledger = new EvidenceLedger();
        var world = new WorldModel(proposals.Select(p => p.Claim.Subject).ToHashSet());

        foreach (var proposal in proposals)
        {
            var (admission, record) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
            var judgment = world.JudgeRelevance(record!);
            Assert.True(judgment.IsRelevant);
            world.Reconcile(record!, judgment);
        }

        var current = world.Current;
        Assert.NotNull(current);
        Assert.Equal("checked", current!.WorldState[$"ui.node.{captureId}#0.checked"].Value);
        Assert.Equal("true", current.WorldState[$"ui.node.{captureId}#0.checkable"].Value);
        Assert.Equal("com.android.settings:id/wifi_switch",
            current.WorldState[$"ui.node.{captureId}#0.resource_id"].Value);
        Assert.Equal(proposals.Count, current.EvidenceBasis.Count);
        // Unknown（selected）与 Unsupported（hint/visible）不进 belief——缺检测不产观察
        Assert.DoesNotContain($"ui.node.{captureId}#0.selected", current.WorldState.Keys);
        Assert.DoesNotContain($"ui.node.{captureId}#0.hint", current.WorldState.Keys);
    }

    // ---- Architecture closure：legacy projection 不可达 P2/Ledger/WorldModel ----

    [Fact]
    public void Closure_CompatibilityNamespace_CannotReachP2LedgerWorldModel()
    {
        var forbidden = new HashSet<string>
        {
            typeof(EvidenceLedger).FullName!,
            typeof(ObservationProposal).FullName!,
            typeof(ObservationClaim).FullName!,
            typeof(EvidenceRecord).FullName!,
            typeof(AdmissionRecord).FullName!,
            typeof(WorldModel).FullName!,
        };

        var compatibilityTypes = typeof(LegacyStateProjection).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "UniClaw.Kernel.Compatibility")
            .ToArray();
        Assert.NotEmpty(compatibilityTypes);

        foreach (var type in compatibilityTypes)
        {
            AssertNoForbiddenReference(forbidden, $"{type.FullName} methods",
                type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => new[] { m.ReturnType }.Concat(m.GetParameters().Select(p => p.ParameterType))));
            AssertNoForbiddenReference(forbidden, $"{type.FullName} properties",
                type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(p => p.PropertyType));
            AssertNoForbiddenReference(forbidden, $"{type.FullName} fields",
                type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(f => f.FieldType));
        }
    }

    private static void AssertNoForbiddenReference(
        HashSet<string> forbidden, string surface, IEnumerable<Type> referenced)
    {
        foreach (var type in referenced)
        {
            Assert.False(
                forbidden.Contains(type.FullName),
                $"closure violation: {surface} references {type.FullName} "
                + "(Compatibility namespace is egress-only; legacy → typed/P2/Ledger/WorldModel must not exist)");
        }
    }
}
