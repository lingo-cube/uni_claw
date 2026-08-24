using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Deterministic, pure compiler of <see cref="ExplorationLedgerView"/> from
/// existing evidence records. Compilation owns no state, persists nothing, and
/// mutates no evidence record: identical inputs produce identical ledgers and
/// digests (Roadmap Phase 2 — Exploration Ledger projection).
///
/// Rule and depth derivation is a closed interpretation of the accepted
/// strategy's exploration intent — RuntimeAgent never authors rules:
/// - ExhaustiveWithinScope: containers expand, leaves record, depth cutoff fails closed.
/// - InspectMatchesWithinScope: containers expand, leaves record, depth cutoff fails closed.
/// Bounded-record boundary behavior is declared by the caller (the admission /
/// snapshot seam) choosing <see cref="ExplorationDepthSemantics"/>; this
/// compiler performs no strategy generation of its own.
/// </summary>
public static class ExplorationLedgerCompiler
{
    /// <summary>Derive the closed container/leaf rule pair from the accepted exploration intent.</summary>
    public static (ExplorationRule ContainerRule, ExplorationRule LeafRule) DeriveRules(
        ExplorationIntent explorationIntent)
    {
        return explorationIntent switch
        {
            ExplorationIntent.ExhaustiveWithinScope => (ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly),
            ExplorationIntent.InspectMatchesWithinScope => (ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(explorationIntent)),
        };
    }

    /// <summary>Derive bounded semantic depth mode from the declared maximum depth.</summary>
    public static ExplorationDepthSemantics DeriveDepthSemantics(int declaredMaximumDepth)
    {
        return declaredMaximumDepth switch
        {
            0 => ExplorationDepthSemantics.RootRecordOnly,
            1 => ExplorationDepthSemantics.RootAndDirectChildren,
            >= 2 => ExplorationDepthSemantics.BoundedRecursive,
            _ => throw new ArgumentOutOfRangeException(nameof(declaredMaximumDepth)),
        };
    }

