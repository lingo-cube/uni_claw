using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// Foreground package detection parser (uiautomator XML) — unit coverage for
/// UiAutomatorXml.ForegroundPackage.
/// This parser is AUXILIARY test-side device-state detection only; it never
/// feeds the Runtime observation (Vision-first contract). Regression scope:
/// the parser must resolve the node package attribute INDEPENDENT of XML
/// attribute order and must not require package to be the last attribute of
/// the node's opening tag. The pre-fix pattern
/// (<c>&lt;node[^&gt;]*?package="…"&gt;</c>) demanded <c>&gt;</c> immediately
/// after the package value, so every realistic dump failed to parse and the
/// harness silently fell back to the stale owned foreground
/// (external-boundary-transition-settle-boundary-analysis.md, class D).
/// </summary>
public sealed class ForegroundDetectionTests
{
    [Fact]
    public void Package_FirstAttribute_Resolved()
    {
        var xml = "<node package=\"com.android.settings\" index=\"0\" text=\"\" " +
                  "class=\"android.widget.FrameLayout\" content-desc=\"\" bounds=\"[0,0][1080,1920]\"/>";
        Assert.Equal("com.android.settings", UiAutomatorXml.ForegroundPackage(xml));
    }

    [Fact]
    public void Package_MiddleAttribute_Resolved()
    {
        var xml = "<node index=\"0\" text=\"\" resource-id=\"\" " +
                  "class=\"android.widget.FrameLayout\" package=\"com.android.settings\" " +
                  "content-desc=\"\" bounds=\"[0,0][1080,1920]\"/>";
        Assert.Equal("com.android.settings", UiAutomatorXml.ForegroundPackage(xml));
    }

    [Fact]
    public void Package_FollowedByContentDescAndBounds_Resolved()
    {
        var xml = "<node index=\"1\" text=\"App info\" resource-id=\"\" " +
                  "class=\"android.widget.TextView\" package=\"com.android.permissioncontroller\" " +
                  "content-desc=\"App info\" bounds=\"[0,100][1080,200]\"/>";
        Assert.Equal("com.android.permissioncontroller", UiAutomatorXml.ForegroundPackage(xml));
    }

    [Fact]
    public void Package_LastAttribute_StillResolved()
    {
        // Pre-fix this was the ONLY shape the old pattern could parse —
        // regression guard for the previously working case.
        var xml = "<node index=\"0\" text=\"\" package=\"com.android.settings\"/>";
        Assert.Equal("com.android.settings", UiAutomatorXml.ForegroundPackage(xml));
    }

    [Fact]
    public void Node_WithoutPackage_ReturnsNull()
    {
        var xml = "<node index=\"0\" text=\"\" class=\"android.widget.FrameLayout\" " +
                  "content-desc=\"\" bounds=\"[0,0][1080,1920]\"/>";
        Assert.Null(UiAutomatorXml.ForegroundPackage(xml));
    }

    [Fact]
    public void EmptyXml_ReturnsNull()
    {
        Assert.Null(UiAutomatorXml.ForegroundPackage(""));
        Assert.Null(UiAutomatorXml.ForegroundPackage("   "));
        Assert.Null(UiAutomatorXml.ForegroundPackage("<hierarchy rotation=\"0\"></hierarchy>"));
    }

    [Fact]
    public void ExternalPackage_FirstNode_Detected()
    {
        // Full dump shape: root node carries the foreground package, children
        // follow. External foreground = the first node's package.
        var xml = "<hierarchy rotation=\"0\">" +
                  "<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.FrameLayout\" " +
                  "package=\"com.android.permissioncontroller\" content-desc=\"\" bounds=\"[0,0][1080,1920]\">" +
                  "<node index=\"0\" text=\"Allow UniClaw to access your location?\" resource-id=\"\" " +
                  "class=\"android.widget.TextView\" package=\"com.android.permissioncontroller\" " +
                  "content-desc=\"\" bounds=\"[0,500][1080,700]\"/>" +
                  "</node></hierarchy>";
        Assert.Equal("com.android.permissioncontroller", UiAutomatorXml.ForegroundPackage(xml));
    }

    [Fact]
    public void SettingsRoot_RealisticDump_Detected()
    {
        // Realistic Settings-root dump with attributes both before and after
        // package on the same node — the exact shape that failed pre-fix.
        var xml = "<hierarchy rotation=\"0\">" +
                  "<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.FrameLayout\" " +
                  "package=\"com.android.settings\" content-desc=\"\" bounds=\"[0,0][1080,1920]\">" +
                  "<node index=\"0\" text=\"Search settings\" resource-id=\"com.android.settings:id/search_action_bar\" " +
                  "class=\"android.widget.LinearLayout\" package=\"com.android.settings\" " +
                  "content-desc=\"Search settings\" bounds=\"[0,0][1080,150]\"/>" +
                  "</node></hierarchy>";
        Assert.Equal("com.android.settings", UiAutomatorXml.ForegroundPackage(xml));
    }
}
