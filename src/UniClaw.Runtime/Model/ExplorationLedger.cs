using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Closed per-node exploration rule derived at admission from the accepted
/// strategy's exploration intent (Runtime Exploration Roadmap Phase 2).
/// RuntimeAgent applies rules; it never authors or invents them.
/// </summary>
public enum ExplorationRule
{
    /// <summary>Expand a classified semantic container (verified subtree return required).</summary>
    ExpandContainer = 1,

    /// <summary>Record the node from fresh observation only; no dispatch required.</summary>
    RecordOnly = 2,
}

/// <summary>Closed typed mapping from admitted element categories to exploration rules.</summary>
internal static class ExplorationRuleResolver
{
    internal static ExplorationRule? Resolve(
        ExplorationExecutionSemantics semantics,
        TypeLevelElementCategory category)
        => category switch
        {
            TypeLevelElementCategory.NavigableContainer => semantics.ContainerRule,
            TypeLevelElementCategory.StateChangingControl => semantics.LeafRule,
            _ => null,
        };
}

/// <summary>
/// Bounded semantic depth mode derived at admission from the declared maximum
/// depth and exploration intent. Exhaustive semantics preserve the existing
/// fail-closed depth cutoff; bounded-record semantics process boundary nodes
/// record-only with unknown-frontier ledger entries.
/// </summary>
public enum ExplorationDepthSemantics
{
    /// <summary>Depth 0: the root scope is processed record-only.</summary>
    RootRecordOnly = 1,

    /// <summary>Depth 1: the root is expanded and direct children are processed record-only.</summary>
    RootAndDirectChildren = 2,

    /// <summary>Depth N ≥ 2: bounded recursive expansion; nodes at the boundary are processed
    /// record-only for bounded-record strategies, or fail closed for exhaustive strategies.</summary>
    BoundedRecursive = 3,
}

/// <summary>
/// Admission-derived disposition at the declared exploration boundary. This is
/// an internal interpretation value; it is not part of the Strategy wire shape.
/// </summary>
internal enum ExplorationBoundaryDisposition
{
    RecordOnly = 1,
    FailClosed = 2,
}

/// <summary>
/// Immutable interpretation of one accepted Strategy tuple. The value carries
/// only closed exploration rules and provenance; it owns no execution or
/// lifecycle authority.
/// </summary>
internal sealed record ExplorationExecutionSemantics
{
    internal ExplorationExecutionSemantics(
        string strategyId,
        string runtimeExecutionIntentReference,
        ExplorationRule containerRule,
        ExplorationRule leafRule,
        ExplorationDepthSemantics depthSemantics,
        ExplorationBoundaryDisposition boundaryDisposition,
        int declaredMaximumDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeExecutionIntentReference);
        if (!Enum.IsDefined(containerRule)) throw new ArgumentOutOfRangeException(nameof(containerRule));
        if (!Enum.IsDefined(leafRule)) throw new ArgumentOutOfRangeException(nameof(leafRule));
        if (!Enum.IsDefined(depthSemantics)) throw new ArgumentOutOfRangeException(nameof(depthSemantics));
        if (!Enum.IsDefined(boundaryDisposition)) throw new ArgumentOutOfRangeException(nameof(boundaryDisposition));
        if (declaredMaximumDepth < 0) throw new ArgumentOutOfRangeException(nameof(declaredMaximumDepth));

        StrategyId = strategyId;
        RuntimeExecutionIntentReference = runtimeExecutionIntentReference;
        ContainerRule = containerRule;
        LeafRule = leafRule;
        DepthSemantics = depthSemantics;
        BoundaryDisposition = boundaryDisposition;
        DeclaredMaximumDepth = declaredMaximumDepth;
    }

    public string StrategyId { get; }
    public string RuntimeExecutionIntentReference { get; }
    public ExplorationRule ContainerRule { get; }
    public ExplorationRule LeafRule { get; }
    public ExplorationDepthSemantics DepthSemantics { get; }
    public ExplorationBoundaryDisposition BoundaryDisposition { get; }
    public int DeclaredMaximumDepth { get; }
}

/// <summary>Immutable accepted Strategy provenance bound to one Agent-owned Run.</summary>
internal sealed record AcceptedExplorationRunContext
{
    internal AcceptedExplorationRunContext(string runId, ExplorationExecutionSemantics semantics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(semantics);
        RunId = runId;
        Semantics = semantics;
    }

    public string RunId { get; }
    public ExplorationExecutionSemantics Semantics { get; }
}

