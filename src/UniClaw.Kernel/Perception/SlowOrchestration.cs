using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Perception;

internal enum SlowWaitDisposition
{
    Completed,
    TimedOut,
    Cancelled,
    DuplicateAttempt,
}

/// <summary>Perception 侧只选择 logical profile，不接触 concrete provider identity。</summary>
internal static class SlowProfileSelector
{
    public static LogicalProfileId Select(bool requiresRawArtifact) =>
        requiresRawArtifact ? LogicalProfileId.Visual : LogicalProfileId.Text;
}

internal sealed record SlowOrchestrationResult(
    SlowResult Result,
    SlowWaitDisposition WaitDisposition = SlowWaitDisposition.Completed,
    bool IsLate = false)
{
    public bool EffectAuthorizationAllowed => !IsLate
        && WaitDisposition == SlowWaitDisposition.Completed
        && Result.Status is SlowExecutionStatus.Succeeded or SlowExecutionStatus.Partial;
}

/// <summary>
/// Control-owned orchestration seam. It reserves the keyed attempt before the
/// realization starts and makes the reservation terminal exactly once.
/// </summary>
internal sealed class SlowPerceptionOrchestrator
{
    private readonly EphemeralAttemptLedger _attempts;
    private readonly DeterministicSlowRealization _realization;

    public SlowPerceptionOrchestrator(
        EphemeralAttemptLedger? attempts = null,
        DeterministicSlowRealization? realization = null)
    {
        _attempts = attempts ?? new EphemeralAttemptLedger();
        _realization = realization ?? new DeterministicSlowRealization();
    }

    public EphemeralAttemptLedger Attempts => _attempts;
    public DeterministicSlowRealization Realization => _realization;

    public SlowOrchestrationResult Execute(
        SlowPerceptionRequest request, bool effectCritical = false,
        TimeSpan? boundedWait = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_attempts.TryStart(request.AttemptKey))
        {
            var duplicate = new SlowResult(request.RequestId, request.AttemptKey,
                SlowExecutionStatus.InvalidInput, Array.Empty<ObservationProposal>(), request.Capture,
                request.Binding, SlowSemanticDisposition.Unknown, "duplicate or terminal AttemptKey");
            return new(duplicate, SlowWaitDisposition.DuplicateAttempt);
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var cancelled = new SlowResult(request.RequestId, request.AttemptKey,
                    SlowExecutionStatus.Cancelled, Array.Empty<ObservationProposal>(), request.Capture,
                    request.Binding, SlowSemanticDisposition.Unknown, "cancelled before realization");
                return new(cancelled, SlowWaitDisposition.Cancelled);
            }

            if (effectCritical && boundedWait is { } waitBudget && waitBudget <= TimeSpan.Zero)
            {
                var timeout = new SlowResult(request.RequestId, request.AttemptKey,
                    SlowExecutionStatus.Timeout, Array.Empty<ObservationProposal>(), request.Capture,
                    request.Binding, SlowSemanticDisposition.Unknown, "effect-critical bounded wait exhausted");
                return new(timeout, SlowWaitDisposition.TimedOut);
            }

            // The replay double is deterministic and synchronous. The bounded
            // wait still remains an explicit Control decision and never belongs
            // to Effect Gate.
            var result = _realization.Execute(request);
            var wait = result.Status == SlowExecutionStatus.Timeout
                ? SlowWaitDisposition.TimedOut
                : SlowWaitDisposition.Completed;
            return new(result, wait);
        }
        finally
        {
            _attempts.MarkTerminal(request.AttemptKey);
        }
    }

    public Task<SlowOrchestrationResult> ExecuteAsync(
        SlowPerceptionRequest request, bool effectCritical = false,
        TimeSpan? boundedWait = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Execute(request, effectCritical, boundedWait, cancellationToken));

    public SlowOrchestrationResult ExecuteLate(SlowPerceptionRequest request)
    {
        var result = Execute(request, effectCritical: false);
        return result with { IsLate = true };
    }

    /// <summary>接收已在外部完成的迟到结果，不重新消耗 semantic attempt。</summary>
    public SlowOrchestrationResult AcceptLate(SlowResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(result, SlowWaitDisposition.Completed, IsLate: true);
    }
}

internal sealed record SlowP2Admission(
    bool Accepted,
    IReadOnlyList<KernelResult> Results,
    string? RejectionReason = null)
{
    public int ProposalCount => Results.Count;
}

/// <summary>
/// 唯一正式 ingress：SlowResult → proposal validation → existing P2.
/// This type has no WorldModel or EvidenceLedger mutation path of its own.
/// </summary>
internal static class SlowResultProjector
{
    public static SlowP2Admission Project(SlowResult result, UniKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(kernel);

        if (string.IsNullOrWhiteSpace(result.RequestId)
            || result.AttemptKey is not { IsValid: true }
            || result.Capture is not { IsValid: true }
            || !string.Equals(result.Capture.ObservationCycleId,
                result.AttemptKey.ObservationCycleId, StringComparison.Ordinal))
            return new(false, Array.Empty<KernelResult>(), "correlation/provenance invalid");

        if (result.Status is not (SlowExecutionStatus.Succeeded or SlowExecutionStatus.Partial))
            return new(false, Array.Empty<KernelResult>(), $"status {result.Status} yields zero proposals");

        if (result.Binding is not { IsValid: true, Available: true, Health: true })
            return new(false, Array.Empty<KernelResult>(), "binding snapshot unavailable or invalid");
        if (result.Binding.LogicalProfile != result.AttemptKey.LogicalProfile)
            return new(false, Array.Empty<KernelResult>(), "binding identity does not match AttemptKey");

        var valid = new List<ObservationProposal>();
        foreach (var proposal in result.Proposals)
        {
            if (proposal is null || proposal.Claim is null || proposal.Provenance is null
                || proposal.Kind != IngressKind.Observation
                || string.IsNullOrWhiteSpace(proposal.Claim.Subject)
                || string.IsNullOrWhiteSpace(proposal.Claim.Value)
                || string.IsNullOrWhiteSpace(proposal.Provenance.Producer)
                || proposal.Provenance.CaptureTime != result.Capture.CaptureTimestamp
                || string.IsNullOrWhiteSpace(proposal.Provenance.Scope)
                || !proposal.Provenance.Scope.Contains(result.Capture.CaptureId, StringComparison.Ordinal)
                || proposal.Provenance.TransformationLineage is not { Count: > 0 })
            {
                if (result.Status == SlowExecutionStatus.Succeeded)
                    return new(false, Array.Empty<KernelResult>(), "malformed proposal");
                continue;
            }
            valid.Add(proposal);
        }

        // Empty success is deliberately a no-op: it does not mint absence.
        if (valid.Count == 0)
            return new(true, Array.Empty<KernelResult>());

        var results = valid.Select(proposal => kernel.Process(proposal)).ToArray();
        var admitted = results.Count(r => r.Admission.Decision == AdmissionDecision.Accepted);
        return new(admitted > 0, results,
            admitted == 0 ? "all proposals rejected by P2" : null);
    }

    public static SlowP2Admission Admit(SlowResult result, UniKernel kernel) => Project(result, kernel);
}
