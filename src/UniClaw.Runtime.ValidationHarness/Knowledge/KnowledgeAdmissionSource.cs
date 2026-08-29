namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Admission-source vocabulary (spec requirement "ScenarioKnowledgeFixture as
/// a validation test asset" — "Forbidden knowledge sources are rejected";
/// design D3). CLOSED. ONLY <see cref="ObservedResult"/> is admissible; the
/// forbidden classes are first-class marker values so rejection is explicit,
/// testable, and never swallowed into a generic "invalid source" reason.
/// </summary>
public enum KnowledgeAdmissionSource
{
    /// <summary>
    /// The knowledge traces to an observed result (a run's Result/Evidence —
    /// a real observation with SourceRunId + EvidenceRefs). The ONLY
    /// admissible source; the provenance gate rejects everything else
    /// (spec "Provenance-gated admission").
    /// </summary>
    ObservedResult,

    /// <summary>FORBIDDEN: un-evidenced conjecture presented as knowledge.</summary>
    Guesswork,

    /// <summary>FORBIDDEN: hardcoded UI text treated as truth (never a
    /// runtime truth source).</summary>
    HardcodedTextAsTruth,

    /// <summary>FORBIDDEN: coordinates (pixel positions) as knowledge.</summary>
    Coordinates,

    /// <summary>FORBIDDEN: fixed page paths as knowledge.</summary>
    FixedPath,

    /// <summary>FORBIDDEN: selector scripts / locator scripts as knowledge.</summary>
    SelectorScript,

    /// <summary>FORBIDDEN: learning by probing/execution (trial-and-error) —
    /// dangerous classes are never learned by executing them
    /// (spec "Safety learning without dangerous trial-and-error"; design D4).</summary>
    ProbeByExecution,

    /// <summary>FORBIDDEN: assumptions about runtime internals as knowledge.</summary>
    RuntimeInternalAssumption,
}