/// <summary>
/// Read-only, evidence-derived exploration accounting for one scope
/// (Roadmap Phase 2 — Exploration Ledger). Counts derive exclusively from
/// existing evidence records; this type owns no state and mutates nothing.
/// Visited means rule-satisfied with evidence — never clicked/dispatched.
/// </summary>
public sealed record ExplorationScopeLedger
{
    /// <summary>Create one validated immutable per-scope ledger snapshot.</summary>
    public ExplorationScopeLedger(
        string scopeIdentity,
        int discovered,
        int visited,
        int pending,
        int unresolved,
        int unknownFrontier,
        long sourceObservationSequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeIdentity);
        if (discovered < 0 || visited < 0 || pending < 0 || unresolved < 0 || unknownFrontier < 0)
            throw new ArgumentException("Ledger counts must be non-negative.");
        if (sourceObservationSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceObservationSequence));
        // Frozen-spec invariants (R3 "Visited means rule-satisfied" + R4
        // "Bounded semantic depth control"): unknown frontier is an annotation
        // OVERLAPPING visited (a boundary record-only node is both
        // rule-satisfied/visited per R3 and an unknown-frontier entry per R4),
        // so it does not participate in the disposition sum. Roadmap success
        // categories (Discovered/Visited/Skipped/SafetyBlocked) are already
        // overlapping annotations, not exclusive dispositions.
        if (visited + pending + unresolved > discovered)
            throw new ArgumentException(
                "Ledger dispositions (visited + pending + unresolved) must not exceed discovered.");
        if (unknownFrontier > discovered)
            throw new ArgumentException(
                "Unknown frontier (overlapping visited annotation) must not exceed discovered.");
        ScopeIdentity = scopeIdentity;
        Discovered = discovered;
        Visited = visited;
        Pending = pending;
        Unresolved = unresolved;
        UnknownFrontier = unknownFrontier;
        SourceObservationSequence = sourceObservationSequence;
    }

    /// <summary>Semantic identity of the exploration scope (per parent page).</summary>
    public string ScopeIdentity { get; }

    /// <summary>Nodes discovered in the approved inventory for this scope.</summary>
    public int Discovered { get; }

    /// <summary>Nodes whose applied exploration rule is satisfied with evidence (never click-equivalent).</summary>
    public int Visited { get; }

    /// <summary>Discovered, classified nodes whose rule satisfaction is still awaited.</summary>
    public int Pending { get; }

    /// <summary>Discovered nodes whose classification was unavailable (fail closed, never guessed).</summary>
    public int Unresolved { get; }

    /// <summary>
    /// Discovered containers beyond the declared depth boundary (bounded-record
    /// semantics). Overlapping annotation on <see cref="Visited"/>: a boundary
    /// record-only node counts as visited (rule satisfied by fresh-observation
    /// record) AND as unknown frontier — the two are not exclusive dispositions.
    /// </summary>
    public int UnknownFrontier { get; }

    /// <summary>Latest observation sequence the accounting was compiled against.</summary>
    public long SourceObservationSequence { get; }

    internal string IdentityDigestMaterial { get; init; } = string.Empty;

    internal ExplorationScopeLedger WithIdentityDigestMaterial(
        IEnumerable<string> discovered,
        IEnumerable<string> visited,
        IEnumerable<string> pending,
        IEnumerable<string> unresolved,
        IEnumerable<string> frontier,
        IEnumerable<KeyValuePair<string, long>> recordOnly,
        IEnumerable<string>? correlation = null)
        => this with
        {
            IdentityDigestMaterial = string.Join('|', discovered
                .Select(identity => $"D:{identity}")
                .Concat(visited.Select(identity => $"V:{identity}"))
                .Concat(pending.Select(identity => $"P:{identity}"))
                .Concat(unresolved.Select(identity => $"U:{identity}"))
                .Concat(frontier.Select(identity => $"F:{identity}"))
                .Concat(recordOnly.Select(pair => $"R:{pair.Key}@{pair.Value}"))
                .Concat(correlation ?? [])
                .OrderBy(value => value, StringComparer.Ordinal)
                ),
        };
}

/// <summary>
/// Immutable per-Run exploration ledger projection (Roadmap Phase 2).
/// Compiled deterministically on demand from existing evidence records:
/// branch-progress evidence, revisit-coverage records, and structural-progress
/// facts. It is an evidence input for Agent-owned GoalEvidence — it carries no
/// action, authorization, transition, completion, or recovery authority and is
/// never a completion fact.
/// </summary>
public sealed record ExplorationLedgerView
{
    /// <summary>Contract version constant for this projection shape.</summary>
    public const string CurrentContractVersion = "exploration-ledger.v1";

