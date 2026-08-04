using UniClaw.TraceTool.Commands;
using Xunit;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Serializes console-capturing test classes: Console.Out/Error redirection is process
/// global, so CliTests and JsonContractTests must not run in parallel with each other.
/// </summary>
[CollectionDefinition("TraceCliConsole", DisableParallelization = true)]
public sealed class TraceCliConsoleCollection;

/// <summary>
/// Task 7.2 — CLI subcommand tests via System.CommandLine InvokeAsync through
/// Program.Main. Exit codes follow the TraceExitCodes contract:
/// 0 = success, 1 = diff detected, 2 = usage error / missing run, 3 = empty trace.
/// </summary>
[Collection("TraceCliConsole")]
public sealed class CliTests
{
    private static string SuccessDir => TraceRunFixture.FixturePath("success");
    private static string FailureDir => TraceRunFixture.FixturePath("failure");
    private static string FixturesRoot =>
        Path.GetDirectoryName(SuccessDir)!;

    [Fact]
    public async Task List_JsonFormat_ReturnsSuccess()
    {
        var result = await CliTestHelper.RunAsync(
            "list", "--dir", FixturesRoot, "--format", "json");

        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
        Assert.NotEmpty(result.Out);
    }

    [Fact]
    public async Task List_TableFormat_ReturnsSuccess()
    {
        var result = await CliTestHelper.RunAsync(
            "list", "--dir", FixturesRoot, "--format", "table");

        // Exit 0 regardless of output flavor (non-TTY output auto-switches to JSON).
        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public async Task Timeline_WithRun_ReturnsSuccess()
    {
        // Failure snapshot: 4 engine.step spans — required, the success snapshot
        // predates span recording (0 spans → EmptyTrace exit 3).
        var result = await CliTestHelper.RunAsync("timeline", "--run", FailureDir);

        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public async Task Diagnose_WithRun_ReturnsSuccess()
    {
        var result = await CliTestHelper.RunAsync("diagnose", "--run", FailureDir);

        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public async Task Diff_WithTwoRuns_DetectsDifference()
    {
        // The success snapshot has no spans (CLI EmptyTrace gate, exit 3), so the
        // second run is a copy of the failure fixture with altered result metrics —
        // both runs carry spans and their metrics differ → DiffDetected (1).
        var modified = await TestRunFactory.CreateModifiedFailureRunAsync();
        try
        {
            var result = await CliTestHelper.RunAsync(
                "diff", "--run-a", FailureDir, "--run-b", modified);

            Assert.Equal(TraceExitCodes.DiffDetected, result.ExitCode);
            Assert.Contains("Behavioral differences", result.Out);
        }
        finally
        {
            if (Directory.Exists(modified))
                Directory.Delete(modified, recursive: true);
        }
    }

    [Fact]
    public async Task Report_WithRun_ReturnsSuccess()
    {
        var result = await CliTestHelper.RunAsync("report", "--run", FailureDir);

        Assert.Equal(TraceExitCodes.Success, result.ExitCode);
        Assert.Contains("# Trace Report", result.Out);
    }

    [Fact]
    public async Task Diagnose_MissingRun_ExitsWithUsageError()
    {
        var result = await CliTestHelper.RunAsync(
            "diagnose", "--run", Path.Combine(Path.GetTempPath(), "no-such-run"));

        Assert.Equal(TraceExitCodes.UsageError, result.ExitCode);
        Assert.Contains("run directory not found", result.Error);
    }

    [Fact]
    public async Task Timeline_MissingRun_ExitsWithUsageError()
    {
        var result = await CliTestHelper.RunAsync(
            "timeline", "--run", Path.Combine(Path.GetTempPath(), "no-such-run"));

        Assert.Equal(TraceExitCodes.UsageError, result.ExitCode);
    }

    [Fact]
    public async Task Interactive_DumbTerm_ExitsWithError()
    {
        var originalTerm = Environment.GetEnvironmentVariable("TERM");
        try
        {
            Environment.SetEnvironmentVariable("TERM", "dumb");
            var result = await CliTestHelper.RunAsync("interactive", "--run", FailureDir);

            Assert.Equal(TraceExitCodes.UsageError, result.ExitCode);
            Assert.Contains("real terminal", result.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TERM", originalTerm);
        }
    }
}

/// <summary>
/// Runs Program.Main with stdout/stderr captured and restored around the invocation.
/// </summary>
internal static class CliTestHelper
{
    public static async Task<(int ExitCode, string Out, string Error)> RunAsync(
        params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            var exitCode = await Program.Main(args);
            return (exitCode, outWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
