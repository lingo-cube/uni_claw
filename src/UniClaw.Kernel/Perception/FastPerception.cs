using System.Diagnostics;
using System.Security.Cryptography;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// ArtifactMetadata — RawArtifact 的采集元数据。Frame 是 SpatialFrame 名
///（§17：任何 point/bbox/region 必须绑定 frame；artifact 坐标系即
/// “artifact”）。CaptureTime 由采集侧显式提供——FastPerception 自身无
/// wall-clock（replayability：无隐藏 canonical 输入）。
/// </summary>
public sealed record ArtifactMetadata(
    int Width,
    int Height,
    string Frame,
    DateTimeOffset CaptureTime,
    string? CaptureScope = null);

/// <summary>
/// RawArtifact — capability-plane 的 raw perception 输入（P2 producer 侧）。
/// ArtifactId 内容确定性派生（同 payload → 同 id：R-UW 同族的可引用性，
/// 供未来 Trace/Replay 引用 raw artifact 而不复制内容）。
/// </summary>
public sealed record RawArtifact(
    string ArtifactId,
    byte[] Payload,
    ArtifactMetadata Metadata)
{
    /// <summary>内容寻址构造（sha256 前 16 hex）。</summary>
    public static RawArtifact Capture(byte[] payload, ArtifactMetadata metadata)
    {
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return new RawArtifact("art-" + hash[..16], payload, metadata);
    }
}

/// <summary>
/// Strategy 输出的最小 observation 语义：Subject + Value。Kind/Context/
/// Provenance 由 FastPerception 统一构造（strategy 不感知 ingress 细节；
/// legacy DTO 不得进入本类型——UWM-009 §30 / PER-002 §16）。
/// </summary>
public sealed record ArtifactObservation(string Subject, string Value);

/// <summary>
/// IFastPerceptionStrategy — Fast Perception 可替换 seam（UWM-009 §16：
/// Fast/Slow 策略不冻结）。deterministic corpus double、legacy-adapted
/// provider、future OCR/YOLO provider 可替换，而无需修改 Evidence Ledger、
/// WorldModel 或 ContainerIdentity authority。实现必须确定性（同 artifact
/// 同 observations）。
/// </summary>
public interface IFastPerceptionStrategy
{
    IReadOnlyList<ArtifactObservation> Observe(RawArtifact artifact);
}

/// <summary>
/// IVersionedFastPerceptionStrategy — FCR-001 最小 version seam：strategy
/// 显式声明自身 identity 与 version，作为确定性计算复用缓存键的组成部分。
/// 实现者契约：StrategyIdentity 跨 run / replay 稳定（实现族标识）；
/// StrategyVersion 在模型、规则、配置或实现语义变化时**必须**变化（否则
/// 旧缓存被错误复用——失效责任在实现者，接口无法强制）。两者均禁用进程
/// 随机值、对象引用、时间源（确定性 replay 纪律）。未实现本接口的
/// strategy 不参与缓存（fail-safe：行为与无缓存完全一致）。
/// </summary>
public interface IVersionedFastPerceptionStrategy : IFastPerceptionStrategy
{
    string StrategyIdentity { get; }
    string StrategyVersion { get; }
}

/// <summary>
/// FastPerception — P2 producer 侧薄组件（PER-002B）：RawArtifact →
/// ObservationProposal。Perception proposes observations; WorldModel
/// establishes belief——本类型无任何 belief / ContainerIdentity /
/// AssociationDisposition authority（§14）。产出必须经 EvidenceLedger.Admit
/// （P2）admission 执法，禁止直连 WorldModel（§15）。confidence 等 Provider
/// metadata 不进入 payload（§19：threshold ≠ truth threshold，无 buyer 不加）。
/// missing detection 一律不产生 observation——absence 需要显式 negative
/// evidence，本 seam 不发明（§18）。
/// LAT-001：可选 trace / metrics 观察面（null = 禁用，禁用路径零观察
/// 开销）；观察故障全吸收，不改变本组件行为。
/// </summary>
public sealed class FastPerception
{
    private readonly IFastPerceptionStrategy _strategy;
    private readonly string _producer;
    private readonly IRunTrace? _trace;
    private readonly RuntimeStageMetrics? _metrics;
    private readonly StrategyObservationCache? _cache;
    private readonly IVersionedFastPerceptionStrategy? _versioned;

    /// <param name="producer">ProducerIdentity（入 provenance，如 "perception.fast"）。</param>
    public FastPerception(string producer, IFastPerceptionStrategy strategy)
        : this(producer, strategy, trace: null, metrics: null)
    {
    }

