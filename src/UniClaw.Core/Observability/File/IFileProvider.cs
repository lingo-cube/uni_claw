namespace UniClaw.Core.Observability;

/// <summary>
/// IFileProvider — 6-method abstraction decoupling Core classlib from direct System.IO dependency (D-91).
/// Sync methods, consistent with D-22 ITraceStorage sync-first design.
/// No Delete/Copy/Move — YAGNI for trace storage use case.
/// </summary>
public interface IFileProvider
{
    /// <summary>Create directory and all parent directories (mkdir -p)</summary>
    void EnsureDirectory(string path);

    /// <summary>Append a line (with newline) to a text file. Creates file if not exists.</summary>
    void AppendLine(string path, string line);

    /// <summary>Read entire file content, or null if file does not exist.</summary>
    string? ReadAllText(string path);

    /// <summary>Read all lines from a text file. Returns empty list if file does not exist.</summary>
    IReadOnlyList<string> ReadAllLines(string path);

    /// <summary>Check if a file exists at the given path.</summary>
    bool FileExists(string path);

    /// <summary>Check if a directory exists at the given path.</summary>
    bool DirectoryExists(string path);

    /// <summary>Write entire content to a file, overwriting if exists. Creates file if not exists (D-102).</summary>
    void WriteAllText(string path, string content);
}
