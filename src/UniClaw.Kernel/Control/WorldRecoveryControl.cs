using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Control;

/// <summary>Supplier execution fact, separate from requests, World revisions and successful evidence admission.</summary>
public sealed record SupplementationExecution(string ExecutionId, string SourceReference,
    string CaptureEvidenceId, IReadOnlyList<string> RequestedSubjects,
    DateTimeOffset StartedAt, DateTimeOffset CompletedAt, string Outcome,
    IReadOnlyList<string> ResultReferences, int ResultVersion = 1);

internal sealed record WorldRecoveryAttempt(string GapId, SupplementationExecution Execution);
public sealed record WorldGapRecoveryView(string GapId, int? KnownExecutionCount, string RecordCoverage,
    IReadOnlyList<SupplementationExecution> Executions)
{
    public IReadOnlyList<SupplementationExecution> ResultHistory { get; init; } = ImmutableArray<SupplementationExecution>.Empty;
}
public sealed record WorldRecoveryView(IReadOnlyList<WorldGapRecoveryView> Gaps,
    bool ReviewRequired, string? ReviewKey, string? Reason);

/// <summary>Control owns execution accounting and bounded review reminders.
/// It never resolves a World gap or grants additional resources.</summary>
internal sealed class WorldRecoveryControl
{
    private readonly Dictionary<string, SupplementationExecution> _executions = new(StringComparer.Ordinal);
    private readonly List<WorldRecoveryAttempt> _attempts = [];
    private readonly List<SupplementationExecution> _results = [];
    private int _relevantUpdates;
    private readonly HashSet<string> _reviewed = new(StringComparer.Ordinal);
    private string? _pendingReview;
    private string? _pendingReason;

    internal int RelevantExecutionRecords => _relevantUpdates;
    internal bool HasPriorReview => _reviewed.Count > 0;

    internal bool Record(SupplementationExecution execution, WorldKnowledgeView knowledge)
    {
        if (string.IsNullOrWhiteSpace(execution.ExecutionId) || string.IsNullOrWhiteSpace(execution.SourceReference)
            || string.IsNullOrWhiteSpace(execution.CaptureEvidenceId) || execution.StartedAt == default
            || execution.CompletedAt < execution.StartedAt || execution.ResultVersion < 1 || string.IsNullOrWhiteSpace(execution.Outcome)
            || execution.RequestedSubjects is not { Count: > 0 } || execution.RequestedSubjects.Any(string.IsNullOrWhiteSpace)
            || execution.ResultReferences is null || execution.ResultReferences.Any(string.IsNullOrWhiteSpace)) return false;
        execution = execution with { RequestedSubjects = execution.RequestedSubjects.ToImmutableArray(),
            ResultReferences = execution.ResultReferences.ToImmutableArray() };
        if (_executions.TryGetValue(execution.ExecutionId, out var prior))
        {
            if (JsonSerializer.Serialize(prior) == JsonSerializer.Serialize(execution)) return true;
            if (execution.ResultVersion <= prior.ResultVersion || execution.CompletedAt < prior.CompletedAt
                || execution.SourceReference != prior.SourceReference || execution.CaptureEvidenceId != prior.CaptureEvidenceId
                || execution.StartedAt != prior.StartedAt || !execution.RequestedSubjects.SequenceEqual(prior.RequestedSubjects)) return false;
            _executions[execution.ExecutionId] = execution;
            _results.Add(execution);
            if (_attempts.Any(a => a.Execution.ExecutionId == execution.ExecutionId
                && knowledge.Gaps.Any(g => g.GapId == a.GapId && g.EvidenceIds.Contains(execution.CaptureEvidenceId)))) _relevantUpdates++;
            return true;
        }
        _executions.Add(execution.ExecutionId, execution);
        _results.Add(execution);
        foreach (var gap in knowledge.Gaps.Where(g => g.EvidenceIds.Contains(execution.CaptureEvidenceId)))
        {
            var relevant = gap.Kind switch
            {
                WorldKnowledgeGapKind.Identity or WorldKnowledgeGapKind.Attribution => execution.RequestedSubjects.Contains(WorldModel.CurrentContainerSubject),
                WorldKnowledgeGapKind.RelationBasis => execution.RequestedSubjects.Contains(ContainerContextCodec.Subject),
                WorldKnowledgeGapKind.ObservationCoverage => execution.RequestedSubjects.Contains("ui.coverage.viewport"),
                _ => false,
            };
            if (relevant) { _attempts.Add(new(gap.GapId, execution)); _relevantUpdates++; }
        }
        return true;
    }

    internal bool RequestReview(WorldKnowledgeView knowledge, string reason)
    {
        var signature = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Gaps = knowledge.Gaps.OrderBy(g => g.GapId, StringComparer.Ordinal)
                .Select(g => new { g.GapId, g.Reason, g.Conflicted }),
            Executions = _attempts.Where(a => knowledge.Gaps.Any(g => g.GapId == a.GapId && g.EvidenceIds.Contains(a.Execution.CaptureEvidenceId)))
                .Select(a => a.Execution.ExecutionId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Select(id => _executions[id]),
            Corrections = knowledge.Corrections.Where(c => knowledge.Gaps.Any(g => g.EvidenceIds.Contains(c.TargetCaptureEvidenceId)))
                .Select(c => c.CorrectionEvidenceId).Order(StringComparer.Ordinal),
        });
        var key = "world-review:" + Convert.ToHexString(SHA256.HashData(signature)).ToLowerInvariant();
        if (_reviewed.Contains(key) || _pendingReview == key) return false;
        _pendingReview = key; _pendingReason = reason;
        return true;
    }

    internal WorldRecoveryView Project(WorldKnowledgeView knowledge) => new(
        knowledge.Gaps.Select(g =>
        {
            var attempts = _attempts.Where(a => a.GapId == g.GapId && g.EvidenceIds.Contains(a.Execution.CaptureEvidenceId))
                .Select(a => _executions[a.Execution.ExecutionId]).ToImmutableArray();
            return new WorldGapRecoveryView(g.GapId, attempts.Length == 0 ? null : attempts.Length,
                attempts.Length == 0 ? "not_collected" : "recorded-executions-only", attempts)
            { ResultHistory = _results.Where(r => attempts.Any(a => a.ExecutionId == r.ExecutionId)).ToImmutableArray() };
        }).ToImmutableArray(), _pendingReview is not null, _pendingReview, _pendingReason);

    internal void Reviewed()
    {
        if (_pendingReview is { } key) _reviewed.Add(key);
        _pendingReview = null; _pendingReason = null;
    }
}
