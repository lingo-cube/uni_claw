using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Extracts a <see cref="ContainerSemanticQuery"/> from an Observation.
/// This is the deterministic Fast Semantic feature extraction step.
/// </summary>
public static class FastSemanticFeatureExtractor
{
    /// <summary>Builds a semantic query from an observation's visible elements.</summary>
    public static ContainerSemanticQuery Extract(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var text = observation.Elements
            .Select(e => e.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToImmutableArray();

        var types = observation.Elements
            .Select(e => e.PerceptionType)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        var structural = ImmutableArray.CreateBuilder<string>();
        foreach (var element in observation.Elements)
        {
            if (element.PerceptionType is not null)
            {
                structural.Add($"type:{element.PerceptionType}");
            }

            if (element.SwitchState is { } state)
            {
                structural.Add($"switch:{state}");
            }
        }

        return new ContainerSemanticQuery(
            observation.Elements,
            types,
            text,
            structural.ToImmutable());
    }
}