using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Prototype boundary: the canonical semantic representation of a known
/// Container Identity (text fragments + element types + structural features),
/// plus the optional embedding vector derived from the representation.
///
/// A prototype is owned by the <see cref="IContainerIdentityPrototypeStore"/>
/// — never by a vector index. The vector index only REFERENCES prototype
/// vectors for nearest-candidate retrieval; canonical semantic meaning lives in
/// the prototype store.
/// </summary>
public sealed record ContainerIdentityPrototype
{
    /// <summary>The identity this prototype represents, e.g. "DeveloperOptions".</summary>
    public string IdentityCandidate { get; }

    /// <summary>Stable prototype id, e.g. "prototype:dev:v1:title".</summary>
    public string PrototypeId { get; }

    /// <summary>Canonical text fragments of the identity representation.</summary>
    public ImmutableArray<string> TextFragments { get; }

    /// <summary>Canonical element types of the identity representation.</summary>
    public ImmutableArray<string> ElementTypes { get; }

    /// <summary>Canonical structural features (type:/switch: markers).</summary>
    public ImmutableArray<string> StructuralFeatures { get; }

    /// <summary>Representation version, e.g. "v1".</summary>
    public string Version { get; }

    /// <summary>Profile reference this prototype belongs to, e.g. "v1-canonical-signatures".</summary>
    public string ProfileRef { get; }

    /// <summary>Optional precomputed embedding vector (set when an embedding provider exists).</summary>
    public EmbeddingVector? Vector { get; init; }

    /// <summary>Creates a container identity prototype.</summary>
    public ContainerIdentityPrototype(
        string identityCandidate,
        string prototypeId,
        ImmutableArray<string> textFragments,
        ImmutableArray<string> elementTypes,
        ImmutableArray<string> structuralFeatures,
        string version = "v1",
        string profileRef = "v1-canonical-signatures")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityCandidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);
        IdentityCandidate = identityCandidate;
        PrototypeId = prototypeId;
        TextFragments = textFragments.IsDefault ? ImmutableArray<string>.Empty : textFragments;
        ElementTypes = elementTypes.IsDefault ? ImmutableArray<string>.Empty : elementTypes;
        StructuralFeatures = structuralFeatures.IsDefault ? ImmutableArray<string>.Empty : structuralFeatures;
        Version = version;
        ProfileRef = profileRef;
    }
}