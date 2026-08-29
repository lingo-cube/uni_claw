namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Explicit scope of one knowledge record (spec requirement
/// "ScenarioKnowledgeFixture as a validation test asset" — "explicit scope
/// (scenario id, app/package, semantic capability version, Android/emulator
/// assumptions, locale, created-from run set)"; design D2/D3). Implicit global
/// knowledge and automatic cross-app / cross-version / cross-scenario reuse
/// are forbidden: a record is reusable ONLY when its context is compatible
/// with the query scope.
///
/// Value equality covers every field (including the created-from run set);
/// <see cref="Matches"/> is the reuse-compatibility check and EXCLUDES the
/// created-from run set — knowledge is reusable across runs of the same
/// scenario/app/capability-id/capability-version/locale/android context, and
/// never across a changed context.
/// </summary>
/// <param name="ScenarioId">Scenario identifier (e.g. "settings-real-emulator").</param>
/// <param name="ApplicationPackage">App/package the knowledge applies to (e.g. "com.android.settings").</param>
/// <param name="SemanticCapabilityId">Semantic capability identifier that typed the observation (e.g. "uni-claw.settings.semantic").</param>
/// <param name="SemanticCapabilityVersion">Semantic capability version the observation was typed with.</param>
/// <param name="AndroidAssumptions">Emulator/Android assumptions (image + API level), e.g. "google_apis;API 35".</param>
/// <param name="Locale">Locale of the observation (e.g. "en-US").</param>
/// <param name="CreatedFromRunIds">Runs whose observed results created/refreshed this knowledge (set semantics; excluded from <see cref="Matches"/>).</param>
public sealed record KnowledgeScope(
    string ScenarioId,
    string ApplicationPackage,
    string SemanticCapabilityId,
    string SemanticCapabilityVersion,
    string AndroidAssumptions,
    string Locale,
    IReadOnlyList<string> CreatedFromRunIds)
{
    /// <summary>
    /// True when every CONTEXT field is compatible — scenario, application
    /// package, semantic capability id + version, locale, and Android
    /// assumptions (ordinal equality). The created-from run set is
    /// deliberately excluded: runs are provenance, not reuse context.
    /// </summary>
    public bool Matches(KnowledgeScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return OrdinalEquals(ScenarioId, other.ScenarioId)
               && OrdinalEquals(ApplicationPackage, other.ApplicationPackage)
               && OrdinalEquals(SemanticCapabilityId, other.SemanticCapabilityId)
               && OrdinalEquals(SemanticCapabilityVersion, other.SemanticCapabilityVersion)
               && OrdinalEquals(AndroidAssumptions, other.AndroidAssumptions)
               && OrdinalEquals(Locale, other.Locale);
    }

    // Value equality by content — including the created-from run set as a set
    // (order-independent); overrides record synthesis so IReadOnlyList content
    // is compared by value, not by reference.

    /// <summary>Content equality: all fields incl. the created-from run set
    /// (set semantics, order-independent).</summary>
    public bool Equals(KnowledgeScope? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other)
               || (OrdinalEquals(ScenarioId, other.ScenarioId)
                   && OrdinalEquals(ApplicationPackage, other.ApplicationPackage)
                   && OrdinalEquals(SemanticCapabilityId, other.SemanticCapabilityId)
                   && OrdinalEquals(SemanticCapabilityVersion, other.SemanticCapabilityVersion)
                   && OrdinalEquals(AndroidAssumptions, other.AndroidAssumptions)
                   && OrdinalEquals(Locale, other.Locale)
                   && RunSetEqual(CreatedFromRunIds, other.CreatedFromRunIds));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ScenarioId, StringComparer.Ordinal);
        hash.Add(ApplicationPackage, StringComparer.Ordinal);
        hash.Add(SemanticCapabilityId, StringComparer.Ordinal);
        hash.Add(SemanticCapabilityVersion, StringComparer.Ordinal);
        hash.Add(AndroidAssumptions, StringComparer.Ordinal);
        hash.Add(Locale, StringComparer.Ordinal);
        if (CreatedFromRunIds is not null)
        {
            foreach (var run in CreatedFromRunIds.Order(StringComparer.Ordinal))
            {
                hash.Add(run, StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }

    private static bool OrdinalEquals(string? a, string? b)
        => string.Equals(a, b, StringComparison.Ordinal);

    private static bool RunSetEqual(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        var sortedA = a.Order(StringComparer.Ordinal).ToArray();
        var sortedB = b.Order(StringComparer.Ordinal).ToArray();
        return sortedA.SequenceEqual(sortedB, StringComparer.Ordinal);
    }
}