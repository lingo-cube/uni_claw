using System.Text.Json;

namespace UniClaw.Host;

/// <summary>Observation-side contextual signature hint, not a page registry or
/// canonical identity. World Model still associates accepted observations.</summary>
internal static class SettingsPageIdentity
{
    private const string Prefix = "settings-page:v1:";
    private sealed record Segment(string Route, string Entry);

    internal static string? Derive(string? currentSignature, string? currentRoute,
        string observedRoute, string? deliveredTarget, bool postAction,
        bool overlayPresent = false, string backDescriptor = "Navigate up", string? deliveredTargetDescriptor = null,
        bool entryDescriptorUnique = true)
    {
        if (overlayPresent && currentSignature is not null)
            return currentSignature;
        if (observedRoute == "android.settings")
            return null; // No discriminative page observation; never invent a destination.
        if (observedRoute == currentRoute && currentSignature is not null)
            return currentSignature;
        var path = Read(currentSignature);
        if (observedRoute.Contains("|src=homepage_title|up=0", StringComparison.Ordinal))
            return Write([new Segment(observedRoute, "root")]);
        if (postAction && string.Equals(deliveredTargetDescriptor, backDescriptor, StringComparison.Ordinal)
            && path.Count > 1 && path[^2].Route == observedRoute)
            return Write(path.Take(path.Count - 1).ToArray());
        // Only an observed arrival creates a new signature hint. Same-label
        // destinations reached from distinct contexts remain distinct.
        if (path.Count == 0)
            return Write([new Segment(observedRoute, "initial-observation")]);
        // The actual target is capture-local. Without its accepted descriptor
        // there is no stable entry discriminator; never use the opaque id or
        // pretend that this was an external arrival.
        if (postAction && (string.IsNullOrWhiteSpace(deliveredTargetDescriptor) || !entryDescriptorUnique))
            return null;
        path.Add(new Segment(observedRoute,
            postAction && !string.IsNullOrWhiteSpace(deliveredTargetDescriptor)
                ? deliveredTargetDescriptor : "external-observation"));
        return Write(path);
    }

    private static List<Segment> Read(string? signature)
    {
        if (signature?.StartsWith(Prefix, StringComparison.Ordinal) != true)
            return [];
        try { return JsonSerializer.Deserialize<List<Segment>>(signature[Prefix.Length..]) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string Write(IReadOnlyList<Segment> path) => Prefix + JsonSerializer.Serialize(path);
}
