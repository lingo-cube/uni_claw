using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Feature boundary implementation: Observation → ContainerSemanticQuery.
/// Pure representation — no embedding, no prototype lookup, no threshold, no
/// acceptance, no Runtime belief.
/// </summary>
public sealed class FastSemanticFeatureExtractor : IContainerSemanticFeatureExtractor
{
    /// <inheritdoc />
    public ContainerSemanticQuery Extract(Observation observation)
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