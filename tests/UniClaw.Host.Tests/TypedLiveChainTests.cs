using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-013 Slice F 真机验证（ENVIRONMENT 门控 DSH_TEST_PERCEPTION_LIVE=1，
/// 需 API≥28 模拟器/设备）：real XML capture → typed observation → P2 →
/// WorldModel 全链。fixture 不得冒充本测试 PASS（owner Slice F 规则）。
/// 验证点：metadata 正确（getprop 对拍）、collapsed false 不变 Unchecked
/// （checked claim 数 == 原始 XML checked="true" 数）、typed claims 进 belief。
/// </summary>
public sealed class TypedLiveChainTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1";

    private static bool NoAdb =>
        Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") == "1";

    [Fact]
    public void RealDump_ToTypedObservation_ToP2_ToWorldModel()
    {
        if (!Enabled || NoAdb)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用或 DSH_TEST_NO_ADB=1");
            return;
        }

        var deviceId = Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_DEVICE") ?? "emulator-5554";

        // 1. real XML capture（有界：D8 传输）
        var (xml, isStructural) = UiAutomatorDump.TryDumpToDevice(deviceId);
        Assert.True(xml is not null, $"real dump 失败（structural={isStructural}）——本测试要求真机/模拟器");

        // 2. metadata 事实：adb getprop 对拍
        var apiLevel = UiAutomatorDump.TryGetApiLevel(deviceId);
        Assert.NotNull(apiLevel);
        Assert.True(apiLevel >= 28, $"API {apiLevel} < PER-010 floor 28");
        output.WriteLine($"device={deviceId} apiLevel={apiLevel} xmlBytes={xml!.Length}");

        // 3. typed observation
        var captureId = $"cap-live-{Guid.NewGuid():N}";
        var captureTime = DateTimeOffset.Now;
        var context = new UiAutomatorDump.UiHierarchyParseContext(
            CaptureId: captureId,
            CaptureTimestamp: captureTime,
            DeviceId: deviceId,
            SessionCorrelation: $"live-typed:{deviceId}",
            AndroidApiLevel: apiLevel.Value);
        var result = UiAutomatorDump.ParseHierarchyObservation(xml, context);
        Assert.Equal(UiHierarchyCaptureOutcome.Complete, result.Outcome);
        Assert.NotEmpty(result.Observation!.Nodes);
        output.WriteLine($"outcome={result.Outcome} nodes={result.Observation.Nodes.Count}");

        // 4. projector → P2 → WorldModel（既有 admission/reconciliation，零旁路）
        var proposals = TypedHierarchyProposalProjector.Project(result.Observation, ObservationContext.External);
        Assert.NotEmpty(proposals);
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

        // 5. metadata 正确：accepted record 的结构 descriptor 与采集事实一致
        var sample = ledger.CanonicalRecords.Values.First(r => r.Provenance.Hierarchy is not null);
        var descriptor = sample.Provenance.Hierarchy!;
        Assert.Equal(captureId, descriptor.CaptureId);
        Assert.Equal(apiLevel, descriptor.AndroidApiLevel);
        Assert.Equal(deviceId, descriptor.DeviceId);
        Assert.Equal(UiHierarchyAcquirerKind.LegacyUiAutomatorXml, descriptor.AcquirerKind);
        Assert.Equal(UiHierarchyFormat.UiAutomatorXml, descriptor.HierarchyFormat);

        // 6. collapsed false 不变 Unchecked（M-02 实机执法）：checked claim 数
        //    == 原始 XML checked="true" 出现数（false → Unknown → 无 claim）
        var checkedClaims = proposals
            .Where(p => p.Claim.Subject.EndsWith(".checked", StringComparison.Ordinal))
            .ToArray();
        var checkedTrueInXml = System.Text.RegularExpressions.Regex.Matches(xml, "checked=\"true\"").Count;
        Assert.Equal(checkedTrueInXml, checkedClaims.Length);
        Assert.DoesNotContain(checkedClaims, c => c.Claim.Value is "off" or "unchecked" or "false");
        output.WriteLine($"checkedClaims={checkedClaims.Length} (raw checked=true: {checkedTrueInXml})");

        // 7. belief 携带 typed occurrence-qualified claims
        var current = world.Current!;
        Assert.Equal(proposals.Count, current.EvidenceBasis.Count);
        Assert.Contains($"ui.node.{captureId}#0.class", current.WorldState.Keys);
        output.WriteLine($"beliefClaims={current.WorldState.Count} evidenceBasis={current.EvidenceBasis.Count}");
    }
}
