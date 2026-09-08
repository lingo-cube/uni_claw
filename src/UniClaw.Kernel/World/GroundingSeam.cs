namespace UniClaw.Kernel.World;

/// <summary>
/// P23 ResolveCurrent 的目标描述（UIW-004 / UWM-009 v0.3 §41 / ADR-0011 族）：
/// 机械确定性匹配维度 = Role（必填）+ SemanticDescriptor / OwningContainerId
/// （可选收窄）。不携带 AI 判别——descriptor 语义由 observation 侧派生，
/// 此处只做相等匹配。
/// </summary>
public sealed record TargetDescriptor(string Role, string? SemanticDescriptor = null, string? OwningContainerId = null);

/// <summary>ResolveCurrent 候选集四态（fail-closed 语义）。</summary>
public enum CurrentCandidateSetResultKind
{
    /// <summary>恰好一个匹配 occurrence（可绑）。</summary>
    UniqueCandidate,

    /// <summary>零匹配（无可绑候选）。</summary>
    NoCandidate,

    /// <summary>多个匹配（Identity never creates information，不可绑）。</summary>
    MultipleCandidates,

    /// <summary>Current 缺失或无 observation seam（Occurrences null）——投影不可用，
    /// fail-closed，非「不存在」判定。</summary>
    ScopeProjectionUnavailable,
}

/// <summary>
/// 匹配 occurrence 的 fact 载荷（owner-owned；SourceRevisionId = 派生时
/// Current.RevisionId，消费方据此判 stale）。
/// </summary>
public sealed record CandidateOccurrenceFact(
    string OccurrenceId,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    string SourceRevisionId);

/// <summary>
/// CurrentGroundingView — World Model 为 P23 消费点（grounding / binding
/// candidate 构造）派生的 consumer view（ADR-0011 四原则：consumer-specific
/// immutable projection / Owner-only derivation / 非 second truth / 不承载
/// consumer-owned judgment）。纯只读派生：零 log、零 registry、零 revision
/// 副作用。四态判定是机械确定性结果（0/1/N + 投影不可用），绑定与否的
/// 认定权在 Effect Boundary。
/// </summary>
public sealed record CurrentGroundingView(
    string SourceRevisionId,
    string? OwningContainerId,
    CurrentCandidateSetResultKind Result,
    IReadOnlyList<CandidateOccurrenceFact> Candidates);
