using System.Collections.Immutable;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Immutable, transport-neutral snapshot of the Agent PUBLIC read model only
/// (design.md §4). Constructed strictly from the public surface
/// (Agent.State/Belief/Trace/Reason/RecoveryAnchor/LastTrap/BranchProgress/NavigationEvidence) —
/// the DriverHost NEVER reaches into Container/Traversal/Environment internals.
/// The snapshot is a copy: projecting it can never mutate the live Agent.
/// </summary>
public sealed record AgentStateSnapshot
{
    /// <summary>Run identity taken from the latest trace entry; "" = no trace recorded.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Run state (direct public projection).</summary>
    public RunState State { get; init; }

    /// <summary>World belief (direct public projection); null = none recorded.</summary>
    public WorldBelief? Belief { get; init; }

    /// <summary>Agent reason (direct public projection); null = none.</summary>
    public string? Reason { get; init; }

    /// <summary>Latest trap (direct public projection); null = no trap.</summary>
    public Trap? LastTrap { get; init; }

    /// <summary>Recovery anchor (direct public projection); null = no recovery anchor.</summary>
    public RecoveryAnchor? RecoveryAnchor { get; init; }

    /// <summary>Append-only trace event list copy.</summary>
    public ImmutableArray<DecisionRecord> Trace { get; init; } = [];

    /// <summary>Accepted cross-container navigation observation evidence copy.</summary>
    public ImmutableArray<Observation> NavigationEvidence { get; init; } = [];

    /// <summary>Branch progress evidence copy.</summary>
    public ImmutableDictionary<string, BranchProgressEvidence> BranchProgress { get; init; } =
        ImmutableDictionary<string, BranchProgressEvidence>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>Snapshot the public read model of a live Agent (read-only; never mutates).</summary>
    public static AgentStateSnapshot From(Agent.Agent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return new AgentStateSnapshot
        {
            RunId = agent.Trace.Count > 0 ? agent.Trace[^1].RunId : "",
            State = agent.State,
            Belief = agent.Belief,
            Reason = agent.Reason,
            LastTrap = agent.LastTrap,
            RecoveryAnchor = agent.RecoveryAnchor,
            Trace = [.. agent.Trace],
            NavigationEvidence = [.. agent.NavigationEvidence],
            BranchProgress = agent.BranchProgress.ToImmutableDictionary(StringComparer.Ordinal),
        };
    }
}
