using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 / P26：ScenarioStimulus——由 reviewed fixture（Phase 1 golden-run
/// 资产）派生的 immutable、显式版本化外部输入。不是 Trace Event、不是
/// canonical Evidence、不注入 owner state；Simulation 只经
/// ScenarioStimulusFeed 消费它们（context 纪律 fail closed）。
///
/// DUAL-SOURCE evidence（评审 D18 事实澄清）：主场景证据是双源的——
/// occurrence 景观锚定在 golden response JSON 上，经真实 FastPerception +
/// LiveVisionStrategy（perception 侧）派生；而 obligation 相关的 switch
/// 状态（ReviewedStateClaims）来自 Human-reviewed manifest claims
/// （reviewed 侧），作为独立的 recorded external observation 入证。
/// 不主张任何结果「仅由 response JSON 派生」——两源在 join 后共同进入
/// E2B 管线。
/// </summary>
internal abstract record ScenarioStimulus
{
    /// <summary>唯一 stimulus 身份（bundle 校验拒绝重复；feed Submit 拒绝重复消费）。</summary>
    public required string StimulusId { get; init; }

    /// <summary>virtual time（无 canonical clock，Deferred ⑪；仅供 provenance）。</summary>
    public required DateTimeOffset VirtualTime { get; init; }

    /// <summary>
    /// 外部观察帧：perception artifact（golden response JSON）+ reviewed
    /// elements（role/state/normalized bounds，来自 Human-reviewed manifest）
    /// + reviewed state claims（subject=value，obligation 判定输入）。
    /// </summary>
    public sealed record ObservationFrame(
        string PerceptionArtifactId,
        IReadOnlyList<ReviewedElement> ReviewedElements,
        IReadOnlyList<(string Subject, string Value)> ReviewedStateClaims,
        ObservationContext Context) : ScenarioStimulus;

    /// <summary>宿主/用户 cancel（Phase 1 经 contract SafeStop obligation 表达）。</summary>
    public sealed record CancelRequest(string Reason) : ScenarioStimulus;
}

/// <summary>
/// reviewed manifest element：role（perceptionType）+ state + normalized
/// bounds。join 规则（确定性）：detection 归一化中心落入 bounds 内 →
/// occurrence role/state 采用 reviewed 值；否则 role=raw label、state=null。
/// </summary>
internal sealed record ReviewedElement(
    string Role,
    string? State,
    double X1,
    double Y1,
    double X2,
    double Y2);

/// <summary>
/// 消费纪律执行面：按 driver 期望的 ObservationContext 服务 stimulus；
/// context 失配 / 未知类型 → Unexpected（fail closed 信号）；terminal 后剩余
/// stimulus = unconsumed（场景末尾必须显式核对）。重复 StimulusId 在 bundle
/// 校验时即拒绝；运行期 Submit 的重复 id 进 Rejected（fail closed：不消费、
/// 返回 false，不抛异常）。
/// </summary>
internal sealed class ScenarioStimulusFeed
{
    private readonly Queue<ScenarioStimulus> _pending;
    private readonly List<string> _consumed = new();
    private readonly List<string> _unexpected = new();
    private readonly List<string> _rejected = new();
    private readonly HashSet<string> _knownIds = new(StringComparer.Ordinal);
    private readonly ScenarioPerceptionAdapter _adapter;

    public ScenarioStimulusFeed(
        IEnumerable<ScenarioStimulus> stimuli,
        ScenarioPerceptionAdapter adapter)
    {
        _adapter = adapter;
        _pending = new Queue<ScenarioStimulus>();
        foreach (var stimulus in stimuli)
        {
            if (!_knownIds.Add(stimulus.StimulusId))
                throw new ScenarioBundleException("duplicate stimulus id: " + stimulus.StimulusId);
            _pending.Enqueue(stimulus);
        }
    }

    public IReadOnlyList<string> Consumed => _consumed;
    public IReadOnlyList<string> Unexpected => _unexpected;

    /// <summary>运行期 Submit 拒绝清单（重复 id：不消费、不上抛）。</summary>
    public IReadOnlyList<string> Rejected => _rejected;

