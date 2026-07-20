using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability.File;

/// <summary>
/// PhysicalFileProvider tests — validate System.IO delegation with temp directory.
/// </summary>
public class PhysicalFileProviderTests
{
    private readonly PhysicalFileProvider _provider = new();

    /// <summary>Temp directory root for all tests in this class</summary>
    private string GetTempDir()
    {
        var dir = Path.Combine(System.IO.Path.GetTempPath(), $"pfp_test_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void EnsureDirectory_CreatesNestedPath()
    {
        var temp = GetTempDir();
        var nested = Path.Combine(temp, "sub1", "sub2");

        _provider.EnsureDirectory(nested);

        Assert.True(_provider.DirectoryExists(nested));

        Cleanup(temp);
    }

    [Fact]
    public void AppendLine_CreatesFileAndAppendsContent()
    {
        var temp = GetTempDir();
        var filePath = Path.Combine(temp, "test.jsonl");

        _provider.AppendLine(filePath, "{\"record_type\":\"execution\"}");
        _provider.AppendLine(filePath, "{\"record_type\":\"error\"}");

        Assert.True(_provider.FileExists(filePath));
        var lines = _provider.ReadAllLines(filePath);
        Assert.Equal(2, lines.Count);
        Assert.Contains("execution", lines[0]);
        Assert.Contains("error", lines[1]);

        Cleanup(temp);
    }

    [Fact]
    public void ReadAllText_ReturnsNullForNonexistentFile()
    {
        Assert.Null(_provider.ReadAllText(Path.Combine(System.IO.Path.GetTempPath(), "nonexistent.json")));
    }

    [Fact]
    public void ReadAllLines_ReturnsEmptyForNonexistentFile()
    {
        var result = _provider.ReadAllLines(Path.Combine(System.IO.Path.GetTempPath(), "nonexistent.jsonl"));
        Assert.Empty(result);
    }

    private static void Cleanup(string tempDir)
    {
        try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { }
    }
}
