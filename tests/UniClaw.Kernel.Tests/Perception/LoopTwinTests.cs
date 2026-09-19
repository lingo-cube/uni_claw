using System.Text.Json;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// LOOP-001 — RUN-002 闭环组合的确定性孪生（DETERMINISTIC，默认套件）。
/// 同一组合逻辑（seed→观察→join→Process→DeriveSlice→SelectIntent→
/// ActViaCurrentGrounding→dispatch→再观察）不经模拟器/服务：帧源 = corpus
/// golden JSON（frame2 移除 switch 模拟世界翻转），效果驱动 = Ok double。
/// 作用：闭环组合语义锁进全量回归——任何 kernel/感知重构破坏闭环即 RED，
/// 无需手动启用 ENVIRONMENT 门控。trace 断言同款（span 因果 + artifact
/// 引用 + 台账#1 教训：perception 显式注入 trace）。
/// </summary>
public sealed class LoopTwinTests
{
    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    private sealed class LiveFrameOccurrenceStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (!record.Claim.Value.StartsWith("{\"detects\"", StringComparison.Ordinal))
                return Array.Empty<ProposedOccurrence>();
            var owner = previous?.Containers.Count == 1
                ? previous.Containers[0].Identity.ContainerId : null;
            using var document = JsonDocument.Parse(record.Claim.Value);
            var occurrences = new List<ProposedOccurrence>();
            foreach (var entry in document.RootElement.GetProperty("detects").EnumerateArray())
            {
                var bounds = entry.GetProperty("b").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                occurrences.Add(new ProposedOccurrence(
                    owner, entry.GetProperty("cls").GetString()!, null, null,
                    new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3],
                        AdbEffectDriver.SupportedFrame)));
            }
            return occurrences;
        }
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "twin:ok",
                new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));
    }

    private static (string Digest, string RevisionId) ObserveAndProcess(
        UniKernel kernel, FastPerception perception, string analysisJson,
        string artifactScope, DateTimeOffset captureTime)
    {
        var derived = RawArtifact.Capture(
            System.Text.Encoding.UTF8.GetBytes(analysisJson),
            new ArtifactMetadata(1080, 1920, "artifact", captureTime, artifactScope));
        var proposals = perception.Observe(derived);
        var classes = proposals
            .Where(p => p.Claim.Subject.StartsWith("ui.detect.", StringComparison.Ordinal)
                && p.Claim.Subject.EndsWith(".class", StringComparison.Ordinal)
                && p.Claim.Subject.Length > "ui.detect..class".Length)
            .ToDictionary(
                p => p.Claim.Subject["ui.detect.".Length..^".class".Length],
                p => p.Claim.Value);
        var joined = new List<string>();
        foreach (var (id, cls) in classes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var boundsValue = proposals
                .FirstOrDefault(p => p.Claim.Subject == $"spatial.artifact.bounds.detect.{id}")?.Claim.Value;
            if (boundsValue is null)
                continue;
            var px = boundsValue.Split(',').Select(int.Parse).ToArray();
            joined.Add($"{{\"id\":\"{id}\",\"cls\":\"{cls}\",\"b\":[{px[0] / 1080.0},{px[1] / 1920.0},{px[2] / 1080.0},{px[3] / 1920.0}]}}");
        }
        kernel.Process(new ObservationProposal(
            new ObservationClaim("live.frame", $"{{\"detects\":[{string.Join(",", joined)}]}}"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("twin.join", captureTime, "scope:live.frame",
                new[] { $"artifact:{derived.ArtifactId}" })));
        var current = kernel.CurrentBelief!;
        var digest = string.Join("|", (current.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Select(o => $"{o.Role}@{o.Locator?.X1:F4},{o.Locator?.Y1:F4}")
            .OrderBy(s => s, StringComparer.Ordinal));
        return (digest, current.RevisionId);
    }

    private static string GoldenJson(bool withSwitch)
    {
        var text = File.ReadAllText(Path.Combine(
            CorpusRoot, "legacy-direct", "golden-run-v1", "case-a-before.json"));
        return withSwitch ? text : RemoveSwitch(text);
    }

    private static string RemoveSwitch(string text)
    {
        using var document = JsonDocument.Parse(text);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name != "yolo")
                {
                    writer.WritePropertyName(property.Name);
                    property.Value.WriteTo(writer);
                    continue;
                }
                writer.WritePropertyName("yolo");
                writer.WriteStartArray();
                foreach (var detection in property.Value.EnumerateArray())
                {
                    if (detection.GetProperty("label").GetString() == "switch")
                        continue;
                    detection.WriteTo(writer);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void ClosedLoopComposition_DeterministicTwin_WithRealTrace()
    {
        var traceScope = RunTraceFactory.BeginRun(new RunCorrelation("loop-twin"));
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            new HashSet<string> { "live.frame" },
            new SeedContainerAssociationStrategy(),
            new LiveFrameOccurrenceStrategy());
        var perception = new FastPerception(
            "perception.live.vision", new LiveVisionStrategy(), traceScope.Trace, metrics);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, traceScope.Trace,
            new RunModel(),
            new ControlLoop(new DescriptorTargetPolicy(new[]
                { new TargetSpec("switch", null, "tap") })),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new OkDriver()),
            metrics);

        Assert.True(kernel.AdmitContract(new ExecutionContract(
            "v1", "flip-toggle", new HashSet<string> { "live.frame" },
            new HashSet<string> { "tap" }, new HashSet<string>(),
            new[] { "world-flip" })).Accepted);

        // seed（台账#3 组合解：root container 先铸，occurrence 才有 owner 入 Slice）
        var seed = kernel.Process(new ObservationProposal(
            new ObservationClaim("live.frame", "seed"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("twin.seed", new DateTimeOffset(2026, 9, 12, 11, 0, 0, TimeSpan.Zero),
                "scope:live.frame", new[] { "twin:seed" })));
        Assert.NotNull(seed.ResultingRevision);

        // frame1（golden 含 switch）→ 控制 → 接地 → dispatch
        var (digest1, revision1) = ObserveAndProcess(
            kernel, perception, GoldenJson(withSwitch: true),
            "derived:vision-service:art-frame1",
            new DateTimeOffset(2026, 9, 12, 11, 1, 0, TimeSpan.Zero));
        Assert.Contains("switch@", digest1, StringComparison.Ordinal);

        var root = kernel.CurrentBelief!.Containers.Single().Identity.ContainerId;
        var intent = kernel.SelectIntent(kernel.DeriveSlice(root));
        Assert.Equal(ControlIntentKind.Act, intent.Kind);

        var grounded = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("switch"));
        Assert.NotNull(grounded.Act);
        Assert.NotNull(grounded.Act!.Receipt); // dispatch 完成（double 驱动）
        Assert.Equal(DispatchOutcome.DeliveryCompleted, grounded.Act.Receipt!.Outcome);

        // frame2（switch 移除 = 世界翻转）→ 再观察证实
        var (digest2, revision2) = ObserveAndProcess(
            kernel, perception, GoldenJson(withSwitch: false),
            "derived:vision-service:art-frame2",
            new DateTimeOffset(2026, 9, 12, 11, 2, 0, TimeSpan.Zero));
        Assert.NotEqual(revision1, revision2);
        Assert.DoesNotContain("switch@", digest2, StringComparison.Ordinal);

        // 翻转后的第二意图：switch 已消失 → Observe（fail-closed 不猜）
        var intent2 = kernel.SelectIntent(kernel.DeriveSlice(root));
        Assert.Equal(ControlIntentKind.Observe, intent2.Kind);

        // trace：span 全 Complete，感知 span 引用 artifact（台账#1 教训已接线）
        var artifact = traceScope.FinalizeArtifact();
        Assert.True(artifact.Spans.Length > 0);
        Assert.DoesNotContain(artifact.Spans, s => s.StructuralOutcome == StructuralOutcome.Incomplete);
        Assert.Contains(artifact.Spans, s => s.References.Any(r =>
            r.Kind == TraceReferenceKind.Artifact && r.Value.StartsWith("art-", StringComparison.Ordinal)));
        Assert.True(metrics.ArtifactsPresented >= 2);
    }
}
