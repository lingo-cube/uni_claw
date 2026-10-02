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
}
