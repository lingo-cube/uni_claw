using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Stateless pure function: BindingEvidence → ObjectBinding proposals.
///
/// BindingReconciler owns the reconciliation algorithm (aggregating
/// structured evidence into binding proposals). It is NOT a state owner,
/// NOT a truth oracle, and NOT an Agent authority.
///
/// Element indices come from BindingEvidence.ElementIndices — structured
/// and explicit. SemanticEvidence.Reason is diagnostic-only and is never
/// machine-parsed.
///
/// Container remains sole owner of _objectBindings mutable state (I-2).
/// The reconciler only computes the next proposed value.
/// </summary>
public static class BindingReconciler
{
    /// <summary>
    /// Reconciles structured binding evidence into ObjectBinding proposals.
    ///
    /// Pure function: evidence + known objects → bindings.
    /// Container owns the resulting mutable state.
    /// </summary>
    /// <param name="evidence">BindingEvidence from BindingAnalysis.Analyze.</param>
    /// <param name="knownObjects">Known SemanticObjects — scopes reconciliation to recognized objects.</param>
    /// <returns>Proposed ObjectBindings. May be empty.</returns>
    public static ImmutableArray<ObjectBinding> Reconcile(
        ImmutableArray<BindingEvidence> evidence,
        ImmutableArray<SemanticObject> knownObjects)
    {
        var bindings = ImmutableArray.CreateBuilder<ObjectBinding>();

        foreach (var obj in knownObjects)
        {
            var claim = $"binds to {obj.Identity}";
            var supportingEvidence = evidence
                .Where(e => e.Evidence.Claim == claim
                    && e.Evidence.Stance == SemanticEvidenceStance.Supports)
                .ToImmutableArray();

            if (supportingEvidence.Length == 0)
                continue;

            var indices = ImmutableArray.CreateBuilder<int>();
            var basisParts = ImmutableArray.CreateBuilder<string>();

            foreach (var be in supportingEvidence)
            {
                foreach (var idx in be.ElementIndices)
                {
                    if (!indices.Contains(idx))
                        indices.Add(idx);
                }
                if (!basisParts.Contains(be.Evidence.Source))
                    basisParts.Add(be.Evidence.Source);
            }

            if (indices.Count > 0)
            {
                bindings.Add(new ObjectBinding(
                    obj.Identity,
                    indices.ToImmutable(),
                    string.Join("+", basisParts)));
            }
        }

        return bindings.ToImmutable();
    }
}
