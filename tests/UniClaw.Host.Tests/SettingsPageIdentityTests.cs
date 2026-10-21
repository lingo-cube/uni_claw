using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsPageIdentityTests
{
    private const string Root = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private const string Child = "android.settings|rk1:Details|src=title|up=1";

    [Fact]
    public void RevisitedEntry_UsesAcceptedDescriptorRatherThanCaptureLocalOccurrenceIdentity()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        var first = SettingsPageIdentity.Derive(root, Root, Child, "occ-first-capture", true,
            deliveredTargetDescriptor: "Battery");
        var second = SettingsPageIdentity.Derive(root, Root, Child, "occ-second-capture", true,
            deliveredTargetDescriptor: "Battery");
        var otherEntry = SettingsPageIdentity.Derive(root, Root, Child, "occ-other-entry", true,
            deliveredTargetDescriptor: "Storage");

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherEntry);
    }

    [Fact]
    public void OpaqueOccurrenceWithoutObservedDescriptor_DoesNotInventEntryContext()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        Assert.Null(SettingsPageIdentity.Derive(root, Root, Child, "occ-first-capture", true));
    }

    [Fact]
    public void NonUniqueObservedEntryDescriptor_DoesNotMergeDifferentEntrances()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        Assert.Null(SettingsPageIdentity.Derive(root, Root, Child, "occ-first-details", true,
            deliveredTargetDescriptor: "Details", entryDescriptorUnique: false));
    }

    [Fact]
    public void NestedBackWithOpaqueOccurrenceIdReusesObservedParentIdentity()
    {
        const string nestedRoute = "android.settings|rk1:Internet|src=collapsing_toolbar|up=1";
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        var parent = SettingsPageIdentity.Derive(root, Root, Child, "occ-root-network", true,
            deliveredTargetDescriptor: "Network");
        var nested = SettingsPageIdentity.Derive(parent, Child, nestedRoute, "occ-network-internet", true,
            deliveredTargetDescriptor: "Internet");
        var returned = SettingsPageIdentity.Derive(nested, nestedRoute, Child, "occ-back-opaque-123", true,
            deliveredTargetDescriptor: "Navigate up");
        Assert.Equal(parent, returned);
    }

    [Fact]
    public void ScrollAndRepeatedObservationKeepPageIdentity()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        var child = SettingsPageIdentity.Derive(root, Root, Child, "occ-battery", true,
            deliveredTargetDescriptor: "Battery");
        Assert.Equal(child, SettingsPageIdentity.Derive(child, Child, Child, "ui.scroll:content", true));
        Assert.Equal(child, SettingsPageIdentity.Derive(child, Child, Child, null, false));
    }

    [Fact]
    public void SameTitleInDifferentEntryContextsDoesNotMergeAndBackReusesObservedParent()
    {
        var root = SettingsPageIdentity.Derive(null, null, Root, null, false);
        var first = SettingsPageIdentity.Derive(root, Root, Child, "occ-battery-1", true,
            deliveredTargetDescriptor: "Battery");
        var second = SettingsPageIdentity.Derive(root, Root, Child, "occ-storage", true,
            deliveredTargetDescriptor: "Storage");
        Assert.NotEqual(first, second);
        Assert.Equal(root, SettingsPageIdentity.Derive(first, Child, Root, "ui.element:Navigate up", true));
        Assert.Equal(first, SettingsPageIdentity.Derive(root, Root, Child, "occ-battery-2", true,
            deliveredTargetDescriptor: "Battery"));
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
