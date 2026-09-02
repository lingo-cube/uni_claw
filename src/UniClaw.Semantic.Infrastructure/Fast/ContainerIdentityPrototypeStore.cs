using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Prototype store contract: Identity → one or more prototypes. The store OWNS
/// the known identity representations (canonical semantic meaning). Retrieval
/// backends read from it; they never own prototypes.
/// </summary>
public interface IContainerIdentityPrototypeStore
{
    /// <summary>Prototype profile version held by this store.</summary>
    string ProfileVersion { get; }

    /// <summary>All prototypes in the store.</summary>
    IReadOnlyList<ContainerIdentityPrototype> All();

    /// <summary>Prototypes for one identity (empty when unknown).</summary>
    IReadOnlyList<ContainerIdentityPrototype> Resolve(string identity);
}

/// <summary>
/// Immutable in-memory prototype store. Built from
/// <see cref="SemanticPattern"/> seeds (legacy representation) or from
/// <see cref="ContainerIdentityPrototype"/> values directly.
/// </summary>
public sealed class ContainerIdentityPrototypeStore : IContainerIdentityPrototypeStore
{
    private readonly IReadOnlyList<ContainerIdentityPrototype> _prototypes;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ContainerIdentityPrototype>> _byIdentity;

    /// <inheritdoc />
    public string ProfileVersion { get; }

    /// <summary>Creates an empty store.</summary>
    public ContainerIdentityPrototypeStore(string profileVersion = "v1")
        : this(ImmutableArray<ContainerIdentityPrototype>.Empty, profileVersion)
    {
    }

    /// <summary>Creates a store from prototypes.</summary>
    public ContainerIdentityPrototypeStore(
        IReadOnlyList<ContainerIdentityPrototype> prototypes,
        string profileVersion = "v1")
    {
        _prototypes = prototypes ?? Array.Empty<ContainerIdentityPrototype>();
        ProfileVersion = profileVersion;
        _byIdentity = _prototypes
            .GroupBy(p => p.IdentityCandidate, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ContainerIdentityPrototype>)new ReadOnlyCollection<ContainerIdentityPrototype>(g.ToList()));
    }

    /// <summary>
    /// Compatibility constructor: converts legacy <see cref="SemanticPattern"/>
    /// seeds (used by the legacy reference matcher path) into prototypes.
    /// </summary>
    public static ContainerIdentityPrototypeStore FromSemanticPatterns(
        ImmutableArray<SemanticPattern> patterns,
        string version = "v1",
        string profileRef = "legacy-patterns-v1")
    {
        var prototypes = ImmutableArray.CreateBuilder<ContainerIdentityPrototype>(patterns.Length);
        for (var i = 0; i < patterns.Length; i++)
        {
            var pattern = patterns[i];
            prototypes.Add(new ContainerIdentityPrototype(
                pattern.IdentityCandidate,
                pattern.PatternReference,
                pattern.TextFragments,
                pattern.ElementTypes,
                pattern.StructuralFeatures,
                version,
                profileRef));
        }

        return new ContainerIdentityPrototypeStore(prototypes.ToImmutable(), profileRef);
    }

    /// <inheritdoc />
    public IReadOnlyList<ContainerIdentityPrototype> All() => _prototypes;

    /// <inheritdoc />
    public IReadOnlyList<ContainerIdentityPrototype> Resolve(string identity)
        => _byIdentity.TryGetValue(identity, out var list) ? list : Array.Empty<ContainerIdentityPrototype>();
}