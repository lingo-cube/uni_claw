using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-013 Slice E / M-06：live feed XML 证据路由 cutover——typed per-node
/// 通道完全替代 legacy dump.Claims 通道；legacy {role}.state 映射按相位保留；
/// 无 dual-read（per-node surface 单路由）。reader inventory 见
/// changes/PER-013/state.md。
/// </summary>
public sealed class LiveXmlRouteCutoverTests
{
    private const string Fixture = """
        <hierarchy rotation="0">
          <node index="0" text="" resource-id="com.android.settings:id/wifi_switch" class="android.widget.Switch" package="com.android.settings" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[940,300][1040,360]"/>
          <node index="1" text="Network &amp; internet" resource-id="android:id/title" class="android.widget.TextView" package="com.android.settings" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[40,280][600,340]"/>
        </hierarchy>
        """;

    private static readonly DateTimeOffset Capture = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static UiAutomatorDump.UiHierarchyParseContext TypedContext(int apiLevel = 35) =>
        new(
            CaptureId: "cap-live-1",
            CaptureTimestamp: Capture,
            DeviceId: "emulator-5554",
            SessionCorrelation: "live:emulator-5554:screen-1",
            AndroidApiLevel: apiLevel);

    private static ObservationProposal MappedLegacyStateClaim() =>
        new(
            new ObservationClaim("switch.state", "on"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance(UiAutomatorDump.Producer, Capture, "scope:switch.state",
                new[] { "xml-map:wifi_switch", "xml-checkable:True", "xml-unique:true" }));

    [Fact]
    public void M06_TypedRoute_ReplacesLegacyPerNodeChannel_NoDualRead()
    {
        var extras = UiAutomatorDump.ComposeXmlEvidence(
            Fixture,
            mappedLegacyStateClaim: MappedLegacyStateClaim(),
            isPostPhase: false,
            typedContext: TypedContext(),
            context: ObservationContext.External);

        // typed per-node claims：occurrence-qualified subject
        var perNode = extras.Where(p => p.Claim.Subject.StartsWith("ui.node.", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(perNode);
        Assert.All(perNode, p =>
            Assert.StartsWith("ui.node.cap-live-1#", p.Claim.Subject, StringComparison.Ordinal));

        // legacy per-node 通道（dump.Claims 的 ui.node.{localId}.* 形状）不在场：
        // 无 capture 限定的 bare localId subject = dual-read 回潮 → 禁止
        Assert.DoesNotContain(perNode, p => p.Claim.Subject.StartsWith("ui.node.cap-live-1.", StringComparison.Ordinal));
        Assert.DoesNotContain(extras, p => p.Claim.Subject == "ui.node.wifi_switch.class");
        Assert.DoesNotContain(extras, p => p.Claim.Subject == "ui.node.title.text");

        // legacy {role}.state 映射 claim（initial 相位并置）保留——effect-critical
        // 旧 consumer 的 egress surface，按 PER-012 只保留至删除条件满足
        var state = extras.Single(p => p.Claim.Subject == "switch.state");
        Assert.Equal("on", state.Claim.Value);
    }

    [Fact]
    public void M06_PostPhase_MappedClaimNotDuplicated_TypedOnly()
    {
        var extras = UiAutomatorDump.ComposeXmlEvidence(
            Fixture,
            mappedLegacyStateClaim: MappedLegacyStateClaim(),
            isPostPhase: true,
            typedContext: TypedContext(),
            context: ObservationContext.PostActionEffectFlow);

        Assert.DoesNotContain(extras, p => p.Claim.Subject == "switch.state"); // post 相：映射走 stateClaim 主通道
        Assert.NotEmpty(extras.Where(p => p.Claim.Subject.StartsWith("ui.node.cap-live-1#", StringComparison.Ordinal)));
    }

    [Fact]
    public void M06_TypedContextUnavailable_PerNodeEvidenceHonestlyAbsent_NoLegacyFallback()
    {
        // typed 路由不可用（如 API level 未知）→ 不回退 legacy per-node 通道
        // （rollback = routing-only 显式动作并标 legacy/degraded，非自动回退）
        var extras = UiAutomatorDump.ComposeXmlEvidence(
            Fixture,
            mappedLegacyStateClaim: MappedLegacyStateClaim(),
            isPostPhase: false,
            typedContext: null,
            context: ObservationContext.External);

        var only = Assert.Single(extras);
        Assert.Equal("switch.state", only.Claim.Subject); // 仅 legacy 映射保留
        Assert.DoesNotContain(extras, p => p.Claim.Subject.StartsWith("ui.node.", StringComparison.Ordinal));
    }

    [Fact]
    public void M06_MalformedXml_TypedRouteFailClosed_NoPartialNodes()
    {
        var extras = UiAutomatorDump.ComposeXmlEvidence(
            "<hierarchy><node",
            mappedLegacyStateClaim: null,
            isPostPhase: false,
            typedContext: TypedContext(),
            context: ObservationContext.External);

        Assert.Empty(extras); // typed Malformed → 无部分节点；legacy 映射输入也缺席
    }

    [Fact]
    public void M06_TypedEvidenceAdmits_P2Roundtrip()
    {
        var extras = UiAutomatorDump.ComposeXmlEvidence(
            Fixture, MappedLegacyStateClaim(), isPostPhase: false,
            TypedContext(), ObservationContext.External);
        var ledger = new EvidenceLedger();

        foreach (var proposal in extras)
        {
            var (admission, _) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
        }

        Assert.Equal(extras.Length, ledger.CanonicalRecords.Count);
    }
}
