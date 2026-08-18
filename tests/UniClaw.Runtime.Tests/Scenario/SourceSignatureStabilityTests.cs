using System.Collections.Immutable;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_SOURCE_SIGNATURE_STABILITY — SIG-1..SIG-12.
///
/// Evidence-contract repair: the source-equivalence identity key is
/// TitleText | Class | ResourceId | ContentDescription — the RAW DESCRIPTIVE /
/// LIVE SummaryText is excluded (a live summary such as "38% used - 9.97 GB
/// free" changes between observations of the SAME logical source and breaks the
/// unique ordered-overlap chain). SummaryText stays raw evidence but never
/// creates or disambiguates identity: stable-key collisions remain AMBIGUOUS
/// and fail closed. The normalizer algorithm is untouched.
/// </summary>
public sealed class SourceSignatureStabilityTests
{
    private const string App = "com.uniclaw.fixture";

    private static StructuredElementEvidence Row(
        string title,
        string? summary,
        string resourceId = "com.uniclaw.fixture:id/row_title",
        string @class = "android.widget.LinearLayout",
        ElementBounds? bounds = null)
        => new(@class, resourceId, true, false, false, true, true,
            bounds ?? new ElementBounds(0, 0, 1, 0.1f),
            title, summary, false, null, null);

    private static Observation Obs(long seq, params StructuredElementEvidence[] rows)
        => new([], App, seq) { StructuredElements = rows.ToImmutableArray() };

    // ── SIG-1 / SIG-2: same source, live summary changes -> SAME_SOURCE ─────

    [Theory]
    [InlineData("Storage", "", "38% used - 9.97 GB free")]
    [InlineData("Battery", "Charged", "Battery saver on")]
    public void SIG12_LiveSummaryChange_SameSource(string title, string summaryA, string summaryB)
    {
        var v1 = Obs(1, Row(title, summaryA));
        var v2 = Obs(2, Row(title, summaryB));
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(normalization.IsResolved);
        Assert.Single(normalization.UniqueSourceSignatures);
        var occ1 = SourceEquivalenceNormalizer.OccurrencesOf(v1)[0];
        var occ2 = SourceEquivalenceNormalizer.OccurrencesOf(v2)[0];
        Assert.Equal(
            SourceGroundingValidator.TryResolveLogicalSource(occ1, normalization),
            SourceGroundingValidator.TryResolveLogicalSource(occ2, normalization));
    }

    // ── SIG-3: summary-only change -> logical source cardinality unchanged ──

    [Fact]
    public void SIG3_SummaryOnlyChange_CardinalityUnchanged()
    {
        var v1 = Obs(1, Row("Storage", ""), Row("Battery", "Charged"));
        var v2 = Obs(2, Row("Storage", "38% used - 9.97 GB free"), Row("Battery", "Charged"));
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(normalization.IsResolved);
        Assert.Equal(2, normalization.UniqueSourceSignatures.Length); // Storage + Battery, no inflation
    }

    // ── SIG-4: different TitleText -> distinct logical sources ──────────────

    [Fact]
    public void SIG4_DifferentTitle_DistinctSources()
    {
        var obs = Obs(1, Row("Network & internet", "x"), Row("Connected devices", "y"));
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(obs));

        Assert.True(normalization.IsResolved);
        Assert.Equal(2, normalization.UniqueSourceSignatures.Length);
    }

    // ── SIG-5 / SIG-6: stable-key collision in one viewport -> fail closed ──

    [Theory]
    [InlineData("same summary")]
    [InlineData("different summaries")]
    public void SIG56_StableSignatureCollision_AmbiguousUnresolved(string mode)
    {
        var obs = mode == "same summary"
            ? Obs(1, Row("Shared", "s"), Row("Shared", "s"))
            : Obs(1, Row("Shared", "s1"), Row("Shared", "s2"));
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(obs));

        // Summary cannot resolve identity ambiguity -> UNRESOLVED -> fail closed.
        Assert.False(normalization.IsResolved);
    }

    // ── SIG-7: bounds / node-path changes -> identity unaffected ────────────

    [Fact]
    public void SIG7_BoundsChange_IdentityUnaffected()
    {
        var v1 = Obs(1, Row("Storage", "", bounds: new ElementBounds(0, 0, 1, 0.1f)));
        var v2 = Obs(2, Row("Storage", "38% used - 9.97 GB free", bounds: new ElementBounds(0, 0.9f, 1, 1f)));
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(normalization.IsResolved);
        Assert.Single(normalization.UniqueSourceSignatures); // same logical source despite bounds change
    }

    // ── SIG-8: real Settings-style V1->V3 overlap with volatile Storage ─────

    [Fact]
    public void SIG8_RealSettingsStyleOverlap_VolatileSummary_Resolves()
    {
        var v1 = Obs(2,
            Row("Network & internet", "Mobile, Wi‑Fi, hotspot"),
            Row("Connected devices", "Bluetooth, pairing"),
            Row("Apps", "Recent apps, default apps"),
            Row("Notifications", "Notification history, conversations"),
            Row("Battery", "Charged"),
            Row("Storage", ""));
        var v3 = Obs(3,
            Row("Apps", "Recent apps, default apps"),
            Row("Notifications", "Notification history, conversations"),
            Row("Battery", "Charged"),
            Row("Storage", "38% used - 9.97 GB free"),   // volatile summary
            Row("Sound & vibration", "Volume, vibration, Do Not Disturb"),
            Row("Display", "Dark theme, font size, brightness"));

        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v3));

        // Unique ordered overlap of the STABLE keys: suffix [Apps, Notifications,
        // Battery, Storage] == prefix [Apps, Notifications, Battery, Storage].
        Assert.True(normalization.IsResolved);
        Assert.Equal(8, normalization.UniqueSourceSignatures.Length);
    }

    // ── SIG-9: SummaryText retained as raw evidence ─────────────────────────

    [Fact]
    public void SIG9_SummaryRetainedAsRawEvidence()
    {
        var raw = Row("Storage", "38% used - 9.97 GB free");
        Assert.Equal("38% used - 9.97 GB free", raw.SummaryText);

        var obs = Obs(1, raw);
        _ = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(obs));
        // After normalization the raw evidence still carries the summary.
        Assert.Equal("38% used - 9.97 GB free", obs.StructuredElements[0].SummaryText);
    }

    // ── SIG-10: COMPOSE-05 source normalization unchanged ───────────────────

    [Fact]
    public void SIG10_Compose05NormalizationUnchanged()
    {
        var top = Obs(2, Row("Child 01", null), Row("Child 02", null), Row("Child 03", null), Row("Child 04", null));
        var mid = Obs(3, Row("Child 03", null), Row("Child 04", null), Row("Child 05", null), Row("Child 06", null), Row("Child 07", null));
        var bottom = Obs(4, Row("Child 05", null), Row("Child 06", null), Row("Child 07", null), Row("Child 08", null));
        var terminal = Obs(5, Row("Child 05", null), Row("Child 06", null), Row("Child 07", null), Row("Child 08", null));

        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(top, mid, bottom, terminal));

        Assert.True(normalization.IsResolved);
        Assert.Equal(8, normalization.UniqueSourceSignatures.Length); // unchanged: 8 sources
    }

    // ── SIG-11 / SIG-12: regression + identity semantics unchanged ──────────
    // Covered by the full deterministic suite (PROV / ACCEPT / NM / RVT2 /
    // AFF / SET / CURRENT / U2 / identity-safety) — SIG-11; and ACCEPT-3
    // (BranchIdentity != destination identity) — SIG-12. See the gate result.
}
