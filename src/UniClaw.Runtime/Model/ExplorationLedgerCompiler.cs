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
/// Accepted Strategy semantics are supplied by the immutable Run context; this
/// compiler performs no strategy generation of its own.
/// </summary>
internal sealed record ExplorationScopeEvidence(
    BranchProgressEvidence Progress,
    ImmutableHashSet<string> UnresolvedIds,
    ImmutableDictionary<string, long> RecordOnlyIds,
    ImmutableHashSet<string> UnknownFrontierIds,
    ImmutableHashSet<string> RevisitCoverageIds)
{
    internal ExplorationScopeEvidence(
        BranchProgressEvidence progress,
        IEnumerable<string>? unresolvedIds = null,
        IEnumerable<KeyValuePair<string, long>>? recordOnlyIds = null,
        IEnumerable<string>? unknownFrontierIds = null,
        IEnumerable<string>? revisitCoverageIds = null)
        : this(
            progress ?? throw new ArgumentNullException(nameof(progress)),
            (unresolvedIds ?? []).ToImmutableHashSet(StringComparer.Ordinal),
            (recordOnlyIds ?? []).ToImmutableDictionary(StringComparer.Ordinal),
            (unknownFrontierIds ?? []).ToImmutableHashSet(StringComparer.Ordinal),
            (revisitCoverageIds ?? []).ToImmutableHashSet(StringComparer.Ordinal))
    {
    }
}

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
    /// identity evidence. Discovered is the approved identity set; visited is
    /// the union of verified completion, verified boundary, and RecordOnly
    /// identities; pending is the strict identity complement after unresolved.
    /// Unknown frontier is an overlapping RecordOnly annotation.
    /// </summary>
    /// <exception cref="InvalidOperationException">A revisit-coverage identity
    /// is absent from the scope's approved sibling inventory (fail closed).</exception>
    internal static ExplorationScopeLedger CompileScope(ExplorationScopeEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var progress = evidence.Progress;
        var approved = progress.ApprovedSiblingEvidence;
        var discoveredIds = approved.Keys.ToImmutableHashSet(StringComparer.Ordinal);
        foreach (var disposition in progress.VerifiedBoundaryDispositions)
            if (!discoveredIds.Any(identity => disposition.Relation.SourceOccurrenceReference
                    .StartsWith(identity + "@", StringComparison.Ordinal)))
                throw new InvalidOperationException($"Verified-boundary identity is outside approved inventory of scope '{progress.ParentSemanticPage}'.");
        ValidateSubset(evidence.UnresolvedIds, discoveredIds, "unresolved", progress.ParentSemanticPage);
        ValidateSubset(evidence.RecordOnlyIds.Keys, discoveredIds, "record-only", progress.ParentSemanticPage);
        ValidateSubset(evidence.UnknownFrontierIds, discoveredIds, "unknown-frontier", progress.ParentSemanticPage);
        ValidateSubset(evidence.RevisitCoverageIds, discoveredIds, "revisit", progress.ParentSemanticPage);
        var verifiedBoundaryIds = discoveredIds
            .Where(progress.IsBoundaryVerifiedForSource)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var visitedIds = progress.CompletedSiblingEvidence.Keys
            .Concat(verifiedBoundaryIds)
            .Concat(evidence.RecordOnlyIds.Keys)
            .ToImmutableHashSet(StringComparer.Ordinal);
        if (evidence.RecordOnlyIds.Keys.Any(progress.CompletedSiblingEvidence.ContainsKey)
            || evidence.RecordOnlyIds.Keys.Any(verifiedBoundaryIds.Contains))
            throw new InvalidOperationException($"Record-only identity overlaps verified evidence in scope '{progress.ParentSemanticPage}'.");
        if (!evidence.UnknownFrontierIds.IsSubsetOf(evidence.RecordOnlyIds.Keys)
            || !evidence.UnresolvedIds.Intersect(visitedIds).IsEmpty)
            throw new InvalidOperationException($"Contradictory exploration evidence for scope '{progress.ParentSemanticPage}'.");
        foreach (var (identity, recordSequence) in evidence.RecordOnlyIds)
            if (approved[identity] != recordSequence)
                throw new InvalidOperationException($"Record-only sequence mismatch for '{identity}' in scope '{progress.ParentSemanticPage}'.");
        var pendingIds = discoveredIds.Except(visitedIds).Except(evidence.UnresolvedIds).ToImmutableHashSet(StringComparer.Ordinal);
        var sequence = approved.Values
            .Concat(progress.CompletedSiblingEvidence.Values)
            .Concat(progress.AuthorizedSiblingEvidence.Values)
            .Concat(evidence.RecordOnlyIds.Values)
            .Concat(progress.RequiredBoundaryObligations.Select(obligation => obligation.Relation.SourceObservationSequence))
            .Concat(progress.VerifiedBoundaryDispositions.Select(disposition => disposition.Relation.SourceObservationSequence))
            .Concat(progress.VerifiedBoundaryDispositions.Select(disposition => disposition.EvidenceSequence))
            .DefaultIfEmpty(0L).Max();
        var ledger = new ExplorationScopeLedger(progress.ParentSemanticPage, discoveredIds.Count, visitedIds.Count,
            pendingIds.Count, evidence.UnresolvedIds.Count, evidence.UnknownFrontierIds.Count, sequence);
        var correlation = progress.AuthorizedSiblingEvidence
            .Select(pair => $"A:{pair.Key}@{pair.Value}")
            .Concat(evidence.RevisitCoverageIds.Select(identity => $"X:{identity}"))
            .Concat(progress.RequiredBoundaryObligations.Select(obligation =>
                $"B:{obligation.Relation.SourceOccurrenceReference}@{obligation.Relation.SourceObservationSequence}:{obligation.State}"))
            .Concat(progress.VerifiedBoundaryDispositions.Select(disposition =>
                $"VBD:{disposition.Relation.SourceOccurrenceReference}@{disposition.Relation.SourceObservationSequence}:{disposition.ReturnedParentIdentity}@{disposition.EvidenceSequence}"));
        return ledger.WithIdentityDigestMaterial(
            approved.Select(pair => $"{pair.Key}@{pair.Value}"),
            progress.CompletedSiblingEvidence.Select(pair => $"{pair.Key}@{pair.Value}").Concat(verifiedBoundaryIds),
            pendingIds,
            evidence.UnresolvedIds,
            evidence.UnknownFrontierIds,
            evidence.RecordOnlyIds,
            correlation);
    }

    /// <summary>
    /// Compile the per-Run ledger view from the accepted Run context and
    /// Agent-owned identity evidence. Pure and deterministic.
    /// </summary>
    internal static ExplorationLedgerView Compile(
        AcceptedExplorationRunContext context,
        ImmutableArray<ExplorationScopeEvidence> scopes,
        StrategyExecutionEvidenceView? structuralEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateStructuralEvidence(context, structuralEvidence);
        var compiled = scopes
            .OrderBy(s => s.Progress.ParentSemanticPage, StringComparer.Ordinal)
            .Select(CompileScope)
            .ToImmutableArray();
        var ledger = new ExplorationLedgerView(
            context.RunId,
            context.Semantics.RuntimeExecutionIntentReference,
            context.Semantics.ContainerRule,
            context.Semantics.LeafRule,
            context.Semantics.DepthSemantics,
            context.Semantics.DeclaredMaximumDepth,
            compiled);
        var digestMaterial = CanonicalStructuralCorrelation(structuralEvidence);
        var inspectionMaterial = structuralEvidence is null
            ? string.Empty
            : $"{digestMaterial}|accepted-view:{structuralEvidence.EvidenceViewDigest}";
        return ledger.WithStructuralCorrelationMaterial(inspectionMaterial, digestMaterial);
    }

    private static void ValidateStructuralEvidence(
        AcceptedExplorationRunContext context,
        StrategyExecutionEvidenceView? evidence)
    {
        if (evidence is null) return;
        if (!string.Equals(evidence.ContractVersion, StrategyExecutionEvidenceView.CurrentContractVersion, StringComparison.Ordinal)
            || !string.Equals(evidence.RunId, context.RunId, StringComparison.Ordinal)
            || !string.Equals(evidence.RuntimeExecutionIntentReference, context.Semantics.RuntimeExecutionIntentReference, StringComparison.Ordinal))
            throw new InvalidOperationException("Strategy evidence is not bound to the accepted exploration Run.");
        var previousRevision = -1L;
        foreach (var fact in evidence.StructuralProgressFacts)
        {
            if (!Enum.IsDefined(fact.Kind)
                || fact.Revision < 0
                || fact.Revision < previousRevision
                || fact.Revision > evidence.StructuralProgressRevision
                || string.IsNullOrWhiteSpace(fact.EvidenceReference))
                throw new InvalidOperationException("Strategy structural-progress evidence is invalid.");
            previousRevision = fact.Revision;
        }
    }

    private static string CanonicalStructuralCorrelation(StrategyExecutionEvidenceView? evidence)
    {
        if (evidence is null) return string.Empty;
        var facts = evidence.StructuralProgressFacts
            .Select(fact => $"F:{(int)fact.Kind}@{fact.Revision}:{fact.EvidenceReference}")
            .OrderBy(value => value, StringComparer.Ordinal);
        var coverage = evidence.CoverageEvidenceReferences.OrderBy(value => value, StringComparer.Ordinal).Select(value => $"C:{value}");
        var contradictions = evidence.ContradictionEvidenceReferences.OrderBy(value => value, StringComparer.Ordinal).Select(value => $"X:{value}");
        var traces = evidence.TraceReferences.OrderBy(value => value, StringComparer.Ordinal).Select(value => $"T:{value}");
        return string.Join('|', new[]
        {
            $"contract:{evidence.ContractVersion}", $"run:{evidence.RunId}", $"intent:{evidence.RuntimeExecutionIntentReference}",
            $"obs:{evidence.AcceptedObservationSequence}", $"belief:{evidence.BeliefRevision}:{evidence.BeliefDigest}",
            $"progress:{evidence.StructuralProgressRevision}", $"trace:{evidence.TraceDigest}",
        }.Concat(facts).Concat(coverage).Concat(contradictions).Concat(traces));
    }

    private static void ValidateSubset(IEnumerable<string> identities, ImmutableHashSet<string> approved, string label, string scope)
    {
        var outside = identities.Where(identity => !approved.Contains(identity)).ToArray();
        if (outside.Length > 0)
            throw new InvalidOperationException($"{label} identity '{outside[0]}' is outside approved inventory of scope '{scope}'.");
    }
}
