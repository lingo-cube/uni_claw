namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>Result of an evidence-sufficiency assessment — an explainable, deterministic
/// breakdown of why the observation is (in)sufficient to support an identity
/// claim. Insufficient → the policy ABSTAINS.</summary>
public sealed record EvidenceAssessment(
    int TotalEvidenceCount,
    int NonGenericTextCount,
    int DiscriminativeAnchorCount,
    int StructuralSignalCount,
    bool IsSufficient,
    string? Reason)
{
    /// <summary>Insufficient because the observation has no text evidence (near-empty).</summary>
    public static EvidenceAssessment NearEmpty() =>
        new(0, 0, 0, 0, false, "near-empty: no text fragments");

    /// <summary>Insufficient with an explainable reason.</summary>
    public static EvidenceAssessment Insufficient(int total, int nonGeneric, int anchors, int structural, string reason) =>
        new(total, nonGeneric, anchors, structural, false, reason);

    /// <summary>Sufficient with the assessed counts.</summary>
    public static EvidenceAssessment Sufficient(int total, int nonGeneric, int anchors, int structural) =>
        new(total, nonGeneric, anchors, structural, true, null);
}