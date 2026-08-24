using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// Mechanical authority guards for the in-process trace/span read model.
/// The read model is a Data Plane projection; it must not leak into Runtime
/// authority or expand the frozen DriverHost wire surface.
/// </summary>
public sealed class TraceSpanReadModelArchitectureGuardTests
{
    private const string RuntimeSource = "src/UniClaw.Runtime";
    private const string RuntimeProject = "src/UniClaw.Runtime/UniClaw.Runtime.csproj";
    private const string DriverHostServer = "src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs";
    private const string DriverHostTransport = "src/UniClaw.Runtime.DriverHost/Transport";

    private static readonly string[] RuntimeReadModelTokens =
    {
        "TraceRunSummary", "TraceRunSummaryResult", "TraceSpanPage", "TraceSpanEnvelope",
        "TraceSpanCursor", "TraceSpanFilter", "TraceSpanReadModelProjector",
        "ITraceCaptureReader", "FileTraceCaptureReader", "TraceCaptureReadResult",
        "TraceCaptureReadStatus", "UniClaw.Runtime.DriverHost", "UniClaw.Runtime.Harness.Capture",
    };

    private static readonly string[] ApprovedMethods =
    {
        "ping", "run.list", "run.snapshot.get", "run.trap.get", "run.events.after",
        "run.events.drain", "evidence.get", "control.support", "run.start",
        "run.strategy.start", "assistance.pending", "assistance.resolve",
    };

    private static readonly Regex DispatchCase = new(
        @"case\s+""(?<method>[^""]+)""\s*:",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TransportDtoDeclaration = new(
        @"\b(?:record|class|struct|interface)\s+(?<name>\w*(?:Trace|Span|Capture)\w*Dto)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Existing capture publication DTOs are part of the frozen baseline and
    // are not trace-read-model query DTOs.
    private static readonly HashSet<string> ExistingCaptureDtos = new(StringComparer.Ordinal)
    {
        "UniClawCaptureRecordDto", "UniClawCaptureArtifactDto",
    };

    [Fact]
    public void RuntimeSource_HasNoTraceSpanReadModelOrCaptureReaderDependency()
    {
        var projectPath = RepoPath(RuntimeProject);
        Assert.True(File.Exists(projectPath), "Missing required project: " + RuntimeProject);
        var project = File.ReadAllText(projectPath);
        Assert.DoesNotContain("DriverHost", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Harness", project, StringComparison.Ordinal);

        foreach (var file in SourceFiles(RuntimeSource))
        {
            var source = File.ReadAllText(file);
            foreach (var token in RuntimeReadModelTokens)
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DriverHostDispatch_HasExactlyTheFrozenMethodSet()
    {
        var path = RepoPath(DriverHostServer);
        Assert.True(File.Exists(path), "Missing required source: " + DriverHostServer);
        var methods = DispatchCase.Matches(File.ReadAllText(path))
            .Cast<Match>()
            .Select(match => match.Groups["method"].Value)
            .ToArray();

        Assert.Equal(ApprovedMethods, methods);
        Assert.DoesNotContain(methods, method =>
            method.StartsWith("trace.", StringComparison.Ordinal)
            || method.StartsWith("span.", StringComparison.Ordinal)
            || method.StartsWith("capture.", StringComparison.Ordinal)
            || method.StartsWith("scenario.", StringComparison.Ordinal));
    }

    [Fact]
    public void DriverHostTransport_HasNoTraceSpanCaptureReadModelDtos()
    {
        var transportPath = RepoPath(DriverHostTransport);
        Assert.True(Directory.Exists(transportPath), "Missing required directory: " + DriverHostTransport);

        foreach (var file in SourceFiles(DriverHostTransport))
        {
            var violations = TransportDtoDeclaration.Matches(File.ReadAllText(file))
                .Cast<Match>()
                .Select(match => match.Groups["name"].Value)
                .Where(name => !ExistingCaptureDtos.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.True(violations.Length == 0,
                "Trace/span/capture read-model DTO leaked into frozen transport: "
                + file + ": " + string.Join(", ", violations));
        }
    }

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(RepoPath(directory), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RepoPath(string relativePath)
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        return Path.GetFullPath(relativePath);
    }
}
