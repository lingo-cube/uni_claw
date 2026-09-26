using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-009 closure audit A-4 测试债（PER-013 Slice B 偿还）：MapTargetStateClaim
/// IoU/唯一余量/checkable guard、D8 探测状态机（CoObserveXml 决策核心 +
/// ResetForNewRun）、degraded:no-xml lineage 标记。全部确定性、零 adb。
/// </summary>
public sealed class UiAutomatorDumpDebtTests
{
    private static readonly DateTimeOffset Capture = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static UiAutomatorDump.DumpResult Parse(string xml) =>
        UiAutomatorDump.Parse(xml, Capture, ObservationContext.External);

    // ---- MapTargetStateClaim：IoU / 唯一余量 / checkable guard ----

    private const string SingleSwitchXml = """
        <hierarchy rotation="0">
          <node index="0" text="" resource-id="com.android.settings:id/wifi_switch" class="android.widget.Switch" package="com.android.settings" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" bounds="[940,300][1040,360]"/>
        </hierarchy>
        """;

    [Fact]
    public void MapTargetState_UniqueStrongOverlap_ProducesStateClaimWithLineage()
    {
        var dump = Parse(SingleSwitchXml);
        var target = (940 / 1080.0, 300 / 1920.0, 1040 / 1080.0, 360 / 1920.0);

        var claim = UiAutomatorDump.MapTargetStateClaim(
            dump, "switch", target, 1080, 1920, Capture, ObservationContext.External);

        Assert.NotNull(claim);
        Assert.Equal("switch.state", claim!.Claim.Subject);
        Assert.Equal("on", claim.Claim.Value); // checked=true → on
        Assert.Equal(UiAutomatorDump.Producer, claim.Provenance!.Producer);
        Assert.Contains(claim.Provenance.TransformationLineage, l => l.StartsWith("xml-map:", StringComparison.Ordinal));
        Assert.Contains("xml-checkable:True", claim.Provenance.TransformationLineage);
        Assert.Contains("xml-unique:true", claim.Provenance.TransformationLineage);
    }

    [Fact]
    public void MapTargetState_BelowIoUThreshold_ReturnsNull()
    {
        var dump = Parse(SingleSwitchXml);
        var farTarget = (0.0, 1000 / 1920.0, 100 / 1080.0, 1100 / 1920.0);

        Assert.Null(UiAutomatorDump.MapTargetStateClaim(
            dump, "switch", farTarget, 1080, 1920, Capture, ObservationContext.External));
    }

    [Fact]
    public void MapTargetState_AmbiguousMargin_ReturnsNull()
    {
        // 两个同位 checkable 节点：IoU 并列第一，余量 0 < 0.25 → 不裁决
        var dump = Parse("""
            <hierarchy rotation="0">
              <node index="0" text="" resource-id="id/a" class="android.widget.Switch" package="p" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" bounds="[940,300][1040,360]"/>
              <node index="1" text="" resource-id="id/b" class="android.widget.Switch" package="p" checkable="true" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" bounds="[940,300][1040,360]"/>
            </hierarchy>
            """);
        var target = (940 / 1080.0, 300 / 1920.0, 1040 / 1080.0, 360 / 1920.0);

        Assert.Null(UiAutomatorDump.MapTargetStateClaim(
            dump, "switch", target, 1080, 1920, Capture, ObservationContext.External));
    }

    [Fact]
    public void MapTargetState_UniquelyOverlappingNonCheckable_ReturnsNull()
    {
        // D12 guard：唯一最佳重叠但 checkable=false → checked 不具权威，不映射
        var dump = Parse("""
            <hierarchy rotation="0">
              <node index="0" text="Network" resource-id="id/title" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" bounds="[40,280][600,340]"/>
            </hierarchy>
            """);
        var target = (40 / 1080.0, 280 / 1920.0, 600 / 1080.0, 340 / 1920.0);

        Assert.Null(UiAutomatorDump.MapTargetStateClaim(
            dump, "switch", target, 1080, 1920, Capture, ObservationContext.External));
    }

    // ---- D8 决策核心：CoObserveXml（探测门 / 结构性 vs 瞬时 / 成功 / 非法 XML）----

