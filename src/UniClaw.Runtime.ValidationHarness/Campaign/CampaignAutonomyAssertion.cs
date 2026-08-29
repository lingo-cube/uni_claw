using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// Per-round autonomy assertion (Phase 2.6, spec "Every run is autonomous and
/// independent"): exactly one accepted <c>run.strategy.start</c> in THIS round's
/// own call-log slice, with zero driver/wire control calls after admission.
/// Re-derived per round from the round's slice — never carried over from a
/// prior round.
/// </summary>
public sealed record CampaignAutonomyAssertion(
    bool Passed,
    int AcceptedStartCount,
    int EntriesAfterAdmission,
    ImmutableArray<string> EvidenceRefs,
    string? OffendingEvidence);