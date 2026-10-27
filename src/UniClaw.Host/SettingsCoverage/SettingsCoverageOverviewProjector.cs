using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>A single observation-derived descriptor with its ledger provenance.</summary>
public sealed record CoverageObservedElement(
    long EventSequence,
    string Route,
    string Role,
    string Descriptor,
    string? NativeRid);

/// <summary>
/// Builds the Host-owned, read-only contribution to Runtime's execution overview.
/// The ledger and configuration remain the only mutable fact owners.
/// </summary>
public static class SettingsCoverageOverviewProjector
{
    public static ExecutionOverviewProjection Project(
        SettingsCoverageLedger ledger,
        SettingsCoverageConfig config)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(config);

        var snapshot = ledger.Snapshot(config);
        var currentRoute = snapshot.CurrentRoute;
        var rootObservations = ledger.ObservedElements
            .Where(item => string.Equals(item.Route, config.RootRoute, StringComparison.Ordinal))
            .GroupBy(item => item.Descriptor, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.EventSequence).Distinct().ToArray(), StringComparer.Ordinal);
        var allObservationSequences = ledger.ObservedElements
            .GroupBy(item => item.Descriptor, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.EventSequence).Distinct().ToArray(), StringComparer.Ordinal);
        var verifiedAfterByTarget = ledger.Events
            .Where(item => item.Type == CoverageEventType.PostActionVerified && item.Verified == true && item.Target is not null)
            .GroupBy(item => item.Target!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(item => item.Sequence), StringComparer.Ordinal);
        var unresolvedFailures = ledger.Events
            .Where(item => item.Type == CoverageEventType.PostActionVerified
                && item.Verified == false
                && item.Target is not null
                && (!verifiedAfterByTarget.TryGetValue(item.Target!, out var verifiedSequence) || verifiedSequence < item.Sequence))
            .GroupBy(item => item.Target!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Sequence).First(), StringComparer.Ordinal);
        var entries = new List<string>();
        var factRefs = new HashSet<string>(StringComparer.Ordinal);
        var progressKeys = new HashSet<string>(StringComparer.Ordinal);
        var actionableItems = new Dictionary<string, ActionableItem>(StringComparer.Ordinal);
        var enteredRoots = ledger.EnteredRootDescriptors(config);

        foreach (var target in config.TargetPages)
        {
            var mark = ledger.TargetMarks.GetValueOrDefault(target);
            var discovered = rootObservations.TryGetValue(target, out var observedAtRoot);
            var (status, reason, sequences) = mark?.Kind switch
            {
                TargetCoverageMarkKind.RouteEntered or TargetCoverageMarkKind.ReachedOverlay
                    => ("verified", "target mark", new[] { mark.EventSequence }),
                TargetCoverageMarkKind.Skipped
                    => ("skipped", mark.Reason ?? "Agent marked skipped", new[] { mark.EventSequence }),
                _ when enteredRoots.Contains(target)
                    => ("verified", "root route entry verified; subtree completion not implied", ledger.Events
                        .Where(item => item.Type == CoverageEventType.PostActionVerified
                            && item.Verified == true && item.Target == target)
                        .Select(item => item.Sequence).ToArray()),
                _ when unresolvedFailures.TryGetValue(target, out var failure)
                    => ("blocked", failure.Reason ?? "latest action verification failed", new[] { failure.Sequence }),
                _ => ("pending", discovered ? "observed at root" : "configured seed; not yet observed", discovered ? observedAtRoot! : Array.Empty<long>()),
            };
            AddEntry(entries, factRefs, status, "configured-target", target,
                $"discovered={discovered.ToString().ToLowerInvariant()}; source=config{(discovered ? "+root-observation" : "")}; {reason}", sequences,
                actionableItems: actionableItems, itemKey: $"root:{target}");
            if (discovered)
                progressKeys.Add($"discovered:root:{target}");
            if (status == "verified") progressKeys.Add($"verified:configured-root:{target}");
            if (status == "skipped") progressKeys.Add($"skipped:configured-root:{target}:{reason}");
            if (status == "blocked") progressKeys.Add($"blocked:configured-root:{target}:{reason}");
        }

        var structuralSkips = ledger.DynamicRootSkipReasons(config);
        // The overview records observed facts even when the legacy navigation
        // policy does not use dynamic candidates as its directive frontier.
        var dynamicObserved = rootObservations.Keys
            .Concat(ledger.DynamicRootFrontier(config))
            .Concat(ledger.DynamicRootVisited(config))
            .Concat(structuralSkips.Keys)
            .Where(target => !config.TargetPages.Contains(target, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var target in dynamicObserved.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (structuralSkips.TryGetValue(target, out var skipReason))
            {
                if (skipReason == "entry-verification-failed" && unresolvedFailures.TryGetValue(target, out var failedEntry))
                {
                    AddEntry(entries, factRefs, "blocked", "dynamic-observation", target, $"discovered=true; {failedEntry.Reason ?? skipReason}", new[] { failedEntry.Sequence },
                        actionableItems: actionableItems, itemKey: $"root:{target}");
                    progressKeys.Add($"blocked:dynamic-root:{target}:{failedEntry.Reason ?? skipReason}");
                }
                else
                {
                    AddEntry(entries, factRefs, "skipped", "dynamic-observation", target, $"discovered=true; {skipReason}", rootObservations.GetValueOrDefault(target) ?? Array.Empty<long>());
                    progressKeys.Add($"excluded:dynamic-root:{target}:{skipReason}");
                }
                progressKeys.Add($"discovered:dynamic-root:{target}");
                continue;
            }

            var mark = ledger.TargetMarks.GetValueOrDefault(target);
            if (mark is { Kind: TargetCoverageMarkKind.RouteEntered or TargetCoverageMarkKind.ReachedOverlay })
            {
                AddEntry(entries, factRefs, "verified", "dynamic-observation", target, $"discovered=true; target mark", new[] { mark.EventSequence });
                progressKeys.Add($"verified:dynamic-root:{target}");
            }
            else if (mark is { Kind: TargetCoverageMarkKind.Skipped })
            {
                AddEntry(entries, factRefs, "skipped", "dynamic-observation", target, $"discovered=true; {mark.Reason ?? "Agent marked skipped"}", new[] { mark.EventSequence });
                progressKeys.Add($"skipped:dynamic-root:{target}:{mark.Reason ?? "Agent marked skipped"}");
            }
            else if (unresolvedFailures.TryGetValue(target, out var failure))
            {
                AddEntry(entries, factRefs, "blocked", "dynamic-observation", target, $"discovered=true; {failure.Reason ?? "latest action verification failed"}", new[] { failure.Sequence },
                    actionableItems: actionableItems, itemKey: $"root:{target}");
                progressKeys.Add($"blocked:dynamic-root:{target}:{failure.Reason ?? "latest action verification failed"}");
            }
            else
                AddEntry(entries, factRefs, "pending", "dynamic-observation", target, "discovered=true; observed at root; not yet verified", rootObservations[target],
                    actionableItems: actionableItems, itemKey: $"root:{target}");
            progressKeys.Add($"discovered:dynamic-root:{target}");
        }

        AddNestedEntries(ledger, config, entries, factRefs, progressKeys, actionableItems);

        var blockers = new List<string>();
        foreach (var failure in unresolvedFailures.Values)
        {
            var target = failure.Target ?? "unknown target";
            var description = $"{target}: {failure.Reason ?? "action verification failed"} [coverage-event:{failure.Sequence}]";
            if (!blockers.Contains(description, StringComparer.Ordinal))
                blockers.Add(description);
            factRefs.Add($"coverage-event:{failure.Sequence}");
            progressKeys.Add($"blocked:effect:{target}:{failure.Reason ?? "action verification failed"}");
        }

        var unknownEvents = ledger.Events
            .Where(item => item.Type == CoverageEventType.ObservationAccepted
                && string.Equals(item.Route, SettingsCoverageDirector.UnknownRouteIdentity, StringComparison.Ordinal))
            .ToArray();
        var unknowns = unknownEvents
            .Select(item => $"page identity unknown [coverage-event:{item.Sequence}]")
            .Distinct(StringComparer.Ordinal)
            .Append($"scope={config.Session.TaskTitle}; limited to current device/account/permissions/system state; unobserved pages and branches are not proven exhaustive")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var item in unknownEvents)
            factRefs.Add($"coverage-event:{item.Sequence}");

        if (config.TargetPages.Count > 0)
            factRefs.Add("coverage-config:targetPages");
        factRefs.Add("coverage-config:scope");
        progressKeys.Add("scope:current-device-account-permissions-system-state");
        actionableItems["explore:unobserved-scope"] = new(new[] { "coverage-config:scope" }, progressKeys.Order(StringComparer.Ordinal).ToArray());
        // Preserve the existing requirement owner's verdicts. Reaching an old
        // page is progress only when it first proves a declared requirement.
        factRefs.Add("coverage-config:coverage");
        foreach (var item in ledger.Report(config).Items)
        {
            if (item.Covered)
                progressKeys.Add($"verified:requirement:{item.Requirement}");
            else
                actionableItems[$"requirement:{item.Requirement}"] = new(new[] { "coverage-config:coverage" }, new[] { $"requirement-state:{item.Requirement}:{item.Detail}" });
        }
        if (snapshot.MaxScrollsReached && !snapshot.CoverageComplete)
        {
            entries.Add("[blocked][configured-requirement] scroll-discovered-entry — configured scroll attempt limit reached; other branches remain independently actionable [coverage-config:bounds.maxScrolls,coverage-config:coverage.scrollDiscoveredEntries]");
            factRefs.Add("coverage-config:bounds.maxScrolls");
            factRefs.Add("coverage-config:coverage.scrollDiscoveredEntries");
        }
        foreach (var sequences in allObservationSequences.Values)
            foreach (var sequence in sequences)
                factRefs.Add($"coverage-event:{sequence}");
        foreach (var mark in ledger.TargetMarks.Values)
            factRefs.Add($"coverage-event:{mark.EventSequence}");

        return new ExecutionOverviewProjection(
            CurrentBranch: currentRoute,
            KnownItems: entries,
            Unknowns: unknowns,
            Blockers: blockers,
            PreviousSegmentExpectation: null,
            PreviousSegmentActual: null,
            SourceFactRefs: factRefs.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ProjectionRevision: $"coverage-ledger:{ledger.LastEventSequence}",
            ProgressFactKeys: progressKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ActionableItems: actionableItems);
    }

    private static void AddEntry(
        ICollection<string> entries,
        ISet<string> factRefs,
        string status,
        string source,
        string target,
        string reason,
        IEnumerable<long> sequences,
        string fallbackConfigRef = "coverage-config:targetPages",
        IDictionary<string, ActionableItem>? actionableItems = null,
        string? itemKey = null)
    {
        var refs = sequences.Distinct().Order().Select(sequence => $"coverage-event:{sequence}").ToArray();
        foreach (var reference in refs)
            factRefs.Add(reference);
        var provenance = refs.Length == 0 ? fallbackConfigRef : string.Join(",", refs);
        if (refs.Length == 0)
            factRefs.Add(fallbackConfigRef);
        entries.Add($"[{status}][{source}] {target} — {reason} [{provenance}]");
        if (status is "pending" or "blocked" && actionableItems is not null && itemKey is not null)
            actionableItems[itemKey] = new(refs.Length > 0 ? refs : new[] { fallbackConfigRef },
                new[] { $"item-state:{itemKey}:{status}:{reason}" });
    }

    private static void AddNestedEntries(
        SettingsCoverageLedger ledger,
        SettingsCoverageConfig config,
        ICollection<string> entries,
        ISet<string> factRefs,
        ISet<string> progressKeys,
        IDictionary<string, ActionableItem> actionableItems)
    {
        var observedConfigured = new Dictionary<(string Parent, string Child), (string Key, long Sequence)>();
        foreach (var seed in ParseNestedSeeds(config.DeepTraversalSettings.Entries))
        {
            var observed = ledger.ObservedElements
                .Where(item => item.Role == "ui.element" && item.Descriptor == seed.Child
                    && IsParentRoute(item.Route, seed.Parent))
                .OrderBy(item => item.EventSequence)
                .FirstOrDefault();
            if (observed is not null)
                observedConfigured[seed] = ($"{observed.Route}>{seed.Child}", observed.EventSequence);
        }
        var visited = ledger.DynamicNestedVisited(config).ToHashSet(StringComparer.Ordinal);
        var frontier = ledger.DynamicNestedFrontier(config).ToHashSet(StringComparer.Ordinal);
        var skips = ledger.DynamicNestedSkipReasons(config);
        var configuredKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var seed in ParseNestedSeeds(config.DeepTraversalSettings.Entries))
        {
            var hasObservation = observedConfigured.TryGetValue(seed, out var observed);
            var key = hasObservation ? observed.Key : null;
            if (key is not null)
                configuredKeys.Add(key);
            var isDiscovered = key is not null;
            var status = key is not null && visited.Contains(key) ? "verified"
                : key is not null && skips.TryGetValue(key, out var skipReason) && skipReason != "entry-verification-failed" ? "skipped"
                : key is not null && skips.TryGetValue(key, out _) ? "blocked" : "pending";
            var reason = status switch
            {
                "verified" => "nested route entry verified",
                "skipped" => skips.GetValueOrDefault(key ?? "") ?? "configured nested entry skipped",
                "blocked" => "nested route entry verification failed",
                _ => isDiscovered ? "observed nested entry; not yet verified" : "configured nested seed; not yet observed",
            };
            var source = isDiscovered ? "configured-nested+dynamic-observation" : "configured-nested-seed";
            var sequences = isDiscovered ? new[] { observed.Sequence } : Array.Empty<long>();
            AddEntry(entries, factRefs, status, source, $"{seed.Parent}>{seed.Child}",
                $"discovered={isDiscovered.ToString().ToLowerInvariant()}; parent-scope={seed.Parent}; {reason}", sequences,
                "coverage-config:deepTraversal.entries", actionableItems, $"nested:{seed.Parent}>{seed.Child}");
            if (isDiscovered) progressKeys.Add($"discovered:nested:{seed.Parent}:{seed.Child}");
            if (status == "verified") progressKeys.Add($"verified:nested:{seed.Parent}:{seed.Child}");
            if (status == "skipped") progressKeys.Add($"excluded:nested:{seed.Parent}:{seed.Child}:{reason}");
            if (status == "blocked") progressKeys.Add($"blocked:nested:{seed.Parent}:{seed.Child}:{reason}");
        }

        foreach (var key in frontier.Concat(visited).Concat(skips.Keys)
            .Where(candidate => !configuredKeys.Contains(candidate))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(candidate => candidate, StringComparer.Ordinal))
        {
            var status = visited.Contains(key) ? "verified"
                : skips.TryGetValue(key, out var reason) && reason != "entry-verification-failed" ? "skipped"
                : skips.ContainsKey(key) ? "blocked" : "pending";
            var detail = status switch
            {
                "verified" => "nested route entry verified",
                "skipped" => skips[key],
                "blocked" => "nested route entry verification failed",
                _ => "observed nested entry; not yet verified",
            };
            var factSequence = ledger.Events.LastOrDefault(item => item.Type == CoverageEventType.ObservationAccepted
                && key.StartsWith(item.Route + ">", StringComparison.Ordinal))?.Sequence;
            AddEntry(entries, factRefs, status, "dynamic-nested-observation", key,
                $"discovered=true; parent-route={key[..key.LastIndexOf('>')]}; {detail}",
                factSequence is { } sequence ? new[] { sequence } : Array.Empty<long>(),
                actionableItems: actionableItems, itemKey: $"nested:{key}");
            var separator = key.LastIndexOf('>');
            var parentRoute = separator >= 0 ? key[..separator] : "unknown-parent";
            var child = separator >= 0 ? key[(separator + 1)..] : key;
            progressKeys.Add($"discovered:nested:{parentRoute}:{child}");
            if (status == "verified") progressKeys.Add($"verified:nested:{parentRoute}:{child}");
            if (status == "skipped") progressKeys.Add($"excluded:nested:{parentRoute}:{child}:{detail}");
            if (status == "blocked") progressKeys.Add($"blocked:nested:{parentRoute}:{child}:{detail}");
        }
    }

    private static IEnumerable<(string Parent, string Child)> ParseNestedSeeds(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var separator = value.IndexOf('>');
            var width = 1;
            if (separator < 0)
            {
                separator = value.IndexOf("::", StringComparison.Ordinal);
                width = 2;
            }
            if (separator <= 0 || separator + width >= value.Length)
                continue;
            var parent = value[..separator].Trim();
            var child = value[(separator + width)..].Trim();
            if (parent.Length > 0 && child.Length > 0)
                yield return (parent, child);
        }
    }

    private static bool IsParentRoute(string route, string parent) =>
        string.Equals(route, parent, StringComparison.Ordinal)
        || route.EndsWith($"|route:{parent}", StringComparison.Ordinal)
        || route.EndsWith($"|rk1:{parent}", StringComparison.Ordinal);
}
