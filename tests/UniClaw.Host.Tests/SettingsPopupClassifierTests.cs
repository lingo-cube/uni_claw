using System.Xml.Linq;
using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-009 §1 — 确定性 XML 结构弹窗分类器 + typed 声明 proposal。
/// 判据：窗口根 package ≠ hostPackage ∨ resource-id ∈ 白名单；几何不作判据；
/// 无 XML ⇒ 无声明（绝不从 Fast/截图伪造结构事实）。
/// </summary>
public sealed class SettingsPopupClassifierTests
{
    private const string HostPackage = "com.android.settings";

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    /// <summary>e1 真机语料：弹窗 XML（com.android.permissioncontroller + parentPanel）。</summary>
    private static string PopupXml()
    {
        var path = Path.Combine(RepoRoot(),
            "evidence", "real-settings-coverage-negative-20261001", "run-e1-with-evidence",
            "evidence", "capture-3e3809ea136e408aaa72f75513953086.xml");
        return File.ReadAllText(path);
    }

    /// <summary>e1 真机语料：正常 Settings 页 XML（package=com.android.settings）。</summary>
    private static string NormalSettingsXml()
    {
        var path = Path.Combine(RepoRoot(),
            "evidence", "real-settings-coverage-negative-20261001", "run-e1-with-evidence",
            "evidence", "capture-0d1006df470e4e0391fb8bb8b7f97719.xml");
        return File.ReadAllText(path);
    }

    [Fact]
    public void RealPopupXml_ClassifiesPresent()
    {
        Assert.Equal("present", SettingsTraversalLiveFeed.DerivePopupState(PopupXml(), HostPackage));
    }

    [Fact]
    public void RealNormalSettingsXml_ClassifiesAbsent()
    {
        Assert.Equal("absent", SettingsTraversalLiveFeed.DerivePopupState(NormalSettingsXml(), HostPackage));
    }

    [Fact]
    public void NullXml_YieldsNoClaim()
    {
        Assert.Null(SettingsTraversalLiveFeed.DerivePopupState(null, HostPackage));
    }

    [Fact]
    public void UnparseableXml_YieldsNoClaim()
    {
        Assert.Null(SettingsTraversalLiveFeed.DerivePopupState("<hierarchy><node></hierarchy>", HostPackage));
    }

    [Fact]
    public void WhitelistedResourceId_ClassifiesPresent_EvenUnderHostPackage()
    {
        const string xml = """
            <?xml version='1.0'?><hierarchy><node resource-id="" class="FrameLayout" package="com.android.settings">
            <node resource-id="android:id/alertTitle" class="TextView" package="com.android.settings"/>
            </node></hierarchy>
            """;
        Assert.Equal("present", SettingsTraversalLiveFeed.DerivePopupState(xml, HostPackage));
    }

    [Fact]
    public void HostPackageRoot_WithoutWhitelist_ClassifiesAbsent()
    {
        const string xml = """
            <?xml version='1.0'?><hierarchy><node resource-id="" class="FrameLayout" package="com.android.settings">
            <node resource-id="com.android.settings:id/homepage_title" class="TextView" package="com.android.settings"/>
            </node></hierarchy>
            """;
        Assert.Equal("absent", SettingsTraversalLiveFeed.DerivePopupState(xml, HostPackage));
    }

    [Fact]
    public void PopupProposal_EntersCycleBatch_WithProvenanceShape()
    {
        var now = DateTimeOffset.Parse("2026-10-02T09:00:00Z");
        var proposal = SettingsTraversalLiveFeed.PopupProposal(
            "present", "capture-abc", "settings-cycle-001", now, ObservationContext.External);

        Assert.NotNull(proposal);
        Assert.Equal("ui.overlay.popup", proposal!.Claim.Subject);
        Assert.Equal("present", proposal.Claim.Value);
        Assert.Equal(IngressKind.Observation, proposal.Kind);
        Assert.Equal("host.live.settings", proposal.Provenance!.Producer);
        Assert.Equal(now, proposal.Provenance.CaptureTime);
        Assert.Contains("settings-cycle-001", proposal.Provenance.Scope, StringComparison.Ordinal);
        Assert.Contains("capture:capture-abc", proposal.Provenance.TransformationLineage);
    }

    [Fact]
    public void PopupProposal_NullState_ProducesNoProposal()
    {
        Assert.Null(SettingsTraversalLiveFeed.PopupProposal(
            null, "capture-abc", "settings-cycle-001", DateTimeOffset.UtcNow, ObservationContext.External));
    }

