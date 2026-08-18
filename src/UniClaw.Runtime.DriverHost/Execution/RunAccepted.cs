using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Result of an ACCEPTED run.start: the DriverHost-owned authoritative runId and
/// the Agent's truthful state at acceptance (Idle for a freshly constructed
/// Agent). The call returns immediately after acceptance/scheduling; the run
/// executes asynchronously and is observed through the existing read-only
/// surfaces (run.events.after / run.snapshot.get / run.trap.get / run.events.drain
/// / evidence.get) keyed by this runId.
/// </summary>
public sealed record RunAccepted(string RunId, RunState RunState);
