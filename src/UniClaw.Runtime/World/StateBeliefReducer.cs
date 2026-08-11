using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Stateless pure function: Observation + ObjectBinding → state-belief proposals.
///
/// StateBeliefReducer owns the computation of object-state beliefs from
/// observation-local element SwitchState. It is NOT a state owner, NOT a truth
/// oracle, and NOT an Agent authority.
///
/// Container remains sole owner of _objectStateBeliefs mutable state (I-2).
/// The reducer only computes the next proposed value — Container atomically
/// applies it.
///
/// Rules:
///   - Exactly 1 toggle-type element with non-null SwitchState → belief populated
///   - 0 or ≥2 toggle candidates → null (UNKNOWN — safe, no fabrication)
///   - Unknown state is truthful, not a failure
/// </summary>
public static class StateBeliefReducer
{
    /// <summary>
    /// Computes object-state belief proposals from the current observation and bindings.
    ///
    /// Pure function: bindings + observation → state beliefs.
    /// Container owns the resulting mutable dictionary.
    /// </summary>
    /// <param name="observation">Fresh observation — belief is always observation-local.</param>
    /// <param name="bindings">Current object bindings.</param>
    /// <returns>State-belief proposals keyed by "{ObjectIdentity}.Enabled". May be empty.</returns>
    public static ImmutableDictionary<string, bool?> Reduce(
        Observation observation,
        ImmutableArray<ObjectBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var builder = ImmutableDictionary.CreateBuilder<string, bool?>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            var key = $"{binding.ObjectIdentity}.Enabled";
            var stateBearing = binding.ElementIndices
                .Select(idx => observation.Elements.FirstOrDefault(e => e.Index == idx))
                .Where(element => element?.SwitchState is not null
                    && string.Equals(element.PerceptionType, "toggle", StringComparison.Ordinal))
                .ToArray();

            // One and only one current toggle is sufficient evidence.
            // Missing or ambiguous surface → null (UNKNOWN), never a fabricated value.
            builder[key] = stateBearing.Length == 1 ? stateBearing[0]!.SwitchState : null;
        }

        return builder.ToImmutable();
    }
}
