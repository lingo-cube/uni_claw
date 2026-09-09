using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Kernel.Diagnostics;

namespace UniClaw.Kernel.World;

/// <summary>
/// Revision-bound, authority-free lookup projection. Every value is derived
/// from one immutable WorldBeliefRevision; callers can only receive canonical
/// objects already owned by that revision. The index never participates in a
/// reconciliation decision and is never exposed outside World Model.
/// </summary>
internal sealed class WorldRevisionIndex
{
    private WorldRevisionIndex(
        ImmutableDictionary<string, int> containerPositions,
        ImmutableDictionary<string, OccurrenceBelief> occurrencesById,
        ImmutableDictionary<string, IReadOnlyList<OccurrenceBelief>> occurrencesByRole,
        ImmutableDictionary<string, IReadOnlyList<IndexedOccurrence>> occurrencesByContainer,
        ImmutableDictionary<string, int> logicalItemPositions,
        IReadOnlyList<int> missingContainerLogicalItemPositions,
        ImmutableHashSet<string> conflictSubjects,
        ImmutableDictionary<string, IReadOnlyList<IndexedConflict>> conflictsBySubject,
        ImmutableDictionary<string, ImmutableList<IndexedClaim>> claimsByContainerPrefix,
        ImmutableDictionary<string, ContainerClaimPosition> containerClaimPositions)
    {
        ContainerPositions = containerPositions;
        OccurrencesById = occurrencesById;
        OccurrencesByRole = occurrencesByRole;
        OccurrencesByContainer = occurrencesByContainer;
        LogicalItemPositions = logicalItemPositions;
        MissingContainerLogicalItemPositions = missingContainerLogicalItemPositions;
        ConflictSubjects = conflictSubjects;
        ConflictsBySubject = conflictsBySubject;
        ClaimsByContainerPrefix = claimsByContainerPrefix;
        ContainerClaimPositions = containerClaimPositions;
    }

    internal ImmutableDictionary<string, int> ContainerPositions { get; }
    internal ImmutableDictionary<string, OccurrenceBelief> OccurrencesById { get; }
    internal ImmutableDictionary<string, IReadOnlyList<OccurrenceBelief>> OccurrencesByRole { get; }
    internal ImmutableDictionary<string, IReadOnlyList<IndexedOccurrence>> OccurrencesByContainer { get; }
    internal ImmutableDictionary<string, int> LogicalItemPositions { get; }
    internal IReadOnlyList<int> MissingContainerLogicalItemPositions { get; }
    internal ImmutableHashSet<string> ConflictSubjects { get; }
    internal ImmutableDictionary<string, IReadOnlyList<IndexedConflict>> ConflictsBySubject { get; }
    internal ImmutableDictionary<string, ImmutableList<IndexedClaim>> ClaimsByContainerPrefix { get; }
    private ImmutableDictionary<string, ContainerClaimPosition> ContainerClaimPositions { get; }

    internal static WorldRevisionIndex Create(
        WorldBeliefRevision revision,
        WorldBeliefRevision? parentRevision,
        WorldRevisionIndex? parent,
        RuntimeStageMetrics? metrics)
    {
        var start = metrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = metrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        long scanned = 0;
        long copied = 0;

        var containers = revision.Containers ?? Array.Empty<ContainerBelief>();
        ImmutableDictionary<string, int> containerPositions;
        if (parent is not null && parentRevision is not null
            && (ReferenceEquals(revision.Containers, parentRevision.Containers)
                || containers.Count == (parentRevision.Containers?.Count ?? 0)))
        {
            // Matched association changes evidence basis, never identity position.
            containerPositions = parent.ContainerPositions;
        }
        else if (parent is not null && parentRevision is not null
                 && containers.Count == (parentRevision.Containers?.Count ?? 0) + 1)
        {
            scanned++;
            containerPositions = parent.ContainerPositions.Add(
                containers[^1].Identity.ContainerId, containers.Count - 1);
            copied++;
        }
        else
        {
            var builder = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < containers.Count; i++)
            {
                scanned++;
                builder[containers[i].Identity.ContainerId] = i;
            }
            containerPositions = builder.ToImmutable();
            copied += containers.Count;
        }