    /// <summary>LAT-001 观察面注入（trace 结构因果 + metrics 量化；均可 null = 禁用）。
    /// FCR-001：cacheCapacity 非 null 且 strategy 实现 IVersionedFastPerceptionStrategy
    /// 时启用有界确定性计算复用（缓存 = 本实例内部实现细节；strategy 未实现
    /// version seam ⇒ 缓存自动禁用，行为与无缓存一致）。</summary>
    public FastPerception(
        string producer, IFastPerceptionStrategy strategy, IRunTrace? trace, RuntimeStageMetrics? metrics,
        int? cacheCapacity = null)
    {
        if (string.IsNullOrWhiteSpace(producer))
            throw new ArgumentException("producer identity 必须非空", nameof(producer));
        _producer = producer;
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _trace = trace;
        _metrics = metrics;
        if (cacheCapacity is { } capacity && strategy is IVersionedFastPerceptionStrategy versioned)
        {
            _cache = new StrategyObservationCache(capacity);
            _versioned = versioned;
        }
    }

    /// <summary>
    /// 观察一个 raw artifact，产出完整 provenance 的 ObservationProposal。
    /// context 表达观察流程上下文（External / PostActionEffectFlow——ING-006
    /// 语义；post-action 帧由调用侧声明，perception 不自行推断）。
    /// </summary>
    public IReadOnlyList<ObservationProposal> Observe(
        RawArtifact artifact,
        ObservationContext context = ObservationContext.External)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var artifactRefs = new[] { new TraceReference(TraceReferenceKind.Artifact, artifact.ArtifactId) };
        var observeSpan = StartTraced(TraceCatalog.PerceptionObserve, parent: null, artifactRefs);
        _metrics?.CountArtifactPresentation(artifact.ArtifactId);
        try
        {
            // FCR-001 D5：strategy span / 计时 / FastPerceptionStrategy invocation
            // 计数只发生在真实计算（elected caller，经 Compute 局部函数统一）；
            // 缓存命中不伪造调用（无 strategy span、无 invocation 计数）。
            // provenance 恒为每次调用现场重建（emission 阶段照旧）。
            var strategyInvoked = false;
            var strategyTicks = 0L;
            ITraceOperationScope? strategySpan = null;
            IReadOnlyList<ArtifactObservation> observations;
            IReadOnlyList<ArtifactObservation> Compute()
            {
                strategyInvoked = true;
                strategySpan = StartTraced(TraceCatalog.PerceptionStrategy, observeSpan.Context, artifactRefs);
                var start = Stopwatch.GetTimestamp();
                try
                {
                    return _strategy.Observe(artifact);
                }
                finally
                {
                    strategyTicks = Stopwatch.GetTimestamp() - start;
                }
            }
            try
            {
                // FCR-001：identity / version 逐调用读取（策略配置热变化的失效
                // 语义即时生效；契约仍要求同语义下稳定）
                observations = _cache is { } cache
                    ? cache.ObserveWithReuse(
                        _versioned!.StrategyIdentity, _versioned.StrategyVersion,
                        artifact.ArtifactId, Compute, _metrics)
                    : Compute();
            }
            catch (Exception)
            {
                if (strategySpan is not null)
                    TryComplete(strategySpan, StructuralOutcome.Faulted);
                throw;
            }
            if (strategySpan is not null)
                TryComplete(strategySpan, StructuralOutcome.Completed);
            if (strategyInvoked)
                _metrics?.Record(RuntimeStage.FastPerceptionStrategy, strategyTicks,
                    inputSize: artifact.Payload.Length, outputSize: observations.Count);

            var emissionSpan = StartTraced(TraceCatalog.PerceptionEmitProposal, observeSpan.Context, artifactRefs);
            var emissionStart = Stopwatch.GetTimestamp();
            var proposals = observations
                .Select(o => new ObservationProposal(
                    new ObservationClaim(o.Subject, o.Value),
                    IngressKind.Observation,
                    context,
                    new Provenance(
                        Producer: _producer,
                        CaptureTime: artifact.Metadata.CaptureTime,
                        Scope: artifact.Metadata.CaptureScope ?? $"artifact:{artifact.ArtifactId}",
                        TransformationLineage: new[] { $"fast:{_producer}", $"artifact:{artifact.ArtifactId}" })))
                .ToList();
            var emissionTicks = Stopwatch.GetTimestamp() - emissionStart;
            TryComplete(emissionSpan, StructuralOutcome.Completed);
            _metrics?.Record(RuntimeStage.ProposalEmission, emissionTicks,
                inputSize: observations.Count, outputSize: proposals.Count);

            TryComplete(observeSpan, StructuralOutcome.Completed);
            return proposals;
        }
        catch (Exception)
        {
            TryComplete(observeSpan, StructuralOutcome.Faulted);
            throw;
        }
    }

    /// <summary>trace 观察面 fail-safe（同 UniKernel 先例：trace 故障不改变行为）。</summary>
    private ITraceOperationScope StartTraced(
        SpanDefinition definition, TraceContext? parent, IReadOnlyList<TraceReference> references)
    {
        if (_trace is null)
            return NoOpOperationScope.Instance;
        try
        {
            return _trace.StartOperation(definition, parent, references);
        }
        catch (Exception)
        {
            return NoOpOperationScope.Instance;
        }
    }

    private static void TryComplete(ITraceOperationScope span, StructuralOutcome outcome)
    {
        try
        {
            span.Complete(outcome);
        }
        catch (Exception)
        {
            // trace 故障不得改变 Runtime 行为
        }
    }
}
