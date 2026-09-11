using System.Text;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-005 A1（P-1 同源对拍，DETERMINISTIC）——live strategy 解析路径与
/// corpus 导入路径（tools/legacy-perception-import/import.cs 的 DIRECT 映射
/// 产物）在同源 golden-run-v1 真实服务响应上产出语义一致的 subject/value
/// 集合（replay parity 是本 change 最硬验收）。附：确定性（同 artifact 同
/// observations）、OK_EMPTY 零 observation、非法 envelope fail-closed。
/// </summary>
public sealed class LiveVisionStrategyParityTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 9, 9, 10, 30, 0, TimeSpan.Zero);
    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    private static byte[] GoldenJsonBytes() =>
        File.ReadAllBytes(Path.Combine(CorpusRoot, "legacy-direct", "golden-run-v1", "case-a-before.json"));

    private static RawArtifact GoldenArtifact() => RawArtifact.Capture(
        GoldenJsonBytes(),
        new ArtifactMetadata(1080, 1920, "artifact", CaptureTime, CaptureScope: "derived:vision-service:art-test"));

    // ---- A1：同源对拍 ------------------------------------------------------

    [Fact]
    public void P1_LiveStrategyParityWithCorpusImport_SameSubjectValueSet()
    {
        var corpus = CorpusManifest.Load().Scenario("golden-case-a-before");
        var live = new LiveVisionStrategy().Observe(GoldenArtifact());

        // 集合语义一致（PER-005 A1：「subject/value 集合语义一致」）
        var expected = corpus.Observations
            .Select(o => (o.Subject, o.Value)).ToHashSet();
        var actual = live.Select(o => (o.Subject, o.Value)).ToHashSet();
        Assert.True(expected.SetEquals(actual),
            $"parity 破坏：仅 corpus [{string.Join(",", expected.Except(actual))}] / " +
            $"仅 live [{string.Join(",", actual.Except(expected))}]");
        Assert.Equal(expected.Count, actual.Count);

        // 数量级守护：golden 帧 = 16 yolo + 11 ocr → 54 observations
        Assert.Equal(54, live.Count);
    }

    // ---- 确定性与语义边界 --------------------------------------------------

    [Fact]
    public void SameArtifact_ProducesIdenticalObservations_FCYDeterminism()
    {
        var strategy = new LiveVisionStrategy();
        var first = strategy.Observe(GoldenArtifact());
        var second = strategy.Observe(GoldenArtifact());
        Assert.Equal(first, second);
    }

    [Fact]
    public void EmptyYoloOcr_OkEmptyYieldsZeroObservations()
    {
        var payload = Encoding.UTF8.GetBytes("""{"yolo":[],"ocr":[],"candidates":[]}""");
        var artifact = RawArtifact.Capture(
            payload, new ArtifactMetadata(8, 8, "artifact", CaptureTime));
        Assert.Empty(new LiveVisionStrategy().Observe(artifact));
    }

    [Fact]
    public void MissingEnvelopeArrays_FailClosed_Throws()
    {
        var payload = Encoding.UTF8.GetBytes("""{"candidates":[]}""");
        var artifact = RawArtifact.Capture(
            payload, new ArtifactMetadata(8, 8, "artifact", CaptureTime));
        Assert.Throws<InvalidOperationException>(() => new LiveVisionStrategy().Observe(artifact));
    }

    [Fact]
    public void MalformedJson_FailClosed_Throws()
    {
        var artifact = RawArtifact.Capture(
            "not-json"u8.ToArray(), new ArtifactMetadata(8, 8, "artifact", CaptureTime));
        Assert.Throws<InvalidOperationException>(() => new LiveVisionStrategy().Observe(artifact));
    }

    [Fact]
    public void VersionedSeam_StableIdentityAndVersion()
    {
        // FCR-001 cache 参与前提：identity/version 稳定且无进程随机源
        var strategy = new LiveVisionStrategy();
        Assert.Equal("perception.live.vision", strategy.StrategyIdentity);
        Assert.Equal("1", strategy.StrategyVersion);
        Assert.Equal(strategy.StrategyIdentity, new LiveVisionStrategy().StrategyIdentity);
    }

    [Fact]
    public void OcrSubjectsUseFrameOrdinalIndex_NotTokenId_CorpusConvention()
    {
        // corpus 约定：ui.text.ocr{i} 用帧内顺序索引（token id 如 "ocr_1" 不进 subject）
        var json = """
            {"yolo":[],"ocr":[
              {"id":"ocr_7","text":"alpha","boundsPx":[1,2,3,4]},
              {"id":"ocr_9","text":"beta","boundsPx":[5,6,7,8]}]}
            """;
        var artifact = RawArtifact.Capture(
            Encoding.UTF8.GetBytes(json), new ArtifactMetadata(8, 8, "artifact", CaptureTime));
        var subjects = new LiveVisionStrategy().Observe(artifact)
            .Select(o => o.Subject).ToArray();
        Assert.Contains("ui.text.ocr0", subjects);
        Assert.Contains("ui.text.ocr1", subjects);
        Assert.DoesNotContain(subjects, s => s.Contains("ocr_7", StringComparison.Ordinal));
        Assert.DoesNotContain(subjects, s => s.Contains("ocr_9", StringComparison.Ordinal));
    }
}
