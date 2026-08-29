using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using UniClaw.Semantic.Settings;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// WI-VEC-0 — STOP-2 deterministic RED derivation (test-authoring only; zero production
/// change). Change: openspec/changes/runtime-viewport-exhaustion-confirmation.
///
/// ════════════════════════════════ DIAGNOSIS (frozen from evidence) ════════════════════════════════
///
/// Evidence: openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/
/// G-stage-a/reentry/run6-frames.json (12 frames; seq 1,2,4,5,7,8,10,11,13,14,16,18).
/// Reconstruction (public surface only): each frame → Observation (elements = fused
/// ObservedElement(text, null, idx, bounds, perceptionType: 'menu_item'|'text_block'|'input');
/// structured = StructuredElementEvidence rows; Sources = correlated PrimaryVision +
/// AuxiliaryStructured) → SemanticObservationFactProjector.Project →
/// SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync →
/// AdmittedSemanticEvidenceSnapshot(batch.Accepted) → SourceEquivalenceNormalizer.Normalize.
///
/// 1. REAL accepted subset reproducing Unresolved (today's extension-only contract):
///    subset (a) DECISION FRAMES [2,5,8,11,14,16,18] → IsResolved == false. Also
///    (c)/(d)/(e); (b)/(e) fail one step earlier at the initial frame.
/// 2. REAL signature sequences (primary-filtered, per decision frame):
///      seq 2  [N&I|menu_item, Connected devices|menu_item, Apps|menu_item,
///              Notifications|menu_item, Battery|menu_item, Storage|menu_item]
///      seq 5  [.. seq2 .., Sound & vibration|menu_item]
///      seq 8  [Apps|menu_item, Notifications|menu_item, Battery|menu_item,
///              Storage|menu_item, Sound & vibration|menu_item, Display|menu_item,
///              Wallpaper|menu_item, Accessibility|menu_item]
///      seq 11 [Battery|menu_item, Storage|menu_item, Sound & vibration|menu_item,
///              Display|menu_item, Wallpaper|menu_item, Accessibility|menu_item,
///              Security & privacy|menu_item, Location|menu_item]
///      seq 14 [Sound & vibration|text_block, Sound & vibration|text_block,   ← DUPLICATE
///              Display|text_block, Wallpaper|menu_item, Accessibility|menu_item,
///              Security & privacy|menu_item, Location|menu_item]
///      seq 16 [Wallpaper|menu_item, Accessibility|menu_item, Security & privacy|menu_item,
///              Location|menu_item, Safety & emergency|menu_item,
///              Passwords, passkeys & accounts|menu_item, System|menu_item,
///              About emulated device|menu_item]
///      seq 18 == seq 16 (byte-identical terminal pair)
///    (Structured LinearLayout rows also match, but with sources declared the
///    analyzer keeps only authorization-eligible primary occurrences.)
/// 3. EXACT old-contract failure condition: sequence 14 contains the in-frame
///    duplicate 'Sound & vibration|text_block' admitted TWICE — frames 13/14 are the
///    roll-over/sub-page transition frames where ONE logical row is rendered as two
///    corroborated text_block occurrences (title+caption fused separately; the
///    Settings capability corroborates each against the same structured row). The old
///    contract fails closed: "Observation 14 contains duplicate structured navigation
///    signatures; equivalence is ambiguous." Subsets (b)/(e) fail one frame earlier:
///    "Observation 1 has no structured navigation candidates." (initial '女' frame —
///    no admitted navigation occurrence).
/// 4. Signature divergence vs the menu-text-only simulation (the gate erratum):
///    (i) ALL admitted NavigationCandidate occurrences enter the sequence, including
///        corroborated text_block rows — the menu-only idealization hides frames
///        13/14's duplicate entirely; (ii) the union-overlap machinery never sees the
///        terminal pair, because the chain dies 3 windows earlier at the duplicate.
/// 5. Canonical STOP-2 shape ("extend×5 → identical zero-motion terminal pair" with
///    clean distinct signatures) RESOLVES on today's code (empirical probe P1): the
///    terminal pair *is* the unique union-tail suffix, so FindUniqueSuffixPrefixOverlap
///    returns the full unique overlap and Normalize resolves with zero new sources.
///    ⇒ The leader's recorded mechanism ("union-overlap absent on the terminal pair")
///    is CONFIRMED NOT REPRODUCIBLE — the frozen erratum holds even on the real
///    signature extraction. The old contract is red on run-6 only via the frames-13/14
///    duplicate (or the empty initial frame), mechanisms the new contract's own
///    confirmation conditions (spec condition (e), no-in-frame-duplicates) PRESERVE
///    as fail-closed.
///
/// SUBSET MATRIX (real admission, today's code): (a) false; (b) false (obs 1);
/// (c) false (obs 1); (d) false (obs 13); (e) false (obs 1). All five Unresolved —
/// never via the terminal pair.
///
/// FROZEN ABSTRACTION (text-free): M1..M16 = distinct menu rows; T1 (double
/// occurrence) and T2 = corroborated text_block rows; same window count/order/index
/// structure as the decided frames above. Deterministic: no device/network/time
/// dependency (UnixEpoch), no Settings text, no coordinates, no click counts.
///
/// The confirmation change classifies the identical tail pair; it does NOT change
/// IsResolved for any input in this file (empirical). These tests lock the OLD
/// contract's red state on the TRUE run-6 mechanism (WI-VEC-0 step 3) and record the
/// erratum-confirming control. The flippable "old red → new green" reproducer (task
/// I.5) is not constructible from the archived evidence — flagged to the leader
/// (§12 stop condition review; see WI-VEC-0 report matrix).
/// </summary>
public sealed class ViewportExhaustionConfirmationRedTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static ObservedElement Menu(string text, int index) =>
        new(text, null, index, new ElementBounds(0.1f, 0.1f + index * 0.05f, 0.9f, 0.14f + index * 0.05f), "menu_item");

    private static ObservedElement TextBlock(string text, int index) =>
        new(text, null, index, new ElementBounds(0.1f, 0.1f + index * 0.05f, 0.9f, 0.14f + index * 0.05f), "text_block");

    private static StructuredElementEvidence Row(string text) =>
        new(Class: "android.widget.LinearLayout", ResourceId: null, Clickable: true, Checkable: null, Checked: null,
            Enabled: true, Focusable: true, Bounds: null, ContentDescription: null, RawText: text);

    private static Observation Frame(long seq, ImmutableArray<ObservedElement> elements,
        ImmutableArray<StructuredElementEvidence>? structured = null) =>
        new(elements, "fixture", seq)
        {
            StructuredElements = structured ?? [],
            Sources =
            [
                new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, seq, $"frame-{seq}", 1080, 1920, "perception-fusion", "vision"),
                new ObservationSourceMetadata(ObservationSourceTier.AuxiliaryStructured, true, seq, $"frame-{seq}", 1080, 1920, "uiautomator", "adb"),
            ],
        };

    /// <summary>Real capability admission path: Project → Runtime → AdmittedSemanticEvidenceSnapshot.</summary>
    private static async Task<Observation> AdmitAsync(Observation raw)
    {
        var projected = SemanticObservationFactProjector.Project(raw);
        var batch = await new SemanticCapabilityRuntime(new SettingsSemanticCapability()).EvaluateAsync(
            projected, projected.Observation, projected.Sources, Now);
        return raw with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(batch.Accepted) };
    }

    /// <summary>
    /// Decision-frame chain [2,5,8,11,14,16,18] abstracted from run-6 (frame-14 shape
    /// carries the double-admitted corroborated text_block → in-frame duplicate).
    /// </summary>
    private static async Task<ImmutableArray<Observation>> Run6DecisionChainAsync()
    {
        var f2 = await AdmitAsync(Frame(2, [Menu("M1", 0), Menu("M2", 1), Menu("M3", 2), Menu("M4", 3), Menu("M5", 4), Menu("M6", 5)]));
        var f5 = await AdmitAsync(Frame(5, [Menu("M1", 0), Menu("M2", 1), Menu("M3", 2), Menu("M4", 3), Menu("M5", 4), Menu("M6", 5), Menu("M7", 6)]));
        var f8 = await AdmitAsync(Frame(8, [Menu("M3", 0), Menu("M4", 1), Menu("M5", 2), Menu("M6", 3), Menu("M7", 4), Menu("M8", 5), Menu("M9", 6), Menu("M10", 7)]));
        var f11 = await AdmitAsync(Frame(11, [Menu("M5", 0), Menu("M6", 1), Menu("M7", 2), Menu("M8", 3), Menu("M9", 4), Menu("M10", 5), Menu("M11", 6), Menu("M12", 7)]));
        var f14 = await AdmitAsync(Frame(14,
            [TextBlock("T1", 0), TextBlock("T1", 1), TextBlock("T2", 2), Menu("M9", 3), Menu("M10", 4), Menu("M11", 5), Menu("M12", 6)],
            [Row("T1"), Row("T2")]));
        var f16 = await AdmitAsync(Frame(16, [Menu("M9", 0), Menu("M10", 1), Menu("M11", 2), Menu("M12", 3), Menu("M13", 4), Menu("M14", 5), Menu("M15", 6), Menu("M16", 7)]));
        var f18 = await AdmitAsync(Frame(18, [Menu("M9", 0), Menu("M10", 1), Menu("M11", 2), Menu("M12", 3), Menu("M13", 4), Menu("M14", 5), Menu("M15", 6), Menu("M16", 7)]));
        return [f2, f5, f8, f11, f14, f16, f18];
    }

    // ── WI-VEC-0 step 3: frozen mechanism lock ────────────────────────────────────

    /// <summary>
    /// THE real run-6 exhaustion chain (decision frames, real admission) is unresolved
    /// under the old extension-only contract. The precise failing condition is the
    /// in-frame duplicate admitted at the frame-14-shaped window: the same logical row
    /// corroborated TWICE as text_block occurrences (
    /// "contains duplicate structured navigation signatures; equivalence is ambiguous").
    /// With the initial frame included (subsets b/e) the failure surfaces one window
    /// earlier: the initial frame admits no navigation occurrence at all.
    /// </summary>
    [Fact]
    public async Task Run6DecisionChain_RealSignatures_IsUnresolved_UnderExtensionOnlyContract()
    {
        var chain = await Run6DecisionChainAsync();

        var result = SourceEquivalenceNormalizer.Normalize(chain);
        Assert.False(result.IsResolved);

        // Mechanism lock: the failing frame-14-shaped window carries an in-frame
        // duplicate navigation signature (the double-admitted corroborated text_block).
        var frameSigs = SourceEquivalenceNormalizer.OccurrencesOf(chain[4])
            .Select(o => o.StructuredSignature)
            .ToImmutableArray();
        Assert.Contains(frameSigs.GroupBy(s => s, StringComparer.Ordinal), g => g.Count() > 1);
        Assert.Contains(frameSigs, s => s.StartsWith("T1|text_block|", StringComparison.Ordinal));

        // The extending windows before the failing window contain no duplicates.
        Assert.All(chain.Take(4), o =>
            Assert.Equal(OccurrencesOfDistinctCount(o), SourceEquivalenceNormalizer.OccurrencesOf(o).Length));
    }

    private static int OccurrencesOfDistinctCount(Observation o) =>
        SourceEquivalenceNormalizer.OccurrencesOf(o).Select(x => x.StructuredSignature).Distinct(StringComparer.Ordinal).Count();

    // ── WI-VEC-0 step 3: required control ─────────────────────────────────────────

    /// <summary>
    /// Control (protocol step 3): the extending-only prefix [2,5,8,11] of the same
    /// chain RESOLVES — every window is a clean unique suffix(union)↔prefix(window)
    /// extension. This proves the Unresolved above is introduced specifically by the
    /// final-window SHAPE (the double-admitted corroborated text_block window), not by
    /// the preceding extensions — and that the menu-only idealized simulation of run-6
    /// (the gate erratum) has no failing window anywhere in this chain.
    /// </summary>
    [Fact]
    public async Task ExtendingOnlyPrefix_Resolves_Control()
    {
        var chain = await Run6DecisionChainAsync();

        var prefix = SourceEquivalenceNormalizer.Normalize(chain.Take(4).ToImmutableArray());

        Assert.True(prefix.IsResolved);
        Assert.Equal(12, prefix.UniqueSourceSignatures.Length);
    }

    // ── Erratum record: canonical STOP-2 shape resolves on the old contract ───────

    /// <summary>
    /// The canonical STOP-2 shape (extend ×5 → byte-identical zero-motion terminal
    /// pair, clean distinct signatures) RESOLVES on today's code: the terminal window
    /// is exactly the unique suffix of the accumulated union, so the old overlap
    /// machinery returns the full unique overlap and Normalize resolves with zero new
    /// sources. This is the frozen-gate erratum, now confirmed empirically on the old
    /// public surface: the leader's "union-overlap absent on the terminal pair"
    /// attribution is NOT the run-6 mechanism; run-6's deterministic red is the
    /// frames-13/14 duplicate locked by the test above.
    /// </summary>
    [Fact]
    public async Task CanonicalStop2Shape_TerminalPair_ResolvesOnOldContract_ErratumConfirmed()
    {
        var w1 = await AdmitAsync(Frame(1, [Menu("M1", 0), Menu("M2", 1)]));
        var w2 = await AdmitAsync(Frame(2, [Menu("M1", 0), Menu("M2", 1), Menu("M3", 2)]));
        var w3 = await AdmitAsync(Frame(3, [Menu("M2", 0), Menu("M3", 1), Menu("M4", 2)]));
        var w4 = await AdmitAsync(Frame(4, [Menu("M3", 0), Menu("M4", 1), Menu("M5", 2), Menu("M6", 3)]));
        var w5 = await AdmitAsync(Frame(5, [Menu("M4", 0), Menu("M5", 1), Menu("M6", 2), Menu("M7", 3), Menu("M8", 4)]));
        var w6 = await AdmitAsync(Frame(6, [Menu("M5", 0), Menu("M6", 1), Menu("M7", 2), Menu("M8", 3), Menu("M9", 4), Menu("M10", 5)]));
        var w7 = await AdmitAsync(Frame(7, [Menu("M5", 0), Menu("M6", 1), Menu("M7", 2), Menu("M8", 3), Menu("M9", 4), Menu("M10", 5)]));

        var result = SourceEquivalenceNormalizer.Normalize([w1, w2, w3, w4, w5, w6, w7]);

        Assert.True(result.IsResolved);
        Assert.Equal(10, result.UniqueSourceSignatures.Length);
    }
}