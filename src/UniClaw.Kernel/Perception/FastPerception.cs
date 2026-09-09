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

    /// <param name="producer">ProducerIdentity（入 provenance，如 "perception.fast"）。</param>
    public FastPerception(string producer, IFastPerceptionStrategy strategy)
        : this(producer, strategy, trace: null, metrics: null)
    {
    }

    /// <summary>LAT-001 观察面注入（trace 结构因果 + metrics 量化；均可 null = 禁用）。</summary>
    public FastPerception(
        string producer, IFastPerceptionStrategy strategy, IRunTrace? trace, RuntimeStageMetrics? metrics)
    {
        if (string.IsNullOrWhiteSpace(producer))
            throw new ArgumentException("producer identity 必须非空", nameof(producer));
        _producer = producer;
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _trace = trace;
        _metrics = metrics;
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
            var strategySpan = StartTraced(TraceCatalog.PerceptionStrategy, observeSpan.Context, artifactRefs);
            IReadOnlyList<ArtifactObservation> observations;
            var start = Stopwatch.GetTimestamp();
            try
            {
                observations = _strategy.Observe(artifact);
            }
            catch (Exception)
            {
                TryComplete(strategySpan, StructuralOutcome.Faulted);
                throw;
            }
            var strategyTicks = Stopwatch.GetTimestamp() - start;
            TryComplete(strategySpan, StructuralOutcome.Completed);
            _metrics?.Record(RuntimeStage.FastPerceptionStrategy, strategyTicks,
                inputSize: artifact.Payload.Length, outputSize: observations.Count);

            var emissionSpan = StartTraced(TraceCatalog.PerceptionEmitProposal, observeSpan.Context, artifactRefs);
            start = Stopwatch.GetTimestamp();
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
            var emissionTicks = Stopwatch.GetTimestamp() - start;
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