    [Fact]
    public void PopupProposal_IsAdmittedByKernelP2_WhenInScope()
    {
        // 弹窗声明与 route claim 同形：在 scope 内经既有 P2 接纳为世界证据。
        var now = DateTimeOffset.Parse("2026-10-02T09:00:00Z");
        var proposal = SettingsTraversalLiveFeed.PopupProposal(
            "present", "capture-abc", "settings-cycle-001", now, ObservationContext.External)!;
        var kernel = new UniClaw.Kernel.UniKernel(
            new UniClaw.Kernel.Evidence.EvidenceLedger(),
            new UniClaw.Kernel.World.WorldModel(
                new HashSet<string> { UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.PopupSubject }),
            UniClaw.Kernel.Trace.DisabledRunTrace.Instance);

        var result = kernel.Process(proposal);

        Assert.Equal(UniClaw.Kernel.Evidence.AdmissionDecision.Accepted, result.Admission.Decision);
        Assert.Empty(kernel.EffectReceipts);
    }

    // ---- AGT-009 §8：有界 Slow 触发谓词与请求构造 -------------------------

    [Fact]
    public void SlowTrigger_NoXml_Predicted()
    {
        Assert.Equal("NoXml", SettingsTraversalLiveFeed.DeriveSlowTrigger(
            hierarchyAvailable: false, fastAvailable: true, clickableNodeCount: 0,
            screenIdentity: "android.settings|route:Settings", popupPresentStreak: 0, slow: null));
    }

    [Fact]
    public void SlowTrigger_StructuralVisualConflict_Predicted()
    {
        // fast 有检出而 hierarchy 零可点击节点（documented deterministic predicate）。
        Assert.Equal("StructuralVisualConflict", SettingsTraversalLiveFeed.DeriveSlowTrigger(
            hierarchyAvailable: true, fastAvailable: true, clickableNodeCount: 0,
            screenIdentity: "android.settings|route:Settings", popupPresentStreak: 0, slow: null));
        // 反向：fast 无检出而 hierarchy 有可点击节点。
        Assert.Equal("StructuralVisualConflict", SettingsTraversalLiveFeed.DeriveSlowTrigger(
            hierarchyAvailable: true, fastAvailable: false, clickableNodeCount: 5,
            screenIdentity: "android.settings|route:Settings", popupPresentStreak: 0, slow: null));
    }

    [Fact]
    public void SlowTrigger_PopupStreakAndSemanticUnclear_Predicted()
    {
        Assert.Equal("PopupConsecutiveFailures", SettingsTraversalLiveFeed.DeriveSlowTrigger(
            hierarchyAvailable: true, fastAvailable: true, clickableNodeCount: 3,
            screenIdentity: "android.settings|route:Settings", popupPresentStreak: 2,
            slow: new UniClaw.Host.SettingsCoverage.SlowTriggerConfig()));
        Assert.Equal("SemanticUnclear", SettingsTraversalLiveFeed.DeriveSlowTrigger(
            hierarchyAvailable: true, fastAvailable: true, clickableNodeCount: 3,
            screenIdentity: "android.settings", popupPresentStreak: 0, slow: null));
        Assert.Null(SettingsTraversalLiveFeed.DeriveSlowTrigger(
            hierarchyAvailable: true, fastAvailable: true, clickableNodeCount: 3,
            screenIdentity: "android.settings|route:Settings", popupPresentStreak: 0, slow: null));
    }

    [Fact]
    public void SlowRequest_BuildsFromTrigger_WithBoundedContext()
    {
        var now = DateTimeOffset.Parse("2026-10-02T09:00:00Z");
        var request = SettingsTraversalLiveFeed.BuildSlowRequest(
            "NoXml", visual: false, screenshot: new byte[] { 1 },
            "capture-abc", now, "settings-cycle-001");
        Assert.Equal("slow-NoXml-capture-abc", request.RequestId);
        Assert.Equal("ui.screen.route", request.ClaimSubject);
        Assert.False(request.RequiresRawArtifact);
        Assert.Null(request.RawArtifact);
        Assert.Equal("slow-trigger:NoXml", request.Reason);
        Assert.Equal("settings-cycle-001", request.ObservationCycleId);

        var popup = SettingsTraversalLiveFeed.BuildSlowRequest(
            "PopupConsecutiveFailures", visual: true, screenshot: new byte[] { 1 },
            "capture-abc", now, "settings-cycle-001");
        Assert.Equal("ui.overlay.popup", popup.ClaimSubject);
        Assert.True(popup.RequiresRawArtifact);
        Assert.NotNull(popup.RawArtifact);
    }

    [Fact]
    public void SlowTrace_FormatCarriesStatusAndProjection()
    {
        var trace = SettingsTraversalLiveFeed.FormatSlowTrace("NoXml",
            new SlowConsultationOutcome(SlowConsultationStatus.TimedOut, false, 0, "timeout", false));
        Assert.Equal("NoXml|TimedOut|projected=0", trace);
        var late = SettingsTraversalLiveFeed.FormatSlowTrace("NoXml",
            new SlowConsultationOutcome(SlowConsultationStatus.Succeeded, true, 1, null, true));
        Assert.Equal("NoXml|Succeeded|projected=1|late", late);
    }
}