    public IReadOnlyList<string> Remaining => _pending.Select(s => s.StimulusId).ToList();

    /// <summary>
    /// D21 运行期补充 stimulus（phased 场景由测试经 Host.SubmitStimulus 驱动）。
    /// 重复 StimulusId → 记入 Rejected、返回 false（fail closed：不消费）；
    /// 成功入队 → true。
    /// </summary>
    public bool Submit(ScenarioStimulus stimulus)
    {
        ArgumentNullException.ThrowIfNull(stimulus);
        if (!_knownIds.Add(stimulus.StimulusId))
        {
            _rejected.Add(stimulus.StimulusId);
            return false;
        }
        _pending.Enqueue(stimulus);
        return true;
    }

    /// <summary>driver NextInput seam 的 feed 侧实现。</summary>
    public RunDriverInput? Next(ObservationDirective directive)
    {
        var expected = directive.Context;
        if (_pending.Count == 0)
            return null;
        var stimulus = _pending.Peek();
        switch (stimulus)
        {
            case ScenarioStimulus.CancelRequest cancel:
                _pending.Dequeue();
                _consumed.Add(cancel.StimulusId);
                return new RunDriverInput.Cancel(cancel.Reason, cancel.VirtualTime);

            case ScenarioStimulus.ObservationFrame frame when frame.Context == expected:
                _pending.Dequeue();
                _consumed.Add(frame.StimulusId);
                return new RunDriverInput.Observation(
                    _adapter.BuildProposals(frame));

            case ScenarioStimulus.ObservationFrame mismatch:
                _unexpected.Add($"{mismatch.StimulusId}:expected-{expected}:got-{mismatch.Context}");
                return new RunDriverInput.Unexpected(
                    $"stimulus-context-mismatch:{mismatch.StimulusId}:expected-{expected}");

            default:
                _unexpected.Add(stimulus.StimulusId);
                return new RunDriverInput.Unexpected($"unknown-stimulus:{stimulus.StimulusId}");
        }
    }

    /// <summary>stimulus id → perception artifact id（D18 derivation 映射的 host 侧登记）。</summary>
    internal IReadOnlyDictionary<string, string> StimulusArtifacts => _stimulusArtifacts;
    private readonly Dictionary<string, string> _stimulusArtifacts = new(StringComparer.Ordinal);

    internal void RegisterStimulusArtifact(string stimulusId, string perceptionArtifactId) =>
        _stimulusArtifacts[stimulusId] = perceptionArtifactId;
}

/// <summary>
/// Recorded perception adapter（G11/D16/D18）：golden response JSON 经真实
/// FastPerception + LiveVisionStrategy（determinism anchor，ADR-0020），
/// join 成 live.frame claim（LoopTwin 同款 glue + reviewed role/state 覆写），
/// 外加独立 reviewed state claim。不重跑模型；lineage 引用 stimulus +
/// artifact + bundle。
///
/// INSTANCE 级（D18）：每个 host 一个实例，注入该 host 的 IRunTrace 与
/// RuntimeStageMetrics——FastPerception 的 perception.observe span 以
/// TraceReferenceKind.Artifact 引用 RawArtifact（"art-" + sha256 前 16 hex，
/// 与 bundle 资产 content hash 同源），使 sealed trace artifact 成为
/// derivation 的真实输入源。trace 故障由 FastPerception fail-safe 吸收。
/// </summary>
internal sealed class ScenarioPerceptionAdapter
{
    private readonly FastPerception _perception;
    private readonly BundleAssetRegistry _assets;

    public ScenarioPerceptionAdapter(
        BundleAssetRegistry assets,
        IRunTrace? trace,
        RuntimeStageMetrics? metrics)
    {
        _assets = assets;
        _perception = new FastPerception("perception.replay.vision", new LiveVisionStrategy(), trace, metrics);
    }

