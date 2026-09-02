using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Feature boundary: converts an Observation into the semantic representation
/// consumed by the Semantic Perception pipeline (a
/// <see cref="ContainerSemanticQuery"/>). Its ONLY duty is representation.
/// It never embeds, never looks up prototypes, never thresholds, never accepts,
/// and never forms Runtime belief.
/// </summary>
public interface IContainerSemanticFeatureExtractor
{
    /// <summary>Extracts the semantic query representation for an observation.</summary>
    ContainerSemanticQuery Extract(Observation observation);
}