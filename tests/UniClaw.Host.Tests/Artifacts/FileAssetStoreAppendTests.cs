using UniClaw.Host.Artifacts;
using Xunit;

namespace UniClaw.Host.Tests.Artifacts;

/// <summary>
/// FileAssetStore append-mode tests. Verify that Append=true appends to existing
/// file content instead of overwriting.
/// </summary>
public sealed class FileAssetStoreAppendTests : IDisposable
{
    private readonly string _tempDir;

    public FileAssetStoreAppendTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uniclaw-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task AppendTrue_AddsToExistingFile()
    {
        var store = new FileAssetStore(_tempDir);
        var path = "test.jsonl";

        await store.WriteAsync("run1", path, "line1\n"u8.ToArray(), append: true);
        await store.WriteAsync("run1", path, "line2\n"u8.ToArray(), append: true);

        var content = await store.ReadAsync("run1", path);
        Assert.NotNull(content);
        var text = System.Text.Encoding.UTF8.GetString(content!);
        Assert.Equal("line1\nline2\n", text);
    }

    [Fact]
    public async Task AppendFalse_OverwritesExistingFile()
    {
        var store = new FileAssetStore(_tempDir);
        var path = "overwrite.txt";

        await store.WriteAsync("run1", path, "first"u8.ToArray(), append: false);
        await store.WriteAsync("run1", path, "second"u8.ToArray(), append: false);

        var content = await store.ReadAsync("run1", path);
        Assert.NotNull(content);
        var text = System.Text.Encoding.UTF8.GetString(content!);
        Assert.Equal("second", text);
    }

    [Fact]
    public async Task DefaultAppendFalse_KeepsBackwardCompatibility()
    {
        var store = new FileAssetStore(_tempDir);
        var path = "legacy.txt";

        // Default parameter (append: false) — existing call sites unchanged
        await store.WriteAsync("run1", path, "data"u8.ToArray());
        await store.WriteAsync("run1", path, "new-data"u8.ToArray());

        var content = await store.ReadAsync("run1", path);
        var text = System.Text.Encoding.UTF8.GetString(content!);
        Assert.Equal("new-data", text);
    }
}