    /// <summary>Create one validated immutable ledger projection.</summary>
    public ExplorationLedgerView(
        string runId,
        string runtimeExecutionIntentReference,
        ExplorationRule containerRule,
        ExplorationRule leafRule,
        ExplorationDepthSemantics depthSemantics,
        int declaredMaximumDepth,
        ImmutableArray<ExplorationScopeLedger> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeExecutionIntentReference);
        if (!Enum.IsDefined(containerRule)) throw new ArgumentOutOfRangeException(nameof(containerRule));
        if (!Enum.IsDefined(leafRule)) throw new ArgumentOutOfRangeException(nameof(leafRule));
        if (!Enum.IsDefined(depthSemantics)) throw new ArgumentOutOfRangeException(nameof(depthSemantics));
        if (declaredMaximumDepth < 0) throw new ArgumentOutOfRangeException(nameof(declaredMaximumDepth));
        Scopes = scopes.IsDefault ? ImmutableArray<ExplorationScopeLedger>.Empty : scopes;
        RunId = runId;
        RuntimeExecutionIntentReference = runtimeExecutionIntentReference;
        ContainerRule = containerRule;
        LeafRule = leafRule;
        DepthSemantics = depthSemantics;
        DeclaredMaximumDepth = declaredMaximumDepth;
    }

    /// <summary>Contract version of this projection shape.</summary>
    public string ContractVersion => CurrentContractVersion;

    /// <summary>Agent-assigned Run identity this ledger was compiled for.</summary>
    public string RunId { get; }

    /// <summary>Reference to the immutable runtime execution intent the rules were derived from.</summary>
    public string RuntimeExecutionIntentReference { get; }

    /// <summary>Rule applied to nodes classified as semantic containers.</summary>
    public ExplorationRule ContainerRule { get; }

    /// <summary>Rule applied to nodes classified as leaves.</summary>
    public ExplorationRule LeafRule { get; }

    /// <summary>Admission-derived depth semantics for the accepted strategy.</summary>
    public ExplorationDepthSemantics DepthSemantics { get; }

    /// <summary>Immutable declared maximum depth for the Run.</summary>
    public int DeclaredMaximumDepth { get; }

    /// <summary>Per-scope evidence-derived accounting.</summary>
    public ImmutableArray<ExplorationScopeLedger> Scopes { get; }

    internal string StructuralCorrelationMaterial { get; init; } = string.Empty;
    internal string StructuralCorrelationDigestMaterial { get; init; } = string.Empty;

    internal ExplorationLedgerView WithStructuralCorrelationMaterial(string material, string digestMaterial)
        => this with { StructuralCorrelationMaterial = material, StructuralCorrelationDigestMaterial = digestMaterial };

    /// <summary>
    /// Value equality with structural <see cref="Scopes"/> comparison: the default
    /// ImmutableArray equality compares underlying array references, which would
    /// make identical independently compiled ledgers unequal.
    /// </summary>
    public bool Equals(ExplorationLedgerView? other) =>
        other is not null
        && RunId == other.RunId
        && RuntimeExecutionIntentReference == other.RuntimeExecutionIntentReference
        && ContainerRule == other.ContainerRule
        && LeafRule == other.LeafRule
        && DepthSemantics == other.DepthSemantics
        && DeclaredMaximumDepth == other.DeclaredMaximumDepth
        && StructuralCorrelationDigestMaterial == other.StructuralCorrelationDigestMaterial
        && Scopes.AsSpan().SequenceEqual(other.Scopes.AsSpan());

    /// <summary>Hash code consistent with <see cref="Equals(ExplorationLedgerView)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RunId);
        hash.Add(RuntimeExecutionIntentReference);
        hash.Add(ContainerRule);
        hash.Add(LeafRule);
        hash.Add(DepthSemantics);
        hash.Add(DeclaredMaximumDepth);
        hash.Add(StructuralCorrelationDigestMaterial);
        foreach (var scope in Scopes)
            hash.Add(scope);
        return hash.ToHashCode();
    }

    /// <summary>Deterministic digest over the full ledger content.</summary>
    public string LedgerDigest
    {
        get
        {
            var sb = new StringBuilder();
            sb.Append(ContractVersion).Append('|')
              .Append(RunId).Append('|')
              .Append(RuntimeExecutionIntentReference).Append('|')
              .Append((int)ContainerRule).Append('|')
              .Append((int)LeafRule).Append('|')
              .Append((int)DepthSemantics).Append('|')
              .Append(DeclaredMaximumDepth).Append("|struct:")
              .Append(StructuralCorrelationDigestMaterial);
            foreach (var scope in Scopes)
                sb.Append('|').Append(scope.ScopeIdentity).Append(':')
                  .Append(scope.Discovered).Append('/')
                  .Append(scope.Visited).Append('/')
                  .Append(scope.Pending).Append('/')
                  .Append(scope.Unresolved).Append('/')
                  .Append(scope.UnknownFrontier).Append('@')
                  .Append(scope.SourceObservationSequence)
                  .Append('[').Append(scope.IdentityDigestMaterial).Append(']');
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash);
        }
    }
}