        var occurrences = revision.Occurrences ?? Array.Empty<OccurrenceBelief>();
        ImmutableDictionary<string, OccurrenceBelief> occurrencesById;
        ImmutableDictionary<string, IReadOnlyList<OccurrenceBelief>> occurrencesByRole;
        ImmutableDictionary<string, IReadOnlyList<IndexedOccurrence>> occurrencesByContainer;
        if (parent is not null && parentRevision is not null
            && ReferenceEquals(revision.Occurrences, parentRevision.Occurrences))
        {
            occurrencesById = parent.OccurrencesById;
            occurrencesByRole = parent.OccurrencesByRole;
            occurrencesByContainer = parent.OccurrencesByContainer;
        }
        else
        {
            var byId = ImmutableDictionary.CreateBuilder<string, OccurrenceBelief>(StringComparer.Ordinal);
            var byRole = new Dictionary<string, List<OccurrenceBelief>>(StringComparer.Ordinal);
            var byContainer = new Dictionary<string, List<IndexedOccurrence>>(StringComparer.Ordinal);
            for (var i = 0; i < occurrences.Count; i++)
            {
                scanned++;
                var occurrence = occurrences[i];
                byId[occurrence.OccurrenceId] = occurrence;
                if (!byRole.TryGetValue(occurrence.Role, out var roleEntries))
                {
                    roleEntries = new List<OccurrenceBelief>();
                    byRole[occurrence.Role] = roleEntries;
                }
                roleEntries.Add(occurrence);
                if (occurrence.OwningContainerId is { } owner)
                {
                    if (!byContainer.TryGetValue(owner, out var containerEntries))
                    {
                        containerEntries = new List<IndexedOccurrence>();
                        byContainer[owner] = containerEntries;
                    }
                    containerEntries.Add(new IndexedOccurrence(i, occurrence));
                }
                copied += occurrence.OwningContainerId is null ? 2 : 3;
            }
            occurrencesById = byId.ToImmutable();
            occurrencesByRole = byRole.ToImmutableDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<OccurrenceBelief>)kv.Value.ToArray(),
                StringComparer.Ordinal);
            occurrencesByContainer = byContainer.ToImmutableDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<IndexedOccurrence>)kv.Value.ToArray(),
                StringComparer.Ordinal);
        }

        var logicalItems = revision.LogicalItems ?? Array.Empty<LogicalItemBelief>();
        ImmutableDictionary<string, int> logicalItemPositions;
        IReadOnlyList<int> missingContainerLogicalItemPositions;
        if (parent is not null && parentRevision is not null
            && ReferenceEquals(revision.LogicalItems, parentRevision.LogicalItems)
            && ReferenceEquals(containerPositions, parent.ContainerPositions))
        {
            logicalItemPositions = parent.LogicalItemPositions;
            missingContainerLogicalItemPositions = parent.MissingContainerLogicalItemPositions;
        }
        else
        {
            var positions = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            var missing = new List<int>();
            for (var i = 0; i < logicalItems.Count; i++)
            {
                scanned++;
                positions[logicalItems[i].LogicalItemId] = i;
                if (logicalItems[i].OwningContainerId is { } owner
                    && !containerPositions.ContainsKey(owner))
                    missing.Add(i);
            }
            logicalItemPositions = positions.ToImmutable();
            missingContainerLogicalItemPositions = missing.ToArray();
            copied += logicalItems.Count + missing.Count;
        }

        ImmutableHashSet<string> conflictSubjects;
        ImmutableDictionary<string, IReadOnlyList<IndexedConflict>> conflictsBySubject;
        if (parent is not null && parentRevision is not null
            && ReferenceEquals(revision.Conflicts, parentRevision.Conflicts))
        {
            conflictSubjects = parent.ConflictSubjects;
            conflictsBySubject = parent.ConflictsBySubject;
        }
        else if (parent is not null && parentRevision is not null
                 && revision.Conflicts.Count == parentRevision.Conflicts.Count + 1)
        {
            scanned++;
            var position = revision.Conflicts.Count - 1;
            var conflict = revision.Conflicts[position];
            conflictSubjects = parent.ConflictSubjects.Add(conflict.Subject);
            var priorEntries = parent.ConflictsBySubject.TryGetValue(conflict.Subject, out var bucket)
                ? bucket
                : Array.Empty<IndexedConflict>();
            conflictsBySubject = parent.ConflictsBySubject.SetItem(
                conflict.Subject,
                priorEntries.Append(new IndexedConflict(position, conflict)).ToArray());
            copied += priorEntries.Count + 2;
        }
        else
        {
            var subjects = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            var bySubject = new Dictionary<string, List<IndexedConflict>>(StringComparer.Ordinal);
            for (var i = 0; i < revision.Conflicts.Count; i++)
            {
                scanned++;
                var conflict = revision.Conflicts[i];
                subjects.Add(conflict.Subject);
                if (!bySubject.TryGetValue(conflict.Subject, out var entries))
                {
                    entries = new List<IndexedConflict>();
                    bySubject[conflict.Subject] = entries;
                }
                entries.Add(new IndexedConflict(i, conflict));
            }
            conflictSubjects = subjects.ToImmutable();
            conflictsBySubject = bySubject.ToImmutableDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<IndexedConflict>)kv.Value.ToArray(),
                StringComparer.Ordinal);
            copied += revision.Conflicts.Count + conflictSubjects.Count;
        }

        var claimsByContainerPrefix = parent?.ClaimsByContainerPrefix
            ?? ImmutableDictionary.Create<string, ImmutableList<IndexedClaim>>(StringComparer.Ordinal);
        var containerClaimPositions = parent?.ContainerClaimPositions
            ?? ImmutableDictionary.Create<string, ContainerClaimPosition>(StringComparer.Ordinal);
        IEnumerable<KeyValuePair<string, WorldClaim>> changedClaims =
            parentRevision is not null && ReferenceEquals(revision.WorldState, parentRevision.WorldState)
                ? Enumerable.Empty<KeyValuePair<string, WorldClaim>>()
                : revision.WorldState is PersistentRevisionDictionary<string, WorldClaim> persistent
                ? persistent.ChangedEntries
                : revision.WorldState;
        foreach (var claim in changedClaims)
        {
            scanned++;
            var separator = claim.Key.IndexOf('.', StringComparison.Ordinal);
            if (separator <= 0)
                continue;
            var prefix = claim.Key[..separator];
            // Keep unresolved prefixes as non-authoritative lookup candidates:
            // a later revision may establish that container identity, at which
            // point the unchanged accepted claim must enter its Slice exactly as
            // the baseline full WorldState projection did.
            if (containerClaimPositions.TryGetValue(claim.Key, out var position))
            {
                var entries = claimsByContainerPrefix[position.Prefix];
                claimsByContainerPrefix = claimsByContainerPrefix.SetItem(
                    position.Prefix,
                    entries.SetItem(position.BucketPosition,
                        new IndexedClaim(position.Ordinal, claim.Key, claim.Value)));
            }
            else
            {
                var entries = claimsByContainerPrefix.TryGetValue(prefix, out var bucket)
                    ? bucket
                    : ImmutableList<IndexedClaim>.Empty;
                var ordinal = containerClaimPositions.Count;
                claimsByContainerPrefix = claimsByContainerPrefix.SetItem(
                    prefix, entries.Add(new IndexedClaim(ordinal, claim.Key, claim.Value)));
                containerClaimPositions = containerClaimPositions.Add(
                    claim.Key,
                    new ContainerClaimPosition(prefix, entries.Count, ordinal));
            }
            copied++;
        }

        var result = new WorldRevisionIndex(
            containerPositions,
            occurrencesById,
            occurrencesByRole,
            occurrencesByContainer,
            logicalItemPositions,
            missingContainerLogicalItemPositions,
            conflictSubjects,
            conflictsBySubject,
            claimsByContainerPrefix,
            containerClaimPositions);

        metrics?.RecordWorldModel(
            WorldModelOperation.RevisionIndexBuild,
            scanned,
            copiedEntries: copied,
            outputEntries: containerPositions.Count + occurrencesById.Count
                + logicalItemPositions.Count + conflictSubjects.Count + containerClaimPositions.Count,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - start);
        return result;
    }

    internal static WorldRevisionIndex ForContinuityRevision(
        WorldBeliefRevision revision,
        WorldRevisionIndex parent,
        RuntimeStageMetrics? metrics)
    {
        var start = metrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = metrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        var logicalItems = revision.LogicalItems ?? Array.Empty<LogicalItemBelief>();
        ImmutableDictionary<string, int> logicalItemPositions;
        IReadOnlyList<int> missingContainerLogicalItemPositions;
        long scanned;
        long copied;
        if (logicalItems.Count == parent.LogicalItemPositions.Count)
        {
            // Continuity can update basis/lifecycle but never an item's identity
            // or owner position, so both lookup projections remain valid.
            logicalItemPositions = parent.LogicalItemPositions;
            missingContainerLogicalItemPositions = parent.MissingContainerLogicalItemPositions;
            scanned = 0;
            copied = 0;
        }
        else if (logicalItems.Count == parent.LogicalItemPositions.Count + 1)
        {
            var position = logicalItems.Count - 1;
            var item = logicalItems[position];
            logicalItemPositions = parent.LogicalItemPositions.Add(item.LogicalItemId, position);
            missingContainerLogicalItemPositions = item.OwningContainerId is { } owner
                && !parent.ContainerPositions.ContainsKey(owner)
                ? parent.MissingContainerLogicalItemPositions.Append(position).ToArray()
                : parent.MissingContainerLogicalItemPositions;
            scanned = 1;
            copied = missingContainerLogicalItemPositions.Count
                == parent.MissingContainerLogicalItemPositions.Count ? 1 : 2;
        }
        else
        {
            var positions = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            var missing = new List<int>();
            for (var i = 0; i < logicalItems.Count; i++)
            {
                positions[logicalItems[i].LogicalItemId] = i;
                if (logicalItems[i].OwningContainerId is { } owner
                    && !parent.ContainerPositions.ContainsKey(owner))
                    missing.Add(i);
            }
            logicalItemPositions = positions.ToImmutable();
            missingContainerLogicalItemPositions = missing.ToArray();
            scanned = logicalItems.Count;
            copied = logicalItems.Count + missing.Count;
        }
        var result = new WorldRevisionIndex(
            parent.ContainerPositions,
            parent.OccurrencesById,
            parent.OccurrencesByRole,
            parent.OccurrencesByContainer,
            logicalItemPositions,
            missingContainerLogicalItemPositions,
            parent.ConflictSubjects,
            parent.ConflictsBySubject,
            parent.ClaimsByContainerPrefix,
            parent.ContainerClaimPositions);
        metrics?.RecordWorldModel(
            WorldModelOperation.RevisionIndexBuild,
            scannedEntries: scanned,
            copiedEntries: copied,
            outputEntries: logicalItemPositions.Count,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - start);
        return result;
    }

    internal sealed record IndexedOccurrence(int Ordinal, OccurrenceBelief Occurrence);
    internal sealed record IndexedConflict(int Ordinal, Conflict Conflict);
    internal sealed record IndexedClaim(int Ordinal, string Subject, WorldClaim Claim);
    private sealed record ContainerClaimPosition(string Prefix, int BucketPosition, int Ordinal);
}
