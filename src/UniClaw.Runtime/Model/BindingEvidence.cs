using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Structured binding evidence — the minimum typed representation between
/// BindingAnalysis and BindingReconciler.
///
/// Replaces the transitional string protocol where element indices were
/// encoded in SemanticEvidence.Reason as "element[N]" and extracted via regex.
///
/// Immutable, observation-local, no mutable state, no authority.
/// SemanticEvidence remains generic; Reason is diagnostic-only.
/// </summary>
public sealed record BindingEvidence(
    string ObjectIdentity,
    ImmutableArray<int> ElementIndices,
    SemanticEvidence Evidence)
{
    /// <summary>Structural equality — ImmutableArray&lt;T&gt; does not implement IEquatable&lt;T&gt;
    /// so the record-generated equality would use reference equality on the underlying array.</summary>
    public bool Equals(BindingEvidence? other)
        => other is not null
            && string.Equals(ObjectIdentity, other.ObjectIdentity, StringComparison.Ordinal)
            && ElementIndices.SequenceEqual(other.ElementIndices)
            && Evidence == other.Evidence;

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ObjectIdentity);
        foreach (var idx in ElementIndices)
            hash.Add(idx);
        hash.Add(Evidence);
        return hash.ToHashCode();
    }
}
