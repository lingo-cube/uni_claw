using System.Collections.Immutable;
using System.Xml.Linq;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// SETTINGS_SOURCE_ROLE_STABILITY — ROLE-1..ROLE-10.
///
/// Verifies the parser's title extraction excludes explicit summary-role
/// descendants (android:id/summary) from the title fallback, so a live
/// descriptive summary ("38% used - 9.97 GB free") is never promoted to
/// TitleText when the title descendant is temporarily missing (RecyclerView
/// mid-fling). SummaryText is captured normally; the StableSourceSignature
/// and normalizer are unchanged.
/// </summary>
public sealed class SourceRoleStabilityTests
{
    private const int W = 1080, H = 1920;

    // Minimal XML helper: one node with optional children
    private static string Row(string ownText, string titleIdText, string summaryId, string summaryText, string childText)
    {
        var children = "";
        if (titleIdText != null)
            children += $"<node index=\"0\" text=\"{titleIdText}\" resource-id=\"android:id/title\" class=\"android.widget.TextView\" "
                       + "content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"false\" enabled=\"true\" focusable=\"false\" "
                       + "bounds=\"[0,0][1080,200]\"/>";
        if (summaryId != null)
            children += $"<node index=\"1\" text=\"{summaryText}\" resource-id=\"{summaryId}\" class=\"android.widget.TextView\" "
                       + "content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"false\" enabled=\"true\" focusable=\"false\" "
                       + "bounds=\"[0,200][1080,400]\"/>";
        if (childText != null)
            children += $"<node index=\"2\" text=\"{childText}\" resource-id=\"\" class=\"android.widget.TextView\" "
                       + "content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"false\" enabled=\"true\" focusable=\"false\" "
                       + "bounds=\"[0,400][1080,600]\"/>";
        return $"<node index=\"0\" text=\"{ownText}\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
             + "content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"true\" enabled=\"true\" focusable=\"true\" "
             + $"bounds=\"[0,0][1080,600]\">{children}</node>";
    }

    private static ImmutableArray<StructuredElementEvidence> Parse(string nodeXml)
    {
        var xml = $"<?xml version='1.0' encoding='UTF-8' standalone='yes' ?><hierarchy rotation=\"0\">{nodeXml}</hierarchy>";
        return AdbUiHierarchySource.Parse(xml, W, H);
    }

    // ── ROLE-1: title present + summary present -> TitleText = title, SummaryText = summary ──
    [Fact]
    public void ROLE1_TitlePresent_SummaryPresent_TitleTextIsTitle()
    {
        var parsed = Parse(Row("", "Storage", "android:id/summary", "38% used - 9.97 GB free", ""));
        Assert.Single(parsed);
        Assert.Equal("Storage", parsed[0].TitleText);
        Assert.Equal("38% used - 9.97 GB free", parsed[0].SummaryText);
    }

    // ── ROLE-2: title missing + summary present -> TitleText must NOT equal summary ──
    [Fact]
    public void ROLE2_TitleMissing_SummaryPresent_TitleTextNotSummary()
    {
        var parsed = Parse(Row("", null, "android:id/summary", "38% used - 9.97 GB free", ""));
        Assert.Single(parsed);
        // The summary must NOT leak into TitleText; the row has no title -> TitleText is null.
        Assert.Null(parsed[0].TitleText);
        Assert.Equal("38% used - 9.97 GB free", parsed[0].SummaryText);
    }

    // ── ROLE-3: legitimate own text fallback preserved ──
    [Fact]
    public void ROLE3_OwnTextFallback_Preserved()
    {
        var parsed = Parse(Row("Fixture Root", null, null, "", ""));
        Assert.Single(parsed);
        Assert.Equal("Fixture Root", parsed[0].TitleText);
    }

    // ── ROLE-4: legitimate ordinary descendant fallback preserved ──
    [Fact]
    public void ROLE4_OrdinaryDescendantFallback_Preserved()
    {
        var parsed = Parse(Row("", null, null, "", "Ordinary child text"));
        Assert.Single(parsed);
        Assert.Equal("Ordinary child text", parsed[0].TitleText);
    }

    // ── ROLE-5: explicit summary descendant excluded from the title fallback ──
    [Fact]
    public void ROLE5_SummaryDescendant_ExcludedFromTitleFallback()
    {
        // Title missing, summary present, non-summary descendant present:
        // the non-summary descendant should be the fallback.
        var parsed = Parse(Row("", null, "android:id/summary", "38% used - 9.97 GB free", "Ordinary text"));
        Assert.Single(parsed);
        Assert.Equal("Ordinary text", parsed[0].TitleText);  // non-summary descendant wins
        Assert.Equal("38% used - 9.97 GB free", parsed[0].SummaryText);
    }

    // ── ROLE-6: summary mutation does not change source identity ──
    [Fact]
    public void ROLE6_SummaryMutation_DoesNotChangeSourceIdentity()
    {
        var v1 = new Observation([], "com.android.settings", 1)
        {
            StructuredElements = ImmutableArray.Create(
                new StructuredElementEvidence("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                    true, false, false, true, true, new ElementBounds(0, 0, 1, 0.2f),
                    "Storage", "38% used", false, null, null)),
        };
        var v2 = new Observation([], "com.android.settings", 2)
        {
            StructuredElements = ImmutableArray.Create(
                new StructuredElementEvidence("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                    true, false, false, true, true, new ElementBounds(0, 0, 1, 0.2f),
                    "Storage", "42% used", false, null, null)),
        };
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));
        Assert.True(normalization.IsResolved);
        Assert.Single(normalization.UniqueSourceSignatures); // SAME_SOURCE despite summary change
    }

    // ── ROLE-7: insufficient evidence cannot fabricate title ──
    [Fact]
    public void ROLE7_InsufficientEvidence_NoFabricatedTitle()
    {
        var parsed = Parse(Row("", null, null, "", ""));
        Assert.Single(parsed);
        Assert.Null(parsed[0].TitleText); // no title, no own text, no descendants -> null
    }

    // ── ROLE-8: COMPOSE-05 unchanged ──
    [Fact]
    public void ROLE8_Compose05_Unchanged()
    {
        var top = new Observation([], "com.uniclaw.fixture", 2)
        {
            StructuredElements = ImmutableArray.Create(
                new StructuredElementEvidence("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                    true, false, false, true, true, new ElementBounds(0, 0, 1, 0.1f), "Child 01", null, false, null, null),
                new StructuredElementEvidence("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                    true, false, false, true, true, new ElementBounds(0, 0.1f, 1, 0.2f), "Child 02", null, false, null, null)),
        };
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(top));
        Assert.True(normalization.IsResolved);
        Assert.Equal(2, normalization.UniqueSourceSignatures.Length);
    }

    // ── ROLE-9: SIG/SEARCH/SQ/PROV/NM/RVT/AFF/SET green — covered by the full suite ──

    // ── ROLE-10: stable-signature ambiguity still fail-closed ──
    [Fact]
    public void ROLE10_StableSignatureAmbiguity_FailClosed()
    {
        var obs = new Observation([], "com.android.settings", 1)
        {
            StructuredElements = ImmutableArray.Create(
                new StructuredElementEvidence("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                    true, false, false, true, true, new ElementBounds(0, 0, 1, 0.1f), "Shared", null, false, null, null),
                new StructuredElementEvidence("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                    true, false, false, true, true, new ElementBounds(0, 0.1f, 1, 0.2f), "Shared", null, false, null, null)),
        };
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(obs));
        Assert.False(normalization.IsResolved);
    }
}