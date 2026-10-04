using System.Collections.Concurrent;
using System.Collections.Frozen;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.Perception;

/// <summary>Slow 所需的最小 claim 维度；不携带 expected value 或模型提示。</summary>
internal sealed record RequiredClaim(string Subject, string Field)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Subject) && !string.IsNullOrWhiteSpace(Field);
}

/// <summary>产品拥有的逻辑 profile 标识，concrete provider 绑定不在此处决定。</summary>
internal readonly record struct LogicalProfileId(string Value)
{
    public static LogicalProfileId Text => new("slow.semantic.text");
    public static LogicalProfileId Visual => new("slow.semantic.visual");
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

/// <summary>一次语义调用的 correlation key。transport retry 不产生新的 key。</summary>
internal sealed record SlowAttemptKey(
    string Target,
    string ObservationCycleId,
    RequiredClaim RequiredClaim,
    LogicalProfileId LogicalProfile)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Target)
        && !string.IsNullOrWhiteSpace(ObservationCycleId)
        && RequiredClaim is { IsValid: true }
        && LogicalProfile.IsValid;
}

internal enum SlowAttemptState { InFlight, Terminal }

/// <summary>
/// 进程内、短生命周期的 keyed reservation。它不是 Evidence Ledger 或持久状态 owner。
/// </summary>
internal sealed class EphemeralAttemptLedger
{
    private readonly ConcurrentDictionary<SlowAttemptKey, SlowAttemptState> _attempts = new();

    public int Count => _attempts.Count;

    public bool TryStart(SlowAttemptKey key) => key is { IsValid: true }
        && _attempts.TryAdd(key, SlowAttemptState.InFlight);

    public bool TryStart(SlowAttemptKey key, out AttemptReservation reservation)
    {
        if (TryStart(key))
        {
            reservation = new AttemptReservation(key, this);
            return true;
        }

        reservation = default;
        return false;
    }

    public bool TryGetState(SlowAttemptKey key, out SlowAttemptState state) => _attempts.TryGetValue(key, out state);

    public bool MarkTerminal(SlowAttemptKey key) => _attempts.TryUpdate(key, SlowAttemptState.Terminal, SlowAttemptState.InFlight);

    public bool CanSemanticRetry(SlowAttemptKey key) => !_attempts.ContainsKey(key);

    public bool IsTerminal(SlowAttemptKey key) => _attempts.TryGetValue(key, out var state)
        && state == SlowAttemptState.Terminal;

    internal readonly struct AttemptReservation : IDisposable
    {
        private readonly EphemeralAttemptLedger? _owner;
        public SlowAttemptKey Key { get; }
        internal AttemptReservation(SlowAttemptKey key, EphemeralAttemptLedger owner) { Key = key; _owner = owner; }
        public void Dispose() => _owner?.MarkTerminal(Key);
    }
}

/// <summary>Raw artifact 的窄引用；ArtifactId 与 CaptureId 保持不同语义。</summary>
internal sealed record RawArtifactRef(string ArtifactId)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ArtifactId);
}

internal enum ExclusionDisposition
{
    Included,
    OutOfScope,
    IrrelevantToClaim,
    BudgetExceeded,
    ContextInsufficient,
}

/// <summary>布局/候选的 bounded 投影，不携带完整 hierarchy 或 raw XML。</summary>
internal sealed record ElementLayoutContext(
    string CaptureId,
    RequiredClaim RequiredClaim,
    IReadOnlyList<string> CandidateEvidenceIds,
    IReadOnlyDictionary<string, UiBounds>? CandidateBounds = null)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(CaptureId)
        && RequiredClaim is { IsValid: true }
        && CandidateEvidenceIds is not null
        && CandidateEvidenceIds.All(id => !string.IsNullOrWhiteSpace(id));
}

/// <summary>文本语义推理的 bounded admitted evidence 投影。</summary>
internal sealed record SemanticReasoningContext(
    RequiredClaim RequiredClaim,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string>? LineageParentEvidenceIds = null,
    string? Reason = null)
{
    public bool IsValid => RequiredClaim is { IsValid: true }
        && EvidenceIds is not null
        && EvidenceIds.All(id => !string.IsNullOrWhiteSpace(id))
        && (LineageParentEvidenceIds is null || LineageParentEvidenceIds.All(id => !string.IsNullOrWhiteSpace(id)));
}

/// <summary>冲突/歧义的最小原子 basis；不能拆成只保留一边。</summary>
internal sealed record ConflictBasisBundle(
    IReadOnlyList<string> EvidenceIds,
    string Reason,
    string? CaptureId = null)
{
    public bool IsValid => EvidenceIds is { Count: > 0 }
        && EvidenceIds.All(id => !string.IsNullOrWhiteSpace(id))
        && !string.IsNullOrWhiteSpace(Reason);

    public bool Fits(int evidenceBudget) => IsValid && evidenceBudget >= EvidenceIds.Count;
}

