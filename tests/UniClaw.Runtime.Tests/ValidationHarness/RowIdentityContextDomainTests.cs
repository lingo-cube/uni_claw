using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.SettingsCampaign;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// STABLEKEY_CONTAINER_DOMAIN minimal repair (PROJECT_LEADER gate): stable row
/// keys keep their candidate-correlation role but their LEGAL correlation
/// scope is ONE container domain. NEW container entry must not inherit parent
/// row identity by text; verified return reactivates the preserved parent
/// domain; same-container scroll/revisit/true-duplicate behavior is preserved.
/// </summary>
public sealed class RowIdentityContextDomainTests
{
    private static ElementBounds Band(double centerY) => new(0f, (float)centerY, 1f, (float)(centerY + 0.02));

    private static Observation OneRow(
        string text, string stableKey, double centerY, long seq = 1, string? type = "menu_item")
        => new(
            ImmutableArray.Create(new ObservedElement(text, null, 0, Band(centerY), type)
            {
                StableKey = stableKey,
            }),
            "settings.app", seq);

    // ═══ Z4 falsifier: cross-container inheritance = 0 ═══

    [Fact]
    public void Z4_ChildFrame_DoesNotInheritParentRowKey()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("SettingsRoot");
        var rootAccessibility = ctx.FindOrCreateId("Accessibility", Band(0.50));   // row_001 @ Root

        ctx.BeginContainer("SettingsSubpage(Accessibility)");
        // Child frame: toolbar title AND content row both arrive (as in Z4)
        // python-confirmed with the ROOT key (text match).
        var titleKey = ctx.Stabilize(OneRow("Accessibility", rootAccessibility, 0.089)).Elements[0].StableKey!;
        var contentKey = ctx.Stabilize(OneRow("Accessibility", rootAccessibility, 0.138)).Elements[0].StableKey!;

