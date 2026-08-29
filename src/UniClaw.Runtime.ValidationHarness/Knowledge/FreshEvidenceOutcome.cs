namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// The fresh-evidence conflict action chosen by the upper agent for ONE
/// existing record (spec requirement "Knowledge never substitutes for fresh
/// evidence" — "the contradicting knowledge is downgraded to CONTRADICTED/
/// STALE, superseded, or invalidated — never force-applied"; design D3 —
/// CURRENT FRESH EVIDENCE FIRST). Every action DOWNGRADES the target record;
/// none re-activates. CLOSED: exactly these four dispositions.
/// </summary>
public enum FreshEvidenceAction
{
    /// <summary>Fresh evidence directly contradicts the record → CONTRADICTED.</summary>
    Contradicts,

    /// <summary>A newer record replaces the record → SUPERSEDED (pair link via <see cref="FreshEvidenceOutcome.ReplacementRecordId"/>).</summary>
    Supersedes,

    /// <summary>Fresh evidence invalidates the record → INVALIDATED.</summary>
    Invalidates,

    /// <summary>Fresh evidence ages/weakens the record → STALE.</summary>
    Stales,
}

/// <summary>
/// One fresh-evidence disposition request naming the OLD record to downgrade
/// (and, for supersession, the newly admitted replacement's RecordId so the
/// Supersedes/SupersededBy pair stays traceable). Construct exclusively via
/// the factory methods <see cref="Contradicts"/> / <see cref="Supersedes"/> /
/// <see cref="Invalidates"/> / <see cref="Stales"/>. Applied by
/// <see cref="ScenarioKnowledgeFixture.ApplyFreshEvidence"/>.
/// </summary>
public sealed record FreshEvidenceOutcome
{
    /// <summary>The existing (old) record to downgrade.</summary>
    public required ScenarioKnowledgeRecord Target { get; init; }

    /// <summary>Which fresh-evidence disposition to apply.</summary>
    public required FreshEvidenceAction Action { get; init; }

    /// <summary>RecordId of the newly admitted replacement (only for
    /// <see cref="FreshEvidenceAction.Supersedes"/>; becomes the target's
    /// SupersededBy link).</summary>
    public string? ReplacementRecordId { get; init; }

    /// <summary>The downgrade status this outcome produces on the target.</summary>
    internal KnowledgeStatus ResultingStatus => Action switch
    {
        FreshEvidenceAction.Contradicts => KnowledgeStatus.Contradicted,
        FreshEvidenceAction.Supersedes => KnowledgeStatus.Superseded,
        FreshEvidenceAction.Invalidates => KnowledgeStatus.Invalidated,
        FreshEvidenceAction.Stales => KnowledgeStatus.Stale,
        _ => throw new ArgumentOutOfRangeException(nameof(Action), Action, "Unknown fresh-evidence action."),
    };

    /// <summary>Fresh evidence contradicts the target record.</summary>
    public static FreshEvidenceOutcome Contradicts(ScenarioKnowledgeRecord target)
        => new() { Target = target, Action = FreshEvidenceAction.Contradicts };

    /// <summary>Fresh evidence supersedes the target record; when the
    /// replacement is already admitted, pass its RecordId to complete the
    /// Supersedes/SupersededBy pair.</summary>
    public static FreshEvidenceOutcome Supersedes(ScenarioKnowledgeRecord target, string? replacementRecordId = null)
        => new() { Target = target, Action = FreshEvidenceAction.Supersedes, ReplacementRecordId = replacementRecordId };

    /// <summary>Fresh evidence invalidates the target record.</summary>
    public static FreshEvidenceOutcome Invalidates(ScenarioKnowledgeRecord target)
        => new() { Target = target, Action = FreshEvidenceAction.Invalidates };

    /// <summary>Fresh evidence ages the target record to Stale.</summary>
    public static FreshEvidenceOutcome Stales(ScenarioKnowledgeRecord target)
        => new() { Target = target, Action = FreshEvidenceAction.Stales };
}