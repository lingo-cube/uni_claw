namespace UniClaw.Runtime.ValidationHarness.Classification;

/// <summary>
/// The protocol failure taxonomy (design D6; .ai/development-protocol.md §17.2)
/// as a FIXED eight-owner set. Every scenario failure is labelled with exactly
/// one owner plus a First Divergence Point; a bare "Runtime failed" conclusion
/// is impossible by type design (the constructor of
/// <see cref="ProtocolFailureClassification"/> requires both an owner and a
/// non-blank First Divergence Point).
/// </summary>
public enum FailureOwner
{
    /// <summary>The strategy could not be compiled into a legal directive:
    /// closed-vocabulary / shape violations refused before transport,
    /// deterministic admission Reject(code), or a missing directive.</summary>
    StrategyCompilation = 0,

    /// <summary>Scope discovery diverged: at terminal the coverage ledger
    /// still carries unresolved / unknown-frontier scope (the runtime could
    /// not complete discovery of the declared scope).</summary>
    Discovery = 1,

    /// <summary>Grounding diverged: the runtime's world belief and the
    /// observed reality parted (stale belief, belief/reality mismatch).</summary>
    Grounding = 2,

    /// <summary>Authorization diverged: a forbidden effect / action was
    /// attempted or denied by the runtime.</summary>
    Authorization = 3,

    /// <summary>Execution failed: the run terminated through the existing
    /// RunFailed path with a recorded failure reason (settle/transition
    /// failure etc.).</summary>
    Execution = 4,

    /// <summary>The trap / recovery path diverged: a trap was raised or a
    /// recovery started and the run still failed. Also carries the
    /// BLOCKED_FOR_SPEC workflow-stop marker (S2) as classification metadata
    /// only — that marker is never a runtime failure.</summary>
    Recovery = 5,

    /// <summary>The device environment diverged: popup, external boundary,
    /// unexpected navigation, unclassifiable node — the runtime acted on an
    /// environment that no longer matched its belief.</summary>
    Environment = 6,

    /// <summary>The validation harness itself diverged (tooling-side, never
    /// the runtime): transport failure, missing-directive input, a driver
    /// call outside the frozen method, an unresolvable harness-side
    /// condition.</summary>
    TestHarness = 7,
}