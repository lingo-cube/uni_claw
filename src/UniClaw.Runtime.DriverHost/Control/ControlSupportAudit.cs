using System.Collections.Immutable;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Frozen audit of control operations against the UniClaw Kernel (protocol
/// baseline design.md §13/§14 — deterministic human control seam).
///
/// For EVERY candidate control operation this table records a truthful buyer:
/// either a read-only operation on the DriverHost observability surface
/// (implemented) or an explicit DEFERRED_NO_KERNEL_CONTROL_BUYER with the source
/// evidence for why no Kernel control API exists. The table is DATA, not
/// authority: it never mutates Kernel state and never invents a control.
/// </summary>
public static class ControlSupportAudit
{
    /// <summary>Every candidate Kernel control operation audited by the baseline.</summary>
    public static readonly ImmutableArray<string> CandidateOperations =
        ["start", "pause", "resume", "stop", "abort"];

    /// <summary>Read-only operations the DriverHost surface truthfully supports.</summary>
    public static readonly ImmutableArray<string> ReadOnlyOperations =
        ["ping", "run.list", "inspect.run", "inspect.trap", "evidence.open", "run.events", "control.support"];

    /// <summary>Reason constant for audited-but-unsupported operations.</summary>
    public const string DeferredNoKernelControlBuyer = "DEFERRED_NO_KERNEL_CONTROL_BUYER";

    /// <summary>Reason constant for the authorized run-start entry (dsh-runtime-agent-subagent-run-entry).</summary>
    public const string AuthorizedRunStartEntry = "AUTHORIZED_RUN_START_ENTRY";

    /// <summary>Reason constant for supported read-only operations.</summary>
    public const string ReadOnlyInspect = "READ_ONLY_INSPECT";

    /// <summary>Reason constant for operations absent from the audit table.</summary>
    public const string UnknownOperation = "UNKNOWN_OPERATION";

    /// <summary>
    /// Source evidence for the authorized run-start entry. The previous frozen
    /// table deferred "start" (no public Start control); this change adds the
    /// authorized execution entry run.start → IUniClawRunExecution. The Kernel
    /// keeps execution/state/GoalEvidence authority; run.start only requests a
    /// semantic task start (no physical/GoalEvidence authority for DSH).
    /// </summary>
    private static readonly ImmutableDictionary<string, ImmutableArray<string>> AuthorizedExecutionEntries =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["start"] = [
                "Authorized run-start entry (dsh-runtime-agent-subagent-run-entry): run.start wire method → IUniClawRunExecution → RunExecutionCoordinator → existing Agent.RunSemanticGoalAsync.",
                "run identity is DriverHost-owned; acceptance is asynchronous; the Kernel retains execution/state/GoalEvidence authority; DSH gains no physical or goal-evidence authority.",
            ],
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Source evidence per deferred operation. Every string cites the audited
    /// Kernel public surface — the Agent exposes no lifecycle control API.
    /// </summary>
    private static readonly ImmutableDictionary<string, ImmutableArray<string>> DeferredEvidence =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["pause"] = [
                "No public Pause control on UniClaw.Runtime.Agent (pinned baseline 2026-08-15).",
                "Pausing Kernel execution is a Kernel-owned authority; no truthful buyer exists on the public surface.",
            ],
            ["resume"] = [
                "No public Resume control on UniClaw.Runtime.Agent (pinned baseline 2026-08-15).",
                "Resuming Kernel execution is a Kernel-owned authority; no truthful buyer exists on the public surface.",
            ],
            ["stop"] = [
                "No public Stop control on UniClaw.Runtime.Agent (pinned baseline 2026-08-15).",
                "Run completion is Kernel-owned (GoalEvidence / RunCompleted / RunFailed); DSH must not mutate run completion.",
            ],
            ["abort"] = [
                "No public Abort control on UniClaw.Runtime.Agent (pinned baseline 2026-08-15).",
                "Aborting is a Kernel execution authority; no truthful buyer exists on the public surface.",
            ],
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Resolve one operation against the frozen audit.
    /// Never throws; unknown operations resolve to UNKNOWN_OPERATION.
    /// </summary>
    public static ControlSupportResult Audit(string operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (AuthorizedExecutionEntries.TryGetValue(operation, out var entryEvidence))
        {
            return new ControlSupportResult(
                Operation: operation,
                Supported: true,
                Reason: AuthorizedRunStartEntry,
                Evidence: entryEvidence,
                ReadOnly: false);
        }

        if (DeferredEvidence.TryGetValue(operation, out var evidence))
        {
            return new ControlSupportResult(
                Operation: operation,
                Supported: false,
                Reason: DeferredNoKernelControlBuyer,
                Evidence: evidence,
                ReadOnly: false);
        }

        if (ReadOnlyOperations.Contains(operation, StringComparer.Ordinal))
        {
            return new ControlSupportResult(
                Operation: operation,
                Supported: true,
                Reason: ReadOnlyInspect,
                Evidence: ["Read-only inspection on the DriverHost observability surface."],
                ReadOnly: true);
        }

        return new ControlSupportResult(
            Operation: operation,
            Supported: false,
            Reason: UnknownOperation,
            Evidence: [$"Operation '{operation}' is absent from the frozen control audit table."],
            ReadOnly: false);
    }
}

/// <summary>Structured result of one control-audit lookup.</summary>
public sealed record ControlSupportResult(
    string Operation,
    bool Supported,
    string Reason,
    ImmutableArray<string> Evidence,
    bool ReadOnly);
