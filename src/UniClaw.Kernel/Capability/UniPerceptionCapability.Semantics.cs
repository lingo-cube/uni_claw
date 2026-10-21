using UniClaw.Kernel.Perception;
using System.Text.Json;

namespace UniClaw.Kernel.Capability;

public sealed partial class UniPerceptionCapability
{
    private SemanticPerceptionExecutor? _semanticExecutor;
    private readonly EphemeralAttemptLedger _semanticAttempts = new();
    private const int MaximumRequestBytes = 128 * 1024;
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    /// <summary>Explicit immutable composition; replacement does not change consumers or capability identity.</summary>
    public UniPerceptionCapability WithSemanticExecutor(SemanticPerceptionExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        return new UniPerceptionCapability(_sources, _description) { _semanticExecutor = executor };
    }

    /// <summary>Validate before invoking the supplier, and normalize every returned claim before projection.</summary>
    public async ValueTask<SemanticPerceptionResult> ObserveSemanticsAsync(
        SemanticPerceptionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var error = ValidateSemanticRequest(request);
        if (error is not null)
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Unknown,
                PerceptionDisposition.Unaligned, error);
        if (_semanticExecutor is null)
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Unavailable,
                PerceptionDisposition.Unsupported, "semantic execution is not configured");
        if (cancellationToken.IsCancellationRequested)
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Cancelled,
                PerceptionDisposition.Unknown, "cancelled before execution");
        var key = new SlowAttemptKey($"{request.Current.SessionCorrelation}:{request.Current.CaptureId}",
            request.Current.ObservationCycleId, new RequiredClaim("ui.semantic.page", "meaning"), LogicalProfileId.Text);
        if (!_semanticAttempts.TryStart(key, out var reservation))
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Duplicate,
                PerceptionDisposition.Unknown, "semantic attempt already reserved or terminal");
        using (reservation)
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(request.Budget ?? TimeSpan.FromSeconds(30));
            try
            {
                var result = await _semanticExecutor(request, budget.Token).AsTask()
                    .WaitAsync(budget.Token).ConfigureAwait(false);
                return NormalizeSemanticResult(request, result);
            }
            catch (OperationCanceledException)
            {
                return SemanticPerceptionResult.Empty(request,
                    cancellationToken.IsCancellationRequested ? CapabilityStatus.Cancelled : CapabilityStatus.TimedOut,
                    PerceptionDisposition.Unknown, "semantic execution cancelled or budget exhausted");
            }
            catch (Exception supplierError)
            {
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Failed,
                    PerceptionDisposition.Unavailable, $"semantic supplier failed: {supplierError.GetType().Name}");
            }
        }
    }

    internal static string? ValidateSemanticRequest(SemanticPerceptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.Correlation is null
            || !ValidContext(request.Current, requireFresh: true)) return "current observation missing, stale or invalid";
        var current = request.Current;
        if (current.PageName is not null || current.PagePurpose is not null || current.PageKey is not null)
            return "current page labels are not observations; supply layout and keep intent separate";
        if (request.Correlation.RunId != current.SessionCorrelation
            || request.Correlation.CaptureId != current.CaptureId
            || request.Correlation.ObservationCycleId != current.ObservationCycleId)
            return "request/capture/cycle/session mismatch";
        if (request.Candidates.Count is < 1 or > 64 || request.Candidates.Any(c =>
                c is null || string.IsNullOrWhiteSpace(c.Key) || c.Key == "unknown"
                || string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Purpose))
            || request.Candidates.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != request.Candidates.Count)
            return "invalid or duplicate semantic catalogue";
        if (request.Budget is { } bound && (bound <= TimeSpan.Zero || bound > TimeSpan.FromMinutes(2)))
            return "semantic budget must be positive and at most two minutes";
        if (request.Previous is { } previous && (!ValidContext(previous, requireFresh: false)
            || previous.SessionCorrelation != current.SessionCorrelation
            || previous.CaptureId == current.CaptureId || previous.CaptureTime > current.CaptureTime))
            return "previous observation is unaligned";
        if (request.Action is { } action && (request.Previous is null
            || action.SourceCaptureId != request.Previous.CaptureId
            || string.IsNullOrWhiteSpace(action.Kind) || string.IsNullOrWhiteSpace(action.ElementType)
            || string.IsNullOrWhiteSpace(action.ElementText) || string.IsNullOrWhiteSpace(action.DescriptionSource)
            || (action.X is null) != (action.Y is null)
            || action.X is { } x && (x < 0 || x >= request.Previous.Space.PixelWidth)
            || action.Y is { } y && (y < 0 || y >= request.Previous.Space.PixelHeight)))
            return "operation has no aligned source observation or explicit element source";
        if (request.Intent is { } intent && (!request.Candidates.Any(c => c.Key == intent.ExpectedPageKey)
            || string.IsNullOrWhiteSpace(intent.Description))) return "intent has no declared candidate";
        if (current.EvidenceIds.Concat(request.Previous?.EvidenceIds ?? []).Distinct().Count() > 32)
            return "bounded evidence budget exceeded";
        if (JsonSerializer.SerializeToUtf8Bytes(request, PayloadJson).Length > MaximumRequestBytes)
            return "semantic request exceeds the 128 KiB UTF-8 JSON payload budget";
        return null;
    }

    private static bool ValidContext(SemanticObservationContext? context, bool requireFresh)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.CaptureId)
            || string.IsNullOrWhiteSpace(context.ObservationCycleId) || string.IsNullOrWhiteSpace(context.SessionCorrelation)
            || context.CaptureTime == default || context.Space is null || context.Space.CaptureId != context.CaptureId
            || requireFresh && !context.IsFresh || context.Items.Count is < 1 or > 512
            || context.EvidenceIds.Count is < 1 or > 32 || context.EvidenceIds.Any(string.IsNullOrWhiteSpace)
            || context.EvidenceIds.Distinct().Count() != context.EvidenceIds.Count
            || context.SourceLeafEvidenceIds.Count == 0 || context.SourceLeafEvidenceIds.Any(string.IsNullOrWhiteSpace)) return false;
        return context.Items.All(i => i is not null && !string.IsNullOrWhiteSpace(i.Id) && !string.IsNullOrWhiteSpace(i.Kind)
            && i.Bounds is { IsValid: true } && i.Bounds.X1 >= 0 && i.Bounds.Y1 >= 0
            && i.Bounds.X2 <= context.Space.PixelWidth && i.Bounds.Y2 <= context.Space.PixelHeight
            && (i.Confidence is null || double.IsFinite(i.Confidence.Value) && i.Confidence is >= 0 and <= 1))
            && context.Items.Select(i => i.Id).Distinct().Count() == context.Items.Count;
    }

    internal static SemanticPerceptionResult NormalizeSemanticResult(SemanticPerceptionRequest request, SemanticPerceptionResult result)
    {
        if (result is null || result.RequestId != request.RequestId || result.CaptureId != request.Current.CaptureId
            || result.ObservationCycleId != request.Current.ObservationCycleId || result.Assessment is null
            || !Enum.IsDefined(result.Status) || !Enum.IsDefined(result.Relation) || !Enum.IsDefined(result.ExpectedMatch)
            || !Enum.IsDefined(result.Assessment.Disposition)
            || result.Assessment.ProviderConfidence is { } overallConfidence
                && (!double.IsFinite(overallConfidence) || overallConfidence is < 0 or > 1))
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Failed, PerceptionDisposition.Unaligned, "supplier result is unaligned");
        if (result.Status != CapabilityStatus.Completed)
            return result with { Observation = null, Relation = SemanticPageRelation.Unknown, ExpectedMatch = SemanticExpectedMatch.Unknown };
        var observation = result.Observation;
        if (observation is not null)
        {
            var candidate = request.Candidates.SingleOrDefault(c => c.Key == observation.PageKey);
            var basis = request.Current.EvidenceIds.Concat(request.Previous?.EvidenceIds ?? []).Distinct().ToHashSet();
            if (candidate is null || observation.PageName != candidate.Name || observation.PagePurpose != candidate.Purpose
                || observation.Assessment is null || observation.Assessment.Disposition != PerceptionDisposition.Supported
                || observation.BasisEvidenceIds.Count != basis.Count || !basis.SetEquals(observation.BasisEvidenceIds)
                || observation.ObservedTitle is not null && !request.Current.Items.Any(i => i.Text == observation.ObservedTitle)
                || observation.Assessment.ProviderConfidence is { } confidence && (!double.IsFinite(confidence) || confidence is < 0 or > 1))
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Failed, PerceptionDisposition.Unknown, "invalid semantic observation/schema");
        }
        var match = request.Intent is null || observation is null ? SemanticExpectedMatch.Unknown : result.ExpectedMatch;
        var relation = request.Previous is null ? SemanticPageRelation.Unknown : result.Relation;
        if (request.Intent is { } intent && observation is not null
            && (match == SemanticExpectedMatch.Match && observation.PageKey != intent.ExpectedPageKey
                || match == SemanticExpectedMatch.Mismatch && observation.PageKey == intent.ExpectedPageKey))
            return result with { ExpectedMatch = SemanticExpectedMatch.Conflicted, Relation = relation,
                Assessment = result.Assessment with { Disposition = PerceptionDisposition.Partial,
                    Diagnostic = "observed page and intent comparison conflict; observation retained" } };
        return result with { ExpectedMatch = match, Relation = relation };
    }
}
