using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// The four frozen invariant ids, verbatim from the change principles (Phase
/// 2.6, spec "Four frozen invariants"). Each is asserted PER RUN with evidence
/// refs drawn from that run's own outputs — a prior round's conclusion is never
/// reused.
/// </summary>
public static class CampaignInvariantIds
{
    /// <summary>Historical (fixture/loaded) knowledge is never current world truth;
    /// fresh runtime evidence always wins.</summary>
    public const string HistoricalKnowledgeNotCurrentWorldTruth = "HISTORICAL_KNOWLEDGE != CURRENT_WORLD_TRUTH";

    /// <summary>A prior Result never authorizes actions in the current run; the
    /// fresh directive alone is dispatch authority.</summary>
    public const string HistoricalResultNotRuntimeActionAuthority = "HISTORICAL_RESULT != RUNTIME_ACTION_AUTHORITY";

    /// <summary>Runtime completion (terminal runState) and validation scenario
    /// acceptance are distinct verdicts — never merged into one field.</summary>
    public const string RuntimeCompletedNotValidationScenarioPass = "RUNTIME_COMPLETED != VALIDATION_SCENARIO_PASS";

    /// <summary>An autonomous exception disposition (trap/failure) is never
    /// reported as universal recovery; the harness makes no recovery claim.</summary>
    public const string AutonomousExceptionDispositionNotUniversalRecovery = "AUTONOMOUS_EXCEPTION_DISPOSITION != UNIVERSAL_RECOVERY";
}

/// <summary>
/// One named four-invariant assertion outcome for a single run (Phase 2.6, spec
/// "Four frozen invariants"): pass/fail, the invariant id, the evidence
/// references the runner derived from THIS round's own result and call log, and
/// an optional reason (populated on failure with the offending evidence).
/// </summary>
public sealed record InvariantAssertion(
    bool Passed,
    string InvariantId,
    ImmutableArray<string> EvidenceRefs,
    string? Reason);