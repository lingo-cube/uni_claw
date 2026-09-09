using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>corpus manifest 条目（target-neutral，normalized survivor）。</summary>
public sealed record CorpusScenario(
    string ScenarioId,
    string Provenance,
    string Artifact,
    DateTimeOffset CaptureTime,
    IReadOnlyList<(string Subject, string Value)> Observations,
    string? KnownAmbiguity,
    string? KnownFailure);

/// <summary>
/// PER-002 target-neutral corpus 装载器（测试工具）。读取
/// Perception/Corpus/scenarios.json + artifacts；不解析任何 legacy 格式
///（legacy XML 的解析只发生在 one-time adapter 工具与 adapter 验证测试中）。
/// </summary>
public sealed class CorpusManifest
{
    public IReadOnlyList<CorpusScenario> Scenarios { get; }
    public IReadOnlySet<string> SubjectScope { get; }

    private CorpusManifest(IReadOnlyList<CorpusScenario> scenarios, IReadOnlySet<string> scope) =>
        (Scenarios, SubjectScope) = (scenarios, scope);

    public static CorpusManifest Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "scenarios.json")));

        var scenarios = doc.RootElement.GetProperty("scenarios").EnumerateArray()
            .Select(s => new CorpusScenario(
                s.GetProperty("scenarioId").GetString()!,
                s.GetProperty("provenance").GetString()!,
                s.GetProperty("artifact").GetString()!,
                DateTimeOffset.Parse(s.GetProperty("captureTime").GetString()!),
                s.GetProperty("observations").EnumerateArray()
                    .Select(o => (o.GetProperty("subject").GetString()!, o.GetProperty("value").GetString()!))
                    .ToList(),
                s.TryGetProperty("knownAmbiguity", out var a) && a.ValueKind != JsonValueKind.Null
                    ? a.GetString() : null,
                s.TryGetProperty("knownFailure", out var f) && f.ValueKind != JsonValueKind.Null
                    ? f.GetString() : null))
            .ToList();

        var scope = new HashSet<string>();
        foreach (var s in scenarios)
            foreach (var (subject, _) in s.Observations)
                scope.Add(subject);
        return new CorpusManifest(scenarios, scope);
    }

    public CorpusScenario Scenario(string scenarioId) =>
        Scenarios.FirstOrDefault(s => s.ScenarioId == scenarioId)
        ?? throw new KeyNotFoundException($"corpus scenario 不存在：{scenarioId}");

    /// <summary>以内容寻址方式构造 RawArtifact（metadata 来自 manifest/frameDefaults）。</summary>
    public RawArtifact Artifact(string scenarioId)
    {
        var scenario = Scenario(scenarioId);
        var bytes = File.ReadAllBytes(Path.Combine(
            Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus"), scenario.Artifact));
        return RawArtifact.Capture(bytes, new ArtifactMetadata(
            Width: 1080, Height: 1920, Frame: "artifact",
            CaptureTime: scenario.CaptureTime));
    }
}

/// <summary>
/// CorpusFastPerception — deterministic Fast strategy double：返回指定 scenario
/// 的 recorded observations。构造时绑定 detection set（同一 artifact 可能对应
/// 完整/部分/降级检测——provider capability level 是 provider 状态，不是 artifact
/// 属性）；fail-loud 校验 artifact bytes 与 scenario 匹配（不得对无关 artifact
/// 幻觉输出）。同输入同输出（determinism 契约）。
/// FCR-001：实现 version seam——identity = corpus 策略族；version = corpus
/// 格式稳定常量（detection set 变化不改变 version：同一 corpus 版本内
/// 不同 detection set 对各自 artifact 仍是确定性函数）。
/// </summary>
public sealed class CorpusFastPerception : IVersionedFastPerceptionStrategy
{
    /// <summary>corpus 策略族 identity（FCR-001 稳定标识）。</summary>
    public const string Identity = "corpus-fast-perception";

    /// <summary>corpus 格式 version（FCR-001：corpus 内容/格式变化时必须更新）。</summary>
    public const string Version = "corpus/2026-09";

    private readonly CorpusManifest _manifest;
    private readonly string _detectionSet;

    public CorpusFastPerception(CorpusManifest manifest, string detectionSetScenarioId) =>
        (_manifest, _detectionSet) = (manifest, detectionSetScenarioId);

    public string StrategyIdentity => Identity;

    /// <summary>FCR-001：version 携带 detection set——同一 artifact 的完整/
    /// 局部 detection 是不同的 strategy 语义配置，必须键分离（D1 契约示范）。</summary>
    public string StrategyVersion => $"{Version}#{_detectionSet}";

    public IReadOnlyList<ArtifactObservation> Observe(RawArtifact artifact)
    {
        var scenario = _manifest.Scenario(_detectionSet);
        var expected = _manifest.Artifact(scenario.ScenarioId);
        if (expected.ArtifactId != artifact.ArtifactId)
            throw new InvalidOperationException(
                $"detection set {scenario.ScenarioId} 不属于 artifact {artifact.ArtifactId}——provider 不得幻觉输出");
        return scenario.Observations
            .Select(o => new ArtifactObservation(o.Subject, o.Value))
            .ToList();
    }
}

/// <summary>
/// Fast 语义 hint 双轨 association double：page signature（perception.page.signature）
/// 驱动 identity 判别；其余 observation 无 identity 信号（Insufficient，无变异）。
/// prior 永不单独决定 Matched（gate 在 WorldModel 边界）。
/// </summary>
public sealed class SignatureHintAssociationStrategy : IAssociationStrategy
{
    public const string SignatureSubject = "perception.page.signature";

    public AssociationProposal Propose(AssociationInput input)
    {
        if (input.Current.Claim.Subject != SignatureSubject)
            return Insufficient("no-identity-signal");
        var sig = input.Current.Claim.Value;
        var evId = input.Current.EvidenceId;

        if (input.Previous is null)
            return NewProposal(evId, "first-observation");

        var matches = input.Previous.Containers
            .Where(c => input.Previous.WorldState.TryGetValue(
                WorldModel.SignatureSubjectPrefix + c.Identity.ContainerId, out var claim)
                && claim.Value == sig)
            .Select(c => c.Identity.ContainerId)
            .ToList();

        return matches.Count == 1
            ? new AssociationProposal(
                AssociationDispositionKind.Matched, matches[0],
                new[] { new AssociationCandidate(matches[0], new[] { evId }, Array.Empty<string>()) },
                Array.Empty<ProposedRelation>(), "page-signature-match")
            : NewProposal(evId, "unseen-page-signature");
    }

    internal static AssociationProposal NewProposal(string evId, string reason) => new(
        AssociationDispositionKind.New, MatchedContainerId: null,
        new[] { new AssociationCandidate("(new)", new[] { evId }, Array.Empty<string>()) },
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);

    internal static AssociationProposal Insufficient(string reason) => new(
        AssociationDispositionKind.Insufficient, MatchedContainerId: null,
        Candidates: Array.Empty<AssociationCandidate>(),
        Relations: Array.Empty<ProposedRelation>(), Reason: reason);
}

/// <summary>
/// S7 组合 double：page signature 走 SignatureHint 逻辑；AlertDialog title
///（ui.text.alertTitle）驱动 overlay-New + Overlays relation（evidence-backed；
/// dialog id 按与 WorldModel 相同的确定性铸造约定推导——test double 知晓
/// realization 约定，product 不感知）。
/// </summary>
public sealed class PageAndOverlayAssociationStrategy : IAssociationStrategy
{
    public const string DialogTitleSubject = "ui.text.alertTitle";

    public AssociationProposal Propose(AssociationInput input)
    {
        if (input.Current.Claim.Subject == DialogTitleSubject)
        {
            if (input.Previous is null || input.Previous.Containers.Count == 0)
                return SignatureHintAssociationStrategy.Insufficient("no-page-context");
            var evId = input.Current.EvidenceId;
            var pageId = input.Previous.Containers[0].Identity.ContainerId;
            var dialogId = "ctr-" + evId[3..15];
            return new AssociationProposal(
                AssociationDispositionKind.New, MatchedContainerId: null,
                new[] { new AssociationCandidate("(new)", new[] { evId }, Array.Empty<string>()) },
                new[] { new ProposedRelation(ContainerRelationKind.Overlays, dialogId, pageId, new[] { evId }) },
                "dialog-overlay-new");
        }
        return new SignatureHintAssociationStrategy().Propose(input);
    }
}
