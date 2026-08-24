using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>
/// Input for Container Identity validation (Phase 1 reservation). Combines Text
/// Evidence + Semantic Evidence + Current Observation for Runtime Identity
/// Validation. This is an input contract only — no resolver behavior here.
/// </summary>
public sealed record ContainerIdentityFusionInput
{
    /// <summary>Text evidence from the current observation (What exists?).</summary>
    public ImmutableArray<ObservedElement> TextEvidence { get; }

    /// <summary>SemanticEvidence candidates (What might this mean?).</summary>
    public ImmutableArray<SemanticEvidence> SemanticEvidence { get; }

    /// <summary>The current observation.</summary>
    public Observation CurrentObservation { get; }

    /// <summary>Creates a container identity fusion input.</summary>
    public ContainerIdentityFusionInput(
        Observation currentObservation,
        ImmutableArray<ObservedElement>? textEvidence = null,
        ImmutableArray<SemanticEvidence>? semanticEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(currentObservation);
        CurrentObservation = currentObservation;
        TextEvidence = textEvidence ?? currentObservation.Elements;
        SemanticEvidence = semanticEvidence ?? ImmutableArray<SemanticEvidence>.Empty;
    }
}

/// <summary>
/// Container Identity validation result: accepted/rejected SemanticEvidence only.
/// It does NOT replace ContainerIdentityResolver / CreateMultiPageResolver and it
/// does NOT produce a Fact. Runtime Identity Validation is the owner.
/// </summary>
public sealed record ContainerIdentityValidationResult
{
    /// <summary>SemanticEvidence accepted for identity consideration.</summary>
    public ImmutableArray<SemanticEvidence> AcceptedEvidence { get; }

    /// <summary>SemanticEvidence rejected for identity consideration.</summary>
    public ImmutableArray<SemanticEvidence> RejectedEvidence { get; }

    /// <summary>Creates a container identity validation result.</summary>
    public ContainerIdentityValidationResult(
        ImmutableArray<SemanticEvidence> acceptedEvidence,
        ImmutableArray<SemanticEvidence> rejectedEvidence)
    {
        AcceptedEvidence = acceptedEvidence;
        RejectedEvidence = rejectedEvidence;
    }
}

/// <summary>
/// RESERVED Phase 1 container identity evidence fusion port. Declares the
/// Text Evidence + Semantic Evidence → Runtime Identity Validation seam. No
/// production implementation is provided and no existing resolver
/// (CreateMultiPageResolver / ContainerIdentityResolver) is replaced.
/// </summary>
public interface IContainerIdentityEvidenceFusion
{
    /// <summary>Validates semantic container identity evidence candidates.</summary>
    ContainerIdentityValidationResult Validate(ContainerIdentityFusionInput input);
}
