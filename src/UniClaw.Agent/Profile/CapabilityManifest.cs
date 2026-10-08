using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Agent.Profile;

/// <summary>Capabilities exposed by a Product realization profile. The Product
/// profile is intentionally a small positive allowlist.</summary>
public sealed record CapabilityManifest(IReadOnlyList<string> Capabilities)
{
    /// <summary>The frozen headless Product manifest: exactly one capability,
    /// <c>submit_decision</c>.</summary>
    public static CapabilityManifest ProductHeadless { get; } =
        new(new[] { ProductCapabilities.SubmitDecision });

    /// <summary>Trimmed, de-duplicated, ordinal-sorted capability list used for
    /// cross-checking config files against the compiled identity.</summary>
    public IReadOnlyList<string> NormalizedCapabilities => (Capabilities ?? Array.Empty<string>())
        .Where(static c => !string.IsNullOrWhiteSpace(c))
        .Select(static c => c.Trim())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    /// <summary>True when the raw list contains blanks or duplicates; such a
    /// manifest fails closed instead of hashing silently.</summary>
    public bool HasDuplicates => Capabilities is null
        || Capabilities.Count != NormalizedCapabilities.Count;

    /// <summary>Stable content hash of the normalized list; exchanged at
    /// handshake so both peers prove the same frozen capability surface.</summary>
    public string ManifestHash => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", NormalizedCapabilities)))).ToLowerInvariant();
}
