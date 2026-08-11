using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Observation-scoped, stateless element-to-object binding evidence producer.
///
/// BindingAnalysis is a PURE FUNCTION — NOT a state owner, NOT a truth oracle.
/// It produces SemanticEvidence about which ObservedElements may instantiate
/// which SemanticObjects in the current Observation.
///
/// Binding signals used (minimum):
///   TEXT_IDENTITY    — element Text matches object's expected text anchor
///   PERCEPTION_TYPE  — element PerceptionType matches object's expected control type
///   SPATIAL_RELATION — elements share same-row / proximity relation
///
/// Binding comes from COMBINED evidence, not any single signal.
/// Follows the PageAnalysis pattern (stateless pure function).
/// </summary>
public static class BindingAnalysis
{
    /// <summary>
    /// Produces structured BindingEvidence about object bindings from an Observation.
    /// ElementIndices are explicit — Reason is diagnostic prose only.
    /// </summary>
    /// <param name="observation">Fresh observation.</param>
    /// <param name="criteria">Caller-provided binding recognition criteria.</param>
    /// <returns>Structured binding evidence with explicit element indices.</returns>
    public static ImmutableArray<BindingEvidence> Analyze(
        Observation observation,
        ElementBindingCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(criteria);

        var evidence = ImmutableArray.CreateBuilder<BindingEvidence>();

        foreach (var obj in criteria.KnownObjects)
        {
            var textAnchor = criteria.ObjectTextAnchors?.GetValueOrDefault(obj.Identity);
            var controlType = criteria.ObjectControlTypes?.GetValueOrDefault(obj.Identity);

            // Find primary candidates: elements matching the text anchor
            var primaryCandidates = FindByText(observation, textAnchor);

            // Find control candidates: elements matching the control type
            var controlCandidates = FindByType(observation, controlType);

            if (primaryCandidates.Length == 0)
            {
                evidence.Add(new BindingEvidence(
                    obj.Identity,
                    [],
                    new SemanticEvidence(
                        "TEXT_IDENTITY",
                        $"binds to {obj.Identity}",
                        SemanticEvidenceStance.Insufficient,
                        $"no element matching text anchor '{textAnchor}'")));
                continue;
            }

            foreach (var primary in primaryCandidates)
            {
                // Primary candidate alone: partial evidence
                evidence.Add(new BindingEvidence(
                    obj.Identity,
                    [primary.Index],
                    new SemanticEvidence(
                        "TEXT_IDENTITY",
                        $"binds to {obj.Identity}",
                        SemanticEvidenceStance.Supports,
                        $"text='{primary.Text}' matches anchor '{textAnchor}'")));

                // Check for associated control on same row
                if (controlType is not null && primary.Bounds is not null)
                {
                    foreach (var control in controlCandidates)
                    {
                        if (control.Bounds is not null && SameRow(primary.Bounds, control.Bounds))
                        {
                            evidence.Add(new BindingEvidence(
                                obj.Identity,
                                [primary.Index, control.Index],
                                new SemanticEvidence(
                                    "SPATIAL_RELATION",
                                    $"binds to {obj.Identity}",
                                    SemanticEvidenceStance.Supports,
                                    $"(type={controlType}) share same row")));
                        }
                    }
                }
            }
        }

        return evidence.ToImmutable();
    }

    private static ImmutableArray<ObservedElement> FindByText(
        Observation observation, string? textAnchor)
    {
        if (textAnchor is null)
            return [];
        return observation.Elements
            .Where(e => string.Equals(e.Text, textAnchor, StringComparison.Ordinal))
            .ToImmutableArray();
    }

    private static ImmutableArray<ObservedElement> FindByType(
        Observation observation, string? controlType)
    {
        if (controlType is null)
            return [];
        return observation.Elements
            .Where(e => string.Equals(e.PerceptionType, controlType, StringComparison.Ordinal))
            .ToImmutableArray();
    }

    /// <summary>
    /// Two elements share the same row if their vertical ranges overlap.
    /// Pure spatial predicate — no geometry engine.
    /// </summary>
    public static bool SameRow(ElementBounds a, ElementBounds b)
    {
        // Vertical overlap: a's top is above b's bottom AND b's top is above a's bottom
        return a.Y1 <= b.Y2 && b.Y1 <= a.Y2;
    }
}