        Assert.NotEqual(rootAccessibility, titleKey);    // foreign key rejected
        Assert.NotEqual(rootAccessibility, contentKey);
        Assert.NotEqual(titleKey, contentKey);           // distinct bands → distinct rows
        Assert.DoesNotContain(rootAccessibility, ctx.ToHeaderJson() ?? "");  // root row not offered
    }

    // ═══ 1. same container, next viewport: confirmed key retained ═══

    [Fact]
    public void SameContainer_NextViewport_ConfirmedKeyRetained()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("A");
        var id = ctx.FindOrCreateId("Battery", Band(0.21));
        // Next viewport: the row scrolled to a new band, python re-confirms id.
        var confirmed = ctx.Stabilize(OneRow("Battery", id, 0.34)).Elements[0].StableKey!;
        Assert.Equal(id, confirmed);
    }

    // ═══ 2+3. revisit / verified parent return: preserved domain reactivated ═══

    [Fact]
    public void VerifiedReturn_RestoresParentDomainOriginalKeys()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Root");
        var rootRow = ctx.FindOrCreateId("Network & internet", Band(0.45));
        ctx.BeginContainer("Child");
        var childRow = ctx.FindOrCreateId("Display", Band(0.3));
        Assert.NotEqual(rootRow, childRow);

        ctx.BeginContainer("Root");                     // verified return
        Assert.Equal(rootRow, ctx.FindOrCreateId("Network & internet", Band(0.45)));
        Assert.DoesNotContain(childRow, ctx.ToHeaderJson() ?? "");
    }

    // ═══ 4. parent row text == child title text (the Z4 core) ═══

    [Fact]
    public void ParentRowText_EqualsChildTitle_NoInheritance()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("SettingsRoot");
        var parentId = ctx.FindOrCreateId("Accessibility", Band(0.50));
        ctx.BeginContainer("SettingsSubpage(Accessibility)");
        var titleId = ctx.FindOrCreateId("Accessibility", Band(0.089));
        Assert.NotEqual(parentId, titleId);
    }

    // ═══ 5. same text, two child rows: text alone cannot merge ═══

    [Fact]
    public void SameText_TwoRowsInOneDomain_AreDistinct()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var a = ctx.FindOrCreateId("Appearance", Band(0.2));
        var b = ctx.FindOrCreateId("Appearance", Band(0.5));
        Assert.NotEqual(a, b);
    }

    // ═══ 6. same text + similar geometry, different containers: no merge ═══

    [Fact]
    public void SameTextSimilarGeometry_DifferentContainers_NoMerge()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("A");
        var a = ctx.FindOrCreateId("Sound & vibration", Band(0.3));
        ctx.BeginContainer("B");
        var b = ctx.FindOrCreateId("Sound & vibration", Band(0.3));
        Assert.NotEqual(a, b);
    }

    // ═══ 7. true duplicate representation in one container: same id ═══

    [Fact]
    public void TrueDuplicate_WithinContainer_SameKey()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var id = ctx.FindOrCreateId("Dark theme", Band(0.42));
        Assert.Equal(id, ctx.FindOrCreateId("Dark theme", Band(0.42)));
    }

    // ═══ 8. source navigation row → destination page identical text ═══

    [Fact]
    public void SourceRow_DestinationPageSameText_IdentitiesDistinct()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("SettingsRoot");
        var source = ctx.FindOrCreateId("Display", Band(0.41));
        ctx.BeginContainer("SettingsSubpage(Display)");
        var destinationTitle = ctx.FindOrCreateId("Display", Band(0.089));
        Assert.NotEqual(source, destinationTitle);
        Assert.DoesNotContain(source, ctx.ToHeaderJson() ?? "");
    }

    // ═══ 9. nested Root → Child → Grandchild → Child → Root ═══

    [Fact]
    public void Nested_Tree_DomainsRestoredDeterministically()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Root");
        var rootRow = ctx.FindOrCreateId("Network & internet", Band(0.45));
        ctx.BeginContainer("Child");
        var childRow = ctx.FindOrCreateId("Display", Band(0.3));
        ctx.BeginContainer("Grandchild");
        var grandRow = ctx.FindOrCreateId("Screen", Band(0.25));
        ctx.BeginContainer("Child");
        Assert.Equal(childRow, ctx.FindOrCreateId("Display", Band(0.3)));
        ctx.BeginContainer("Root");
        Assert.Equal(rootRow, ctx.FindOrCreateId("Network & internet", Band(0.45)));
        Assert.DoesNotContain(grandRow, ctx.ToHeaderJson() ?? "");
    }

    // ═══ 10. sibling ChildA keys must not leak into ChildB ═══

    [Fact]
    public void Sibling_Domains_DoNotLeak()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Root");
        ctx.BeginContainer("ChildA");
        var aRow = ctx.FindOrCreateId("Wallpaper & style", Band(0.4));
        ctx.BeginContainer("Root");
        ctx.BeginContainer("ChildB");
        ctx.FindOrCreateId("Security & privacy", Band(0.36));
        var header = ctx.ToHeaderJson() ?? "";
        Assert.DoesNotContain(aRow, header);
        Assert.DoesNotContain("Wallpaper", header);
    }

    // ═══ null identity (title-off/scroll frame) keeps the current domain ═══

    [Fact]
    public void NullIdentity_Frame_KeepsCurrentDomain()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var id = ctx.FindOrCreateId("Screen timeout", Band(0.6));
        ctx.BeginContainer(null);                       // unresolvable frame
        Assert.Equal(id, ctx.FindOrCreateId("Screen timeout", Band(0.6)));
    }

    // ═══ P26-V2 run 6 residual 1 — sticky label demotion type export ═══
    // The X-Known-Rows header carries each known row's LATEST upstream
    // PerceptionType (additive ``type`` field) so the perception engine can
    // keep a previously-demoted section label NonInteractive across frames.
    // The type is memory only: it never changes identity and never leaks
    // across container domains.

    [Fact]
    public void StickyType_NonInteractiveRow_IsExported()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("SettingsSubpage(Display)");
        var labelId = ctx.FindOrCreateId("Color", Band(0.40));
        ctx.Stabilize(OneRow("Color", labelId, 0.40, type: "NonInteractive"));

        var header = ctx.ToHeaderJson();
        Assert.NotNull(header);
        Assert.Contains("\"type\":\"NonInteractive\"", header);
        Assert.Contains(labelId, header);
    }

    [Fact]
    public void StickyType_LatestSightingWins()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var id = ctx.FindOrCreateId("Color", Band(0.40));
        // Frame 1: demoted label; frame 2: the same row id re-sighted as a
        // menu_item (a genuine reclassification) — the export reflects the
        // LATEST type, never a sticky-forever fabrication.
        ctx.Stabilize(OneRow("Color", id, 0.40, seq: 1, type: "NonInteractive"));
        ctx.Stabilize(OneRow("Color", id, 0.40, seq: 2, type: "menu_item"));

        var header = ctx.ToHeaderJson();
        Assert.NotNull(header);
        Assert.Contains("\"type\":\"menu_item\"", header);
        Assert.DoesNotContain("\"type\":\"NonInteractive\"", header);
    }

    [Fact]
    public void StickyType_InteractiveRow_NotStickyEvidence()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var id = ctx.FindOrCreateId("Colors", Band(0.55));
        ctx.Stabilize(OneRow("Colors", id, 0.55, type: "menu_item"));

        var header = ctx.ToHeaderJson();
        Assert.NotNull(header);
        Assert.DoesNotContain("NonInteractive", header);
    }

    [Fact]
    public void StickyType_DoesNotLeakAcrossDomains()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("ChildA");
        var labelId = ctx.FindOrCreateId("Color", Band(0.40));
        ctx.Stabilize(OneRow("Color", labelId, 0.40, type: "NonInteractive"));
        ctx.BeginContainer("ChildB");
        ctx.FindOrCreateId("Screen saver", Band(0.5));

        var header = ctx.ToHeaderJson();
        Assert.NotNull(header);
        Assert.DoesNotContain(labelId, header);          // other domain's row never offered
        Assert.DoesNotContain("NonInteractive", header);
    }

    [Fact]
    public void StickyType_Reset_ClearsTypeMemory()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var id = ctx.FindOrCreateId("Color", Band(0.40));
        ctx.Stabilize(OneRow("Color", id, 0.40, type: "NonInteractive"));
        ctx.Reset();
        ctx.BeginContainer("Child");

        Assert.DoesNotContain("NonInteractive", ctx.ToHeaderJson() ?? "");
    }
}