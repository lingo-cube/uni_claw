using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Perception;

/// <summary>Deterministic replay response；typed proposals are the only payload.</summary>
internal sealed record SlowReplayResponse(
    SlowExecutionStatus Status,
    IReadOnlyList<ObservationProposal>? Proposals = null,
    SlowSemanticDisposition SemanticDisposition = SlowSemanticDisposition.Unknown,
    string? Diagnostic = null);

/// <summary>
/// Text/Visual 两个 logical profile 共用的 executable double。它只返回
/// SlowResult，不接触 Evidence Ledger、WorldModel 或 Effect Boundary。
/// </summary>
internal sealed class DeterministicSlowRealization
{
    private readonly SlowModelManagement _models;
    private readonly Dictionary<string, SlowReplayResponse> _responses = new(StringComparer.Ordinal);

    public DeterministicSlowRealization(SlowModelManagement? models = null)
        => _models = models ?? SlowReplayProfiles.CreateDefault();

    public int SemanticInvocationCount { get; private set; }

    public void SetResponse(string requestId, SlowReplayResponse response)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("request id must be non-empty", nameof(requestId));
        ArgumentNullException.ThrowIfNull(response);
        _responses[requestId] = response;
    }

    public SlowResult Execute(SlowPerceptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SemanticInvocationCount++;

        request = ResolveBinding(request);

        var invalid = ValidateRequest(request);
        if (invalid is not null)
            return Empty(request, invalid.Value.Status, invalid.Value.Diagnostic);

        var response = _responses.TryGetValue(request.RequestId, out var configured)
            ? configured
            : new SlowReplayResponse(SlowExecutionStatus.Succeeded);
        return Normalize(request, response);
    }

    /// <summary>
    /// Transport retries stay inside one semantic invocation. The caller may
    /// provide several transport responses; only the first terminal semantic
    /// response is normalized and the invocation counter increases once.
    /// </summary>
    public SlowResult ExecuteWithTransportRetries(
        SlowPerceptionRequest request, IReadOnlyList<SlowReplayResponse> transportResponses)
    {
        ArgumentNullException.ThrowIfNull(transportResponses);
        SemanticInvocationCount++;
        request = ResolveBinding(request);
        var invalid = ValidateRequest(request);
        if (invalid is not null)
            return Empty(request, invalid.Value.Status, invalid.Value.Diagnostic);

        if (transportResponses.Count == 0)
            return Normalize(request, new SlowReplayResponse(SlowExecutionStatus.InfrastructureFailure,
                Diagnostic: "transport produced no response"));

        // A response marked as transport-only is represented by InfrastructureFailure
        // with a retryable diagnostic. Skip those and preserve one semantic call.
        var selected = transportResponses.FirstOrDefault(r =>
            r.Status is not SlowExecutionStatus.InfrastructureFailure
            || !string.Equals(r.Diagnostic, "transport-retry", StringComparison.Ordinal));
        return Normalize(request, selected ?? transportResponses[^1]);
    }

    private SlowPerceptionRequest ResolveBinding(SlowPerceptionRequest request)
    {
        var resolution = _models.Resolve(request.LogicalProfile);
        if (!resolution.IsResolved)
            return request with { Binding = null };
        if (request.Binding is { IsValid: true }
            && request.Binding.LogicalProfile == request.LogicalProfile)
            return request;
        return request with { Binding = resolution.Binding };
    }

    private static (SlowExecutionStatus Status, string Diagnostic)? ValidateRequest(SlowPerceptionRequest request)
    {
        if (request.Context is null || request.Capture is null)
            return (SlowExecutionStatus.InvalidInput, "request context or capture is missing");
        if (request.Context.IsContextInsufficient)
            return (SlowExecutionStatus.ContextInsufficient, "bounded evidence context is insufficient");
        if (request.LogicalProfile == LogicalProfileId.Visual && request.RawArtifact is null)
            return (SlowExecutionStatus.InvalidInput, "visual profile requires RawArtifact");
        if (!request.IsValid)
            return (SlowExecutionStatus.InvalidInput, "request correlation, provenance, or required context is invalid");
        if (request.AttemptKey.LogicalProfile != request.LogicalProfile
            || request.AttemptKey.RequiredClaim != request.RequiredClaim)
            return (SlowExecutionStatus.InvalidInput, "attempt key does not correlate to request");
        if (request.Context.ConflictBasis is { } basis && !basis.Fits(request.Context.Budget))
            return (SlowExecutionStatus.ContextInsufficient, "conflict basis cannot fit context budget");
        if (!string.Equals(request.Capture.ObservationCycleId, request.AttemptKey.ObservationCycleId,
                StringComparison.Ordinal))
            return (SlowExecutionStatus.InvalidInput, "capture and attempt cycle do not correlate");
        if (request.Binding is null || !request.Binding.IsValid
            || !request.Binding.Available || !request.Binding.Health)
            return (SlowExecutionStatus.ModelUnavailable, "ROUTING_UNAVAILABLE");
        if (request.Binding.LogicalProfile != request.LogicalProfile)
            return (SlowExecutionStatus.ModelUnavailable, "binding identity does not match logical profile");
        return null;
    }

    private static SlowResult Normalize(SlowPerceptionRequest request, SlowReplayResponse response)
    {
        var status = response.Status;
        if (status is not (SlowExecutionStatus.Succeeded or SlowExecutionStatus.Partial))
            return Empty(request, status, response.Diagnostic);

        var proposals = new List<ObservationProposal>();
        foreach (var proposal in response.Proposals ?? Array.Empty<ObservationProposal>())
        {
            if (IsAdmissibleProposal(request, proposal))
                proposals.Add(proposal);
            else if (status == SlowExecutionStatus.Succeeded)
                return Empty(request, SlowExecutionStatus.SchemaFailure, "proposal provenance/schema invalid");
        }

        return new SlowResult(request.RequestId, request.AttemptKey, status, proposals,
            request.Capture, request.Binding, response.SemanticDisposition, response.Diagnostic);
    }

    private static bool IsAdmissibleProposal(SlowPerceptionRequest request, ObservationProposal proposal)
    {
        var claim = proposal?.Claim;
        var provenance = proposal?.Provenance;
        return proposal is not null
            && claim is { }
            && !string.IsNullOrWhiteSpace(claim.Subject)
            && !string.IsNullOrWhiteSpace(claim.Value)
            && proposal.Kind == IngressKind.Observation
            && provenance is { }
            && !string.IsNullOrWhiteSpace(provenance.Producer)
            && provenance.CaptureTime == request.Capture.CaptureTimestamp
            && !string.IsNullOrWhiteSpace(provenance.Scope)
            && provenance.Scope.Contains(request.Capture.CaptureId, StringComparison.Ordinal)
            && provenance.TransformationLineage is { Count: > 0 };
    }

    private static SlowResult Empty(SlowPerceptionRequest request, SlowExecutionStatus status, string? diagnostic)
        => new(request.RequestId, request.AttemptKey, status, Array.Empty<ObservationProposal>(),
            request.Capture, request.Binding, SlowSemanticDisposition.Unknown, diagnostic);

    public static ObservationProposal Proposal(
        SlowPerceptionRequest request, string subject, string value, string producer = "slow.replay")
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("proposal subject/value must be non-empty");
        return new ObservationProposal(
            new ObservationClaim(subject, value), IngressKind.Observation,
            ObservationContext.External,
            new Provenance(producer, request.Capture.CaptureTimestamp,
                $"slow:{request.Capture.CaptureId}",
                new[] { $"slow:{request.LogicalProfile.Value}", $"capture:{request.Capture.CaptureId}" }));
    }
}
