namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Additive Strategy Contract admission seam. It is separate from the frozen
/// run.start interface and can create at most one Agent-owned Run per strategy id.
/// </summary>
public interface IUniClawStrategyExecution
{
    /// <summary>Validate, interpret, and admit one start-time bounded strategy.</summary>
    StrategyRunAdmission StartStrategyRun(StrategyRunStartRequest request);
}
