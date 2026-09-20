namespace UniClaw.FileSystemRealization.Tests;

/// <summary>
/// 测试侧文件系统 mini-world。它只知道 BCL 文件观察，不引用 Kernel 或 Core。
/// Core 记录只在 FileSystemCoreProjection 中产生。
/// </summary>
public sealed class FileSystemWorld
{
    private readonly Dictionary<string, string> _identities = new(StringComparer.Ordinal);
    private readonly List<FileSystemObservation> _history = new();
    private int _nextIdentity;

    public FileSystemWorld(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        WorldKey = $"world:{RootPath}";
    }

    public string RootPath { get; }

    public string WorldKey { get; }

    public IReadOnlyList<FileSystemObservation> History => _history;

    public FileSystemObservation Observe(string relativeDirectory, DateTimeOffset observedAt)
    {
        var capture = Capture(relativeDirectory, observedAt);
        Register(capture);
        return capture;
    }

    public FileSystemObservation Capture(string relativeDirectory, DateTimeOffset observedAt)
    {
        var directory = Resolve(relativeDirectory);
        var entries = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Describe(Path.GetRelativePath(RootPath, path)))
            .ToArray();

        var normalizedDirectory = Normalize(relativeDirectory);
        var key = $"observation:{observedAt.UtcTicks}:{normalizedDirectory}";
        return new FileSystemObservation(key, normalizedDirectory, observedAt, entries);
    }

    public void Register(FileSystemObservation observation)
    {
        if (_history.All(existing => existing.Id != observation.Id))
            _history.Add(observation);
    }

    public FileObservation Describe(string relativePath)
    {
        var normalized = Normalize(relativePath);
        var info = new FileInfo(Resolve(normalized));
        if (!info.Exists)
            throw new FileNotFoundException("File is not present in the observed world.", info.FullName);

        if (!_identities.TryGetValue(normalized, out var identity))
        {
            identity = $"file:{++_nextIdentity}";
            _identities[normalized] = identity;
        }

        return new FileObservation(
            identity,
            normalized,
            new FileResourceVersion(info.Length, info.LastWriteTimeUtc.Ticks));
    }

    public FileResourceVersion CurrentVersion(string relativePath)
        => Describe(relativePath).Version;

    public string Resolve(string relativePath)
        => Path.GetFullPath(Path.Combine(RootPath, Normalize(relativePath)));

    private static string Normalize(string relativePath)
        => string.IsNullOrWhiteSpace(relativePath) || relativePath == "."
            ? "."
            : relativePath.Replace('\\', '/').Trim('/');
}

public sealed record FileSystemObservation(
    string Id,
    string Directory,
    DateTimeOffset ObservedAt,
    IReadOnlyList<FileObservation> Entries);

public sealed record FileObservation(
    string Identity,
    string RelativePath,
    FileResourceVersion Version);

public readonly record struct FileResourceVersion(long Length, long LastWriteTimeUtcTicks)
{
    public string SnapshotKey => $"{Length}:{LastWriteTimeUtcTicks}";
}
