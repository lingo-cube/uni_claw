using System.IO;

namespace UniClaw.Core.Observability;

/// <summary>
/// PhysicalFileProvider — System.IO implementation of IFileProvider (D-91).
/// Delegates to Directory/File static methods. Sealed, no constructor parameters.
/// </summary>
public sealed class PhysicalFileProvider : IFileProvider
{
    /// <inheritdoc/>
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public void AppendLine(string path, string line) => File.AppendAllText(path, line + "\n");

    /// <inheritdoc/>
    public string? ReadAllText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <inheritdoc/>
    public IReadOnlyList<string> ReadAllLines(string path) => File.Exists(path)
        ? File.ReadAllLines(path)
        : Array.Empty<string>();

    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