    public IReadOnlyList<ObservationProposal> BuildProposals(
        ScenarioStimulus.ObservationFrame frame)
    {
        var bytes = _assets.ReadArtifact(frame.PerceptionArtifactId);
        var derived = RawArtifact.Capture(bytes, new ArtifactMetadata(
            1080, 1920, "artifact", frame.VirtualTime,
            $"derived:replay:{frame.PerceptionArtifactId}"));
        var proposals = _perception.Observe(derived, frame.Context);

        // join（LoopTwin glue 模式）：ui.detect.* + spatial → detections；
        // reviewed element 匹配 = detection 归一化中心落入 reviewed bounds。
        // occurrence 语义真值 = reviewed manifest（Human-reviewed）：
        //   - matched element：role/state 取 review，locator 取该 detection 归一化 bounds
        //   - unmatched element：role/state/locator 全取 review（未检出 ≠ absence）
        //   - unmatched detection：role=raw label、state=null
        var classes = proposals
            .Where(p => p.Claim.Subject.StartsWith("ui.detect.", StringComparison.Ordinal)
                && p.Claim.Subject.EndsWith(".class", StringComparison.Ordinal))
            .ToDictionary(
                p => p.Claim.Subject["ui.detect.".Length..^".class".Length],
                p => p.Claim.Value);
        var detections = new List<(string Id, string Cls, double X1, double Y1, double X2, double Y2)>();
        foreach (var (id, cls) in classes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var boundsValue = proposals
                .FirstOrDefault(p => p.Claim.Subject == $"spatial.artifact.bounds.detect.{id}")?.Claim.Value;
            if (boundsValue is null)
                continue;
            var px = boundsValue.Split(',').Select(int.Parse).ToArray();
            detections.Add((id, cls, px[0] / 1080.0, px[1] / 1920.0, px[2] / 1080.0, px[3] / 1920.0));
        }

        var matched = new HashSet<string>(StringComparer.Ordinal);
        var joined = new List<string>();
        foreach (var element in frame.ReviewedElements)
        {
            var detection = detections.FirstOrDefault(d =>
                !matched.Contains(d.Id)
                && (d.X1 + d.X2) / 2 >= element.X1 && (d.X1 + d.X2) / 2 <= element.X2
                && (d.Y1 + d.Y2) / 2 >= element.Y1 && (d.Y1 + d.Y2) / 2 <= element.Y2);
            var (x1, y1, x2, y2) = string.IsNullOrEmpty(detection.Id)
                ? (element.X1, element.Y1, element.X2, element.Y2)
                : (detection.X1, detection.Y1, detection.X2, detection.Y2);
            if (!string.IsNullOrEmpty(detection.Id))
                matched.Add(detection.Id);
            var stateSegment = element.State is null ? "" : $",\"st\":\"{element.State}\"";
            joined.Add($"{{\"id\":\"rev-{element.Role}\",\"cls\":\"{(string.IsNullOrEmpty(detection.Id) ? "reviewed" : detection.Cls)}\","
                + $"\"role\":\"{element.Role}\"{stateSegment},\"b\":[{x1},{y1},{x2},{y2}]}}");
        }
        foreach (var detection in detections.Where(d => !matched.Contains(d.Id)).OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            joined.Add($"{{\"id\":\"{detection.Id}\",\"cls\":\"{detection.Cls}\","
                + $"\"role\":\"{detection.Cls}\",\"b\":[{detection.X1},{detection.Y1},{detection.X2},{detection.Y2}]}}");
        }

        var lineage = new List<string>
        {
            $"artifact:{derived.ArtifactId}",
            $"stimulus:{frame.StimulusId}",
            $"bundle-asset:{frame.PerceptionArtifactId}",
        };
        var result = new List<ObservationProposal>
        {
            new(
                new ObservationClaim("live.frame", $"{{\"detects\":[{string.Join(",", joined)}]}}"),
                IngressKind.Observation,
                frame.Context,
                new Provenance("sim.replay.join", frame.VirtualTime, "scope:live.frame", lineage)),
        };
        foreach (var (subject, value) in frame.ReviewedStateClaims)
        {
            result.Add(new ObservationProposal(
                new ObservationClaim(subject, value),
                IngressKind.Observation,
                frame.Context,
                new Provenance("sim.replay.reviewed", frame.VirtualTime, $"scope:{subject}", lineage)));
        }
        return result;
    }
}
