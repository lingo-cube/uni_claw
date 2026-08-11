using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>
/// Observation-scoped, stateless, evidence-producing page semantic capability.
///
/// PageAnalysis is a PURE FUNCTION — NOT a state owner, NOT a truth oracle,
/// NOT a continuity verifier, NOT a transition verifier.
///
/// Given a fresh <see cref="Observation"/> and caller-provided recognition criteria
/// (<see cref="PageAnalysisCriteria"/> — knowledge, not verdict), produces
/// multi-source <see cref="SemanticEvidence"/> about page identity.
///
/// Evidence Claim Granularity:
///   Each evidence source supports only the claim its observable signal justifies.
///   FOREGROUND → "application scope is X" (NOT "page is Y").
///   TEXT_ANCHOR → "page is X" only when page-specific anchors are present.
///   SWITCH_DISTRIBUTION → "page is X" only when SwitchState-bearing anchors match.
///   TEXT_ANCHOR_NEGATIVE → "page is X" contradicted when excluding anchors appear.
///
/// Same input → same output (deterministic, no hidden state, no randomness).
/// Follows the <see cref="Reconcile"/> pattern (stateless pure function).
/// </summary>
public static class PageAnalysis
{
    /// <summary>
    /// Produces source-attributed SemanticEvidence from a fresh Observation
    /// using caller-provided recognition criteria.
    ///
    /// Evidence sources produced (when criteria are provided):
    ///   FOREGROUND           — application scope match
    ///   TEXT_ANCHOR           — per-page positive text anchor presence
    ///   TEXT_ANCHOR_NEGATIVE  — per-page negative text anchor presence
    ///   SWITCH_DISTRIBUTION   — per-page SwitchState-bearing anchor presence
    ///
    /// All sources are independent. Sources can disagree.
    /// No single source is authoritative.
    /// </summary>
    /// <param name="observation">Fresh observation (evidence, not truth — I-4).</param>
    /// <param name="criteria">Caller-provided recognition knowledge (NOT verdict).</param>
    /// <returns>Multi-source SemanticEvidence. Never null; may be empty if no criteria match.</returns>
    /// <exception cref="ArgumentNullException">observation or criteria is null.</exception>
    public static ImmutableArray<SemanticEvidence> Analyze(
        Observation observation,
        PageAnalysisCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(criteria);

        var evidence = ImmutableArray.CreateBuilder<SemanticEvidence>();

        AddForegroundEvidence(evidence, observation, criteria);
        AddTextAnchorEvidence(evidence, observation, criteria);
        AddNegativeAnchorEvidence(evidence, observation, criteria);
        AddSwitchDistributionEvidence(evidence, observation, criteria);

        return evidence.ToImmutable();
    }

    /// <summary>
    /// FOREGROUND source: application scope evidence.
    /// Claim = "application scope is {ExpectedForegroundApplication}" — NOT "page is X".
    /// Weak signal → weak claim. Obey claim granularity.
    /// </summary>
    private static void AddForegroundEvidence(
        ImmutableArray<SemanticEvidence>.Builder evidence,
        Observation observation,
        PageAnalysisCriteria criteria)
    {
        if (criteria.ExpectedForegroundApplication is null)
            return;

        var matches = string.Equals(
            observation.ForegroundApplication,
            criteria.ExpectedForegroundApplication,
            StringComparison.Ordinal);

        evidence.Add(new SemanticEvidence(
            "FOREGROUND",
            $"application scope is {criteria.ExpectedForegroundApplication}",
            matches ? SemanticEvidenceStance.Supports : SemanticEvidenceStance.Contradicts,
            $"ForegroundApplication={observation.ForegroundApplication ?? "null"}"));
    }

    /// <summary>
    /// TEXT_ANCHOR source: per-page positive text anchor evidence.
    /// Claim = "page is {pageName}" — only when page-specific anchors are present.
    /// If some (but not all) anchors for a page are present → Supports.
    /// If NO anchors for a page are present → Insufficient (NOT Contradicts — absence ≠ contradiction).
    /// </summary>
    private static void AddTextAnchorEvidence(
        ImmutableArray<SemanticEvidence>.Builder evidence,
        Observation observation,
        PageAnalysisCriteria criteria)
    {
        foreach (var (pageName, anchors) in criteria.PageAnchors)
        {
            var presentAnchors = anchors
                .Where(a => observation.Elements.Any(
                    e => string.Equals(e.Text, a, StringComparison.Ordinal)))
                .ToImmutableArray();

            if (presentAnchors.Length > 0)
            {
                evidence.Add(new SemanticEvidence(
                    "TEXT_ANCHOR",
                    $"page is {pageName}",
                    SemanticEvidenceStance.Supports,
                    $"anchors present: [{string.Join(", ", presentAnchors)}]"));
            }
            else
            {
                evidence.Add(new SemanticEvidence(
                    "TEXT_ANCHOR",
                    $"page is {pageName}",
                    SemanticEvidenceStance.Insufficient,
                    $"no expected anchors present; expected: [{string.Join(", ", anchors)}]"));
            }
        }
    }

    /// <summary>
    /// TEXT_ANCHOR_NEGATIVE source: per-page contradicting text anchors.
    /// Claim = "page is {pageName}" — contradicted when a negative anchor text appears.
    /// Use for texts that SHOULD NOT be present on a specific page.
    /// </summary>
    private static void AddNegativeAnchorEvidence(
        ImmutableArray<SemanticEvidence>.Builder evidence,
        Observation observation,
        PageAnalysisCriteria criteria)
    {
        if (criteria.PageNegativeAnchors is null)
            return;

        foreach (var (pageName, negativeAnchors) in criteria.PageNegativeAnchors)
        {
            var contradictingAnchors = negativeAnchors
                .Where(a => observation.Elements.Any(
                    e => string.Equals(e.Text, a, StringComparison.Ordinal)))
                .ToImmutableArray();

            if (contradictingAnchors.Length > 0)
            {
                evidence.Add(new SemanticEvidence(
                    "TEXT_ANCHOR_NEGATIVE",
                    $"page is {pageName}",
                    SemanticEvidenceStance.Contradicts,
                    $"negative anchors present: [{string.Join(", ", contradictingAnchors)}]"));
            }
        }
    }

    /// <summary>
    /// SWITCH_DISTRIBUTION source: per-page SwitchState-bearing anchor evidence.
    /// Claim = "page is {pageName}" — supported when a SwitchState-bearing anchor matches.
    /// SwitchState-bearing elements are strong page indicators (e.g., Wi‑Fi switch on WifiPage).
    /// </summary>
    private static void AddSwitchDistributionEvidence(
        ImmutableArray<SemanticEvidence>.Builder evidence,
        Observation observation,
        PageAnalysisCriteria criteria)
    {
        if (criteria.PageSwitchStateAnchors is null)
            return;

        foreach (var (pageName, switchAnchors) in criteria.PageSwitchStateAnchors)
        {
            var switchBearingAnchors = switchAnchors
                .Where(a => observation.Elements.Any(
                    e => string.Equals(e.Text, a, StringComparison.Ordinal)
                        && e.SwitchState is not null))
                .ToImmutableArray();

            if (switchBearingAnchors.Length > 0)
            {
                evidence.Add(new SemanticEvidence(
                    "SWITCH_DISTRIBUTION",
                    $"page is {pageName}",
                    SemanticEvidenceStance.Supports,
                    $"SwitchState-bearing anchors present: [{string.Join(", ", switchBearingAnchors)}]"));
            }
        }
    }
}
