using System.Xml.Linq;

namespace UniClaw.Host;

/// <summary>XML-only popup classification shared by live acquisition and offline
/// replay. Produces no action authorization; missing or malformed XML yields no claim.</summary>
public static class SettingsPopupClassifier
{
    public static string? Classify(string? xml, string hostPackage, IReadOnlyList<string> popupResourceIds)
    {
        if (xml is null) return null;
        XElement? root;
        try { root = XDocument.Parse(xml).Root; }
        catch (Exception) { return null; }
        if (root is null) return null;
        var nodes = root.Descendants("node").ToList();
        // Whitelist (dialog structure ids) is checked BEFORE the package branch:
        // a package mismatch alone is not proof of an overlay window (SIM-017),
        // so cross-package without a whitelist hit yields no claim instead of
        // "present" (EVO-001 candidate; window-stack evidence still pending).
        var whitelist = popupResourceIds.ToHashSet(StringComparer.Ordinal);
        if (nodes.Any(node => node.Attribute("resource-id") is { } id && whitelist.Contains(id.Value)))
            return "present";
        var windowPackage = (string?)nodes.FirstOrDefault()?.Attribute("package");
        return windowPackage is not null && windowPackage != hostPackage
            ? null
            : "absent";
    }
}
