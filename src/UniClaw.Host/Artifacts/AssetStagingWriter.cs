using System.Text;

namespace UniClaw.Host.Artifacts;

/// <summary>
/// Reusable atomic file writer shared by <see cref="FileAssetStore"/> and <see cref="RunAssetSession"/>.
/// Writes to a temp file then moves to the target path — readers see either the old file or the complete new one.
/// </summary>
public static class AssetStagingWriter
{
    /// <summary>Write text with atomic tmp+move (UTF-8 without BOM).</summary>
    public static async Task WriteTextAsync(string path, string content, CancellationToken ct = default)
    {
        var tmp = $"{path}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(false), ct);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Write bytes with atomic tmp+move.</summary>
    public static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken ct = default)
    {
        var tmp = $"{path}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllBytesAsync(tmp, bytes, ct);
        File.Move(tmp, path, overwrite: true);
    }
}
