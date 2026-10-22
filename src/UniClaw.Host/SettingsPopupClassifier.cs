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
        var windowPackage = (string?)nodes.FirstOrDefault()?.Attribute("package");
        if (windowPackage is not null && windowPackage != hostPackage) return "present";
        var whitelist = popupResourceIds.ToHashSet(StringComparer.Ordinal);
        return nodes.Any(node => node.Attribute("resource-id") is { } id && whitelist.Contains(id.Value))
            ? "present" : "absent";
    }
}
