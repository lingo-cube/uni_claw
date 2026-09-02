using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.SettingsCampaign;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// ROW_IDENTITY transitions (TRANSITION_SIGNAL_REFINEMENT gate): RowIdentityContext
/// carries NO action-type-driven transition-pending heuristic — a Tap is NOT a
/// container transition. Null-identity frames (same-container scroll or local
/// interaction) retain the current container domain (keys + offer preserved).
/// Grounded new-container identity activates a fresh domain; verified return
/// reactivates the preserved parent domain. The unresolved-first-frame-of-a-
/// child-entry edge (parent rows still offered during a NULL frame) is a
/// documented residual pending an authoritative execution seam
/// (MISSING_TRANSITION_OBSERVABILITY_SEAM) — never hidden by heuristics.
/// </summary>
public sealed class RowIdentityContextTransitionSafetyTests
{
    private static ElementBounds Band(double centerY) => new(0f, (float)centerY, 1f, (float)(centerY + 0.02));

    private static Observation OneRow(string text, double centerY)
        => new(ImmutableArray.Create(new ObservedElement(text, null, 0, Band(centerY), "menu_item")), "settings.app", 1);

    // Same-container scroll, temporary null identity → current domain retained

    [Fact]
    public void SameContainerScrollNull_Frames_RetainCurrentDomain()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Display");
        var displayId = ctx.FindOrCreateId("Screen timeout", Band(0.6));

        ctx.BeginContainer(null);             // ScrollForward title-off frame

        Assert.Contains(displayId, ctx.ToHeaderJson() ?? "");
        Assert.Equal(displayId, ctx.FindOrCreateId("Screen timeout", Band(0.6)));
    }

    // False-suspension falsifier (gate §5/#2): same-container local interaction
    // (a Tap on a toggle) + next frame unresolved MUST retain the domain — an
    // action-type driven pending is a false container-boundary inference.

    [Fact]
    public void SameContainerLocalInteraction_NullNextFrame_RetainsDomain()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Display");
        var timeoutId = ctx.FindOrCreateId("Screen timeout", Band(0.6));

        ctx.BeginContainer(null);             // same-container local Tap, frame merely unresolved

        Assert.NotNull(ctx.ToHeaderJson());
        Assert.Contains(timeoutId, ctx.ToHeaderJson() ?? "");
        Assert.Equal(timeoutId, ctx.FindOrCreateId("Screen timeout", Band(0.6)));
    }

    // Normal same-container revisit unchanged

    [Fact]
    public void SameContainerRevisit_Unchanged()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("Child");
        var id = ctx.FindOrCreateId("Dark theme", Band(0.42));
        ctx.BeginContainer("Child");          // revisit (same identity)
        Assert.Equal(id, ctx.FindOrCreateId("Dark theme", Band(0.42)));
        Assert.Contains(id, ctx.ToHeaderJson() ?? "");
    }

    // Exact Z4 falsifier remains GREEN (grounded child entry — no inheritance)

    [Fact]
    public void Z4_Falsifier_StillGreen()
    {
        var ctx = new RowIdentityContext();
        ctx.BeginContainer("SettingsRoot");
        var rootId = ctx.FindOrCreateId("Accessibility", Band(0.50));
        ctx.BeginContainer("SettingsSubpage(Accessibility)");

        var titleKey = ctx.Stabilize(OneRow("Accessibility", 0.089)).Elements[0].StableKey!;
        var contentKey = ctx.Stabilize(OneRow("Accessibility", 0.138)).Elements[0].StableKey!;
        Assert.NotEqual(rootId, titleKey);
        Assert.NotEqual(rootId, contentKey);
        Assert.NotEqual(titleKey, contentKey);
        Assert.DoesNotContain(rootId, ctx.ToHeaderJson() ?? "");
    }
}