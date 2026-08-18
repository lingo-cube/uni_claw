using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Deterministic Settings-scoped reducer from raw structured Android UI evidence
/// to <see cref="InteractionAffordanceEvidence"/>.
///
/// This is Runtime semantic interpretation, not Environment truth.
/// It never treats clickable alone as navigation.
/// </summary>
public static class InteractionAffordanceAnalyzer
{
    /// <summary>
    /// Produces one affordance evidence per structured element in the Observation.
    /// Precision is preferred over recall; ambiguous evidence becomes Unknown.
    /// </summary>
    public static ImmutableArray<InteractionAffordanceEvidence> Analyze(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.StructuredElements.IsDefaultOrEmpty)
            return [];

        var builder = ImmutableArray.CreateBuilder<InteractionAffordanceEvidence>(observation.StructuredElements.Length);
        for (int i = 0; i < observation.StructuredElements.Length; i++)
        {
            var raw = observation.StructuredElements[i];
            builder.Add(new InteractionAffordanceEvidence(
                observation.SequenceNumber,
                i,
                Classify(raw),
                Reason(raw),
                raw.ResourceId));
        }
        return builder.ToImmutable();
    }

    private static InteractionAffordanceKind Classify(StructuredElementEvidence raw)
    {
        // INTERACTION RELEVANCE GATE: elements with no interaction signal at all
        // (not clickable, not checkable, not focusable/actionable, no switch/
        // checkable child evidence) are structurally NON_INTERACTIVE — plain
        // title/status/decorative text. They never produce a blocking Unknown.
        // This is structural fact, NOT a TextView/class/resource-id rule.
        if (raw.Clickable != true
            && raw.Checkable != true
            && raw.Focusable != true
            && !HasSwitchClass(raw.Class)
            && raw.HasSwitchChild != true)
        {
            return InteractionAffordanceKind.NonInteractive;
        }

        // Local controls first: Switch/checkable evidence must never become navigation.
        if (raw.Checkable == true
            || HasSwitchClass(raw.Class)
            || raw.HasSwitchChild == true)
        {
            return InteractionAffordanceKind.LocalControl;
        }

        // SEARCH-ROLE RESOLUTION (generic, role-based — never title / package /
        // page): an interactive element carrying a STABLE search-role structured
        // token — the SearchView / SearchBar view families or the standard
        // "search_action_bar" resource-id leaf — is a resolved search action
        // bar: LOCAL_CONTROL. It is interactive, resolved, NOT a navigation
        // source, NOT a child-inventory entry, and NOT a recursive obligation.
        // TitleText ("Search settings") is descriptive/localized evidence only
        // and is never used to classify.
        if (raw.Clickable == true && HasSearchRole(raw))
        {
            return InteractionAffordanceKind.LocalControl;
        }

        // Settings Preference navigation row: clickable LinearLayout with title/summary
        // and no local-control child evidence.
        if (raw.Clickable == true
            && string.Equals(raw.Class, "android.widget.LinearLayout", StringComparison.Ordinal)
            && (!string.IsNullOrWhiteSpace(raw.TitleText) || !string.IsNullOrWhiteSpace(raw.SummaryText)))
        {
            return InteractionAffordanceKind.NavigationCandidate;
        }

        // Genuinely interactive but ambiguous: real interaction possible, evidence
        // insufficient to decide nav/local. Must still block completeness.
        return InteractionAffordanceKind.Unknown;
    }

    /// <summary>
    /// STABLE SEARCH-ROLE STRUCTURED TOKEN detection (role-based; TitleText /
    /// package / page are never consulted): the Android SearchView / SearchBar
    /// view families, or the standard "search_action_bar" resource-id semantic
    /// leaf (the Settings action bar's search element). Generic clickable
    /// ViewGroups without such a token remain UNKNOWN.
    /// </summary>
    private static bool HasSearchRole(StructuredElementEvidence raw)
    {
        if (raw.Class is not null
            && (raw.Class.Contains("SearchView", StringComparison.Ordinal)
                || raw.Class.Contains("SearchBar", StringComparison.Ordinal)))
        {
            return true;
        }
        if (raw.ResourceId is { } resourceId)
        {
            var leaf = resourceId;
            int colon = leaf.LastIndexOf(':');
            int slash = leaf.LastIndexOf('/');
            int cut = Math.Max(colon, slash);
            if (cut >= 0)
                leaf = leaf[(cut + 1)..];
            if (string.Equals(leaf, "search_action_bar", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool HasSwitchClass(string? className)
        => className is not null
            && (className.Contains("Switch", StringComparison.Ordinal)
                || className.Contains("CheckBox", StringComparison.Ordinal));

    private static string Reason(StructuredElementEvidence raw)
    {
        if (raw.Clickable != true && raw.Checkable != true && raw.Focusable != true
            && !HasSwitchClass(raw.Class) && raw.HasSwitchChild != true)
        {
            return $"Structurally non-interactive element (class={raw.Class ?? "null"}, clickable={raw.Clickable?.ToString() ?? "null"}, focusable={raw.Focusable?.ToString() ?? "null"}).";
        }
        if (raw.Checkable == true || HasSwitchClass(raw.Class) || raw.HasSwitchChild == true)
            return $"Structured evidence indicates local control (class={raw.Class ?? "null"}, checkable={raw.Checkable?.ToString() ?? "null"}, hasSwitchChild={raw.HasSwitchChild?.ToString() ?? "null"}).";
        if (raw.Clickable == true && HasSearchRole(raw))
            return $"Structured evidence carries a stable search-role token (class={raw.Class ?? "null"}, resourceId={raw.ResourceId ?? "null"}) — resolved search action bar, LOCAL_CONTROL (interactive, not a navigation source, not a recursive obligation).";
        if (raw.Clickable == true
            && string.Equals(raw.Class, "android.widget.LinearLayout", StringComparison.Ordinal)
            && (!string.IsNullOrWhiteSpace(raw.TitleText) || !string.IsNullOrWhiteSpace(raw.SummaryText)))
        {
            return $"Settings Preference row is clickable with title/summary and no local-control child (title='{raw.TitleText ?? ""}', class={raw.Class}).";
        }
        return $"Structured evidence is insufficient for Settings navigation/local classification (class={raw.Class ?? "null"}, clickable={raw.Clickable?.ToString() ?? "null"}).";
    }
}
