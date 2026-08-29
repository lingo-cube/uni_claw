using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// One declared freedom change inside a <see cref="PlanDelta"/> (spec "PlanDelta
/// contract"; design D5 closed freedom surface): the revised freedom, a
/// human-readable why, and the resolvable knowledge / evidence citations that
/// justify it. Citations are a HARD contract, not a soft warning: every knowledge
/// ref must resolve within the round's LoadedKnowledge ∪ NewKnowledge and every
/// evidence ref within the observed result's EvidenceRefs, enforced by
/// <see cref="PlanDeltaValidator"/>. A change revises directive levers only —
/// it never carries UI action sequences, coordinates, selectors, or paths.
/// Validation artifact; no field ever enters the wire.
/// </summary>
public sealed record PlanDeltaChange
{
    /// <summary>Create one declared freedom change with ≥1 knowledge and ≥1 evidence citations.</summary>
    public PlanDeltaChange(
        PlanDeltaFreedom freedom,
        string description,
        IReadOnlyList<string> knowledgeRefs,
        IReadOnlyList<string> evidenceRefs)
    {
        if (!Enum.IsDefined(freedom))
            throw new ArgumentOutOfRangeException(nameof(freedom));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(knowledgeRefs);
        ArgumentNullException.ThrowIfNull(evidenceRefs);
        if (knowledgeRefs.Count == 0)
            throw new ArgumentException("A plan delta change must cite at least one knowledge ref.", nameof(knowledgeRefs));
        if (evidenceRefs.Count == 0)
            throw new ArgumentException("A plan delta change must cite at least one evidence ref.", nameof(evidenceRefs));
        if (knowledgeRefs.Any(string.IsNullOrWhiteSpace) || evidenceRefs.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Knowledge and evidence refs must be non-empty strings.");

        Freedom = freedom;
        Description = description;
        KnowledgeRefs = knowledgeRefs.ToImmutableArray();
        EvidenceRefs = evidenceRefs.ToImmutableArray();
    }

    /// <summary>The single directive freedom this change revises.</summary>
    public PlanDeltaFreedom Freedom { get; }

    /// <summary>Human-readable why (recorded as evidence, never transported).</summary>
    public string Description { get; }

    /// <summary>Knowledge record ids this change builds on (≥1; must resolve in the round).</summary>
    public IReadOnlyList<string> KnowledgeRefs { get; }

    /// <summary>Evidence refs of the observed result this change builds on (≥1; must resolve in the round).</summary>
    public IReadOnlyList<string> EvidenceRefs { get; }
}