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
using UniClaw.Kernel.Tests.Perception;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// LAT-001 验收 A1–A4 —— Product Runtime Latency Baseline 固定 benchmark
/// fixture（PER-002 corpus 真实资产，REAL_ASSET_COVERAGE 全场景）：
/// 阶段计数 / 规模计数为确定性断言（同输入同值）；elapsed ticks 仅作原始
/// 观测记录（Benchmark fact 输出，不断言任何阈值——P0 只建立事实基线）。
/// cold = 同 kernel 首观察（新 revision 路径）；warm = 同 artifact 再观察
/// （admission 内容去重 + reconcile 幂等路径）；partial = partial/degraded
/// detection set；fast/slow escalation：当前运行时无 slow path，本 fixture
/// 不含该相（不伪造运行路径）。
/// </summary>
public sealed class LatencyBaselineTests
{
    private static readonly CorpusManifest Corpus = CorpusManifest.Load();
    private static readonly DateTimeOffset ActTime = new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<IReadOnlySet<string>> SignatureScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.PageSignatureSubject));

    private static readonly Lazy<IReadOnlySet<string>> DialogScopes =
        new(() => ScopesWithSubject(CorpusAssociationStrategy.DialogTitleSubject));

    /// <summary>完整 observation 场景（corpus 全部完整 detection set）。</summary>
    private static readonly string[] FullScenarios =
    {
        "golden-case-a-before", "scroll01-v1", "scroll01-v2", "nav03-parent",
        "nav03-childa", "popup01-dialog", "popup04-page", "popup09-before",
    };

    /// <summary>局部 / 降级 observation 场景（partial detection set）。</summary>
    private static readonly string[] PartialScenarios = { "scroll01-v1-partial", "popup04-degraded" };

    private readonly ITestOutputHelper _output;

    public LatencyBaselineTests(ITestOutputHelper output) => _output = output;

    private static IReadOnlySet<string> ScopesWithSubject(string subject) => Corpus.Scenarios
        .Where(s => s.Observations.Any(o => o.Subject == subject))
        .Select(s => "artifact:" + Corpus.Artifact(s.ScenarioId).ArtifactId)
        .ToHashSet();

    // ---- 测试替身 ---------------------------------------------------------

    /// <summary>occurrence derivation 缝的量化装饰器（LAT-001 D2：真实缝
    /// 在 WorldModel.Reconcile 内部，metrics 经组合根装饰器记录，不伪造
    /// trace 拓扑）。</summary>
    private sealed class MeasuredObservationStrategy : IUiObservationStrategy
    {
        private readonly IUiObservationStrategy _inner;
        private readonly RuntimeStageMetrics? _metrics;

        public MeasuredObservationStrategy(IUiObservationStrategy inner, RuntimeStageMetrics? metrics) =>
            (_inner, _metrics) = (inner, metrics);

        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (_metrics is null)
                return _inner.Derive(record, previous);
            var start = Stopwatch.GetTimestamp();
            var proposed = _inner.Derive(record, previous);
            _metrics.Record(RuntimeStage.OccurrenceDerivation, Stopwatch.GetTimestamp() - start,
                inputSize: previous?.Occurrences?.Count ?? 0, outputSize: proposed.Count);
            return proposed;
        }
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", ActTime);
    }

    /// <summary>trace 观察面故障 double（TRC-001 ThrowingRunTrace 先例）。</summary>
    private sealed class ThrowingTrace : IRunTrace
    {
        public ITraceOperationScope StartOperation(
            SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references) =>
            throw new InvalidOperationException("trace-fault-injected");
    }

    // ---- 组装 / 驱动 -------------------------------------------------------

    private static ArtifactMetadata Meta(CorpusScenario scenario) =>
        new(Width: 1080, Height: 1920, Frame: "artifact", CaptureTime: scenario.CaptureTime);

    /// <summary>
    /// raw artifact capture 阶段测量（v0.1 为 host-owned seam，D3：包裹真实
    /// RawArtifact.Capture 调用——与 Corpus.Artifact 同 bytes 同 metadata，
    /// 同 ArtifactId）。
    /// </summary>
    private static RawArtifact CaptureTimed(RuntimeStageMetrics? metrics, string scenarioId)
    {
        var scenario = Corpus.Scenario(scenarioId);
        var bytes = File.ReadAllBytes(Path.Combine(
            Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus"), scenario.Artifact));
        if (metrics is null)
            return RawArtifact.Capture(bytes, Meta(scenario));
        var start = Stopwatch.GetTimestamp();
        var artifact = RawArtifact.Capture(bytes, Meta(scenario));
        metrics.Record(RuntimeStage.CaptureArtifact, Stopwatch.GetTimestamp() - start,
            inputSize: bytes.Length, outputSize: 1);
        return artifact;
    }

    private static WorldModel NewWorld(RuntimeStageMetrics? metrics, bool owned = false)
    {
        var observation = new MeasuredObservationStrategy(
            owned ? new OwnedCorpusObservationStrategy() : new CorpusObservationStrategy(), metrics);
        return new WorldModel(
            Corpus.SubjectScope,
            new CorpusAssociationStrategy(SignatureScopes.Value, DialogScopes.Value),
            observation,
            new CorpusContinuityStrategy());
    }

    private static UniKernel ObservationKernel(WorldModel world, IRunTrace trace, RuntimeStageMetrics? metrics) =>
        new(new EvidenceLedger(), world, trace, metrics: metrics);

    private static UniKernel FullStackKernel(
        WorldModel world, IRunTrace trace, RuntimeStageMetrics? metrics, TargetSpec[] specs,
        string objective = "tap-the-children", string allowedEffect = "tap")
    {
        var kernel = new UniKernel(
            new EvidenceLedger(), world, trace,
            new RunModel(), new ControlLoop(new DescriptorTargetPolicy(specs)),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(new OkDriver()), metrics);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1",
            Objective: objective,
            Scope: new HashSet<string> { "perception.page.signature" },
            AllowedEffects: new HashSet<string> { allowedEffect },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "children-tapped" }));
        return kernel;
    }

    private static int ObserveScenario(
        UniKernel kernel, string scenarioId, RawArtifact? artifact = null,
        RuntimeStageMetrics? metrics = null, IRunTrace? trace = null,
        ObservationContext context = ObservationContext.External, TransitionContext? transition = null)
    {
        artifact ??= Corpus.Artifact(scenarioId);
        var perception = new FastPerception(
            "perception.corpus", new CorpusFastPerception(Corpus, scenarioId), trace, metrics);
        var count = 0;
        foreach (var proposal in perception.Observe(artifact, context))
        {
            kernel.Process(proposal, transition);
            count++;
        }
        return count;
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

    private static void AssertStage(
        IReadOnlyDictionary<RuntimeStage, RuntimeStageAggregate> stages, RuntimeStage stage,
        long invocations, long? inputSize = null, long? secondarySize = null, long? outputSize = null)
    {
        Assert.True(stages.TryGetValue(stage, out var aggregate), $"阶段 {stage} 无记录");
        Assert.Equal(invocations, aggregate.Invocations);
        if (inputSize.HasValue) Assert.Equal(inputSize.Value, aggregate.TotalInputSize);
        if (secondarySize.HasValue) Assert.Equal(secondarySize.Value, aggregate.TotalSecondarySize);
        if (outputSize.HasValue) Assert.Equal(outputSize.Value, aggregate.TotalOutputSize);
        Assert.True(aggregate.MinTicks >= 0);
        Assert.True(aggregate.MaxTicks >= aggregate.MinTicks);
    }

    // ---- F1：cold（完整 observation，逐场景确定性计数） ----------------------

    [Fact]
    public void F1_ColdObservation_DeterministicStageCounts_PerScenario()
    {
        foreach (var scenarioId in FullScenarios)
        {
            var metrics = new RuntimeStageMetrics();
            var world = NewWorld(metrics);
            var kernel = ObservationKernel(world, DisabledRunTrace.Instance, metrics);
            var artifact = CaptureTimed(metrics, scenarioId);

            var proposals = ObserveScenario(kernel, scenarioId, artifact, metrics);

            var observations = ExpectedProposals(scenarioId);
            var distinct = ExpectedDistinct(scenarioId);
            Assert.Equal(observations, proposals);
            Assert.Equal(1, metrics.ArtifactsPresented);
            Assert.Equal(1, metrics.DistinctArtifacts);
            Assert.Equal(observations, metrics.AdmissionsAccepted);
            Assert.Equal(0, metrics.AdmissionsRejected);
            Assert.Equal(distinct, metrics.ReconciliationsNew);
            Assert.Equal(observations - distinct, metrics.ReconciliationsIdempotent);
            Assert.Equal(distinct, world.RevisionHistory.Count);

            var stages = metrics.Stages;
            AssertStage(stages, RuntimeStage.CaptureArtifact, 1, inputSize: artifact.Payload.Length, outputSize: 1);
            AssertStage(stages, RuntimeStage.FastPerceptionStrategy, 1,
                inputSize: artifact.Payload.Length, outputSize: observations);
            AssertStage(stages, RuntimeStage.ProposalEmission, 1,
                inputSize: observations, outputSize: observations);
            AssertStage(stages, RuntimeStage.EvidenceAdmission, observations,
                inputSize: observations, outputSize: observations);
            AssertStage(stages, RuntimeStage.WorldReconciliation, observations, outputSize: distinct);
            AssertStage(stages, RuntimeStage.OccurrenceDerivation, distinct);
        }
    }

    // ---- F2：warm（同 artifact 再观察 → 幂等路径） ---------------------------

    [Fact]
    public void F2_WarmReobservation_SameArtifact_IdempotentPath_ZeroNewRevisions()
    {
        const string scenarioId = "scroll01-v1";
        var observations = ExpectedProposals(scenarioId);
        var distinct = ExpectedDistinct(scenarioId);

        var metrics = new RuntimeStageMetrics();
        var world = NewWorld(metrics);
        var kernel = ObservationKernel(world, DisabledRunTrace.Instance, metrics);
        var artifact = CaptureTimed(metrics, scenarioId);
        ObserveScenario(kernel, scenarioId, artifact, metrics);
        var coldRevisions = world.RevisionHistory.Count;

        ObserveScenario(kernel, scenarioId, artifact, metrics); // warm：同 bytes + 同 CaptureTime

        Assert.Equal(coldRevisions, world.RevisionHistory.Count);
        Assert.Equal(distinct, metrics.ReconciliationsNew);
        Assert.Equal(observations, metrics.ReconciliationsIdempotent); // 第二轮全幂等
        Assert.Equal(2, metrics.ArtifactsPresented);
        Assert.Equal(1, metrics.DistinctArtifacts);
        Assert.Equal(2 * observations, metrics.AdmissionsAccepted); // 内容去重仍 Accepted

        var stages = metrics.Stages;
        AssertStage(stages, RuntimeStage.CaptureArtifact, 1);
        AssertStage(stages, RuntimeStage.FastPerceptionStrategy, 2);
        AssertStage(stages, RuntimeStage.ProposalEmission, 2);
        AssertStage(stages, RuntimeStage.EvidenceAdmission, 2 * observations);
        AssertStage(stages, RuntimeStage.WorldReconciliation, 2 * observations, outputSize: distinct);
        AssertStage(stages, RuntimeStage.OccurrenceDerivation, distinct); // 仅冷轮发生
    }

    // ---- F3：partial / degraded（局部 observation） --------------------------

    [Fact]
    public void F3_PartialAndDegraded_SmallerFixedCounts()
    {
        foreach (var scenarioId in PartialScenarios)
        {
            var metrics = new RuntimeStageMetrics();
            var world = NewWorld(metrics);
            var kernel = ObservationKernel(world, DisabledRunTrace.Instance, metrics);
            var artifact = CaptureTimed(metrics, scenarioId);

            var proposals = ObserveScenario(kernel, scenarioId, artifact, metrics);

            var observations = ExpectedProposals(scenarioId);
            var distinct = ExpectedDistinct(scenarioId);
            Assert.Equal(2, observations); // corpus 约定：partial/degraded = 2 条 detection
            Assert.Equal(observations, proposals);
            Assert.Equal(1, metrics.DistinctArtifacts);
            Assert.Equal(observations, metrics.AdmissionsAccepted);
            Assert.Equal(distinct, metrics.ReconciliationsNew);
            AssertStage(metrics.Stages, RuntimeStage.FastPerceptionStrategy, 1, outputSize: observations);
            AssertStage(metrics.Stages, RuntimeStage.EvidenceAdmission, observations);
        }
    }

    // ---- F4：grounding traversal（slice + resolve-current 真实消费） ---------

    [Fact]
    public void F4_GroundingTraversal_SliceAndGroundingStageCounts()
    {
        const string scenarioId = "nav03-parent";
        var observations = ExpectedProposals(scenarioId);
        var distinct = ExpectedDistinct(scenarioId);
        var children = new[] { "CHILD A", "CHILD B", "CHILD C" };

        var metrics = new RuntimeStageMetrics();
        var world = NewWorld(metrics, owned: true);
        var kernel = FullStackKernel(world, DisabledRunTrace.Instance, metrics,
            children.Select(c => new TargetSpec("Button", c, "tap")).ToArray());
        CaptureTimed(metrics, scenarioId);
        ObserveScenario(kernel, scenarioId, metrics: metrics);
        var root = world.Current!.Containers.Single().Identity.ContainerId;

        long expectedSliceOutput = 0;
        long expectedGroundingOutput = 0;
        var expectedMaxGroundingInput = 0L;
        for (var round = 0; round < 3; round++)
        {
            var slice = kernel.DeriveSlice(root);
            expectedSliceOutput += slice.Occurrences.Count;
            var intent = kernel.SelectIntent(slice);

            expectedMaxGroundingInput = Math.Max(expectedMaxGroundingInput, world.Current!.Occurrences!.Count);
            var bare = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button"));
            expectedGroundingOutput += bare.View.Candidates.Count;
            Assert.Equal(
                world.Current!.Occurrences!.Count(o => o.Role == "Button"),
                bare.View.Candidates.Count);

            expectedMaxGroundingInput = Math.Max(expectedMaxGroundingInput, world.Current!.Occurrences!.Count);
            var grounded = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button", children[round]));
            expectedGroundingOutput += grounded.View.Candidates.Count;
            Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, grounded.View.Result);
            Assert.NotNull(grounded.Act!.Receipt);

            AdvanceRevision(kernel, scenarioId, round);
        }
        var finalSlice = kernel.DeriveSlice(root);
        expectedSliceOutput += finalSlice.Occurrences.Count;
        Assert.Equal(ControlIntentKind.Observe, kernel.SelectIntent(finalSlice).Kind); // 完毕转 Observe

        var stages = metrics.Stages;
        AssertStage(stages, RuntimeStage.SliceDerivation, 4, outputSize: expectedSliceOutput);
        AssertStage(stages, RuntimeStage.CurrentGrounding, 6, outputSize: expectedGroundingOutput);
        Assert.Equal(expectedMaxGroundingInput, stages[RuntimeStage.CurrentGrounding].MaxInputSize);
        // traversal 侧 admission：初始观察 + 每轮 reflux(AttemptReport) + 每轮 advance
        Assert.Equal(observations + 6, metrics.AdmissionsAccepted);
        Assert.Equal(distinct + 3, metrics.ReconciliationsNew);
        Assert.Equal(distinct + 3, world.RevisionHistory.Count);
    }

    // ---- F5：trace × metrics 四象限 canonical 全等 ---------------------------

    [Fact]
    public void F5_TraceAndMetricsToggles_CanonicalOutputsIdentical()
    {
        const string scenarioId = "golden-case-a-before";
        var first = (CanonicalProjection?)null;
        foreach (var (traceOn, metricsOn) in new[]
                 {
                     (false, false), (true, false), (false, true), (true, true),
                 })
        {
            var scope = traceOn ? RunTraceFactory.BeginRun(new RunCorrelation("lat-equiv")) : null;
            var trace = scope?.Trace ?? DisabledRunTrace.Instance;
            var metrics = metricsOn ? new RuntimeStageMetrics() : null;
            var world = NewWorld(metrics, owned: true);
            var kernel = ObservationKernel(world, trace, metrics);
            var artifact = CaptureTimed(metrics, scenarioId);
            ObserveScenario(kernel, scenarioId, artifact, metrics, trace);
            var root = world.Current!.Containers.Single().Identity.ContainerId;
            var slice = kernel.DeriveSlice(root);
            var grounding = world.ResolveCurrent(new TargetDescriptor("switch"));
            var projection = Project(world, kernel, slice, grounding);
            first ??= projection;
            Assert.Equal(first, projection);
        }
    }

    // ---- F6：throwing trace 在新缝全吸收 ------------------------------------

    [Fact]
    public void F6_ThrowingTrace_AbsorbedAt_NewSeams()
    {
        // perception 缝：throwing trace 下 proposals 业务内容与禁用态一致
        const string scenarioId = "golden-case-a-before";
        var artifact = Corpus.Artifact(scenarioId);
        var quiet = new FastPerception("perception.corpus", new CorpusFastPerception(Corpus, scenarioId))
            .Observe(artifact).Select(p => (p.Claim.Subject, p.Claim.Value, p.Provenance.Scope)).ToList();
        var observed = new FastPerception(
            "perception.corpus", new CorpusFastPerception(Corpus, scenarioId), new ThrowingTrace(), null)
            .Observe(artifact).Select(p => (p.Claim.Subject, p.Claim.Value, p.Provenance.Scope)).ToList();
        Assert.Equal(quiet, observed);

        // slice / grounding 缝：throwing trace 下 canonical 输出与禁用态一致
        var children = new[] { "CHILD A", "CHILD B", "CHILD C" };
        var specs = children.Select(c => new TargetSpec("Button", c, "tap")).ToArray();
        foreach (var trace in new IRunTrace[] { DisabledRunTrace.Instance, new ThrowingTrace() })
        {
            var world = NewWorld(metrics: null, owned: true);
            var kernel = FullStackKernel(world, trace, metrics: null, specs);
            ObserveScenario(kernel, "nav03-parent");
            var root = world.Current!.Containers.Single().Identity.ContainerId;
            var slice = kernel.DeriveSlice(root);
            Assert.NotEmpty(slice.Occurrences);
            var grounded = kernel.ActViaCurrentGrounding(
                kernel.SelectIntent(slice), new TargetDescriptor("Button", "CHILD A"));
            Assert.Equal(CurrentCandidateSetResultKind.UniqueCandidate, grounded.View.Result);
            Assert.NotNull(grounded.Act!.Receipt);
        }
    }

    // ---- F7：span 拓扑（新 binding 词表结构正确） ----------------------------

    [Fact]
    public void F7_SpanTopology_NewBindings_ReferenceStructureCorrect()
    {
        const string scenarioId = "nav03-parent";
        var observations = ExpectedProposals(scenarioId);
        var distinct = ExpectedDistinct(scenarioId);
        var children = new[] { "CHILD A", "CHILD B", "CHILD C" };

        var scope = RunTraceFactory.BeginRun(new RunCorrelation("lat-nav03-topology"));
        var metrics = new RuntimeStageMetrics();
        var world = NewWorld(metrics, owned: true);
        var kernel = FullStackKernel(world, scope.Trace, metrics,
            children.Select(c => new TargetSpec("Button", c, "tap")).ToArray());
        var artifact = Corpus.Artifact(scenarioId);
        ObserveScenario(kernel, scenarioId, artifact, metrics, scope.Trace);
        var root = world.Current!.Containers.Single().Identity.ContainerId;
        for (var round = 0; round < 3; round++)
        {
            var intent = kernel.SelectIntent(kernel.DeriveSlice(root));
            kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button"));
            kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button", children[round]));
            AdvanceRevision(kernel, scenarioId, round);
        }
        kernel.SelectIntent(kernel.DeriveSlice(root));

        var finalized = scope.FinalizeArtifact();
        // recorder 侧无词表 / ref-kind 违规；唯一允许项 = 观察场景未发
        // RuntimeOutcome 的诚实降级（TRC-001 Quarantined 语义）
        Assert.All(finalized.RecorderDiagnostics,
            d => Assert.Equal("runtime-outcome-emission-not-observed", d.Reason));
        var spans = finalized.Spans;
        int Count(string operationId) => spans.Count(s => s.SpanDefinitionId == operationId);
        Assert.Equal(1, Count("perception.observe"));
        Assert.Equal(1, Count("perception.strategy"));
        Assert.Equal(1, Count("perception.emit-proposal"));
        Assert.Equal(observations + 6, Count("evidence.admit"));
        Assert.Equal(distinct + 3, Count("world.reconcile"));
        Assert.Equal(4, Count("world.derive-slice"));
        Assert.Equal(6, Count("world.resolve-current"));
        Assert.All(spans, s => Assert.Equal(StructuralOutcome.Completed, s.StructuralOutcome));

        // perception 因果：observe 根 span 引用 Artifact；strategy / emission 为其子 span
        var observeSpan = Assert.Single(spans, s => s.SpanDefinitionId == "perception.observe");
        Assert.Null(observeSpan.ParentSpanId);
        Assert.Contains(observeSpan.References,
            r => r.Kind == TraceReferenceKind.Artifact && r.Value == artifact.ArtifactId);
        var strategySpan = Assert.Single(spans, s => s.SpanDefinitionId == "perception.strategy");
        Assert.Equal(observeSpan.SpanId, strategySpan.ParentSpanId);
        var emissionSpan = Assert.Single(spans, s => s.SpanDefinitionId == "perception.emit-proposal");
        Assert.Equal(observeSpan.SpanId, emissionSpan.ParentSpanId);
        Assert.Contains(spans.Where(s => s.SpanDefinitionId == "world.derive-slice"),
            s => s.References.Any(r => r.Kind == TraceReferenceKind.WorldRevision));
    }

    // ---- F8：固定 benchmark（原始测量输出；只断言确定性计数） ----------------

    [Fact]
    public void F8_Benchmark_FixedScenarios_StageReport()
    {
        var report = new List<string>
        {
            "=== LAT-001 Product Runtime Latency Benchmark ===",
            $".NET {Environment.Version} | Stopwatch.Frequency {Stopwatch.Frequency} Hz | "
                + $".DateTime(UTC) {DateTimeOffset.UtcNow:O}",
        };

        long totalAdmissions = 0;
        long totalNewRevisions = 0;
        long totalProposals = 0;

        // Phase COLD：完整 observation，每场景独立 kernel（cold 语义）
        report.Add("--- phase: cold (full observation, fresh kernel per scenario) ---");
        report.Add("scenario | stage | invocations | totalMs | maxMs | inSize | outSize");
        foreach (var scenarioId in FullScenarios)
        {
            var metrics = new RuntimeStageMetrics();
            var world = NewWorld(metrics);
            var kernel = ObservationKernel(world, DisabledRunTrace.Instance, metrics);
            var artifact = CaptureTimed(metrics, scenarioId);
            var proposals = ObserveScenario(kernel, scenarioId, artifact, metrics);
            totalAdmissions += metrics.AdmissionsAccepted;
            totalNewRevisions += metrics.ReconciliationsNew;
            totalProposals += proposals;
            foreach (var (stage, aggregate) in metrics.Stages.OrderBy(kv => kv.Key))
                report.Add($"{scenarioId} | {stage} | {aggregate.Invocations} | "
                    + $"{RuntimeStageMetrics.TicksToMilliseconds(aggregate.TotalTicks):F3} | "
                    + $"{RuntimeStageMetrics.TicksToMilliseconds(aggregate.MaxTicks):F3} | "
                    + $"{aggregate.TotalInputSize} | {aggregate.TotalOutputSize}");
            // A2：一次 artifact 的直接归因（顺序确定性驱动，D5）
            report.Add($"{scenarioId} | ATTRIBUTION | artifact={artifact.ArtifactId} "
                + $"strategy=1 proposals={proposals} admissions={metrics.AdmissionsAccepted} "
                + $"newRevisions={metrics.ReconciliationsNew}");
        }

        // Phase WARM：同 artifact 连续再观察（首轮为 cold 路径，随后为幂等路径）
        report.Add("--- phase: warm (same artifact re-observed; round 1 cold, rounds 2-4 idempotent) ---");
        const string warmScenario = "scroll01-v1";
        var warmMetrics = new RuntimeStageMetrics();
        var warmWorld = NewWorld(warmMetrics);
        var warmKernel = ObservationKernel(warmWorld, DisabledRunTrace.Instance, warmMetrics);
        var warmArtifact = CaptureTimed(warmMetrics, warmScenario);
        var warmRounds = 4; // 1 cold + 3 warm
        for (var round = 0; round < warmRounds; round++)
            ObserveScenario(warmKernel, warmScenario, warmArtifact, warmMetrics);
        var warmObservations = ExpectedProposals(warmScenario);
        Assert.Equal(warmObservations * (warmRounds - 1), warmMetrics.ReconciliationsIdempotent);
        Assert.Equal(ExpectedDistinct(warmScenario), warmMetrics.ReconciliationsNew);
        Assert.Equal(warmRounds, warmMetrics.ArtifactsPresented);
        Assert.Equal(1, warmMetrics.DistinctArtifacts);
        totalAdmissions += warmMetrics.AdmissionsAccepted;
        totalProposals += warmObservations * warmRounds;
        foreach (var (stage, aggregate) in warmMetrics.Stages.OrderBy(kv => kv.Key))
            report.Add($"{warmScenario}#warm×{warmRounds} | {stage} | {aggregate.Invocations} | "
                + $"{RuntimeStageMetrics.TicksToMilliseconds(aggregate.TotalTicks):F3} | "
                + $"{RuntimeStageMetrics.TicksToMilliseconds(aggregate.MaxTicks):F3} | "
                + $"{aggregate.TotalInputSize} | {aggregate.TotalOutputSize}");

        // Phase PARTIAL：局部 / 降级 observation
        report.Add("--- phase: partial/degraded (detection-set partial) ---");
        foreach (var scenarioId in PartialScenarios)
        {
            var metrics = new RuntimeStageMetrics();
            var world = NewWorld(metrics);
            var kernel = ObservationKernel(world, DisabledRunTrace.Instance, metrics);
            var artifact = CaptureTimed(metrics, scenarioId);
            var proposals = ObserveScenario(kernel, scenarioId, artifact, metrics);
            totalAdmissions += metrics.AdmissionsAccepted;
            totalNewRevisions += metrics.ReconciliationsNew;
            totalProposals += proposals;
            report.Add($"{scenarioId} | ATTRIBUTION | artifact={artifact.ArtifactId} "
                + $"strategy=1 proposals={proposals} admissions={metrics.AdmissionsAccepted} "
                + $"newRevisions={metrics.ReconciliationsNew}");
        }

        // Phase GROUNDING：nav03 三轮 traversal（slice + grounding 真实消费）
        report.Add("--- phase: grounding traversal (nav03-parent, 3 rounds) ---");
        const string groundingScenario = "nav03-parent";
        var groundingMetrics = new RuntimeStageMetrics();
        var groundingWorld = NewWorld(groundingMetrics, owned: true);
        var children = new[] { "CHILD A", "CHILD B", "CHILD C" };
        var groundingKernel = FullStackKernel(groundingWorld, DisabledRunTrace.Instance, groundingMetrics,
            children.Select(c => new TargetSpec("Button", c, "tap")).ToArray());
        CaptureTimed(groundingMetrics, groundingScenario);
        ObserveScenario(groundingKernel, groundingScenario, metrics: groundingMetrics);
        var groundingRoot = groundingWorld.Current!.Containers.Single().Identity.ContainerId;
        for (var round = 0; round < 3; round++)
        {
            var intent = groundingKernel.SelectIntent(groundingKernel.DeriveSlice(groundingRoot));
            groundingKernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button"));
            groundingKernel.ActViaCurrentGrounding(intent, new TargetDescriptor("Button", children[round]));
            AdvanceRevision(groundingKernel, groundingScenario, round);
        }
        groundingKernel.SelectIntent(groundingKernel.DeriveSlice(groundingRoot));
        totalAdmissions += groundingMetrics.AdmissionsAccepted;
        totalNewRevisions += groundingMetrics.ReconciliationsNew;
        foreach (var (stage, aggregate) in groundingMetrics.Stages.OrderBy(kv => kv.Key))
            report.Add($"{groundingScenario}#grounding | {stage} | {aggregate.Invocations} | "
                + $"{RuntimeStageMetrics.TicksToMilliseconds(aggregate.TotalTicks):F3} | "
                + $"{RuntimeStageMetrics.TicksToMilliseconds(aggregate.MaxTicks):F3} | "
                + $"{aggregate.TotalInputSize} | {aggregate.TotalOutputSize}");

        report.Add($"TOTALS proposals={totalProposals} admissions={totalAdmissions} newRevisions={totalNewRevisions}");
        report.Add("--- hotspot ranking (per-phase metrics instances, by totalMs desc) ---");
        report.Add("(见 evidence 文档汇总——各 phase 独立 metrics 实例，原始数字以上表为准)");
        _output.WriteLine(string.Join(Environment.NewLine, report));

        // 确定性总量（wiring 证明）：cold+partial+grounding 的 accepted 计数
        var expected = 0L;
        foreach (var id in FullScenarios.Concat(PartialScenarios))
            expected += ExpectedProposals(id);
        expected += groundingMetrics.AdmissionsAccepted; // traversal 侧完整独立计数
        Assert.Equal(expected + warmMetrics.AdmissionsAccepted, totalAdmissions);
    }

    // ---- canonical 投影（F5 等价比较） --------------------------------------

    private sealed record CanonicalProjection(
        string EvidenceIds,
        string Admissions,
        string Revisions,
        string Slice,
        string Grounding);

    private static CanonicalProjection Project(
        WorldModel world, UniKernel kernel, Slice slice, CurrentGroundingView grounding)
    {
        // 投影面 = world 可见 canonical 面：EvidenceIds（revision basis ∪
        // relevance log）、relevance 判定序列、revision 结构链、Slice、
        // GroundingView（corpus 路径零 rejected admission，accepted 全量
        // 进入 relevance log——覆盖充分）
        var evidenceIds = string.Join(",",
            world.RevisionHistory.SelectMany(r => r.EvidenceBasis)
                .Distinct().OrderBy(id => id, StringComparer.Ordinal));
        var admissions = string.Join(",",
            world.RelevanceLog.Select(j => j.EvidenceId).OrderBy(id => id, StringComparer.Ordinal));
        var revisions = string.Join("||", world.RevisionHistory.Select(r =>
        {
            var state = string.Join(";",
                r.WorldState.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key}={kv.Value.Value}:{kv.Value.EvidenceId}"));
            var occurrences = r.Occurrences is null
                ? "null"
                : string.Join(";",
                    r.Occurrences.Select(o => $"{o.OccurrenceId}:{o.Role}:{o.SemanticDescriptor}"));
            var containers = string.Join(";",
                r.Containers.Select(c => c.Identity.ContainerId).OrderBy(id => id, StringComparer.Ordinal));
            return $"{r.RevisionId}#{r.RevisionNumber}|{state}|{occurrences}|{containers}|{r.Conflicts.Count}|{r.EvidenceBasis.Count}";
        }));
        var sliceText = $"{slice.SourceRevisionId}|{slice.RootContainerId}|"
            + string.Join(";", slice.Occurrences.Select(o => $"{o.OccurrenceId}:{o.Role}:{o.SemanticDescriptor}"))
            + "|" + string.Join(";", slice.ScopedClaims.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));
        var groundingText = $"{grounding.SourceRevisionId}|{grounding.Result}|{grounding.OwningContainerId}|"
            + string.Join(";", grounding.Candidates.Select(c => $"{c.OccurrenceId}:{c.Role}:{c.SemanticDescriptor}"));
        return new CanonicalProjection(evidenceIds, admissions, revisions, sliceText, groundingText);
    }
}
