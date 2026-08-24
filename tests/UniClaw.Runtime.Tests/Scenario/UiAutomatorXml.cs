namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// AUXILIARY test-side uiautomator XML parsing — device-state detection for
/// the real-device harness ONLY. Never a Runtime flow component and never
/// injected into the Runtime observation (Vision-first contract: the
/// observation carries the primary OCR channel; uiautomator is auxiliary
/// analysis — see external-boundary-evidence-analysis.md).
/// </summary>
internal static class UiAutomatorXml
{
    /// <summary>
    /// Foreground package = the package attribute of the first node in the
    /// dump, resolved INDEPENDENT of XML attribute order. The pattern requires
    /// <c>package="…"</c> at attribute position (whitespace-delimited,
    /// quote-delimited value) anywhere inside a node's opening tag, so a node
    /// whose package attribute is followed by content-desc / bounds / etc.
    /// still parses. The <c>[^&gt;]</c> span cannot cross a <c>&gt;</c>, so the
    /// match is confined to a single opening tag.
    /// REGRESSION ROOT CAUSE (classification D —
    /// external-boundary-transition-settle-boundary-analysis.md): the previous
    /// pattern demanded <c>&gt;</c> immediately after the package value, which
    /// only matched when package was the LAST attribute. Real dumps always
    /// carry attributes after package (root node:
    /// index/text/resource-id/class/package/content-desc/bounds/…), so every
    /// frame failed to parse and the harness fell back to the stale owned
    /// foreground — even though XML frames 21-26 were
    /// com.android.permissioncontroller (6 stable external frames, settle
    /// candidate + confirmation conditions all met).
    /// </summary>
    internal static string? ForegroundPackage(string xml)
    {
        var m = System.Text.RegularExpressions.Regex.Match(xml, @"<node\b[^>]*?\spackage=""([^""]*)""");
        return m.Success ? m.Groups[1].Value : null;
    }
}