/// <summary>按 claim/capture 有界组合出的只读上下文。只保存引用和 typed projection。</summary>
internal sealed record EvidenceContext(
    RequiredClaim RequiredClaim,
    IReadOnlyList<string> EvidenceIds,
    ElementLayoutContext? ElementLayout = null,
    SemanticReasoningContext? SemanticReasoning = null,
    ConflictBasisBundle? ConflictBasis = null,
    IReadOnlyDictionary<string, ExclusionDisposition>? Exclusions = null,
    int Budget = 32,
    bool IsContextInsufficient = false)
{
    public IReadOnlyList<string> EvidenceIds { get; } = FreezeIds(EvidenceIds);
    public IReadOnlyDictionary<string, ExclusionDisposition> Exclusions { get; } =
        (Exclusions ?? new Dictionary<string, ExclusionDisposition>(StringComparer.Ordinal))
            .ToFrozenDictionary(StringComparer.Ordinal);

    public bool IsValid => !IsContextInsufficient && RequiredClaim is { IsValid: true }
        && Budget >= 0 && EvidenceIds.Count <= Budget
        && (ElementLayout is null || ElementLayout.IsValid)
        && (SemanticReasoning is null || SemanticReasoning.IsValid)
        && (ConflictBasis is null || ConflictBasis.Fits(Budget));

    public static EvidenceContext ContextInsufficient(RequiredClaim claim, int budget = 0) =>
        new(claim, Array.Empty<string>(), Budget: Math.Max(0, budget), IsContextInsufficient: true);

    private static IReadOnlyList<string> FreezeIds(IReadOnlyList<string> ids) =>
        (ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>
/// Deterministic bounded composer. It accepts only typed projections and evidence ids;
/// callers cannot pass a ledger, world snapshot, provider payload, or raw XML.
/// </summary>
internal static class EvidenceContextBuilder
{
    public static EvidenceContext Build(
        RequiredClaim claim,
        ElementLayoutContext? layout = null,
        SemanticReasoningContext? semantic = null,
        ConflictBasisBundle? conflict = null,
        int budget = 32,
        IReadOnlyDictionary<string, ExclusionDisposition>? exclusions = null)
    {
        if (claim is not { IsValid: true } || budget < 0
            || (layout is not null && (!layout.IsValid || layout.RequiredClaim != claim))
            || (semantic is not null && (!semantic.IsValid || semantic.RequiredClaim != claim)))
            return EvidenceContext.ContextInsufficient(claim, budget);

        var ids = new List<string>();
        if (layout is not null) ids.AddRange(layout.CandidateEvidenceIds);
        if (semantic is not null)
        {
            ids.AddRange(semantic.EvidenceIds);
            ids.AddRange(semantic.LineageParentEvidenceIds ?? Array.Empty<string>());
        }

        if (conflict is not null)
        {
            // A conflict basis is atomic: no partial basis is projected.
            if (!conflict.Fits(budget)) return EvidenceContext.ContextInsufficient(claim, budget);
            ids.AddRange(conflict.EvidenceIds);
        }

        var distinct = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length > budget) return EvidenceContext.ContextInsufficient(claim, budget);
        return new EvidenceContext(claim, distinct, layout, semantic, conflict, exclusions, budget);
    }
}

/// <summary>realization 在启动时解析出的冻结 concrete binding。</summary>
internal sealed record ModelBindingSnapshot(
    LogicalProfileId LogicalProfile,
    string ProviderId,
    string ModelId,
    string? ConfigId = null,
    string? PipelineRevision = null,
    string? DeploymentId = null,
    string? VariantId = null,
    bool Available = true,
    bool Experimental = true,
    string? FallbackFrom = null,
    string SchemaVersion = "slow.binding.v1",
    bool Health = true)
{
    public bool IsValid => LogicalProfile.IsValid && !string.IsNullOrWhiteSpace(ProviderId)
        && !string.IsNullOrWhiteSpace(ModelId)
        && !string.IsNullOrWhiteSpace(SchemaVersion);
}

internal enum SlowExecutionStatus
{
    Succeeded, Partial, Timeout, Cancelled, ModelUnavailable, UnsupportedCapability,
    ContextInsufficient, MalformedResponse, SchemaFailure, InfrastructureFailure, InvalidInput,
}

internal enum SlowSemanticDisposition { Supported, Conflicted, Unknown, Unsupported }

/// <summary>内部 orchestration 请求；Slow provider 不可由此绕过 P2。</summary>
internal sealed record SlowPerceptionRequest(
    string RequestId,
    SlowAttemptKey AttemptKey,
    RequiredClaim RequiredClaim,
    LogicalProfileId LogicalProfile,
    string BuyerRef,
    string Reason,
    FusionCapture Capture,
    EvidenceContext Context,
    RawArtifactRef? RawArtifact = null,
    ModelBindingSnapshot? Binding = null,
    TimeSpan? Budget = null,
    FastTextBasis? FastBasis = null)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(RequestId)
        && AttemptKey is { IsValid: true }
        && RequiredClaim is { IsValid: true }
        && LogicalProfile.IsValid
        && !string.IsNullOrWhiteSpace(BuyerRef)
        && !string.IsNullOrWhiteSpace(Reason)
        && Capture is { IsValid: true }
        && Context is { IsValid: true }
        && (LogicalProfile != LogicalProfileId.Visual || RawArtifact is { IsValid: true });
}

/// <summary>Slow realization 输出；proposal 仍须经既有 P2/Evidence Ledger。</summary>
internal sealed record SlowResult(
    string RequestId,
    SlowAttemptKey AttemptKey,
    SlowExecutionStatus Status,
    IReadOnlyList<ObservationProposal> Proposals,
    FusionCapture Capture,
    ModelBindingSnapshot? Binding = null,
    SlowSemanticDisposition SemanticDisposition = SlowSemanticDisposition.Unknown,
    string? Diagnostic = null)
{
    public IReadOnlyList<ObservationProposal> Proposals { get; } =
        (Proposals ?? Array.Empty<ObservationProposal>()).ToArray();

    public bool IsSuccessful => Status is SlowExecutionStatus.Succeeded or SlowExecutionStatus.Partial;
    public bool HasProposals => Proposals.Count != 0;
}
