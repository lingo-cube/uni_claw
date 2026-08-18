namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// The authorized execution seam — deliberately SEPARATE from the frozen
/// read-only <see cref="IUniClawControlSurface"/>, which stays untouched.
///
/// The only operation is starting a run. Run identity is DriverHost-owned;
/// acceptance is asynchronous (<see cref="RunAccepted"/> is returned before
/// execution completes; never blocks until Completed/Failed). Deterministic
/// rejection (invalid request / unknown device / device busy) throws
/// <see cref="RequestRejectedException"/> — no run is created.
/// </summary>
public interface IUniClawRunExecution
{
    /// <summary>Validate, reserve the device, create the authoritative runId,
    /// register the truthful accepted run, schedule Agent execution, and return
    /// immediately.</summary>
    RunAccepted StartRun(RunStartRequest request);
}
