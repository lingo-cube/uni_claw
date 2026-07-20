using UniClaw.Core.Observability;

namespace UniClaw.Core.Tests.Observability.File;

/// <summary>
/// MockFileProvider — in-memory Dictionary simulating path→content for test injection.
/// Implements IFileProvider without real filesystem.
/// </summary>
public sealed class MockFileProvider : IFileProvider
{
    private readonly Dictionary<string, string> _files = new();   // path → content (single string)
    private readonly HashSet<string> _directories = new();        // tracked directories

    public void EnsureDirectory(string path)
    {
        _directories.Add(path);
    }

    public void AppendLine(string path, string line)
    {
        if (!_files.ContainsKey(path))
            _files[path] = line + "\n";
        else
            _files[path] += line + "\n";
    }

    public string? ReadAllText(string path)
    {
        return _files.TryGetValue(path, out var content) ? content : null;
    }

    public IReadOnlyList<string> ReadAllLines(string path)
    {
        if (!_files.TryGetValue(path, out var content))
            return Array.Empty<string>();

        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _directories.Contains(path);

    // ── Test helpers (not on IFileProvider) ──────────────────

    /// <summary>Get all written file paths for verification</summary>
    public IReadOnlyList<string> WrittenFiles => _files.Keys.ToList();

    /// <summary>Get raw content of a file for verification</summary>
    public string? GetContent(string path) => _files.TryGetValue(path, out var c) ? c : null;

    /// <summary>Clear all stored data</summary>
    public void Clear() { _files.Clear(); _directories.Clear(); }
}
