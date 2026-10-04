using System.Collections.Immutable;

namespace UniClaw.Kernel.Capability;

public enum TrustDomain { Product, RuntimeIntegration, Harness }
public enum CapabilityScope { ProductRuntime, RuntimeIntegration, Harness }
public enum HealthStatus { Unknown, Healthy, Degraded, Unhealthy }
public enum CapabilityLifecycle { Declared, Registered, Ready, Active, Draining, Closed, Failed, Quarantined }
public enum CapabilityCategory { Generic, CompositeProductPerception, IndependentProductPerception, Realization, Source, Adapter }
public enum CapabilityRoleKind { ProductProtocol, Realization, Source, Adapter }
public enum CapabilityRelationshipKind { Requires }

public static class PerceptionProtocol
{
    public const string Semantic = "Semantic Perception";
    public const string UiElement = "UI Element Perception";
    public const string Version = "1.0";
}

/// <summary>
/// Root interface for an executable capability instance. The Hub owns
/// lifecycle and health facts; an implementation only exposes its immutable
/// declaration through this seam.
/// </summary>
public interface ICapability
{
    CapabilityDescription Description { get; }
}

/// <summary>
/// Product protocol marker for semantic perception. Execution payloads remain
/// protocol-specific and are intentionally separate from Hub management.
/// </summary>
public interface ISemanticPerception : ICapability
{
}

/// <summary>
/// Product protocol marker for UI element perception. Execution payloads remain
/// protocol-specific and are intentionally separate from Hub management.
/// </summary>
public interface IUiElementPerception : ICapability
{
}

/// <summary>
/// Management-plane facade implemented by a trust-domain registry. It exposes
/// registration, discovery, executable resolution and lifecycle facts without
/// owning Product results or business authority.
/// </summary>
public interface ICapabilityHub
{
    TrustDomain Domain { get; }
    IReadOnlyList<CapabilityLifecycleFact> Facts { get; }
    CapabilityLifecycleFact Register(ICapability capability, string source);
    CapabilityDescription? Get(string capabilityId);
    ICapability? Resolve(string capabilityId);
    CapabilityLifecycleFact Commit(string capabilityId, CapabilityLifecycle next, string source);
}

