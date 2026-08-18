using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Minimum truthful request to start an EXISTING Runtime.Agent semantic run
/// (dsh-runtime-agent-subagent-run-entry).
///
/// Maps 1:1 into the existing production semantic entry
/// <c>Agent.RunSemanticGoalAsync(goal, objects, capabilities, runId, …)</c>.
/// Carries task-level declarations ONLY — never coordinates, DeviceAction,
/// element indexes, TraversalStep, a Plan, a prompt, or any precompiled
/// physical step. No TaskSpec, no AgentProfile, no consult settings.
/// </summary>
public sealed record RunStartRequest(
    SemanticGoalInput Goal,
    ImmutableArray<SemanticObject> Objects,
    ImmutableArray<Capability> Capabilities,
    DeviceSelector Device);
