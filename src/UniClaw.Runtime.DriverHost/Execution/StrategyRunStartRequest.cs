using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Additive run.strategy.start request. The strategy is authored by UniAgent;
/// DriverHost adds only the explicit device selection needed for Run admission.
/// </summary>
public sealed record StrategyRunStartRequest
{
    /// <summary>Create one start-time strategy request.</summary>
    public StrategyRunStartRequest(StrategyDirective strategy, DeviceSelector device)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(device);
        Strategy = strategy;
        Device = device;
    }

    /// <summary>Immutable UniAgent-authored strategy.</summary>
    public StrategyDirective Strategy { get; }

    /// <summary>Explicit composition-root device selector.</summary>
    public DeviceSelector Device { get; }
}

/// <summary>
/// Admission receipt for run.strategy.start. It reports Agent state only after
/// acceptance; it contains no lifecycle command and cannot transition RunState.
/// </summary>
public sealed record StrategyRunAdmission
{
    private StrategyRunAdmission(
        bool accepted,
        string? runId,
        RunState? runState,
        StrategyRejectionCode? rejectionCode,
        string? rejectionReason)
    {
        Accepted = accepted;
        RunId = runId;
        RunState = runState;
        RejectionCode = rejectionCode;
        RejectionReason = rejectionReason;
    }

    /// <summary>Whether exactly one Run was admitted.</summary>
    public bool Accepted { get; }

    /// <summary>DriverHost-owned Run identity; absent on rejection.</summary>
    public string? RunId { get; }

    /// <summary>Truthful Agent state at acceptance; absent on rejection.</summary>
    public RunState? RunState { get; }

    /// <summary>Stable fail-closed rejection code; absent on acceptance.</summary>
    public StrategyRejectionCode? RejectionCode { get; }

    /// <summary>Bounded rejection reason; absent on acceptance.</summary>
    public string? RejectionReason { get; }

    /// <summary>Create a truthful accepted receipt.</summary>
    public static StrategyRunAdmission Accept(string runId, RunState runState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return new StrategyRunAdmission(true, runId, runState, null, null);
    }

    /// <summary>Create a fail-closed rejection with no Run identity.</summary>
    public static StrategyRunAdmission Reject(StrategyRejectionCode code, string reason)
    {
        if (!Enum.IsDefined(code))
            throw new ArgumentOutOfRangeException(nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new StrategyRunAdmission(false, null, null, code, reason);
    }
}
