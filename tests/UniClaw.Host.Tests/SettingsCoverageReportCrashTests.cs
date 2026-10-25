using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsCoverageReportCrashTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("coverage-report-crash-").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task AcceptedCoverageWithoutItsUniqueSourceFailsExplicitlyAfterHostProcessKill()
    {
        using var writer = StartProbe("write-coverage-delivery", _root, "accepted");
        try
        {
            var accepted = await WaitForStageAsync(writer, "accepted").ConfigureAwait(false);
            Assert.Equal("AwaitingInputs", accepted.GetProperty("Status").GetString());
            Assert.Equal(JsonValueKind.Null, accepted.GetProperty("SourceFingerprint").ValueKind);
            await KillAsync(writer).ConfigureAwait(false);

            using var retry = StartProbe("retry-coverage-delivery", _root, "retry");
            await AssertExitedAsync(retry).ConfigureAwait(false);
            using var result = ReadResult(); var root = result.RootElement;
            Assert.Equal("Failed", root.GetProperty("Status").GetString());
            Assert.False(root.GetProperty("Readable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("SourceFingerprint").ValueKind);
            Assert.Equal(0, root.GetProperty("Attempts").GetInt32());
        }
        finally { await KillAsync(writer).ConfigureAwait(false); }
    }

    [Fact]
    public async Task FixedCoverageInputsSurviveHostProcessKillAndLossOfOriginalDirectory()
    {
        using var writer = StartProbe("write-coverage-delivery", _root, "inputs");
        try
        {
            var fixedInputs = await WaitForStageAsync(writer, "inputs").ConfigureAwait(false);
            Assert.Equal("Ready", fixedInputs.GetProperty("Status").GetString());
            var sourceFingerprint = fixedInputs.GetProperty("SourceFingerprint").GetString();
            var expectedReport = fixedInputs.GetProperty("ExpectedReportSha256").GetString();
            var expectedSteps = fixedInputs.GetProperty("ExpectedStepsSha256").GetString();
            var facts = ReadFacts(); var sourceHash = facts["coverage-source.json"];
            var fixture = File.ReadAllBytes(Path.Combine(_root, "coverage-fixture-control.json"));
            var original = fixedInputs.GetProperty("OriginalDirectory").GetString()!;
            await KillAsync(writer).ConfigureAwait(false);
            Directory.Delete(original, recursive: true);

            using var retry = StartProbe("retry-coverage-delivery", _root, "retry");
            await AssertExitedAsync(retry).ConfigureAwait(false);
            using var result = ReadResult(); var root = result.RootElement;
            Assert.Equal("Delivered", root.GetProperty("Status").GetString());
            Assert.True(root.GetProperty("Readable").GetBoolean());
            Assert.Equal(sourceFingerprint, root.GetProperty("SourceFingerprint").GetString());
            Assert.Equal(expectedReport, root.GetProperty("ArtifactSha256").GetProperty("coverage-report.json").GetString());
            Assert.Equal(expectedSteps, root.GetProperty("ArtifactSha256").GetProperty("coverage-steps.json").GetString());
            Assert.Equal(sourceHash, root.GetProperty("InputReportSha256").GetString());
            Assert.Equal(fixture, File.ReadAllBytes(Path.Combine(_root, "coverage-fixture-control.json")));
        }
        finally { await KillAsync(writer).ConfigureAwait(false); }
    }

    [Fact]
    public async Task GeneratingCoverageUsesActualSharedChildAndRetriesFromFixedInputsAfterTreeKill()
    {
        using var writer = StartProbe("write-coverage-delivery", _root, "generating");
        try
        {
            await WaitForFileAsync(Path.Combine(_root, "generator-entered.json"), writer).ConfigureAwait(false);
            var generating = await WaitForStageAsync(writer, "generating").ConfigureAwait(false);
            Assert.Equal("Generating", generating.GetProperty("Status").GetString());
            Assert.Equal(1, generating.GetProperty("Attempts").GetInt32());
            var expectedReport = generating.GetProperty("ExpectedReportSha256").GetString();
            var expectedSteps = generating.GetProperty("ExpectedStepsSha256").GetString();
            var facts = ReadFacts(); var fixture = File.ReadAllBytes(Path.Combine(_root, "coverage-fixture-control.json"));
            await KillAsync(writer).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(_root, "release.flag"), "retry-may-run-the-pinned-generator");

            using var retry = StartProbe("retry-coverage-delivery", _root, "retry");
            await AssertExitedAsync(retry).ConfigureAwait(false);
            using var result = ReadResult(); var root = result.RootElement;
            Assert.Equal("Delivered", root.GetProperty("Status").GetString());
            Assert.True(root.GetProperty("Readable").GetBoolean());
            Assert.Equal(2, root.GetProperty("Attempts").GetInt32());
            Assert.Equal(expectedReport, root.GetProperty("ArtifactSha256").GetProperty("coverage-report.json").GetString());
            Assert.Equal(expectedSteps, root.GetProperty("ArtifactSha256").GetProperty("coverage-steps.json").GetString());
            AssertFactsUnchanged(facts);
            Assert.Equal(fixture, File.ReadAllBytes(Path.Combine(_root, "coverage-fixture-control.json")));
        }
        finally { await KillAsync(writer).ConfigureAwait(false); }
    }

    [Fact]
    public async Task PublishedCoverageConfirmsAfterProcessKillWithoutAnotherGenerationAttempt()
    {
        using var writer = StartProbe("write-coverage-delivery", _root, "published");
        try
        {
            var published = await WaitForStageAsync(writer, "published").ConfigureAwait(false);
            Assert.Equal("Published", published.GetProperty("Status").GetString());
            var attempts = published.GetProperty("Attempts").GetInt32();
            var expectedReport = published.GetProperty("ExpectedReportSha256").GetString();
            var expectedSteps = published.GetProperty("ExpectedStepsSha256").GetString();
            var facts = ReadFacts(); var fixture = File.ReadAllBytes(Path.Combine(_root, "coverage-fixture-control.json"));
            await KillAsync(writer).ConfigureAwait(false);

            using var retry = StartProbe("retry-coverage-delivery", _root, "retry");
            await AssertExitedAsync(retry).ConfigureAwait(false);
            using var result = ReadResult(); var root = result.RootElement;
            Assert.Equal("Delivered", root.GetProperty("Status").GetString());
            Assert.True(root.GetProperty("Readable").GetBoolean());
            Assert.Equal(attempts, root.GetProperty("Attempts").GetInt32());
            Assert.Equal(expectedReport, root.GetProperty("ArtifactSha256").GetProperty("coverage-report.json").GetString());
            Assert.Equal(expectedSteps, root.GetProperty("ArtifactSha256").GetProperty("coverage-steps.json").GetString());
            AssertFactsUnchanged(facts);
            Assert.Equal(fixture, File.ReadAllBytes(Path.Combine(_root, "coverage-fixture-control.json")));
        }
        finally { await KillAsync(writer).ConfigureAwait(false); }
    }

    private JsonDocument ReadResult() => JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "coverage-retry-result.json")));

    private Dictionary<string, string> ReadFacts()
        => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(_root, "coverage-original-facts.json")))
            ?? throw new InvalidDataException("coverage-facts-missing");

    private void AssertFactsUnchanged(IReadOnlyDictionary<string, string> expected)
    {
        var actual = ReadFacts();
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (path, hash) in expected) Assert.Equal(hash, actual[path]);
    }

    private async Task<JsonElement> WaitForStageAsync(Process writer, string stage)
    {
        var path = Path.Combine(_root, "coverage-stage.json");
        await WaitForFileAsync(path, writer).ConfigureAwait(false);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Assert.Equal(stage, root.GetProperty("Stage").GetString());
        Assert.Equal(writer.Id, root.GetProperty("HostProcessId").GetInt32());
        return root.Clone();
    }

    private static async Task WaitForFileAsync(string path, Process process)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (!File.Exists(path) && !process.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25).ConfigureAwait(false);
        Assert.True(File.Exists(path), process.HasExited ? await process.StandardError.ReadToEndAsync().ConfigureAwait(false) : "coverage stage marker timeout");
    }

    private static async Task AssertExitedAsync(Process process)
    {
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40)).ConfigureAwait(false);
        Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync().ConfigureAwait(false));
    }

    private static async Task KillAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        }
    }

    private static Process StartProbe(params string[] arguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("original probe missing");
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", config, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }
}
