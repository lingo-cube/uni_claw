using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsPageIdentityTests
{
    private const string Root = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private const string Child = "android.settings|rk1:Details|src=title|up=1";

    [Fact]
    public void ScrollAndRepeatedObservationKeepPageIdentity()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        var child = SettingsPageIdentity.Derive(root, Root, Child, "ui.element:Battery", true);
        Assert.Equal(child, SettingsPageIdentity.Derive(child, Child, Child, "ui.scroll:content", true));
        Assert.Equal(child, SettingsPageIdentity.Derive(child, Child, Child, null, false));
    }

    [Fact]
    public void SameTitleInDifferentEntryContextsDoesNotMergeAndBackReusesObservedParent()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        var first = SettingsPageIdentity.Derive(root, Root, Child, "ui.element:Battery", true);
        var second = SettingsPageIdentity.Derive(root, Root, Child, "ui.element:Storage", true);
        Assert.NotEqual(first, second);
        Assert.Equal(root, SettingsPageIdentity.Derive(first, Child, Root, "ui.element:Navigate up", true));
        Assert.Equal(first, SettingsPageIdentity.Derive(root, Root, Child, "ui.element:Battery", true));
    }

    [Fact]
    public void FailedUnchangedNavigationAndOverlayDoNotMintDestination()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        Assert.Equal(root, SettingsPageIdentity.Derive(root, Root, Root, "ui.element:Battery", true));
        Assert.Equal(root, SettingsPageIdentity.Derive(root, Root, Child, "ui.element:Battery", true, overlayPresent: true));
        Assert.Null(SettingsPageIdentity.Derive(root, Root, "android.settings", null, false));
    }
}
