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
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// RUN-002 — Live 闭环 Tracer Bullet（ENVIRONMENT）。注册模拟器 × live 感知 ×
/// ADB 真机效果 × 真实 IRunTrace：contract → 观察（截屏→服务→proposals→join→
/// Process）→ DeriveSlice → SelectIntent → ActViaCurrentGrounding → 真实
/// dispatch → 再观察证实世界翻转 → EvaluateTerminal 如实记录。
/// 门控与 fail-closed 约定同 PerceptionLiveEnvironmentTests
/// （DSH_TEST_PERCEPTION_LIVE=1；模拟器生命周期外部管理）。
/// test-side join 策略 = PER-003 D2「真实策略 test 侧」先例：帧级 master
/// proposal（FastPerception 产出的 54 proposals 在测试侧 join——per-record
/// 派生无法跨 proposal 关联 bounds）。
/// </summary>
public sealed class LiveClosedLoopBulletTests(ITestOutputHelper output)
{
    private static string? _lastProcessSummary;

    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1";

    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static string ProviderRoot => Path.Combine(RepoRoot(), "platforms", "perception");
    private static string VenvPython => Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python");

    /// <summary>帧级 master proposal 的 occurrence 策略：解析 join JSON →
    /// detect occurrences（role=class，locator 归一化 + SupportedFrame；
    /// owner = previous revision 的 seed container——DeriveSlice 只取
    /// container bucket，ownerless occurrence 会静默从 Slice 消失，
    /// 台账发现#3）。非 JSON 的 seed claim → 空。</summary>
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
                    OwningContainerId: owner,
                    Role: entry.GetProperty("cls").GetString()!,
                    SemanticDescriptor: null,
                    State: null,
                    Locator: new SpatialLocator(
                        bounds[0], bounds[1], bounds[2], bounds[3],
                        AdbEffectDriver.SupportedFrame)));
            }
            return occurrences;
        }
    }

    private sealed record LiveServiceContext(VisionServiceHost Host, VisionServiceTransport Transport)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            if (Transport is VisionServiceTransport.UnixDomainSocket uds)
            {
                try { File.Delete(uds.SocketPath); } catch (IOException) { }
            }
        }
    }

    private LiveServiceContext StartHost()
    {
        var cacheRoot = Path.Combine(RepoRoot(), ".perception", "cache");
        Directory.CreateDirectory(Path.Combine(cacheRoot, "matplotlib"));
        var socketPath = Path.Combine(Path.GetTempPath(), $"run002-{Guid.NewGuid():N}.sock");
        var host = new VisionServiceHost(new VisionServiceHostOptions(
            VenvPython,
            ProviderRoot,
            new VisionServiceTransport.UnixDomainSocket(socketPath),
            StartupTimeout: TimeSpan.FromSeconds(180),
            ExtraEnvironment: new Dictionary<string, string>
            {
                ["XDG_CACHE_HOME"] = cacheRoot,
                ["MPLCONFIGDIR"] = Path.Combine(cacheRoot, "matplotlib"),
            }));
        return new LiveServiceContext(host, new VisionServiceTransport.UnixDomainSocket(socketPath));
    }

    private static async Task<string> RunAdb(string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("adb", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("adb 启动失败（fail-closed）");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        Assert.Equal(0, process.ExitCode);
        return stdout;
    }

    /// <summary>一次 live 观察：截屏 → 服务推理 → FastPerception(LiveVisionStrategy)
    /// proposals → 测试侧 join 为 master proposal → Process。返回景观 digest。</summary>
    private static async Task<(string Digest, string RevisionId)> ObserveAndProcess(
        UniKernel kernel, FastPerception perception, VisionServiceClient client,
        AdbScreenshotAcquisition acquisition, DateTimeOffset captureTime)
    {
        var capture = await acquisition.CaptureAsync(CancellationToken.None);
        var analysis = await client.AnalyzeAsync(
            PngImage.Decode(capture.Artifact.Payload).Rgba,
            capture.Width, capture.Height, CancellationToken.None);
        Assert.True(analysis.Success, $"现场推理失败：{analysis.Diagnostic}");

        var derived = RawArtifact.Capture(
            System.Text.Encoding.UTF8.GetBytes(analysis.ResponseJson!),
            new ArtifactMetadata(
                capture.Width, capture.Height, "artifact",
                CaptureTime: captureTime,
                CaptureScope: $"derived:vision-service:{capture.Artifact.ArtifactId}"));
        var proposals = perception.Observe(derived);

        // join：ui.detect.{id}.class ↔ spatial.artifact.bounds.detect.{id}
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
            var boundsSubject = $"spatial.artifact.bounds.detect.{id}";
            var boundsValue = proposals
                .FirstOrDefault(p => p.Claim.Subject == boundsSubject)?.Claim.Value;
            if (boundsValue is null)
                continue;
            var px = boundsValue.Split(',').Select(int.Parse).ToArray();
            joined.Add($"{{\"id\":\"{id}\",\"cls\":\"{cls}\",\"b\":[{px[0] / (double)capture.Width},{px[1] / (double)capture.Height},{px[2] / (double)capture.Width},{px[3] / (double)capture.Height}]}}");
        }
        var master = new ObservationProposal(
            new ObservationClaim("live.frame", $"{{\"detects\":[{string.Join(",", joined)}]}}"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("bullet.join", captureTime, "scope:live.frame",
                new[] { $"artifact:{derived.ArtifactId}" }));
        var processResult = kernel.Process(master);
        _lastProcessSummary = $"admission={processResult.Admission.Decision} rev={(processResult.ResultingRevision is null ? "null" : processResult.ResultingRevision.RevisionId)} occlist={(processResult.ResultingRevision?.Occurrences?.Count ?? -1)}";

        var current = kernel.CurrentBelief!;
        var digest = string.Join("|", (current.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Select(o => $"{o.Role}@{o.Locator?.X1:F4},{o.Locator?.Y1:F4}")
            .OrderBy(s => s, StringComparer.Ordinal));
        return (digest, current.RevisionId);
    }

    [Fact]
    public async Task LiveClosedLoop_TracerBullet_WithRealTrace()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用（模拟器生命周期外部管理）");
            return;
        }
        if (Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") == "1")
        {
            output.WriteLine("跳过：DSH_TEST_NO_ADB=1");
            return;
        }
        Assert.True(File.Exists(Path.Combine(ProviderRoot, "uniclaw_perception", "server.py")), "provider 树缺失");
        Assert.True(File.Exists(VenvPython), "感知环境未拉起（tools/perception-env/setup.sh）");

        var devices = await RunAdb("devices");
        Assert.Contains("emulator-5554", devices, StringComparison.Ordinal);

        await using var hostContext = StartHost();
        var startup = await hostContext.Host.StartAsync();
        Assert.True(startup.Healthy, $"服务启动失败：{startup.Error}\n{startup.StderrTail}");

        await RunAdb("-s emulator-5554 shell am start -a android.settings.WIFI_SETTINGS");
        await Task.Delay(TimeSpan.FromSeconds(2));

        var acquisition = new AdbScreenshotAcquisition("emulator-5554", "adb");
        using var client = new VisionServiceClient(hostContext.Transport, timeout: TimeSpan.FromSeconds(120));
        // ---- 组合 kernel（真实 trace + metrics + ADB 真驱动）----------------
        // trace 缺口台账#2：RunId 在 AdmitContract 后才铸，而 kernel ctor 需要
        // trace —— correlation 此处用固定串，与 RunModel.RunId 不同步（记录，
        // 不在本 change 修）。
        var traceScope = RunTraceFactory.BeginRun(new RunCorrelation("run002-live-bullet"));
        var metrics = new RuntimeStageMetrics();
        var world = new WorldModel(
            new HashSet<string> { "live.frame" },
            new SeedContainerAssociationStrategy(),
            new LiveFrameOccurrenceStrategy());
        var run = new RunModel();
        // 台账发现#1 实证：FastPerception 直连不经组合缝时零 span——观察侧必须显式注入 trace/metrics
        var perception = new FastPerception("perception.live.vision", new LiveVisionStrategy(), traceScope.Trace, metrics);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, traceScope.Trace,
            run,
            new ControlLoop(new DescriptorTargetPolicy(new[]
                { new TargetSpec("switch", null, "tap") })),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new AdbLiveEffectDriver("emulator-5554", 1080, 2400)),
            metrics);

        var admission = kernel.AdmitContract(new ExecutionContract(
            Version: "v1",
            Objective: "flip-wifi-toggle",
            Scope: new HashSet<string> { "live.frame" },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "world-flip-reobserved" }));
        Assert.True(admission.Accepted);

        // seed（UIW NewKernel(seed:true) 先例）：首条观察铸 root container，
        // 后续帧的 occurrence 才有 owner 可入 Slice scope（台账发现#3 的组合解）
        var seedResult = kernel.Process(new ObservationProposal(
            new ObservationClaim("live.frame", "seed"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("bullet.seed", DateTimeOffset.UtcNow, "scope:live.frame",
                new[] { "bullet:seed" })));
        output.WriteLine($"seed：admission={seedResult.Admission.Decision} reject={seedResult.Admission.RejectionReason} relevance={(seedResult.Relevance is null ? "null" : seedResult.Relevance.IsRelevant + "/" + seedResult.Relevance.Reason)} rev={(seedResult.ResultingRevision is null ? "null" : seedResult.ResultingRevision.RevisionId)}");
        output.WriteLine($"seed 后：revisions={world.RevisionHistory.Count} current={(world.Current is null ? "null" : world.Current.RevisionId)} containers={(world.Current?.Containers.Count ?? 0)}");

        // ---- 第一帧：live 观察 → 世界 ------------------------------------
        var captureTime = DateTimeOffset.UtcNow;
        var (digest1, revision1) = await ObserveAndProcess(
            kernel, perception, client, acquisition, captureTime);
        output.WriteLine($"frame1：rev={(revision1.Length <= 16 ? revision1 : revision1[..12])}… digest={digest1.Length} revisions={world.RevisionHistory.Count} [{_lastProcessSummary}]");
        Assert.NotEqual(string.Empty, digest1);

        // ---- 控制 → 接地 → 真实 dispatch ---------------------------------
        var root = kernel.CurrentBelief!.Containers.Single().Identity.ContainerId;
        var slice = kernel.DeriveSlice(root);
        var intent = kernel.SelectIntent(slice);
        Assert.Equal(ControlIntentKind.Act, intent.Kind);

        var grounded = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("switch"));
        Assert.True(grounded.Act is not null,
            $"grounding 未产生 Act：{grounded.View.Result}（候选 {grounded.View.Candidates.Count}）");
        Assert.NotNull(grounded.Act!.Receipt); // A1：真实 dispatch
        output.WriteLine($"dispatch：{grounded.Act.Receipt!.Outcome} @ {intent.TargetSubject}");

        // ---- 再观察：世界翻转证实（ADB-002 同款语义）----------------------
        await Task.Delay(TimeSpan.FromSeconds(2)); // 动画稳定（宽松常量）
        var (digest2, revision2) = await ObserveAndProcess(
            kernel, perception, client, acquisition, DateTimeOffset.UtcNow);
        output.WriteLine($"frame2：rev={(revision2.Length <= 16 ? revision2 : revision2[..12])}… digest 长度={digest2.Length}");
        Assert.NotEqual(revision1, revision2);            // A2：revision 推进
        Assert.NotEqual(digest1, digest2);                // A2：景观变化

        // ---- terminal 如实记录（A4：不伪造）-------------------------------
        var terminal = kernel.EvaluateTerminal();
        output.WriteLine($"terminal：proof={(terminal.Proof is not null ? "有" : "无")} " +
            $"transition={terminal.Transition.Accepted}({terminal.Transition.Reason})");

        // ---- trace 断言（A3）---------------------------------------------
        var artifact = traceScope.FinalizeArtifact();
        output.WriteLine($"trace：{artifact.Spans.Length} spans，terminal={artifact.RecorderTerminal} " +
            $"defs=[{string.Join(",", artifact.Spans.Select(s => s.SpanDefinitionId).Distinct())}]");
        Assert.True(artifact.Spans.Length > 0, "零 span");
        Assert.DoesNotContain(artifact.Spans, s => s.StructuralOutcome == StructuralOutcome.Incomplete);
        // 感知 span 引用 derived artifact（reference-oriented 因果）
        var perceptionSpans = artifact.Spans
            .Where(s => s.SpanDefinitionId.Contains("perception", StringComparison.OrdinalIgnoreCase)
                || s.SpanDefinitionId.Contains("observe", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(perceptionSpans.Count > 0, "无感知 span");
        Assert.Contains(perceptionSpans, s => s.References.Any(r =>
            r.Kind == TraceReferenceKind.Artifact && r.Value.StartsWith("art-", StringComparison.Ordinal)));

        // ---- metrics 画像 --------------------------------------------------
        output.WriteLine($"metrics：artifactsPresented={metrics.ArtifactsPresented} " +
            $"distinct={metrics.DistinctArtifacts}");
        Assert.True(metrics.ArtifactsPresented >= 2, "至少两帧经 perception");
    }
}
