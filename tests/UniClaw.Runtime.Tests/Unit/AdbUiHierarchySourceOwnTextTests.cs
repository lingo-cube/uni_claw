using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// CAPSTONE_CHILD_PAGE_EVIDENCE_GATE — TXT-1..TXT-6.
///
/// AdbUiHierarchySource own-text acquisition repair: the node's OWN non-empty
/// `text` attribute is legitimate RAW structured evidence for TitleText (the
/// ADB own-text gap: android.widget.Button text="Fixture Root" carried its text
/// as an attribute, never a descendant, so TitleText was empty). Precedence is
/// fixed, deterministic, never inferred/OCR, never concatenated:
///   android:id/title (self-or-descendant) → own text → first descendant text → empty.
/// TXT-7..TXT-10 (Settings evidence, normalization, provenance, revisit) are
/// covered by the existing deterministic suites (no regression).
/// </summary>
public sealed class AdbUiHierarchySourceOwnTextTests
{
    private const int Width = 1080;
    private const int Height = 1920;

    private static string Node(
        string cls,
        string text,
        bool clickable,
        string resourceId = "",
        bool checkable = false,
        string contentDesc = "")
        => $"<node index=\"0\" text=\"{text}\" resource-id=\"{resourceId}\" class=\"{cls}\" "
           + $"package=\"com.uniclaw.fixture\" content-desc=\"{contentDesc}\" checkable=\"{checkable.ToString().ToLowerInvariant()}\" "
           + $"checked=\"false\" clickable=\"{clickable.ToString().ToLowerInvariant()}\" enabled=\"true\" focusable=\"false\" "
           + $"focused=\"false\" scrollable=\"false\" long-clickable=\"false\" password=\"false\" selected=\"false\" "
           + "bounds=\"[48,300][1032,444]\"/>";

    private static ImmutableArray<StructuredElementEvidence> Parse(params string[] nodes)
    {
        var xml = $"<?xml version='1.0' encoding='UTF-8' standalone='yes' ?><hierarchy rotation=\"0\">"
                  + string.Join("", nodes)
                  + "</hierarchy>";
        return AdbUiHierarchySource.Parse(xml, Width, Height);
    }

    // ── TXT-1: own text is captured ─────────────────────────────────────────

    [Fact]
    public void TXT1_OwnText_Captured()
    {
        var parsed = Parse(Node("android.widget.Button", "Fixture Root", clickable: true));

        Assert.Single(parsed);
        Assert.Equal("Fixture Root", parsed[0].TitleText);
        Assert.Equal("android.widget.Button", parsed[0].Class);
    }

    // ── TXT-2: empty own text must not fabricate a title ────────────────────

    [Fact]
    public void TXT2_EmptyOwnText_NoFabricatedTitle()
    {
        var parsed = Parse(Node("android.widget.Button", "", clickable: true));

        Assert.Single(parsed);
        Assert.Null(parsed[0].TitleText);
    }

    // ── TXT-3: android:id/title descendant compatibility (existing behavior) ─

    [Fact]
    public void TXT3_AndroidIdTitleDescendant_Preserved()
    {
        var parsed = Parse(
            "<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
            + "package=\"com.uniclaw.fixture\" content-desc=\"\" checkable=\"false\" checked=\"false\" "
            + "clickable=\"true\" enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" "
            + "long-clickable=\"false\" password=\"false\" selected=\"false\" bounds=\"[0,366][1080,786]\">"
            + Node("android.widget.TextView", "Child 05", clickable: false, resourceId: "android:id/title")
            + "</node>");

        Assert.Single(parsed);
        Assert.Equal("Child 05", parsed[0].TitleText);
    }

    // ── TXT-4: ordinary descendant text fallback (existing behavior) ────────

    [Fact]
    public void TXT4_OrdinaryDescendantText_Preserved()
    {
        var parsed = Parse(
            "<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
            + "package=\"com.uniclaw.fixture\" content-desc=\"\" checkable=\"false\" checked=\"false\" "
            + "clickable=\"true\" enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" "
            + "long-clickable=\"false\" password=\"false\" selected=\"false\" bounds=\"[0,366][1080,786]\">"
            + Node("android.widget.TextView", "Plain child text", clickable: false)
            + "</node>");

        Assert.Single(parsed);
        Assert.Equal("Plain child text", parsed[0].TitleText);
    }

    // ── TXT-5: own + descendant collision — fixed precedence, no concatenation ──

    [Fact]
    public void TXT5a_OwnTextPlusAndroidIdTitle_AndroidIdTitleWins()
    {
        // Own text AND an android:id/title descendant: android:id/title wins
        // (existing precedence), never concatenated.
        var parsed = Parse(
            "<node index=\"0\" text=\"Own Label\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
            + "package=\"com.uniclaw.fixture\" content-desc=\"\" checkable=\"false\" checked=\"false\" "
            + "clickable=\"true\" enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" "
            + "long-clickable=\"false\" password=\"false\" selected=\"false\" bounds=\"[0,366][1080,786]\">"
            + Node("android.widget.TextView", "Child 05", clickable: false, resourceId: "android:id/title")
            + "</node>");

        Assert.Single(parsed);
        Assert.Equal("Child 05", parsed[0].TitleText);
    }

    [Fact]
    public void TXT5b_OwnTextPlusPlainDescendant_OwnTextWins()
    {
        // Own text AND a plain descendant (no android:id/title): own text wins;
        // never concatenated.
        var parsed = Parse(
            "<node index=\"0\" text=\"Fixture Root\" resource-id=\"\" class=\"android.widget.Button\" "
            + "package=\"com.uniclaw.fixture\" content-desc=\"\" checkable=\"false\" checked=\"false\" "
            + "clickable=\"true\" enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" "
            + "long-clickable=\"false\" password=\"false\" selected=\"false\" bounds=\"[48,300][1032,444]\">"
            + Node("android.widget.TextView", "unrelated descendant", clickable: false)
            + "</node>");

        Assert.Single(parsed);
        Assert.Equal("Fixture Root", parsed[0].TitleText);
    }

    // ── TXT-6: own-text fix must not alter any other evidence field ─────────

    [Fact]
    public void TXT6_OtherEvidenceFieldsUntouched()
    {
        var parsed = Parse(Node(
            "android.widget.Button",
            "Fixture Root",
            clickable: true,
            resourceId: "com.uniclaw.fixture:id/return_button",
            checkable: false,
            contentDesc: "go back"));

        Assert.Single(parsed);
        var evidence = parsed[0];
        Assert.Equal("android.widget.Button", evidence.Class);
        Assert.Equal("com.uniclaw.fixture:id/return_button", evidence.ResourceId);
        Assert.True(evidence.Clickable);
        Assert.False(evidence.Checkable);
        Assert.Equal("go back", evidence.ContentDescription);
        Assert.Equal("0/0", evidence.SourceNodeIdentity); // hierarchy(0) / node(0)
        Assert.NotNull(evidence.Bounds);
        Assert.Equal(48f / Width, evidence.Bounds!.X1);
        Assert.Equal(300f / Height, evidence.Bounds!.Y1);
        Assert.Equal(1032f / Width, evidence.Bounds!.X2);
        Assert.Equal(444f / Height, evidence.Bounds!.Y2);
        Assert.Equal("Fixture Root", evidence.TitleText);
    }
}
