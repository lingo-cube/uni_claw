using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// FSV-001 V4（CONTRACT）——四臂响应（baseline / A=integration / B1=
/// replacement / B2=ocr-off）在**零 Runtime 代码修改**前提下可被现有
/// runtime 边界契约代码消费：LiveVisionStrategy（唯一的 yolo[]/ocr[]
/// 解析者）对同一真实帧（valset 058f62426c1f，1080×2400 settings）的
/// 四臂响应产出契约合法的 ArtifactObservation。additive screenParse 键
/// 被既有解析器天然忽略（向后兼容证明）。B2 空 ocr[] → 零文字观察
/// （诚实降级，非崩溃）。fixtures = 2026-09-12 live 服务/消融脚本真实
/// 输出（Perception/Corpus/FSV001Arms/）。
/// </summary>
public sealed class FastScreenArmContractTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 9, 12, 18, 43, 0, TimeSpan.Zero);

    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus", "FSV001Arms");

    private static RawArtifact ArmArtifact(string arm) => RawArtifact.Capture(
        File.ReadAllBytes(Path.Combine(CorpusRoot, $"{arm}.json")),
        new ArtifactMetadata(1080, 2400, "artifact", CaptureTime, CaptureScope: $"derived:vision-service:fsv001-{arm}"));

    private static (int Yolo, int Ocr) EnvelopeCounts(string arm)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(CorpusRoot, $"{arm}.json")));
        var root = document.RootElement;
        return (root.GetProperty("yolo").GetArrayLength(), root.GetProperty("ocr").GetArrayLength());
    }

    public static TheoryData<string> Arms => new() { "baseline", "integration", "replacement", "ocr-off" };

    [Theory]
    [MemberData(nameof(Arms))]
    public void AllArms_ProduceContractLegalObservations_FromLiveVisionStrategy(string arm)
    {
        var observations = new LiveVisionStrategy().Observe(ArmArtifact(arm));

        // 数量契约：每 yolo 条目 2 条（class + bounds）、每 ocr 条目 2 条
        var (yolo, ocr) = EnvelopeCounts(arm);
        Assert.Equal(yolo * 2 + ocr * 2, observations.Count);

        // subject 契约：仅 ui.detect.*/spatial.artifact.bounds.detect.*/ui.text.ocr*/spatial.artifact.bounds.ocr*
        foreach (var observation in observations)
        {
            var subject = observation.Subject;
            Assert.True(
                subject.StartsWith("ui.detect.", StringComparison.Ordinal)
                || subject.StartsWith("spatial.artifact.bounds.detect.", StringComparison.Ordinal)
                || subject.StartsWith("ui.text.ocr", StringComparison.Ordinal)
                || subject.StartsWith("spatial.artifact.bounds.ocr", StringComparison.Ordinal),
                $"非契约 subject（arm={arm}）：{subject}");
        }

        // bounds 值契约：x1,y1,x2,y2（4 段逗号分隔）
        Assert.All(
            observations.Where(o => o.Subject.StartsWith("spatial.", StringComparison.Ordinal)),
            o => Assert.Equal(4, o.Value.Split(',').Length));

        // class/value 非空
        Assert.All(observations, o => Assert.False(string.IsNullOrWhiteSpace(o.Value)));
    }

    [Theory]
    [MemberData(nameof(Arms))]
    public void AllArms_Deterministic_SameArtifactSameObservations(string arm)
    {
        var strategy = new LiveVisionStrategy();
        Assert.Equal(strategy.Observe(ArmArtifact(arm)), strategy.Observe(ArmArtifact(arm)));
    }

    [Fact]
    public void IntegrationArm_AdditiveScreenParseKey_IgnoredByRuntimeParser()
    {
        // A 臂响应携带 additive screenParse 键；LiveVisionStrategy 输出
        // 不含任何 screenParse 来源的 subject（additive 向后兼容证明）；
        // rescued 元素（fs_ id）以常规 ui.detect.* subject 进入——runtime
        // 可见增益通道
        var withAdditive = new LiveVisionStrategy().Observe(ArmArtifact("integration"));

        Assert.DoesNotContain(withAdditive, o => o.Subject.Contains("screenParse", StringComparison.Ordinal));
        Assert.DoesNotContain(withAdditive, o => o.Subject.Contains("fs_", StringComparison.Ordinal)
            && o.Subject.StartsWith("ui.text", StringComparison.Ordinal));

        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(CorpusRoot, "integration.json")));
        var rescued = document.RootElement.GetProperty("screenParse").GetProperty("summary").GetProperty("rescuedCount").GetInt32();
        if (rescued > 0)
        {
            var detectIds = withAdditive
                .Where(o => o.Subject.StartsWith("ui.detect.", StringComparison.Ordinal) && o.Subject.EndsWith(".class"))
                .Select(o => o.Subject["ui.detect.".Length..^".class".Length]);
            Assert.Contains(detectIds, id => id.StartsWith("fs_", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void OcrOffArm_EmptyOcrArray_YieldsZeroTextObservations()
    {
        // B2 诚实降级：无 OCR → 无文字观察，但 detect 观察照常（fail-open
        // 于能力、fail-closed 于协议——空数组合法，缺键才非法）
        var observations = new LiveVisionStrategy().Observe(ArmArtifact("ocr-off"));
        Assert.Equal(0, EnvelopeCounts("ocr-off").Ocr);
        Assert.DoesNotContain(observations, o => o.Subject.StartsWith("ui.text.", StringComparison.Ordinal));
        Assert.Contains(observations, o => o.Subject.StartsWith("ui.detect.", StringComparison.Ordinal));
    }

    [Fact]
    public void FastPerception_ComposesArmObservations_IntoProposals()
    {
        // FastPerception（P2 producer）对四臂 artifact 均能完成
        // ObservationProposal 组装（provenance/context 合法）
        foreach (var arm in new[] { "baseline", "integration", "replacement", "ocr-off" })
        {
            var perception = new FastPerception("perception.fast", new LiveVisionStrategy());
            var proposals = perception.Observe(ArmArtifact(arm));
            Assert.All(proposals, p =>
            {
                Assert.Equal(IngressKind.Observation, p.Kind);
                Assert.Equal("perception.fast", p.Provenance.Producer);
                Assert.StartsWith("artifact:", p.Provenance.TransformationLineage[^1]);
            });
        }
    }
}
