using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Caller-provided semantic RECOGNITION KNOWLEDGE — NOT a semantic verdict.
///
/// PageAnalysisCriteria expresses "what signals indicate what pages" so that
/// <see cref="World.PageAnalysis"/> can produce multi-source, falsifiable
/// <see cref="SemanticEvidence"/> from a fresh <see cref="Observation"/>.
///
/// This is KNOWLEDGE (what to look for), not VERDICT (what page this IS).
/// The caller provides criteria; PageAnalysis applies them to produce evidence;
/// Container fuses evidence into belief; the external world remains authoritative (I-4).
///
/// Evidence Claim Granularity rule:
///   FOREGROUND evidence supports "application scope is X" — NOT "page is Y".
///   TEXT_ANCHOR evidence supports "page is X" only when page-specific anchors are present.
///   Weak evidence must not manufacture strong semantic claims.
/// </summary>
/// <param name="ExpectedForegroundApplication">
/// Expected foreground application identifier (e.g. "com.android.settings").
/// Produces FOREGROUND evidence about application scope, not page identity.
/// </param>
/// <param name="PageAnchors">
/// Per-page positive text anchors: page name → anchor texts that SUPPORT that page.
/// Absence of ALL anchors for a page produces Insufficient (not Contradicts).
/// </param>
/// <param name="PageNegativeAnchors">
/// Per-page negative text anchors: page name → anchor texts whose presence CONTRADICTS that page.
/// Optional. When a negative anchor is found, a CONTRADICTS stance is produced.
/// </param>
/// <param name="PageSwitchStateAnchors">
/// Per-page SwitchState-bearing anchors: page name → texts expected to carry SwitchState.
/// Optional. Presence of SwitchState on the named text produces SWITCH_DISTRIBUTION evidence.
/// </param>
public sealed record PageAnalysisCriteria(
    string? ExpectedForegroundApplication,
    ImmutableDictionary<string, ImmutableArray<string>> PageAnchors,
    ImmutableDictionary<string, ImmutableArray<string>>? PageNegativeAnchors = null,
    ImmutableDictionary<string, ImmutableArray<string>>? PageSwitchStateAnchors = null)
{
    /// <summary>Creates criteria with foreground app and page anchors only (minimum viable).</summary>
    public PageAnalysisCriteria(
        string? expectedForegroundApplication,
        ImmutableDictionary<string, ImmutableArray<string>> pageAnchors)
        : this(expectedForegroundApplication, pageAnchors, null, null)
    {
    }
}