public sealed record CapabilityRole
{
    public CapabilityRoleKind Kind { get; }
    public string Name { get; }
    public CapabilityRole(CapabilityRoleKind kind, string name) { Kind = kind; Name = Required(name, nameof(name)); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record CapabilityRelationship
{
    public CapabilityRelationshipKind Kind { get; }
    public string TargetCapabilityId { get; }
    public CapabilityRelationship(CapabilityRelationshipKind kind, string targetCapabilityId) { Kind = kind; TargetCapabilityId = Required(targetCapabilityId, nameof(targetCapabilityId)); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record CapabilityProtocol
{
    public string Name { get; }
    public string Version { get; }
    public CapabilityProtocol(string name, string version) { Name = Required(name, nameof(name)); Version = Required(version, nameof(version)); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record CapabilityDependency
{
    public string CapabilityId { get; }
    public string VersionRange { get; }
    public CapabilityDependency(string capabilityId, string versionRange) { CapabilityId = Required(capabilityId, nameof(capabilityId)); VersionRange = Required(versionRange, nameof(versionRange)); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record CapabilityDescription
{
    public string CapabilityId { get; }
    public string Version { get; }
    public CapabilityScope Scope { get; }
    public ImmutableArray<CapabilityProtocol> Protocols { get; }
    public ImmutableArray<CapabilityDependency> Dependencies { get; }
    public HealthStatus Health { get; }
    public CapabilityCategory Category { get; }
    public ImmutableArray<CapabilityRole> Roles { get; }
    public ImmutableArray<CapabilityRelationship> Relationships { get; }
    public CapabilityDescription(string capabilityId, string version, CapabilityScope scope, IEnumerable<CapabilityProtocol> protocols, IEnumerable<CapabilityDependency> dependencies, HealthStatus health)
        : this(capabilityId, version, scope, protocols, dependencies, health, CapabilityCategory.Generic, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>()) { }
    public CapabilityDescription(string capabilityId, string version, CapabilityScope scope, IEnumerable<CapabilityProtocol> protocols, IEnumerable<CapabilityDependency> dependencies, HealthStatus health, CapabilityCategory category, IEnumerable<CapabilityRole> roles, IEnumerable<CapabilityRelationship> relationships)
    { CapabilityId = Required(capabilityId, nameof(capabilityId)); Version = Required(version, nameof(version)); Scope = scope; Protocols = protocols?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(protocols)); Dependencies = dependencies?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(dependencies)); Health = health; Category = category; Roles = roles?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(roles)); Relationships = relationships?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(relationships)); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record CapabilityLifecycleFact(
    string CapabilityId, string Version, TrustDomain Domain,
    CapabilityLifecycle Lifecycle, string Source, long Sequence);

public sealed class CapabilityRegistry : ICapabilityHub
{
    private static readonly IReadOnlyDictionary<CapabilityLifecycle, ImmutableHashSet<CapabilityLifecycle>> Allowed =
        new Dictionary<CapabilityLifecycle, ImmutableHashSet<CapabilityLifecycle>>
        {
            [CapabilityLifecycle.Declared] = [CapabilityLifecycle.Registered, CapabilityLifecycle.Failed, CapabilityLifecycle.Quarantined],
            [CapabilityLifecycle.Registered] = [CapabilityLifecycle.Ready, CapabilityLifecycle.Failed, CapabilityLifecycle.Quarantined],
            [CapabilityLifecycle.Ready] = [CapabilityLifecycle.Active, CapabilityLifecycle.Draining, CapabilityLifecycle.Failed, CapabilityLifecycle.Quarantined],
            [CapabilityLifecycle.Active] = [CapabilityLifecycle.Draining, CapabilityLifecycle.Failed, CapabilityLifecycle.Quarantined],
            [CapabilityLifecycle.Draining] = [CapabilityLifecycle.Closed, CapabilityLifecycle.Failed, CapabilityLifecycle.Quarantined],
            [CapabilityLifecycle.Failed] = [CapabilityLifecycle.Quarantined, CapabilityLifecycle.Closed],
            [CapabilityLifecycle.Quarantined] = [CapabilityLifecycle.Closed],
            [CapabilityLifecycle.Closed] = []
        };
    private readonly Dictionary<string, (CapabilityDescription Description, CapabilityLifecycle State, ICapability? Instance)> entries = new(StringComparer.Ordinal);
    private readonly List<CapabilityLifecycleFact> facts = new();
    public TrustDomain Domain { get; }
    public IReadOnlyList<CapabilityLifecycleFact> Facts => facts.AsReadOnly();

    public CapabilityRegistry(TrustDomain domain) => Domain = domain;

    public CapabilityLifecycleFact Register(ICapability capability, string source)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ValidateImplementation(capability);
        return RegisterCore(capability.Description, capability, source);
    }

    public CapabilityLifecycleFact Register(CapabilityDescription description, string source)
    {
        return RegisterCore(description, instance: null, source);
    }

    private CapabilityLifecycleFact RegisterCore(CapabilityDescription description, ICapability? instance, string source)
    {
        ArgumentNullException.ThrowIfNull(description);
        ValidateSource(source);
        if (!MatchesDomain(description.Scope)) throw new ArgumentException("Capability scope crosses registry trust domain.", nameof(description));
        ValidateDescription(description);
        if (!entries.TryAdd(description.CapabilityId, (description, CapabilityLifecycle.Registered, instance)))
            throw new InvalidOperationException($"Capability '{description.CapabilityId}' is already registered.");
        return Publish(description, CapabilityLifecycle.Registered, source);
    }

    public CapabilityDescription? Get(string capabilityId) => entries.TryGetValue(capabilityId, out var entry) ? entry.Description : null;

    public ICapability? Resolve(string capabilityId) => entries.TryGetValue(capabilityId, out var entry) ? entry.Instance : null;

    public CapabilityLifecycleFact Commit(string capabilityId, CapabilityLifecycle next, string source)
    {
        ValidateSource(source);
        if (!entries.TryGetValue(capabilityId, out var entry)) throw new KeyNotFoundException(capabilityId);
        if (!Allowed[entry.State].Contains(next)) throw new InvalidOperationException($"Illegal lifecycle transition {entry.State} -> {next}.");
        entries[capabilityId] = (entry.Description, next, entry.Instance);
        return Publish(entry.Description, next, source);
    }

    private CapabilityLifecycleFact Publish(CapabilityDescription description, CapabilityLifecycle state, string source)
    {
        var fact = new CapabilityLifecycleFact(description.CapabilityId, description.Version, Domain, state, source, facts.Count + 1L);
        facts.Add(fact);
        return fact;
    }
    private void ValidateSource(string source) { if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("A lifecycle fact source is required.", nameof(source)); }
    private static void ValidateDescription(CapabilityDescription description)
    {
        if (description.Category is not (CapabilityCategory.CompositeProductPerception or CapabilityCategory.IndependentProductPerception)) return;
        if (description.Scope != CapabilityScope.ProductRuntime) throw new ArgumentException("Product perception must use the product scope.", nameof(description));
        if (description.Protocols.Any(p => p.Version != PerceptionProtocol.Version)) throw new ArgumentException("Unsupported perception protocol version.", nameof(description));
        var supportedProtocols = new HashSet<string>(StringComparer.Ordinal)
        {
            PerceptionProtocol.Semantic,
            PerceptionProtocol.UiElement,
        };
        if (description.Protocols.Any(p => !supportedProtocols.Contains(p.Name)))
            throw new ArgumentException("Product perception declares an unsupported protocol.", nameof(description));
        if (description.Protocols.GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Duplicate perception protocols are not allowed.", nameof(description));
        if (description.Roles.GroupBy(r => $"{r.Kind}:{r.Name}", StringComparer.Ordinal).Any(g => g.Count() > 1)) throw new ArgumentException("Duplicate capability roles are not allowed.", nameof(description));
        if (description.Roles.Any(r => r.Kind is CapabilityRoleKind.Source or CapabilityRoleKind.Adapter)) throw new ArgumentException("Product perception cannot claim source or adapter role.", nameof(description));
        var protocolNames = description.Protocols.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var role in description.Roles.Where(r => r.Kind == CapabilityRoleKind.ProductProtocol))
            if (!protocolNames.Contains(role.Name)) throw new ArgumentException("Product protocol role must match a declared protocol.", nameof(description));
        var dependencyIds = description.Dependencies.Select(d => d.CapabilityId).ToHashSet(StringComparer.Ordinal);
        var relationshipTargets = description.Relationships
            .Where(r => r.Kind == CapabilityRelationshipKind.Requires)
            .Select(r => r.TargetCapabilityId)
            .ToHashSet(StringComparer.Ordinal);
        if (!relationshipTargets.SetEquals(dependencyIds))
            throw new ArgumentException("Perception relationships must explicitly mirror declared dependencies.", nameof(description));
        if (description.Category == CapabilityCategory.CompositeProductPerception && !protocolNames.Contains(PerceptionProtocol.Semantic))
            throw new ArgumentException("Composite perception must declare Semantic Perception.", nameof(description));
    }

    private static void ValidateImplementation(ICapability capability)
    {
        var description = capability.Description ?? throw new ArgumentException("Capability description is required.", nameof(capability));
        var protocolNames = description.Protocols.Select(protocol => protocol.Name).ToHashSet(StringComparer.Ordinal);
        var semantic = capability is ISemanticPerception;
        var uiElement = capability is IUiElementPerception;
        var declaresSemantic = protocolNames.Contains(PerceptionProtocol.Semantic);
        var declaresUiElement = protocolNames.Contains(PerceptionProtocol.UiElement);

        if (description.Category is CapabilityCategory.CompositeProductPerception or CapabilityCategory.IndependentProductPerception)
        {
            if (semantic != declaresSemantic)
                throw new ArgumentException("Semantic Perception declaration does not match the capability interface.", nameof(capability));
            if (uiElement != declaresUiElement)
                throw new ArgumentException("UI Element Perception declaration does not match the capability interface.", nameof(capability));
        }
        else if (semantic || uiElement)
        {
            throw new ArgumentException("Product perception interfaces require a product perception category.", nameof(capability));
        }
    }
    private bool MatchesDomain(CapabilityScope scope) => Domain switch
    {
        TrustDomain.Product => scope == CapabilityScope.ProductRuntime,
        TrustDomain.RuntimeIntegration => scope == CapabilityScope.RuntimeIntegration,
        TrustDomain.Harness => scope == CapabilityScope.Harness,
        _ => false
    };
}