    [Fact]
    public void CoObserveXml_TransientFailure_DegradedWithoutConsumingBudget()
    {
        var probe = new UiAutomatorDump.ProbeStateMachine();
        var calls = 0;
        var (dump, _, degraded) = UiAutomatorDump.CoObserveXml(
            probe, Capture, Capture, ObservationContext.External,
            transport: () => { calls++; return ((string?)null, IsStructural: false); });

        Assert.Null(dump);
        Assert.True(degraded);
        Assert.Equal(1, calls);
        // 瞬时不占次数：下一刻仍可探测
        Assert.True(probe.ShouldProbe(Capture.AddSeconds(1)));
    }

    [Fact]
    public void CoObserveXml_StructuralFailure_ConsumesBudget_ThenExhaustedGate()
    {
        var probe = new UiAutomatorDump.ProbeStateMachine();
        var calls = 0;
        (string?, bool) Structural()
        {
            calls++;
            return (null, true);
        }

        // 60s 窗口内一次，窗口外逐次耗尽 3 次
        UiAutomatorDump.CoObserveXml(probe, Capture, Capture, ObservationContext.External, Structural);
        UiAutomatorDump.CoObserveXml(probe, Capture.AddSeconds(61), Capture, ObservationContext.External, Structural);
        UiAutomatorDump.CoObserveXml(probe, Capture.AddSeconds(122), Capture, ObservationContext.External, Structural);
        Assert.Equal(3, calls);
        Assert.True(probe.Exhausted);

        // 超限后：不调传输，直接 degraded（持续标记）
        var (dump, _, degraded) = UiAutomatorDump.CoObserveXml(
            probe, Capture.AddSeconds(183), Capture, ObservationContext.External, Structural);
        Assert.Null(dump);
        Assert.True(degraded);
        Assert.Equal(3, calls); // gate 先于 transport
    }

    [Fact]
    public void CoObserveXml_Success_ReturnsDumpNotDegraded()
    {
        var probe = new UiAutomatorDump.ProbeStateMachine();
        var (dump, _, degraded) = UiAutomatorDump.CoObserveXml(
            probe, Capture, Capture, ObservationContext.External,
            transport: () => (SingleSwitchXml, IsStructural: false));

        Assert.NotNull(dump);
        Assert.False(degraded);
        Assert.NotEmpty(dump!.Claims);
        Assert.Equal(UiAutomatorDump.Producer, dump.Claims[0].Provenance!.Producer);
    }

    [Fact]
    public void CoObserveXml_MalformedXml_MarksTransient_Degraded()
    {
        var probe = new UiAutomatorDump.ProbeStateMachine();
        var (dump, _, degraded) = UiAutomatorDump.CoObserveXml(
            probe, Capture, Capture, ObservationContext.External,
            transport: () => ("<hierarchy><node", IsStructural: false));

        Assert.Null(dump);
        Assert.True(degraded);
        // 非法 XML = 瞬时：不占探测次数
        Assert.True(probe.ShouldProbe(Capture.AddSeconds(1)));
    }

    // ---- ResetForNewRun：Run 边界重置（C-3）----

    [Fact]
    public void ProbeStateMachine_ResetForNewRun_RestoresBudget()
    {
        var probe = new UiAutomatorDump.ProbeStateMachine();
        var t = Capture;
        probe.MarkUnavailable(t);                    // 1
        probe.MarkUnavailable(t.AddSeconds(61));    // 2
        probe.MarkUnavailable(t.AddSeconds(122));   // 3 → Exhausted
        Assert.True(probe.Exhausted);
        Assert.False(probe.ShouldProbe(t.AddSeconds(183)));

        probe.ResetForNewRun();
        Assert.False(probe.Exhausted);
        Assert.True(probe.ShouldProbe(t.AddSeconds(184)));
    }

    // ---- degraded:no-xml lineage 标记（D1：缺席即数据）----

    [Fact]
    public void TagDegradedNoXml_AppendsMarker_PreservesExistingLineage()
    {
        var withLineage = new ObservationProposal(
            new ObservationClaim("switch.state", "on"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("host.live", Capture, "scope:switch.state", new[] { "live:frame:on" }));
        var withoutProvenance = new ObservationProposal(
            new ObservationClaim("ui.screen", "screen-1"),
            IngressKind.Observation, ObservationContext.External, Provenance: null);

        var tagged = UiAutomatorDump.TagDegradedNoXml(new[] { withLineage, withoutProvenance });

        Assert.Equal("live:frame:on", tagged[0].Provenance!.TransformationLineage[0]);
        Assert.Equal("degraded:no-xml", tagged[0].Provenance.TransformationLineage[1]);
        Assert.Null(tagged[1].Provenance); // 不造 provenance
    }
}
