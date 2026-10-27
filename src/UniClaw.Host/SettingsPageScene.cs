using System.Security.Cryptography;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Host;

/// <summary>Host-side scene comparison only; it does not establish page identity.</summary>
internal sealed class SettingsPageScene
{
    private readonly byte[] _hash;
    private readonly byte[] _fullHash;
    private readonly SettingsPopupClassifier.SceneBasis? _basis;
    private readonly int _sourceApiLevel;
    private SettingsPageScene(byte[] hash, byte[] fullHash, SettingsPopupClassifier.SceneBasis? basis, int sourceApiLevel)
    { _hash = hash; _fullHash = fullHash; _basis = basis; _sourceApiLevel = sourceApiLevel; }

    internal bool RequiresWindowEvidence => _basis is not null;
    internal int SourceApiLevel => _sourceApiLevel;
    internal string ComparisonScope(IReadOnlyList<UiBounds>? titleBounds) => CanLimit(titleBounds)
        ? "source-declared-safe-content" : "entire-capture";

    internal static SettingsPageScene Capture(byte[] png, SettingsWindowEvidence? windows)
    {
        var basis = SettingsPopupClassifier.ReadSceneBasis(windows);
        var full = SHA256.HashData(png);
        if (basis is null) return new(full, full, null, windows?.AndroidApiLevel ?? 0);
        var image = PngImage.Decode(png);
        if (image.Width != basis.Width || image.Height != basis.Height)
            return new(full, full, null, windows?.AndroidApiLevel ?? 0);
        return new(ContentHash(image, basis), full, basis, windows!.AndroidApiLevel);
    }

    internal bool Matches(byte[] png, SettingsWindowEvidence? windows, IReadOnlyList<UiBounds>? titleBounds = null)
    {
        if (_basis is null) return SHA256.HashData(png).SequenceEqual(_hash);
        var current = SettingsPopupClassifier.ReadSceneBasis(windows);
        if (current != _basis) return false;
        if (!CanLimit(titleBounds)) return SHA256.HashData(png).SequenceEqual(_fullHash);
        var image = PngImage.Decode(png);
        return image.Width == _basis.Width && image.Height == _basis.Height
            && ContentHash(image, _basis).SequenceEqual(_hash);
    }

    private bool CanLimit(IReadOnlyList<UiBounds>? bounds) => _basis is not null && bounds is { Count: > 0 }
        && bounds.All(b => b.X1 >= 0 && b.Y1 >= _basis.Top && b.X2 <= _basis.Width
            && b.Y2 <= _basis.Bottom && b.X2 > b.X1 && b.Y2 > b.Y1);

    private static byte[] ContentHash(PngImage image, SettingsPopupClassifier.SceneBasis basis)
    {
        // The source proves full-width top/bottom system insets. No arbitrary
        // crop, similarity threshold, or assumption that a translucent window
        // is opaque. Compare every decoded application-content pixel exactly.
        var stride = checked(image.Width * 4);
        return SHA256.HashData(image.Rgba.AsSpan(checked(basis.Top * stride),
            checked((basis.Bottom - basis.Top) * stride)));
    }
}
