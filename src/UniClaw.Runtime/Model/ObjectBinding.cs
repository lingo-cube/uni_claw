using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Immutable observation-local binding: which ObservedElements currently
/// instantiate a SemanticObject in the current Observation.
///
/// Multi-element binding allowed: a SemanticObject may be supported by
/// multiple ObservedElements (e.g., Wi‑Fi row text + toggle control).
///
/// ElementIndices reference ObservedElement.Index — observation-local only.
/// Bindings MUST be refreshed per observation. Index is NOT persistent identity.
///
/// This is Container-owned mutable state (I-2). Container holds the current
/// set of ObjectBindings and updates them on each fresh Observation.
/// </summary>
/// <param name="ObjectIdentity">The SemanticObject.Identity this binding is for.</param>
/// <param name="ElementIndices">ObservedElement indices that support this binding.</param>
/// <param name="EvidenceBasis">What evidence supports this binding (e.g. "TEXT+TYPE+SPATIAL").</param>
public sealed record ObjectBinding(
    string ObjectIdentity,
    ImmutableArray<int> ElementIndices,
    string EvidenceBasis);