    /// <summary>
    /// Compile one per-scope ledger from branch-progress evidence and the
    /// per-page revisit-coverage record. Pending counts authorized-but-not-
    /// completed children; unresolved counts unclassifiable inventory nodes;
    /// unknown frontier counts discovered containers beyond the declared depth
    /// boundary (bounded-record semantics; supplied by the caller as evidence).
    /// Frontier nodes are record-visited: their RecordOnly rule is satisfied by
    /// the fresh-observation record (spec R3), and they remain annotated as
    /// unknown frontier (spec R4) — an overlapping annotation on visited.
    /// The optional revisit-coverage input is a FAIL-CLOSED consistency
    /// cross-check only: every coverage-recorded identity MUST exist in this
    /// scope's approved sibling inventory, otherwise compilation throws —
    /// evidence corruption must never compile to a cleaner ledger. Coverage
    /// concerns still-pending branches and changes NO count semantics.
    /// </summary>
    /// <exception cref="InvalidOperationException">A revisit-coverage identity
    /// is absent from the scope's approved sibling inventory (fail closed).</exception>
    public static ExplorationScopeLedger CompileScope(
        BranchProgressEvidence branchProgress,
        int unresolvedCount,
        int unknownFrontierCount,
        IEnumerable<string>? revisitCoveredIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(branchProgress);

        // REVISIT-COVERAGE CONSISTENCY CROSS-CHECK (fail closed): the coverage
        // record tracks still-pending approved branches, so any covered
        // identity outside the approved inventory is evidence inconsistency —
        // reject rather than silently compiling a cleaner ledger. The five
        // counts below are deliberately UNCHANGED by this input.
        if (revisitCoveredIdentities is not null)
        {
            foreach (var coveredIdentity in revisitCoveredIdentities)
            {
                if (!branchProgress.ApprovedSiblingEvidence.ContainsKey(coveredIdentity))
                {
                    throw new InvalidOperationException(
                        $"Revisit-coverage record '{coveredIdentity}' is not in the approved sibling inventory "
                        + $"of scope '{branchProgress.ParentSemanticPage}'; ledger compilation fails closed "
                        + "(evidence inconsistency, never a cleaner ledger).");
                }
            }
        }

        // Discovered = approved inventory plus unclassified inventory nodes.
        // Unknown-frontier nodes are NOT added to the sum: per the frozen spec
        // (R3 "Record-only node visited by observation" + R4 "Bounded-record
        // boundary is recorded, not failed"), frontier nodes are approved-
        // inventory boundary nodes processed record-only — an overlapping
        // annotation on visited, not a separate disposition population.
        var discovered = branchProgress.ApprovedSiblingEvidence.Count
            + unresolvedCount;
        // Visited = rule-satisfied with evidence: an approved sibling counts as
        // visited only through completed-sibling evidence (verified completion /
        // verified return) — never through a dispatch receipt — PLUS the
        // record-only boundary nodes (frontier count): their RecordOnly rule is
        // satisfied by the fresh-observation record itself, with zero dispatch
        // (R3). The production writer (bounded-record depth boundary) counts
        // exactly the pending approved nodes into the frontier, so frontier
        // nodes are always within the approved inventory and never completed.
        var visited = branchProgress.CompletedSiblingEvidence.Count + unknownFrontierCount;
        // Pending = discovered nodes with an authorized obligation whose verified
        // completion is still awaited, plus boundary obligations awaiting a
        // verified return (discovered-but-denied audit candidates are neither
        // pending nor visited; they reduce to the discovered remainder).
        var pending = branchProgress.ApprovedSiblingEvidence.Keys
            .Count(k => branchProgress.RequiredChildren.ContainsKey(k)
                && !branchProgress.CompletedSiblingEvidence.ContainsKey(k))
            + branchProgress.RequiredBoundaryObligations.Count(
                o => o.State == BoundaryObligationState.Pending);
        var remainder = discovered - visited - pending - unresolvedCount;
        if (remainder < 0)
        {
            // Disposition over-count vs discovered cannot happen with valid
            // evidence; clamp fail-closed to unresolved accounting so the ledger
            // never silently reports a cleaner state than the evidence supports.
            // (Frontier no longer participates: it is an overlapping visited
            // annotation, already included in the visited term above.)
            unresolvedCount -= remainder;
            pending = discovered - visited - unresolvedCount;
            if (pending < 0) pending = 0;
        }

        var sequence = branchProgress.ApprovedSiblingEvidence.Values.Append(
            branchProgress.CompletedSiblingEvidence.Values.Append(0L).Max()).Append(0L).Max();

        return new ExplorationScopeLedger(
            branchProgress.ParentSemanticPage,
            discovered,
            visited,
            pending,
            unresolvedCount,
            unknownFrontierCount,
            sequence);
    }

    /// <summary>
    /// Compile the per-Run ledger view from evidence records. Pure and
    /// deterministic: no state ownership, no persistence, no mutation. Each
    /// scope carries its optional revisit-coverage identities for the
    /// fail-closed consistency cross-check (count semantics unchanged).
    /// </summary>
    public static ExplorationLedgerView Compile(
        string runId,
        string runtimeExecutionIntentReference,
        ExplorationIntent explorationIntent,
        int declaredMaximumDepth,
        ImmutableArray<(BranchProgressEvidence Progress, int Unresolved, int UnknownFrontier, ImmutableArray<string> RevisitCoverage)> scopes)
    {
        var (containerRule, leafRule) = DeriveRules(explorationIntent);
        var depthSemantics = DeriveDepthSemantics(declaredMaximumDepth);
        var compiled = scopes
            .Select(s => CompileScope(s.Progress, s.Unresolved, s.UnknownFrontier, s.RevisitCoverage))
            .ToImmutableArray();
        return new ExplorationLedgerView(
            runId,
            runtimeExecutionIntentReference,
            containerRule,
            leafRule,
            depthSemantics,
            declaredMaximumDepth,
            compiled);
    }
}
