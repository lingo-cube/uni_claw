using System.Diagnostics;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// FCR-001 验收 A1–A9 —— FastPerception 内部有界版本化确定性计算复用。
/// 核心断言全部 corpus 真实资产驱动（不止 mock 计数）：warm 4→1、
/// canonical 四相全等（cache off vs on）、provenance 每次现场重建。
/// 缓存物仅 ArtifactObservation 快照；Evidence/Revision/Occurrence/Slice/
/// Grounding 任何下游产物零缓存（边界 4）。
/// </summary>
public sealed class FrameComputationReuseTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static readonly DateTimeOffset ActTime = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public FrameComputationReuseTests(ITestOutputHelper output) => _output = output;

    // ---- 测试替身 ---------------------------------------------------------

    /// <summary>
    /// 计数 / 可失效 / 可故障的 corpus strategy double。Attempt 计数含抛错
    /// 尝试；Identity / Version 可变（R3 键轴验证——模拟策略族 / 语义版本
    /// 更换）；ExposeInternalList 供 R4 验证缓存快照与 strategy 原始返回
    /// 集合的隔离。
    /// </summary>
    private sealed class CountingCorpusStrategy : IVersionedFastPerceptionStrategy
    {
        private readonly CorpusManifest _manifest;
        private readonly string _detectionSet;
        public int Attempts;
        public bool ThrowNext;
        public Func<IReadOnlyList<ArtifactObservation>, IReadOnlyList<ArtifactObservation>>? Post;
        private IReadOnlyList<ArtifactObservation>? _last;

        public CountingCorpusStrategy(CorpusManifest manifest, string detectionSetScenarioId)
        {
            (_manifest, _detectionSet) = (manifest, detectionSetScenarioId);
            StrategyVersion = $"{CorpusFastPerception.Version}#{detectionSetScenarioId}";
        }

        public string StrategyIdentity { get; set; } = CorpusFastPerception.Identity;

        public string StrategyVersion { get; set; }

        public IReadOnlyList<ArtifactObservation> ExposedInternalList => _last!;

        public IReadOnlyList<ArtifactObservation> Observe(RawArtifact artifact)
        {
            Interlocked.Increment(ref Attempts);
            if (ThrowNext)
            {
                ThrowNext = false;
                throw new InvalidOperationException("strategy-fault-injected");
            }
            var scenario = _manifest.Scenario(_detectionSet);
            var observations = scenario.Observations
                .Select(o => new ArtifactObservation(o.Subject, o.Value))
                .ToList();
            _last = Post is null ? observations : Post(observations);
            return _last;
        }
    }

    /// <summary>未实现 version seam 的 strategy（R9 fail-safe 验证）。</summary>
    private sealed class UnversionedCorpusStrategy : IFastPerceptionStrategy
    {
        private readonly CorpusFastPerception _inner;

        public int Attempts;

        public UnversionedCorpusStrategy(CorpusManifest manifest, string detectionSetScenarioId) =>
            _inner = new CorpusFastPerception(manifest, detectionSetScenarioId);

        public IReadOnlyList<ArtifactObservation> Observe(RawArtifact artifact)
        {
            Attempts++;
            return _inner.Observe(artifact);
        }
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", ActTime);
    }

    // ---- 组装 / 驱动 -------------------------------------------------------

    private static ArtifactMetadata Meta(CorpusScenario scenario, DateTimeOffset? captureTime = null) =>
        new(Width: 1080, Height: 1920, Frame: "artifact", CaptureTime: captureTime ?? scenario.CaptureTime);

    private static RawArtifact ArtifactOf(string scenarioId, DateTimeOffset? captureTime = null)
    {
        var scenario = Corpus.Scenario(scenarioId);
        var bytes = File.ReadAllBytes(Path.Combine(
            Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus"), scenario.Artifact));
        return RawArtifact.Capture(bytes, Meta(scenario, captureTime));
    }

    private static int ObserveWith(
        UniKernel kernel, FastPerception perception, RawArtifact artifact,
        ObservationContext context = ObservationContext.External)
    {
        var count = 0;
        foreach (var proposal in perception.Observe(artifact, context))
        {
            kernel.Process(proposal);
            count++;
        }
        return count;
    }

    // ---- R1：warm 4→1（核心验收，corpus 证据非仅计数） -----------------------

    [Fact]
    public void R1_WarmSameArtifact_StrategyInvocationsFourToOne_CanonicalUnchanged()
    {
        const string scenarioId = "scroll01-v1";
        var artifact = Corpus.Artifact(scenarioId);
        var observations = ExpectedProposals(scenarioId);
        var distinct = ExpectedDistinct(scenarioId);

        // cache OFF 基准
        var offStrategy = new CountingCorpusStrategy(Corpus, scenarioId);
        var offMetrics = new RuntimeStageMetrics();
        var (offKernel, offWorld) = NewObservationStack(offMetrics);
        var offPerception = new FastPerception("perception.corpus", offStrategy, DisabledRunTrace.Instance, offMetrics);
        for (var round = 0; round < 4; round++)
            ObserveWith(offKernel, offPerception, artifact);

        // cache ON
        var onStrategy = new CountingCorpusStrategy(Corpus, scenarioId);
        var onMetrics = new RuntimeStageMetrics();
        var (onKernel, onWorld) = NewObservationStack(onMetrics);
        var onPerception = new FastPerception(
            "perception.corpus", onStrategy, DisabledRunTrace.Instance, onMetrics, cacheCapacity: 256);
        for (var round = 0; round < 4; round++)
            ObserveWith(onKernel, onPerception, artifact);

        // 4→1（attempt 计数与 stage 计数双证）
        Assert.Equal(4, offStrategy.Attempts);
        Assert.Equal(1, onStrategy.Attempts);
        AssertStageInvocations(offMetrics, RuntimeStage.FastPerceptionStrategy, 4);
        AssertStageInvocations(onMetrics, RuntimeStage.FastPerceptionStrategy, 1);
        // cache 计数与 emission 恒 4 次（provenance 工作不被省略）
        Assert.Equal(4, onMetrics.CacheLookups);
        Assert.Equal(3, onMetrics.CacheHits);
        Assert.Equal(1, onMetrics.CacheMisses);
        AssertStageInvocations(onMetrics, RuntimeStage.ProposalEmission, 4);

        // canonical：两次驱动的 EvidenceIds / revision 链全等（不止计数）
        Assert.Equal(ProjectRun(offWorld), ProjectRun(onWorld));
        Assert.Equal(distinct, onWorld.RevisionHistory.Count);
        Assert.Equal(observations * 4, onMetrics.AdmissionsAccepted);
    }

    // ---- R2：同内容不同 CaptureTime → 命中 + 全新 provenance ------------------

    [Fact]
    public void R2_SameBytesDifferentCaptureTime_HitsCache_RebuildsProvenance()
    {
        const string scenarioId = "golden-case-a-before";
        var t1 = Corpus.Scenario(scenarioId).CaptureTime;
        var t2 = t1.AddSeconds(42);
        var artifact1 = ArtifactOf(scenarioId, t1);
        var artifact2 = ArtifactOf(scenarioId, t2);
        Assert.Equal(artifact1.ArtifactId, artifact2.ArtifactId); // 内容寻址：同 bytes 同 id

        var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
        var metrics = new RuntimeStageMetrics();
        var (kernel, world) = NewObservationStack(metrics);
        var perception = new FastPerception(
            "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheCapacity: 8);

        var proposals1 = perception.Observe(artifact1);
        foreach (var p in proposals1)
            kernel.Process(p);
        var revisions1 = world.RevisionHistory.Count;

        var proposals2 = perception.Observe(artifact2); // 命中缓存（同 ArtifactId）
        foreach (var p in proposals2)
            kernel.Process(p);

        // 命中：strategy 只算一次
        Assert.Equal(1, strategy.Attempts);
        Assert.Equal(1, metrics.CacheHits);
        // 全新 provenance：CaptureTime=t2 → EvidenceId 互异 → 非 idempotent
        Assert.All(proposals2, p => Assert.Equal(t2, p.Provenance.CaptureTime));
        Assert.Equal(revisions1 * 2, world.RevisionHistory.Count);
        Assert.Equal(0, metrics.ReconciliationsIdempotent);
    }

    // ---- R3：键轴变化 ⇒ miss --------------------------------------------------

    [Fact]
    public void R3_KeyAxisVariation_CausesMiss()
    {
        // (a) ArtifactId 变化：不同 scenario bytes
        var strategyA = new CountingCorpusStrategy(Corpus, "scroll01-v1");
        var metricsA = new RuntimeStageMetrics();
        var (_, _) = NewObservationStack(metricsA);
        var perceptionA = new FastPerception(
            "perception.corpus", strategyA, DisabledRunTrace.Instance, metricsA, cacheCapacity: 8);
        perceptionA.Observe(Corpus.Artifact("scroll01-v1"));
        perceptionA.Observe(Corpus.Artifact("scroll01-v2")); // 不同 artifact
        Assert.Equal(2, strategyA.Attempts);
        Assert.Equal(2, metricsA.CacheMisses);

        // (b) StrategyIdentity 变化（同一实例内策略族更换）
        var strategyB = new CountingCorpusStrategy(Corpus, "scroll01-v1")
        {
            StrategyIdentity = "alternative-family",
        };
        var metricsB = new RuntimeStageMetrics();
        var (kernelB, _) = NewObservationStack(metricsB);
        var perceptionB = new FastPerception(
            "perception.corpus", strategyB, DisabledRunTrace.Instance, metricsB, cacheCapacity: 8);
        var artifact = Corpus.Artifact("scroll01-v1");
        perceptionB.Observe(artifact);
        strategyB.StrategyIdentity = "revoked-family"; // identity 变化
        ObserveWith(kernelB, perceptionB, artifact);
        Assert.Equal(2, strategyB.Attempts);
        Assert.Equal(2, metricsB.CacheMisses);

        // (c) StrategyVersion 变化（语义升级）
        var strategyC = new CountingCorpusStrategy(Corpus, "scroll01-v1");
        var metricsC = new RuntimeStageMetrics();
        var (_, _) = NewObservationStack(metricsC);
        var perceptionC = new FastPerception(
            "perception.corpus", strategyC, DisabledRunTrace.Instance, metricsC, cacheCapacity: 8);
        perceptionC.Observe(artifact);
        Assert.Equal(1, metricsC.CacheMisses); // 首次 miss
        strategyC.StrategyVersion = $"{CorpusFastPerception.Version}#scroll01-v1!v2";
        perceptionC.Observe(artifact); // version 变化 → miss
        Assert.Equal(2, strategyC.Attempts);
        Assert.Equal(2, metricsC.CacheMisses);
        Assert.Equal(0, metricsC.CacheHits);
    }

    // ---- R4：不可变性（返回集合 + 缓存内部值 + strategy 原始集合） -------------

    [Fact]
    public void R4_ReturnedAndCachedValues_ImmutableByCaller()
    {
        const string scenarioId = "scroll01-v1";
        var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
        var perception = new FastPerception(
            "perception.corpus", strategy, DisabledRunTrace.Instance, metrics: null, cacheCapacity: 8);
        var artifact = Corpus.Artifact(scenarioId);

        var first = perception.Observe(artifact);
        // (a) 调用方改写返回集合（经 IList 槽位）→ 不影响后续命中
        if (first is IList<ObservationProposal> mutable)
            mutable[0] = new ObservationProposal(
                new ObservationClaim("forged", "forged"), IngressKind.Observation,
                ObservationContext.External, first[0].Provenance);
        // (b) strategy 原始返回集合事后改动 → 缓存快照不受影响
        var internalList = (List<ArtifactObservation>)strategy.ExposedInternalList;
        internalList[0] = new ArtifactObservation("poisoned", "poisoned");

        var second = perception.Observe(artifact);
        Assert.Equal(1, strategy.Attempts); // 命中，未重算
        Assert.Equal(first.Count, second.Count);
        Assert.NotEqual("forged", second[0].Claim.Subject);
        Assert.NotEqual("poisoned", second[0].Claim.Subject);
        Assert.Equal(Corpus.Scenario(scenarioId).Observations[0].Subject, second[0].Claim.Subject);
    }

    // ---- R5：并发 single-flight ----------------------------------------------

    [Fact]
    public void R5_ConcurrentSameKey_SingleFlight_AllCallersEquivalent()
    {
        const string scenarioId = "scroll01-v1";
        var artifact = Corpus.Artifact(scenarioId);
        var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
        var metrics = new RuntimeStageMetrics();
        var perception = new FastPerception(
            "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheCapacity: 8);

        const int threads = 8;
        var barrier = new Barrier(threads);
        var results = new IReadOnlyList<ObservationProposal>[threads];
        var tasks = Enumerable.Range(0, threads).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            results[i] = perception.Observe(artifact);
        })).ToArray();
        Task.WaitAll(tasks);

        Assert.Equal(1, strategy.Attempts); // 恰一次实际计算
        foreach (var result in results)
        {
            Assert.Equal(results[0].Count, result.Count);
            for (var i = 0; i < result.Count; i++)
                Assert.Equal(
                    (results[0][i].Claim.Subject, results[0][i].Claim.Value),
                    (result[i].Claim.Subject, result[i].Claim.Value));
        }
        Assert.Equal(threads, metrics.CacheLookups);
        // miss 语义 = 完成态缓存查找未命中（elected 计算者 + 全部等待者）；
        // 非命中调用要么 single-flight 等待要么命中（和恒定）
        Assert.Equal(threads, metrics.CacheHits + metrics.CacheMisses);
        Assert.Equal(1 + metrics.CacheSingleFlightWaits, metrics.CacheMisses);
        Assert.Equal(0, metrics.CacheEvictions);
    }

    // ---- R6：失败零污染 -------------------------------------------------------

    [Fact]
    public void R6_StrategyFailure_Propagates_DoesNotPoisonCache_RetriesRecompute()
    {
        const string scenarioId = "scroll01-v1";
        var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
        var metrics = new RuntimeStageMetrics();
        var perception = new FastPerception(
            "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheCapacity: 8);
        var artifact = Corpus.Artifact(scenarioId);

        strategy.ThrowNext = true;
        Assert.Throws<InvalidOperationException>(() => perception.Observe(artifact));
        Assert.Equal(1, strategy.Attempts);
        Assert.Equal(1, metrics.CacheMisses);
        Assert.Equal(0, metrics.CacheHits);

        var recovered = perception.Observe(artifact); // 重算成功
        Assert.Equal(2, strategy.Attempts);
        Assert.Equal(ExpectedProposals(scenarioId), recovered.Count);

        var third = perception.Observe(artifact); // 现在命中
        Assert.Equal(2, strategy.Attempts);
        Assert.Equal(1, metrics.CacheHits);

        // 等待者同型失败：elected 阻塞计算期间到达的并发调用收到同型异常
        var gate = new ManualResetEventSlim();
        var strategy2 = new CountingCorpusStrategy(Corpus, scenarioId)
        {
            Post = observations =>
            {
                gate.Set(); // elected 已进入计算
                Thread.Sleep(150);
                throw new InvalidOperationException("strategy-fault-in-flight");
            },
        };
        var perception2 = new FastPerception(
            "perception.corpus", strategy2, DisabledRunTrace.Instance, metrics: null, cacheCapacity: 8);
        var firstTask = Task.Run(() => Assert.Throws<InvalidOperationException>(
            () => perception2.Observe(artifact)));
        Assert.True(gate.Wait(TimeSpan.FromSeconds(5)));
        var secondTask = Task.Run(() => Assert.Throws<InvalidOperationException>(
            () => perception2.Observe(artifact)));
        firstTask.Wait(TimeSpan.FromSeconds(5));
        secondTask.Wait(TimeSpan.FromSeconds(5));
        Assert.Equal(1, strategy2.Attempts); // 等待者未重算
        // 失败后可重算
        strategy2.Post = null;
        Assert.Equal(ExpectedProposals(scenarioId), perception2.Observe(artifact).Count);
        Assert.Equal(2, strategy2.Attempts);
    }

    // ---- R7：有界 LRU 淘汰 ----------------------------------------------------

    [Fact]
    public void R7_CapacityBound_LruEviction_SafeRecompute()
    {
        var strategy = new CountingCorpusStrategy(Corpus, "scroll01-v1");
        var metrics = new RuntimeStageMetrics();
        var (_, _) = NewObservationStack(metrics);
        var perception = new FastPerception(
            "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheCapacity: 1);
        var a = Corpus.Artifact("scroll01-v1");
        var b = Corpus.Artifact("scroll01-v2");

        perception.Observe(a); // miss+compute
        perception.Observe(b); // miss+compute，容量 1 → 淘汰 A
        perception.Observe(a); // A 已被淘汰 → miss+重算（安全）

        Assert.Equal(3, strategy.Attempts);
        Assert.Equal(3, metrics.CacheMisses);
        Assert.Equal(2, metrics.CacheEvictions);
        Assert.Equal(0, metrics.CacheHits);
    }

    // ---- R8：四相 canonical 全等 + 对比报告（evidence 数据源） -----------------

    [Fact]
    public void R8_FourPhases_CacheOffVsOn_CanonicalIdentical_InvocationsDrop()
    {
        var off = RunSuite(cacheOn: false);
        var on = RunSuite(cacheOn: true);

        // canonical 全等（每相投影逐一比对）
        foreach (var phase in new[] { "cold", "warm", "partial", "grounding" })
        {
            Assert.Equal(off.Canonical[phase], on.Canonical[phase]);
        }

        // warm 核心：4→1；其余相 strategy invocation 不变（cold/partial 各 miss 一次；
        // grounding 观察 miss 一次——partial 不同 detection set ⇒ version 不同 ⇒ 不误共缓存）
        Assert.Equal(off.Phases["warm"].StrategyAttempts, 4);
        Assert.Equal(on.Phases["warm"].StrategyAttempts, 1);
        Assert.Equal(off.Phases["cold"].StrategyAttempts, on.Phases["cold"].StrategyAttempts);
        Assert.Equal(off.Phases["partial"].StrategyAttempts, on.Phases["partial"].StrategyAttempts);
        Assert.Equal(off.Phases["grounding"].StrategyAttempts, on.Phases["grounding"].StrategyAttempts);

        // 计数一致性：每相 admissions / new revisions 全等
        foreach (var phase in new[] { "cold", "warm", "partial", "grounding" })
        {
            Assert.Equal(off.Phases[phase].Admissions, on.Phases[phase].Admissions);
            Assert.Equal(off.Phases[phase].NewRevisions, on.Phases[phase].NewRevisions);
        }

        _output.WriteLine("=== FCR-001 four-phase cache off/on comparison ===");
        foreach (var phase in new[] { "cold", "warm", "partial", "grounding" })
        {
            var o = off.Phases[phase];
            var n = on.Phases[phase];
            _output.WriteLine(
                $"{phase} | attempts off={o.StrategyAttempts} on={n.StrategyAttempts} | "
                + $"strategyMs off={o.StrategyStageMs:F3} on={n.StrategyStageMs:F3} | "
                + $"allStagesMs off={o.AllStagesMs:F3} on={n.AllStagesMs:F3} | "
                + $"hit={n.Hits} miss={n.Misses} | admissions={n.Admissions} newRevisions={n.NewRevisions}");
        }
    }

    // ---- R9：fail-safe 与低开销 ----------------------------------------------

    [Fact]
    public void R9_UnversionedStrategy_CacheAutoDisabled_NullObservationNonIntrusive()
    {
        // (a) 未实现 seam + 显式容量 ⇒ 自动禁用：每次都真算、零 cache 计数
        var unversioned = new UnversionedCorpusStrategy(Corpus, "scroll01-v1");
        var metrics = new RuntimeStageMetrics();
        var (_, _) = NewObservationStack(metrics);
        var perception = new FastPerception(
            "perception.corpus", unversioned, DisabledRunTrace.Instance, metrics, cacheCapacity: 8);
        var artifact = Corpus.Artifact("scroll01-v1");
        perception.Observe(artifact);
        perception.Observe(artifact);
        Assert.Equal(2, unversioned.Attempts);
        Assert.Equal(0, metrics.CacheLookups);
        AssertStageInvocations(metrics, RuntimeStage.FastPerceptionStrategy, 2); // 与无缓存一致

        // (b) null trace + null metrics + 缓存启用：正常工作（无 NRE、命中生效）
        var counting = new CountingCorpusStrategy(Corpus, "scroll01-v1");
        var bare = new FastPerception("perception.corpus", counting, trace: null, metrics: null, cacheCapacity: 8);
        Assert.Equal(ExpectedProposals("scroll01-v1"), bare.Observe(artifact).Count);
        Assert.Equal(ExpectedProposals("scroll01-v1"), bare.Observe(artifact).Count);
        Assert.Equal(1, counting.Attempts);
    }

    // ---- suite 驱动（R8）-----------------------------------------------------

    private sealed record PhaseData(
        int StrategyAttempts, long Hits, long Misses, long Admissions, long NewRevisions,
        double StrategyStageMs, double AllStagesMs);

    private sealed record SuiteResult(
        IReadOnlyDictionary<string, PhaseData> Phases,
        IReadOnlyDictionary<string, string> Canonical);

    private static SuiteResult RunSuite(bool cacheOn)
    {
        var phases = new Dictionary<string, PhaseData>();
        var canonical = new Dictionary<string, string>();

        // Phase COLD：完整 observation（三场景，fresh kernel per scenario；
        // 每场景独立 detection set ⇒ 独立 strategy + perception 实例，共享 metrics）
        {
            var metrics = new RuntimeStageMetrics();
            var worlds = new List<WorldModel>();
            var attempts = 0;
            foreach (var scenarioId in new[] { "golden-case-a-before", "scroll01-v1", "nav03-parent" })
            {
                var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
                var perception = new FastPerception(
                    "perception.corpus", strategy, DisabledRunTrace.Instance, metrics,
                    cacheOn ? 256 : null);
                var (kernel, world) = NewObservationStack(metrics);
                ObserveWith(kernel, perception, Corpus.Artifact(scenarioId));
                attempts += strategy.Attempts;
                worlds.Add(world);
            }
            phases["cold"] = Snapshot(metrics, attempts);
            canonical["cold"] = string.Join("||", worlds.Select(ProjectRun));
        }

        // Phase WARM：同 artifact ×4（1 cold + 3 幂等）
        {
            const string scenarioId = "scroll01-v1";
            var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
            var metrics = new RuntimeStageMetrics();
            var (kernel, world) = NewObservationStack(metrics);
            var perception = new FastPerception(
                "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheOn ? 256 : null);
            var artifact = Corpus.Artifact(scenarioId);
            for (var round = 0; round < 4; round++)
                ObserveWith(kernel, perception, artifact);
            phases["warm"] = Snapshot(metrics, strategy.Attempts);
            canonical["warm"] = ProjectRun(world);
        }

        // Phase PARTIAL：partial/degraded detection set（不同 version ⇒ 不误共缓存）
        {
            var metrics = new RuntimeStageMetrics();
            var worlds = new List<WorldModel>();
            var attempts = 0;
            foreach (var scenarioId in new[] { "scroll01-v1-partial", "popup04-degraded" })
            {
                var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
                var perception = new FastPerception(
                    "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheOn ? 256 : null);
                var (kernel, world) = NewObservationStack(metrics);
                ObserveWith(kernel, perception, Corpus.Artifact(scenarioId));
                attempts += strategy.Attempts;
                worlds.Add(world);
            }
            phases["partial"] = Snapshot(metrics, attempts);
            canonical["partial"] = string.Join("||", worlds.Select(ProjectRun));
        }

        // Phase GROUNDING：nav03 三轮 traversal（slice + grounding 真实消费）
        {
            const string scenarioId = "nav03-parent";
            var children = new[] { "CHILD A", "CHILD B", "CHILD C" };
            var strategy = new CountingCorpusStrategy(Corpus, scenarioId);
            var metrics = new RuntimeStageMetrics();
            var world = NewGroundingWorld(metrics);
            var kernel = NewGroundingKernel(world, metrics,
                children.Select(c => new TargetSpec("Button", c, "tap")).ToArray());
            var perception = new FastPerception(
                "perception.corpus", strategy, DisabledRunTrace.Instance, metrics, cacheOn ? 256 : null);
            ObserveWith(kernel, perception, Corpus.Artifact(scenarioId));
            var root = world.Current!.Containers.Single().Identity.ContainerId;
            for (var round = 0; round < 3; round++)
            {
                var intent = kernel.SelectIntent(kernel.DeriveSlice(root));
                kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button"));
                kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button", children[round]));
                AdvanceRevision(kernel, scenarioId, round);
            }
            kernel.SelectIntent(kernel.DeriveSlice(root));
            phases["grounding"] = Snapshot(metrics, strategy.Attempts);
            canonical["grounding"] = ProjectRun(world) + "||slice="
                + ProjectSlice(kernel.DeriveSlice(root));
        }

        return new SuiteResult(phases, canonical);
    }

    private static PhaseData Snapshot(RuntimeStageMetrics metrics, int attempts)
    {
        var stages = metrics.Stages;
        var strategyMs = stages.TryGetValue(RuntimeStage.FastPerceptionStrategy, out var s)
            ? RuntimeStageMetrics.TicksToMilliseconds(s.TotalTicks) : 0;
        var allMs = stages.Values.Sum(a => RuntimeStageMetrics.TicksToMilliseconds(a.TotalTicks));
        return new PhaseData(attempts, metrics.CacheHits, metrics.CacheMisses,
            metrics.AdmissionsAccepted, metrics.ReconciliationsNew, strategyMs, allMs);
    }

    private static (UniKernel Kernel, WorldModel World) NewObservationStack(RuntimeStageMetrics metrics)
    {
        var world = new WorldModel(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(SignatureScopes.Value, DialogScopes.Value),
            new CorpusObservationStrategy(),
            new CorpusContinuityStrategy());
        return (new UniKernel(new EvidenceLedger(), world, DisabledRunTrace.Instance, metrics: metrics), world);
    }

    private static WorldModel NewGroundingWorld(RuntimeStageMetrics metrics) =>
        new(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(SignatureScopes.Value, DialogScopes.Value),
            new OwnedCorpusObservationStrategy(),
            new CorpusContinuityStrategy());

    private static UniKernel NewGroundingKernel(WorldModel world, RuntimeStageMetrics metrics, TargetSpec[] specs)
    {
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(), new ControlLoop(new DescriptorTargetPolicy(specs)),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new OkDriver()), metrics);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: "tap-the-children",
            Scope: new HashSet<string> { "perception.page.signature" },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "children-tapped" }));
        return kernel;
    }

    private static void AdvanceRevision(UniKernel kernel, string scenarioId, int round)
    {
        var artifact = Corpus.Artifact(scenarioId);
        var (subject, value) = Corpus.Scenario(scenarioId).Observations[0];
        kernel.Process(new ObservationProposal(
            new ObservationClaim(subject, value),
            IngressKind.Observation, ObservationContext.PostActionEffectFlow,
            new Provenance(
                Producer: "perception.corpus",
                CaptureTime: ActTime.AddMinutes(round),
                Scope: $"artifact:{artifact.ArtifactId}",
                TransformationLineage: new[] { "fast:perception.corpus", $"artifact:{artifact.ArtifactId}" })));
    }

    private static int ExpectedProposals(string scenarioId) =>
        Corpus.Scenario(scenarioId).Observations.Count;

    private static int ExpectedDistinct(string scenarioId) =>
        Corpus.Scenario(scenarioId).Observations.Select(o => (o.Subject, o.Value)).Distinct().Count();

    private static void AssertStageInvocations(
        RuntimeStageMetrics metrics, RuntimeStage stage, long invocations) =>
        Assert.True(metrics.Stages.TryGetValue(stage, out var aggregate)
            && aggregate.Invocations == invocations,
            $"{stage} invocations 期望 {invocations}，实际 " +
            (metrics.Stages.TryGetValue(stage, out var a) ? a.Invocations.ToString() : "无记录"));

    // ---- canonical 投影 ------------------------------------------------------

    private static string ProjectRun(WorldModel world)
    {
        var evidence = string.Join(",", world.RevisionHistory
            .SelectMany(r => r.EvidenceBasis).Distinct().OrderBy(id => id, StringComparer.Ordinal));
        var revisions = string.Join("||", world.RevisionHistory.Select(r =>
            $"{r.RevisionId}#{r.RevisionNumber}|"
            + string.Join(";", r.WorldState.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value.Value}:{kv.Value.EvidenceId}"))
            + "|" + (r.Occurrences is null ? "null" : string.Join(";",
                r.Occurrences.Select(o => $"{o.OccurrenceId}:{o.Role}:{o.SemanticDescriptor}")))
            + "|" + string.Join(";", r.Containers.Select(c => c.Identity.ContainerId)
                .OrderBy(id => id, StringComparer.Ordinal))
            + $"|{r.Conflicts.Count}|{r.EvidenceBasis.Count}"));
        return $"ev=[{evidence}] rev=[{revisions}]";
    }

    private static string ProjectSlice(Slice slice) =>
        $"{slice.SourceRevisionId}|{slice.RootContainerId}|"
        + string.Join(";", slice.Occurrences.Select(o => $"{o.OccurrenceId}:{o.Role}:{o.SemanticDescriptor}"))
        + "|" + string.Join(";", slice.ScopedClaims.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

    private static readonly Lazy<IReadOnlySet<string>> SignatureScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.PageSignatureSubject));

    private static readonly Lazy<IReadOnlySet<string>> DialogScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.DialogTitleSubject));

    private static IReadOnlySet<string> ScopesWithSubject(string subject) => Corpus.Scenarios
        .Where(s => s.Observations.Any(o => o.Subject == subject))
        .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
        .ToHashSet();
}
