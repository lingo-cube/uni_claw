using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;

namespace UniClaw.TraceTool;

/// <summary>
/// A single verification rule evaluated by <see cref="VerifyEngine"/>.
/// MVP: <see cref="LocateOneItemRule"/> (D-201 semantics ported from Host).
/// </summary>
public interface IVerificationRule
{
    /// <summary>Evaluate the rule. Returns a verdict or null when not applicable.</summary>
    VerifyVerdict? Evaluate(VerificationContext context);
}

/// <summary>
/// Context passed to each verification rule. Contains the data loaded from the run.
/// </summary>
public sealed class VerificationContext
{
    public string RunId { get; init; } = "";
    public VerificationCriteria? Criteria { get; init; }
    public AnalysisRow? LastAnalysisRow { get; init; }
    public string? CompletionReason { get; init; }
    public bool TargetActionExecuted { get; init; }
    public IReadOnlyList<string> ExpectedPageIdentities { get; init; } = [];
    public ITraceEventQuery? Trace { get; init; }  // for reading safety.* events
    public IReadOnlyList<RunIssue> Issues { get; init; } = [];
}
